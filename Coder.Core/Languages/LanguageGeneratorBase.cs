// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
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
	/// The indentation one level of nesting adds.
	/// </summary>
	/// <remarks>
	/// Four spaces rather than <see cref="CodeBlocker.DefaultIndentString"/>'s tab: Python's
	/// indentation is syntax, and four spaces is what PEP 8 asks for.
	/// </remarks>
	protected const string IndentString = "    ";

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
	protected void GenerateReturnStatement(ReturnStatement returnStmt, CodeBlocker code)
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
	protected void GenerateAssignmentStatement(AssignmentStatement assignment, CodeBlocker code)
	{
		Ensure.NotNull(assignment);
		Ensure.NotNull(code);

		GenerateInternal(assignment.Target, code);
		code.Write($" {GetAssignmentOperator(assignment.Operator)} ");
		GenerateInternal(assignment.Value, code);
		EndStatement(code);
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
