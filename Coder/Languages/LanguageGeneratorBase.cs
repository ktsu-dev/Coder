// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Globalization;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Provides a base implementation for language generators with common functionality.
/// </summary>
/// <remarks>
/// Output is written through <see cref="CodeBlocker"/>, which owns indentation: a generator writes
/// the text of a line and never the whitespace in front of it, and opens a <see cref="Scope"/> or an
/// <see cref="IndentScope"/> to indent a body. That also fixes the line terminator to
/// <see cref="CodeBlocker.DefaultNewLineString"/> rather than <see cref="Environment.NewLine"/>, so
/// generated source is byte-identical whichever platform produced it.
/// </remarks>
public abstract class LanguageGeneratorBase : ILanguageGenerator
{
	/// <summary>
	/// Gets the indentation one level of nesting adds.
	/// </summary>
	/// <remarks>
	/// Four spaces rather than <see cref="CodeBlocker.DefaultIndentString"/>'s tab: Python's
	/// indentation is syntax, and four spaces is what PEP 8 asks for.
	/// <para>
	/// Overridable because one target does not get a say. Go is formatted by <c>gofmt</c> rather
	/// than by whoever wrote the file, and <c>gofmt</c> indents with a tab — so a generated Go file
	/// indented any other way is a diff against itself the first time anybody saves it.
	/// </para>
	/// </remarks>
	protected virtual string IndentString => "    ";

	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public abstract string LanguageId { get; }

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public abstract string DisplayName { get; }

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public abstract string FileExtension { get; }

	/// <summary>
	/// Generates code in the target language from an AST node.
	/// </summary>
	/// <param name="astNode">The AST node to generate code from.</param>
	/// <returns>A string containing the generated code in the target language.</returns>
	public string Generate(AstNode astNode)
	{
		Ensure.NotNull(astNode);

		if (!CanGenerate(astNode))
		{
			throw new NotSupportedException($"Cannot generate code for node type: {astNode.GetNodeTypeName()}");
		}

		using CodeBlocker code = CodeBlocker.Create(IndentString);
		GenerateInternal(astNode, code);
		return code.ToString();
	}

	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public virtual bool CanGenerate(AstNode astNode)
	{
		// No null check: a type pattern never matches null.
		return astNode is FunctionDeclaration
			or ReturnStatement
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>;
	}

	/// <summary>
	/// Emits one node.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// There is no indentation level to pass down: <paramref name="code"/> carries the current depth,
	/// and only the scope that opens a body changes it.
	/// </remarks>
	protected abstract void GenerateInternal(AstNode node, CodeBlocker code);

	/// <summary>
	/// Emits the nodes whose spelling is the same in every target language: variable references,
	/// literals, and the legacy <see cref="AstLeafNode{T}"/> shapes.
	/// </summary>
	/// <param name="node">The node to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <returns>True if the node was handled; false if it is the caller's to emit.</returns>
	/// <remarks>
	/// A generator's dispatch handles its structural nodes and defers the rest here, so the literal
	/// cases exist once. Where a language does differ — Python's capitalized booleans — it overrides
	/// <see cref="FormatBoolean"/> rather than repeating the whole set.
	/// </remarks>
	protected bool TryGenerateCommonNode(AstNode node, CodeBlocker code)
	{
		Ensure.NotNull(code);

		switch (node)
		{
			case VariableReference varRef:
				code.Write(varRef.Name);
				return true;

			case LiteralExpression<string> stringLit:
				code.Write($"\"{EscapeString(stringLit.Value ?? string.Empty)}\"");
				return true;

			case LiteralExpression<int> intLit:
				code.Write(intLit.Value.ToString(CultureInfo.InvariantCulture));
				return true;

			case LiteralExpression<bool> boolLit:
				code.Write(FormatBoolean(boolLit.Value));
				return true;

			case LiteralExpression<double> doubleLit:
				code.Write(doubleLit.Value.ToString(CultureInfo.InvariantCulture));
				return true;

			// Legacy support for AstLeafNode types
			case AstLeafNode<string> strLeaf:
				code.Write($"\"{EscapeString(strLeaf.Value ?? string.Empty)}\"");
				return true;

			case AstLeafNode<int> intLeaf:
				code.Write(intLeaf.Value.ToString(CultureInfo.InvariantCulture));
				return true;

			case AstLeafNode<bool> boolLeaf:
				code.Write(FormatBoolean(boolLeaf.Value));
				return true;

			default:
				return false;
		}
	}

