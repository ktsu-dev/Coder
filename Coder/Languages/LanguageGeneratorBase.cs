// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
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
	private HashSet<object>? emittedPreambleItems;
	private Dictionary<object, object>? preambleCloneOrigins;

	internal bool EnforceConservation { get; set; }

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

		HashSet<object>? previousItems = emittedPreambleItems;
		Dictionary<object, object>? previousOrigins = preambleCloneOrigins;
		emittedPreambleItems = new HashSet<object>(ReferenceEqualityComparer.Instance);
		preambleCloneOrigins = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);

		try
		{
			using CodeBlocker code = CodeBlocker.Create(IndentString);
			GenerateInternal(astNode, code);

			if (EnforceConservation)
			{
				EnsureConserved(astNode);
			}

			return code.ToString();
		}
		finally
		{
			emittedPreambleItems = previousItems;
			preambleCloneOrigins = previousOrigins;
		}
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
				code.Write(double.IsFinite(doubleLit.Value) ? FormatDouble(doubleLit.Value) : SpellNonFiniteDouble(doubleLit.Value));
				return true;

			case LiteralExpression<float> floatLit:
				code.Write(FormatSingle(floatLit.Value));
				return true;

			case LiteralExpression<long> longLit:
				code.Write(longLit.Value.ToString(CultureInfo.InvariantCulture));
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
		WriteComment(code, CommentPrefix, what);
	}

	/// <summary>
	/// Gets what an ordinary comment starts with in this language.
	/// </summary>
	protected virtual string CommentPrefix => "//";

	/// <summary>
	/// Writes caller-provided text as comment lines without letting it escape the comment.
	/// </summary>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="prefix">The comment marker to write on each line.</param>
	/// <param name="text">The text to write.</param>
	protected virtual void WriteComment(CodeBlocker code, string prefix, string text)
	{
		Ensure.NotNull(code);
		Ensure.NotNull(prefix);
		Ensure.NotNull(text);

		foreach (string line in text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
		{
			code.WriteLine(line.Length == 0 ? prefix : $"{prefix} {line}");
		}
	}

	/// <summary>
	/// The kind of declaration that owns a preamble.
	/// </summary>
	protected enum PreambleSite
	{
		/// <summary>A class or other type declaration.</summary>
		Type,

		/// <summary>A free function.</summary>
		Function,

		/// <summary>A member function.</summary>
		Method,

		/// <summary>A field declaration.</summary>
		Field,

		/// <summary>A property declaration.</summary>
		Property,

		/// <summary>An enumeration.</summary>
		Enum,
	}

	/// <summary>
	/// Spells an annotation at a specific declaration site.
	/// </summary>
	/// <param name="annotation">The annotation as the declaration carries it.</param>
	/// <param name="site">The kind of declaration carrying it.</param>
	/// <returns>The line to write, or null when the language has no syntax at this site.</returns>
	protected virtual string? SpellAnnotation(Annotation annotation, PreambleSite site) => null;

	/// <summary>
	/// Writes a declaration's documentation and annotations.
	/// </summary>
	/// <param name="declaration">The declaration whose preamble to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="site">The kind of declaration carrying its annotations.</param>
	protected void WritePreamble(AstNode declaration, CodeBlocker code, PreambleSite site)
	{
		Ensure.NotNull(declaration);
		Ensure.NotNull(code);

		if (declaration is IHasDocumentation documented)
		{
			foreach (string line in documented.Documentation)
			{
				WriteComment(code, DocumentationPrefix, line);
				emittedPreambleItems?.Add(line);
			}
		}

		foreach (Annotation annotation in AnnotationsOf(declaration))
		{
			if (SpellAnnotation(annotation, site) is string spelled)
			{
				code.WriteLine(spelled);
			}
			else
			{
				WriteInexpressible(code, $"annotated {annotation}");
			}

			emittedPreambleItems?.Add(annotation);
			if (preambleCloneOrigins?.TryGetValue(annotation, out object? origin) == true)
			{
				emittedPreambleItems?.Add(origin);
			}
		}
	}

	/// <summary>
	/// Clones an annotation and records the original for conservation checks.
	/// </summary>
	/// <param name="annotation">The annotation to clone.</param>
	/// <returns>The clone.</returns>
	protected Annotation CloneAnnotation(Annotation annotation)
	{
		Ensure.NotNull(annotation);
		Annotation clone = annotation.Clone();
		RememberAnnotationClone(clone, annotation);
		return clone;
	}

	/// <summary>
	/// Associates an annotation clone with the original preamble item it represents.
	/// </summary>
	/// <param name="clone">The annotation being emitted.</param>
	/// <param name="original">The annotation present on the input tree.</param>
	protected void RememberAnnotationClone(Annotation clone, Annotation original)
	{
		Ensure.NotNull(clone);
		Ensure.NotNull(original);
		preambleCloneOrigins?.Add(clone, original);
	}

	private static Collection<Annotation> AnnotationsOf(AstNode declaration) => declaration switch
	{
		ClassDeclaration classDeclaration => classDeclaration.Annotations,
		EnumDeclaration enumDeclaration => enumDeclaration.Annotations,
		FieldDeclaration fieldDeclaration => fieldDeclaration.Annotations,
		FunctionDeclaration functionDeclaration => functionDeclaration.Annotations,
		PropertyDeclaration propertyDeclaration => propertyDeclaration.Annotations,
		_ => [],
	};

	private void EnsureConserved(AstNode root)
	{
		HashSet<object> expected = new(ReferenceEqualityComparer.Instance);

		foreach (AstNode node in SelfAndDescendants(root))
		{
			if (node is IHasDocumentation documented)
			{
				foreach (string line in documented.Documentation)
				{
					expected.Add(line);
				}
			}

			foreach (Annotation annotation in AnnotationsOf(node))
			{
				expected.Add(annotation);
			}
		}

		foreach (object item in expected)
		{
			if (!emittedPreambleItems!.Contains(item))
			{
				throw new InvalidOperationException($"{item} was dropped by {DisplayName}.");
			}
		}
	}

	/// <summary>
	/// Lists a node and every node beneath it, each once.
	/// </summary>
	/// <param name="root">The node to start from.</param>
	/// <returns>The root, then the nodes under it, in no particular order.</returns>
	/// <remarks>
	/// Read off the nodes' public properties rather than a list of node types, so a node added to the
	/// AST is walked without anybody remembering to add it here.
	/// </remarks>
	protected static IEnumerable<AstNode> SelfAndDescendants(AstNode root)
	{
		HashSet<AstNode> visited = new(ReferenceEqualityComparer.Instance);
		Stack<AstNode> pending = new();
		pending.Push(root);

		while (pending.TryPop(out AstNode? node))
		{
			if (!visited.Add(node))
			{
				continue;
			}

			yield return node;

			if (node is AstCompositeNode composite)
			{
				foreach (AstNode child in composite.Children.Values)
				{
					pending.Push(child);
				}
			}

			foreach (PropertyInfo property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (property.GetIndexParameters().Length > 0)
				{
					continue;
				}

				object? value = property.GetValue(node);
				if (value is AstNode child)
				{
					pending.Push(child);
				}
				else if (value is IEnumerable children)
				{
					foreach (object? item in children)
					{
						if (item is AstNode sequenceChild)
						{
							pending.Push(sequenceChild);
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Reports whether a file holds a double literal that is not a finite number.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <returns>True if any double literal in the file is NaN or an infinity.</returns>
	/// <remarks>
	/// What a generator asks before deciding whether the file needs the import its spelling of
	/// <see cref="SpellNonFiniteDouble"/> depends on.
	/// </remarks>
	protected static bool ContainsNonFiniteDouble(SourceFile file) =>
		SelfAndDescendants(file).Any(node => node is LiteralExpression<double> literal && !double.IsFinite(literal.Value));

	/// <summary>
	/// Writes down the types a declaration is written over, for a target that has no generics.
	/// </summary>
	/// <param name="parameters">The declaration's type parameters.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Constraints and all, because for a target in this position the whole of the parameter is
	/// something it cannot say: there is no name in the file for the requirement to be attached to.
	/// </remarks>
	protected void WriteTypeParametersDown(IEnumerable<TypeParameter> parameters, CodeBlocker code)
	{
		Ensure.NotNull(parameters);

		string[] written = [.. parameters.Select(parameter => parameter.ToString())];

		if (written.Length > 0)
		{
			WriteInexpressible(code, $"over {string.Join("; ", written)}");
		}
	}

	/// <summary>
	/// Writes down the requirements on a declaration's type parameters that this target has type
	/// parameters but no way to ask for.
	/// </summary>
	/// <param name="parameters">The declaration's type parameters.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="asked">The kinds this target wrote for itself; anything else is written down.</param>
	/// <remarks>
	/// The middle case, and the common one. A target with generics can always write the parameter's
	/// name, and what it can say about that name is where they part: C# says all four, Rust says
	/// two of them and Go one, and C++ says none of them without an include the AST does not carry.
	/// A requirement that goes unwritten is a guarantee quietly dropped, which is the same reason
	/// <see cref="WriteTypePromises"/> exists.
	/// </remarks>
	protected void WriteUnaskedConstraints(
		IEnumerable<TypeParameter> parameters,
		CodeBlocker code,
		params TypeConstraintKind[] asked)
	{
		Ensure.NotNull(parameters);
		Ensure.NotNull(asked);

		string[] unasked =
		[
			.. parameters.SelectMany(parameter => parameter.Constraints
				.Where(constraint => Array.IndexOf(asked, constraint.Kind) < 0)
				.Select(constraint => $"{parameter.Name} is {Describe(constraint)}")),
		];

		if (unasked.Length > 0)
		{
			WriteInexpressible(code, $"requires that {string.Join(", and that ", unasked)}");
		}
	}

	/// <summary>
	/// Says what a constraint asks for, in a reader's terms rather than a language's.
	/// </summary>
	/// <param name="constraint">The constraint to describe.</param>
	/// <returns>The description, to follow "T is".</returns>
	private static string Describe(TypeConstraint constraint) => constraint.Kind switch
	{
		TypeConstraintKind.ValueType => "a value type",
		TypeConstraintKind.ReferenceType => "a reference type",
		TypeConstraintKind.Constructible => "constructible with no arguments",
		_ => constraint.Type?.ToString() ?? "unconstrained",
	};

	/// <summary>
	/// Writes the promises a type declaration makes that this language has no word for.
	/// </summary>
	/// <param name="classDecl">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="recordIsSpelled">
	/// Whether this target has already asked for the record's members some other way, such as
	/// Rust's <c>#[derive]</c>.
	/// </param>
	/// <remarks>
	/// <see cref="ClassDeclaration.IsRecord"/> and <see cref="ClassDeclaration.IsReadOnly"/> are
	/// claims about the type rather than about any one member of it — it compares by value, and no
	/// member of it modifies it — so a target that drops either in silence writes a file that looks
	/// like it still makes the claim.
	/// <para>
	/// <see cref="ClassDeclaration.IsPartial"/> is deliberately not among them, and is dropped
	/// without a note. It claims nothing about the type: it is permission to declare the rest of it
	/// in another file, and a generator that has written the whole declaration has not used the
	/// permission for anything a reader of this file could be missing.
	/// </para>
	/// </remarks>
	protected void WriteTypePromises(ClassDeclaration classDecl, CodeBlocker code, bool recordIsSpelled = false)
	{
		Ensure.NotNull(classDecl);

		if (classDecl.IsRecord && !recordIsSpelled)
		{
			WriteInexpressible(code, "record: compares by value, and copies and prints itself");
		}

		if (classDecl.IsReadOnly)
		{
			WriteInexpressible(code, "readonly: no member of this type modifies it");
		}
	}

	/// <summary>
	/// Spells one of a file's imports, or reports that the language has nothing to write for it.
	/// </summary>
	/// <param name="import">The import as the file carries it.</param>
	/// <returns>The line to write, or null when the language has no import statement.</returns>
	protected virtual string? SpellImport(string import) => null;

	/// <summary>
	/// Lists imports required by the generated contents of a source file.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <returns>The imports the generated declarations need.</returns>
	protected virtual IEnumerable<string> RequiredImports(SourceFile file) => [];

	/// <summary>
	/// Emits whatever a file needs before its imports.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <returns>True if anything was written.</returns>
	/// <remarks>
	/// Empty unless a language has something that must precede everything else: C++'s
	/// <c>#pragma once</c>, Go's package clause, Python's future statement.
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
			WriteComment(code, CommentPrefix, line);
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

		WriteImports(file, code);

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
	/// Emits what a file depends on, and the blank line after it.
	/// </summary>
	/// <param name="file">The file being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A language with no import statement writes nothing at all here, including no blank lines: an
	/// empty import is a group separator, and a separator between groups separates nothing until a
	/// group has been written.
	/// </remarks>
	private void WriteImports(SourceFile file, CodeBlocker code)
	{
		bool wroteImport = false;
		HashSet<string> required = new(RequiredImports(file), StringComparer.Ordinal);

		foreach (string import in required)
		{
			if (SpellImport(import) is string spelled)
			{
				code.WriteLine(spelled);
				wroteImport = true;
			}
		}

		foreach (string import in file.Imports)
		{
			if (required.Contains(import))
			{
				continue;
			}

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
	/// <para>
	/// A number literal receiver is parenthesised. A negative one would otherwise negate the call's
	/// result rather than its receiver, since unary minus binds looser than member access, so
	/// <c>-2.5.abs()</c> is <c>-(2.5.abs())</c>; and an integer one would run the dot into the
	/// literal, which JavaScript and Python read as a malformed number rather than a member call.
	/// </para>
	/// </remarks>
	protected virtual void GenerateCallExpression(CallExpression callExpr, CodeBlocker code)
	{
		Ensure.NotNull(callExpr);
		Ensure.NotNull(code);

		if (callExpr.Receiver is not null)
		{
			bool parenthesise = IsNumberLiteral(callExpr.Receiver);

			if (parenthesise)
			{
				code.Write("(");
			}

			GenerateInternal(callExpr.Receiver, code);

			if (parenthesise)
			{
				code.Write(")");
			}

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
	/// <para>
	/// An operand whose spelling starts with the operator's own last character is parenthesised, so
	/// negating <c>-1</c> is written <c>(-(-1))</c> rather than <c>(--1)</c>, which every C-family
	/// target reads as a decrement.
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

		bool wouldFuse = operatorSpelling.Length > 0 && LeadingSign(unaryExpr.Operand) == operatorSpelling[^1];

		if (wouldFuse)
		{
			code.Write("(");
		}

		GenerateInternal(unaryExpr.Operand, code);

		if (wouldFuse)
		{
			code.Write(")");
		}

		code.Write(")");
	}

	/// <summary>
	/// Reports the sign a node's spelling starts with, if any.
	/// </summary>
	/// <param name="node">The node to inspect.</param>
	/// <returns><c>'-'</c> for a negative number literal; otherwise null.</returns>
	/// <remarks>
	/// Every other expression starts with a name, a quote, a digit or a parenthesis, so only a
	/// negative literal can run its sign into the operator in front of it.
	/// </remarks>
	private static char? LeadingSign(AstNode? node) => node switch
	{
		LiteralExpression<int> { Value: < 0 } => '-',
		LiteralExpression<double> doubleLit when double.IsNegative(doubleLit.Value) && !double.IsNaN(doubleLit.Value) => '-',
		LiteralExpression<float> floatLit when float.IsNegative(floatLit.Value) => '-',
		LiteralExpression<long> { Value: < 0 } => '-',
		AstLeafNode<int> { Value: < 0 } => '-',
		_ => null,
	};

	/// <summary>
	/// Reports whether a node is a number literal.
	/// </summary>
	/// <param name="node">The node to inspect.</param>
	/// <returns>True for an integer or floating-point literal of any sign; otherwise false.</returns>
	private static bool IsNumberLiteral(AstNode? node) => node is LiteralExpression<int>
		or LiteralExpression<double>
		or LiteralExpression<float>
		or LiteralExpression<long>
		or AstLeafNode<int>;

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
			or LiteralExpression<float>
			or LiteralExpression<long>
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
	/// <remarks>
	/// Every language the generators target uses the named escapes, Python included. Every other
	/// control character, and the Unicode line terminators, are escaped numerically through
	/// <see cref="EscapeCodeUnit"/>, because written raw they break the literal: Go and Python reject a
	/// NUL in source, and C# reads U+0085, U+2028 and U+2029 as ending the line.
	/// </remarks>
	protected virtual string EscapeString(string value)
	{
		Ensure.NotNull(value);

		// Ordinal explicitly: these are source-syntax escapes, never subject to a culture.
		string named = value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\t", "\\t", StringComparison.Ordinal);

		if (!named.Any(NeedsNumericEscape))
		{
			return named;
		}

		StringBuilder escaped = new(named.Length + 8);
		foreach (char c in named)
		{
			escaped.Append(NeedsNumericEscape(c) ? EscapeCodeUnit(c) : c.ToString());
		}

		return escaped.ToString();
	}

	/// <summary>
	/// Spells one character that cannot stand raw in a string literal as a numeric escape.
	/// </summary>
	/// <param name="c">A control character or Unicode line terminator.</param>
	/// <returns>The escape sequence.</returns>
	/// <remarks>
	/// Defaults to <c>\uXXXX</c>, which C#, JavaScript, Python and Go all read. Rust writes the code
	/// point in braces, and C and C++ override this with octal, since their <c>\x</c> is greedy and
	/// would swallow a following hex digit.
	/// </remarks>
	protected virtual string EscapeCodeUnit(char c) => $"\\u{(int)c:X4}";

	/// <summary>
	/// Reports whether a character must be written as a numeric escape inside a string literal.
	/// </summary>
	/// <param name="c">The character.</param>
	/// <returns>True for a C0 control, DEL, U+0085, U+2028 or U+2029.</returns>
	private static bool NeedsNumericEscape(char c) =>
		c is < '\u0020' or '\u007F' or '\u0085' or '\u2028' or '\u2029';

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
	/// Spells a floating-point value so that it reads as one.
	/// </summary>
	/// <param name="value">The value to spell.</param>
	/// <returns>The value's round-trip text, with <c>.0</c> added when it would otherwise be an integer.</returns>
	/// <remarks>
	/// Round-trip formatting drops the fraction of a whole number, and <c>2</c> is an integer in every
	/// target: <c>1.0 / 2.0</c> would become integer division in C, C++ and Go, fail to compile in Rust,
	/// and type an inferred local as an integer. A value with a point or an exponent already reads as a
	/// float, and one that is not finite is left as it is — there is no number to add a point to. A
	/// literal that is not finite is spelled by <see cref="SpellNonFiniteDouble"/> instead.
	/// </remarks>
	protected static string FormatDouble(double value) =>
		WithFraction(value.ToString("R", CultureInfo.InvariantCulture), double.IsFinite(value));

	/// <summary>
	/// Spells a double that is NaN or an infinity.
	/// </summary>
	/// <param name="value">The value to spell, which is not finite.</param>
	/// <returns>The language's name for the value.</returns>
	/// <remarks>
	/// JavaScript's <c>NaN</c>, <c>Infinity</c> and <c>-Infinity</c>, which is the one target where the
	/// text round-trip formatting gives is already a name the language knows. Every other language
	/// spells these as a constant of its own, and the C, C++ and Go spellings need an import that
	/// the generator adds through <see cref="RequiredImports"/>.
	/// </remarks>
	protected virtual string SpellNonFiniteDouble(double value) =>
		double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity";

	/// <summary>
	/// Formats a single-precision value so that every target reads it as floating-point.
	/// </summary>
	/// <param name="value">The value to format.</param>
	/// <returns>The value's shortest round-trip spelling, with <c>.0</c> added to a whole number.</returns>
	/// <remarks>
	/// Formatted as a <see cref="float"/> rather than widened to <see cref="double"/> first, which
	/// would write <c>0.1f</c> as <c>0.10000000149011612</c>. The targets without a single-precision
	/// literal read the text as a double, which converts back to the same float.
	/// </remarks>
	protected static string FormatSingle(float value) =>
		WithFraction(value.ToString("R", CultureInfo.InvariantCulture), float.IsFinite(value));

	/// <summary>
	/// Adds <c>.0</c> to a finite number's text when it would otherwise read as an integer.
	/// </summary>
	/// <param name="text">The number's round-trip spelling.</param>
	/// <param name="isFinite">Whether the number is finite.</param>
	/// <returns>The text as a floating-point literal.</returns>
	private static string WithFraction(string text, bool isFinite) =>
		isFinite && text.IndexOfAny(['.', 'E', 'e']) < 0 ? text + ".0" : text;

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

	/// <summary>
	/// Gives the value each member of an enumeration stands for, for a language that has to write
	/// every one out.
	/// </summary>
	/// <param name="enumDecl">The declaration whose members to number.</param>
	/// <returns>One value per member, in order.</returns>
	/// <remarks>
	/// A member with no value of its own is one more than the member before it, which is what every
	/// language with real enumerations gives it; numbering it from its position instead would disagree
	/// with those languages once any member has a value, and could repeat one. When the value being
	/// counted on from is not a plain integer, it is counted on from as an expression, since the
	/// generator cannot work out what it comes to.
	/// </remarks>
	protected static IEnumerable<string> EnumMemberValues(EnumDeclaration enumDecl)
	{
		Ensure.NotNull(enumDecl);

		string? origin = null;
		long offset = 0;

		foreach (EnumMember member in enumDecl.Members)
		{
			if (member.Value is not null)
			{
				bool isNumber = long.TryParse(member.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number);
				origin = isNumber ? null : member.Value;
				offset = isNumber ? number : 0;
				yield return member.Value;
			}
			else
			{
				yield return origin is null
					? offset.ToString(CultureInfo.InvariantCulture)
					: $"({origin}) + {offset.ToString(CultureInfo.InvariantCulture)}";
			}

			offset++;
		}
	}
}