	/// <summary>
	/// Spells a boolean literal.
	/// </summary>
	/// <param name="value">The literal's value.</param>
	/// <returns>The keyword the language uses.</returns>
	protected virtual string FormatBoolean(bool value) => value ? "true" : "false";

	/// <summary>
	/// Gets what a documentation comment starts with in this language.
	/// </summary>
	/// <remarks>
	/// C-family languages that have a documentation comment write <c>///</c>; one that does not, or a
	/// language whose documentation is a construct rather than a comment, overrides this with its own
	/// ordinary comment marker. Writing an ordinary comment is the honest fallback: the lines still
	/// reach the reader, and nothing pretends to be a docstring that is not one.
	/// </remarks>
	protected virtual string DocumentationPrefix => "///";

	/// <summary>
	/// Writes a note where the language has no way to say what a declaration asked for.
	/// </summary>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="what">What was asked for, in the reader's terms.</param>
	/// <remarks>
	/// A generated file that silently drops a member is worse than one that says which member and
	/// why: the first looks complete and is not, and there is nothing in it to search for.
	/// </remarks>
	protected void WriteInexpressible(CodeBlocker code, string what)
	{
		Ensure.NotNull(code);
		code.WriteLine($"{CommentPrefix} {what}");
	}

	/// <summary>
	/// Gets what an ordinary comment starts with in this language.
	/// </summary>
	protected virtual string CommentPrefix => "//";

	/// <summary>
	/// Spells one of a file's imports, or reports that the language has nothing to write for it.
	/// </summary>
	/// <param name="import">The import as the file carries it.</param>
	/// <returns>The line to write, or null when the language has no import statement.</returns>
	protected virtual string? SpellImport(string import) => null;

	/// <summary>
	/// Emits whatever a file needs before its imports.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <returns>True if anything was written.</returns>
	/// <remarks>
	/// Empty for every language but C++, which is the only one here where a file can be included
	/// twice and has to say what that means.
	/// </remarks>
	protected virtual bool WriteFileDirectives(SourceFile file, CodeBlocker code) => false;

	/// <summary>
	/// Emits a whole source file: its banner, what it depends on, and what it declares.
	/// </summary>
	/// <param name="file">The file to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected void GenerateSourceFile(SourceFile file, CodeBlocker code)
	{
		Ensure.NotNull(file);
		Ensure.NotNull(code);

		bool wroteAnything = false;

		foreach (string line in file.HeaderComment)
		{
			code.WriteLine(line.Length == 0 ? CommentPrefix : $"{CommentPrefix} {line}");
			wroteAnything = true;
		}

		if (wroteAnything)
		{
			code.NewLine();
		}

		if (WriteFileDirectives(file, code))
		{
			code.NewLine();
		}

		bool wroteImport = false;
		foreach (string import in file.Imports)
		{
			// An empty import is a group separator rather than an import of nothing, and separates
			// nothing until a group has been written — which is also what keeps a language whose
			// imports are written elsewhere from getting a blank line for each of them here.
			if (import.Length == 0)
			{
				if (wroteImport)
				{
					code.NewLine();
				}

				continue;
			}

			if (SpellImport(import) is string spelled)
			{
				code.WriteLine(spelled);
				wroteImport = true;
			}
		}

		if (wroteImport)
		{
			code.NewLine();
		}

		AstNode? previous = null;
		foreach (AstNode member in file.Members)
		{
			if (previous is not null && NeedsSeparation(previous, member))
			{
				code.NewLine();
			}

			previous = member;
			GenerateInternal(member, code);
		}
	}

	/// <summary>
	/// Reports whether two adjacent declarations want a blank line between them.
	/// </summary>
	/// <param name="previous">The declaration already written.</param>
	/// <param name="member">The declaration about to be written.</param>
	/// <returns>True when a blank line belongs between them.</returns>
	/// <remarks>
	/// Always, unless a language says otherwise. A language whose declarations are dense enough to
	/// want grouping overrides this with the rule it wants.
	/// </remarks>
	protected virtual bool NeedsSeparation(AstNode previous, AstNode member) => true;

	/// <summary>
	/// Emits a declaration's documentation, one comment per line.
	/// </summary>
	/// <param name="node">The declaration whose documentation to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected void GenerateDocumentation(IHasDocumentation node, CodeBlocker code)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(code);

		foreach (string line in node.Documentation)
		{
			// A blank line is written as a bare marker rather than one with a trailing space, which
			// every formatter and most reviewers would strip anyway.
			code.WriteLine(line.Length == 0 ? DocumentationPrefix : $"{DocumentationPrefix} {line}");
		}
	}

	/// <summary>
	/// Ends a statement with the terminator and line break the language uses.
	/// </summary>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Python has neither, and its function body writes its own line breaks, so it overrides this
	/// with an empty body rather than each statement emitter growing a special case.
	/// </remarks>
	protected virtual void EndStatement(CodeBlocker code)
	{
		Ensure.NotNull(code);
		code.WriteLine(";");
	}

	/// <summary>
	/// Emits a return statement, recursing into its expression.
	/// </summary>
	/// <param name="returnStmt">The statement to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Overridable for the one target whose conditional is not an expression. Go's <c>if</c> yields
	/// nothing, so a choice between two values has to be lowered to the statement around it — and
	/// this is that statement.
	/// </remarks>
	protected virtual void GenerateReturnStatement(ReturnStatement returnStmt, CodeBlocker code)
	{
		Ensure.NotNull(returnStmt);
		Ensure.NotNull(code);

		code.Write("return");

		if (returnStmt.Expression is not null)
		{
			code.Write(" ");
			GenerateInternal(returnStmt.Expression, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Emits an assignment statement, recursing into both sides.
	/// </summary>
	/// <param name="assignment">The statement to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <inheritdoc cref="GenerateReturnStatement" path="/remarks"/>
	protected virtual void GenerateAssignmentStatement(AssignmentStatement assignment, CodeBlocker code)
	{
		Ensure.NotNull(assignment);
		Ensure.NotNull(code);

		GenerateInternal(assignment.Target, code);
		code.Write($" {GetAssignmentOperator(assignment.Operator)} ");
		GenerateInternal(assignment.Value, code);
		EndStatement(code);
	}

	/// <summary>
	/// Emits an expression evaluated for its effect, ending it as a statement.
	/// </summary>
	/// <param name="statement">The statement to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected void GenerateExpressionStatement(ExpressionStatement statement, CodeBlocker code)
	{
		Ensure.NotNull(statement);
		Ensure.NotNull(code);

		GenerateInternal(statement.Expression, code);
		EndStatement(code);
	}

	/// <summary>
	/// Emits a call, recursing into its receiver and arguments.
	/// </summary>
	/// <param name="callExpr">The call to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A receiver is written in front of the callee, separated by a dot, which is how five of the
	/// six targets spell a member call. C is the exception and overrides this: it has no member
	/// functions, so the receiver becomes the first argument.
	/// <para>
	/// <see cref="CallExpression.Callee"/> is written verbatim. Nothing here maps a function's name
	/// between languages, and nothing pretends to — see the node's own remarks for why.
	/// </para>
	/// </remarks>
	protected virtual void GenerateCallExpression(CallExpression callExpr, CodeBlocker code)
	{
		Ensure.NotNull(callExpr);
		Ensure.NotNull(code);

		if (callExpr.Receiver is not null)
		{
			GenerateInternal(callExpr.Receiver, code);
			code.Write(".");
		}

		code.Write(callExpr.Callee);
		code.Write("(");
		GenerateArgumentList(callExpr.Arguments, code);
		code.Write(")");
	}

	/// <summary>
	/// Emits a parenthesised choice between two values.
	/// </summary>
	/// <param name="conditional">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Defaults to the C-family <c>?:</c>. Python spells the same thing with its operands in a
	/// different order, and Rust has no ternary operator at all and writes an <c>if</c> expression;
	/// both override this.
	/// <para>
	/// Parenthesised for the reason a binary expression is: the AST carries no precedence, so nesting
	/// one of these inside another would otherwise be ambiguous.
	/// </para>
	/// </remarks>
	protected virtual void GenerateConditionalExpression(ConditionalExpression conditional, CodeBlocker code)
	{
		Ensure.NotNull(conditional);
		Ensure.NotNull(code);

		code.Write("(");
		GenerateInternal(conditional.Condition, code);
		code.Write(" ? ");
		GenerateInternal(conditional.WhenTrue, code);
		code.Write(" : ");
		GenerateInternal(conditional.WhenFalse, code);
		code.Write(")");
	}

	/// <summary>
	/// Emits a comma-separated argument list, without the surrounding parentheses.
	/// </summary>
	/// <param name="arguments">The arguments to emit, in order.</param>
	/// <param name="code">The writer to emit into.</param>
	protected void GenerateArgumentList(IReadOnlyList<AstNode> arguments, CodeBlocker code)
	{
		Ensure.NotNull(arguments);
		Ensure.NotNull(code);

		for (int index = 0; index < arguments.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			GenerateInternal(arguments[index], code);
		}
	}

	/// <summary>
	/// Emits a parenthesised binary expression, recursing into both operands.
	/// </summary>
	/// <param name="binaryExpr">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="operatorSpelling">The operator's spelling in the target language.</param>
	/// <remarks>Always parenthesised: the AST carries no precedence, so nesting would be ambiguous otherwise.</remarks>
	protected void GenerateBinaryExpression(BinaryExpression binaryExpr, CodeBlocker code, string operatorSpelling)
	{
		Ensure.NotNull(binaryExpr);
		Ensure.NotNull(code);

		code.Write("(");
		GenerateInternal(binaryExpr.Left, code);
		code.Write($" {operatorSpelling} ");
		GenerateInternal(binaryExpr.Right, code);
		code.Write(")");
	}

	/// <summary>
	/// Emits a parenthesised unary expression, recursing into its operand.
	/// </summary>
	/// <param name="unaryExpr">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="operatorSpelling">The operator's spelling in the target language.</param>
	/// <remarks>
	/// Parenthesised for the same reason as a binary expression: the AST carries no precedence, so
	/// <c>-(a + b)</c> and <c>(-a) + b</c> would be indistinguishable otherwise.
	/// <para>
	/// A word operator is separated from its operand and a symbolic one is not, so Python's
	/// <c>not ready</c> and C#'s <c>!ready</c> both come out right without either language special-casing
	/// the emitter.
	/// </para>
	/// </remarks>
	protected void GenerateUnaryExpression(UnaryExpression unaryExpr, CodeBlocker code, string operatorSpelling)
	{
		Ensure.NotNull(unaryExpr);
		Ensure.NotNull(code);
		Ensure.NotNull(operatorSpelling);

		code.Write("(");
		code.Write(operatorSpelling);

		if (operatorSpelling.Length > 0 && char.IsLetter(operatorSpelling[^1]))
		{
			code.Write(" ");
		}

		GenerateInternal(unaryExpr.Operand, code);
		code.Write(")");
	}

	/// <summary>
	/// Reports whether a node is one of the standard shapes a generator built on these helpers accepts.
	/// </summary>
	/// <param name="astNode">The node to check.</param>
	/// <returns>True if the node is generatable.</returns>
	protected static bool CanGenerateStandardNodes(AstNode astNode)
	{
		// No null check: a type pattern never matches null.
		return astNode is FunctionDeclaration
			or CompileTimeAssertion
			or UsingAlias
			or MemberInitialiser
			or ConstructionExpression
			or CallExpression
			or ConditionalExpression
			or ExpressionStatement
			or SourceFile
			or NamespaceDeclaration
			or ClassDeclaration
			or EnumDeclaration
			or EnumMember
			or FieldDeclaration
			or EntryPoint
			or Parameter
			or ReturnStatement
			or BinaryExpression
			or UnaryExpression
			or VariableReference
			or LiteralExpression<string>
			or LiteralExpression<int>
			or LiteralExpression<bool>
			or LiteralExpression<double>
			or VariableDeclaration
			or AssignmentStatement
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>;
	}

	/// <summary>
	/// Spells a visibility as the keyword a C-family language writes in front of a declaration.
	/// </summary>
	/// <param name="visibility">The visibility to spell.</param>
	/// <returns>The keyword, or null when nothing should be written for it.</returns>
	/// <remarks>
	/// Null rather than an empty string, so a caller writes the modifier and the space after it
	/// together or writes neither, instead of leaving a stray space in front of a declaration that
	/// carries no visibility. A language that spells one of them differently — or does not spell them
	/// at all — does not call this.
	/// </remarks>
	protected static string? SpellVisibility(Visibility visibility) => visibility switch
	{
		Visibility.Public => "public",
		Visibility.Protected => "protected",
		Visibility.Internal => "internal",
		Visibility.Private => "private",
		_ => null,
	};

	/// <summary>
	/// Reads the visibility a declaration was given.
	/// </summary>
	/// <param name="node">The node to read.</param>
	/// <returns>Its visibility, or <see cref="Visibility.Unspecified"/> for a node that cannot carry one.</returns>
	/// <remarks>
	/// Reached through <see cref="IHasVisibility"/> so a generator grouping a class's members by
	/// visibility does not need a switch over which kind of member each one is.
	/// </remarks>
	protected static Visibility VisibilityOf(AstNode node) =>
		node is IHasVisibility declaration ? declaration.Visibility : Visibility.Unspecified;

	/// <summary>
	/// Escapes a string literal's contents for a target language using C-style backslash escapes.
	/// </summary>
	/// <param name="value">The raw string value.</param>
	/// <returns>The escaped value, without surrounding quotes.</returns>
	/// <remarks>Every language the generators target uses these escapes, Python included.</remarks>
	protected static string EscapeString(string value)
	{
		Ensure.NotNull(value);

		// Ordinal explicitly: these are source-syntax escapes, never subject to a culture.
		return value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\t", "\\t", StringComparison.Ordinal);
	}

	/// <summary>
	/// Maps a binary operator to its C-family spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException">The operator has no mapping.</exception>
	/// <remarks>
	/// C# and C++ use this set unchanged. A generator whose language spells one operator
	/// differently — Python's <c>and</c>/<c>or</c>, JavaScript's strict <c>===</c>/<c>!==</c> —
	/// handles just that operator and defers the rest here.
	/// </remarks>
	protected static string GetBinaryOperator(BinaryOperator op) => OperatorSymbols.GetSymbol(op);

	/// <summary>
	/// Maps a unary operator to its C-family spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException">The operator has no mapping.</exception>
	/// <remarks>
	/// C#, C++ and JavaScript use this set unchanged. Only Python differs, spelling
	/// <see cref="UnaryOperator.LogicalNot"/> as a word.
	/// </remarks>
	protected static string GetUnaryOperator(UnaryOperator op) => OperatorSymbols.GetSymbol(op);

	/// <summary>
	/// Maps an assignment operator to its source spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException">The operator has no mapping.</exception>
	/// <remarks>Every language the generators target spells these identically.</remarks>
	protected static string GetAssignmentOperator(AssignmentOperator op) => OperatorSymbols.GetSymbol(op);
}
