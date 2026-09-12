// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// A <see cref="LanguageGeneratorBase"/> that owns the node dispatch, so a generator supplies only
/// the syntax its language does not share with the others.
/// </summary>
/// <remarks>
/// Every generator's dispatch was the same switch: structural nodes to their emitters, everything
/// else to <see cref="LanguageGeneratorBase.TryGenerateCommonNode"/>, an unrecognised node to a
/// <see cref="NotSupportedException"/>. Only the operator spelling and the language's name in that
/// message differed. It lives here once, and a derived generator writes three methods —
/// <see cref="GenerateFunctionDeclaration"/>, <see cref="GenerateVariableDeclaration"/> and
/// <see cref="GenerateParameter"/> — plus an operator override when its language needs one.
/// <para>
/// <c>CSharpGenerator</c> deliberately does not derive from this. It indents with a precomputed
/// string rather than a level, emits a comment instead of throwing for an unrecognised node, and
/// stringifies a return expression rather than recursing into it. Those are differences to settle on
/// their own terms, not to smuggle in here.
/// </para>
/// </remarks>
public abstract class StandardLanguageGenerator : LanguageGeneratorBase
{
	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public override bool CanGenerate(AstNode astNode) => CanGenerateStandardNodes(astNode);

	/// <summary>
	/// Dispatches a node to the emitter for its shape.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <exception cref="NotSupportedException">The node is not one this generator emits.</exception>
	protected sealed override void GenerateInternal(AstNode node, CodeBlocker code)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(code);

		switch (node)
		{
			case ClassDeclaration classDecl:
				GenerateClassDeclaration(classDecl, code);
				break;

			case FunctionDeclaration funcDecl:
				GenerateFunctionDeclaration(funcDecl, code);
				break;

			case SourceFile file:
				GenerateSourceFile(file, code);
				break;

			case NamespaceDeclaration namespaceDecl:
				GenerateNamespaceDeclaration(namespaceDecl, code);
				break;

			case CompileTimeAssertion assertion:
				GenerateCompileTimeAssertion(assertion, code);
				break;

			case UsingAlias usingAlias:
				GenerateUsingAlias(usingAlias, code);
				break;

			case ConstructionExpression construction:
				GenerateConstructionExpression(construction, code);
				break;

			case CallExpression call:
				GenerateCallExpression(call, code);
				break;

			case ConditionalExpression conditional:
				GenerateConditionalExpression(conditional, code);
				break;

			case ExpressionStatement statement:
				GenerateExpressionStatement(statement, code);
				break;

			case EnumDeclaration enumDecl:
				GenerateEnumDeclaration(enumDecl, code);
				break;

			case PropertyDeclaration property:
				GeneratePropertyDeclaration(property, code);
				break;

			case FieldDeclaration field:
				GenerateFieldDeclaration(field, code);
				break;

			case EntryPoint entryPoint:
				GenerateEntryPoint(entryPoint, code);
				break;

			case Parameter parameter:
				GenerateParameter(parameter, code, 0);
				break;

			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, code);
				break;

			case BinaryExpression binaryExpr:
				GenerateBinaryExpression(binaryExpr, code, GetOperatorSpelling(binaryExpr.Operator));
				break;

			case UnaryExpression unaryExpr:
				GenerateUnaryExpression(unaryExpr, code, GetUnaryOperatorSpelling(unaryExpr.Operator));
				break;

			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, code);
				break;

			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, code);
				break;

			default:
				if (!TryGenerateCommonNode(node, code))
				{
					throw new NotSupportedException($"Unsupported node type for {DisplayName} generation: {node.GetNodeTypeName()}");
				}

				break;
		}
	}

	/// <summary>
	/// Emits a function declaration, including its body.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code);

	/// <summary>
	/// Emits a class declaration, including its members.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code);

	/// <summary>
	/// Emits a namespace and its members.
	/// </summary>
	/// <param name="namespaceDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The default emits the members and nothing around them, which is right for a language whose
	/// unit of naming is the file. A language that writes a namespace overrides this.
	/// </remarks>
	protected virtual void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(namespaceDecl, code);
		WriteMembers(namespaceDecl.Members, code);
	}

	/// <summary>
	/// Writes a run of declarations, separated as this language separates them.
	/// </summary>
	/// <param name="members">The declarations to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The same walk <see cref="LanguageGeneratorBase.GenerateSourceFile"/> does over a file's
	/// members, for the emitters that have a run of declarations to write and no file around them.
	/// </remarks>
	protected void WriteMembers(IEnumerable<AstNode> members, CodeBlocker code)
	{
		Ensure.NotNull(members);
		Ensure.NotNull(code);

		AstNode? previous = null;
		foreach (AstNode member in members)
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
	/// Reports whether two adjacent declarations belong in one block.
	/// </summary>
	/// <param name="previous">The declaration already written.</param>
	/// <param name="member">The declaration about to be written.</param>
	/// <returns>True when no blank line belongs between them.</returns>
	/// <remarks>
	/// Two of a kind that say nothing about themselves stay together, which is what keeps a run of
	/// aliases, of constants or of assertions about one type reading as one block rather than as a
	/// paragraph each. A documented member needs air above it or its first comment line butts against
	/// the member before it and reads as belonging to that one.
	/// <para>
	/// The rule rather than the override, because <see cref="LanguageGeneratorBase.NeedsSeparation"/> is
	/// the question and this is one answer to it: the C family, Rust and Go all give this one, while Python and
	/// JavaScript keep the default of a blank line between everything.
	/// </para>
	/// </remarks>
	protected static bool GroupsWith(AstNode previous, AstNode member)
	{
		Ensure.NotNull(previous);
		Ensure.NotNull(member);

		return previous.GetType() == member.GetType()
			&& !IsDocumented(previous)
			&& !IsDocumented(member);
	}

	/// <summary>
	/// Reports whether a declaration carries documentation.
	/// </summary>
	/// <param name="member">The declaration to test.</param>
	/// <returns>True when it does.</returns>
	protected static bool IsDocumented(AstNode member) =>
		member is IHasDocumentation documented && documented.Documentation.Count > 0;

	/// <summary>
	/// Emits something that must be true when the program is built.
	/// </summary>
	/// <param name="assertion">The assertion to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code);

	/// <summary>
	/// Emits an alias giving a type a second name.
	/// </summary>
	/// <param name="usingAlias">The alias to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code);

	/// <summary>
	/// Emits an expression that builds a value.
	/// </summary>
	/// <param name="construction">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code);

	/// <summary>
	/// Emits an enumeration declaration, including its members.
	/// </summary>
	/// <param name="enumDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code);

	/// <summary>
	/// Emits a field of a type.
	/// </summary>
	/// <param name="field">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code);

	/// <summary>
	/// Emits a property, as whichever of the two things it is in this language.
	/// </summary>
	/// <param name="declaration">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The default is for the four targets that have no properties, and it does not approximate
	/// one: it separates the property into the declarations it is made of and emits those through
	/// the emitters that already exist. A property whose accessors have no bodies is a field with a
	/// storage location the compiler supplies, so it comes out as a field; one with bodies is a
	/// pair of functions, so it comes out as the pair. Both are what a person would have written.
	/// <para>
	/// The one thing the field form loses is that a property may be readable and not writable,
	/// which a plain field cannot be. That is said rather than dropped.
	/// </para>
	/// </remarks>
	protected virtual void GeneratePropertyDeclaration(PropertyDeclaration declaration, CodeBlocker code)
	{
		Ensure.NotNull(declaration);
		Ensure.NotNull(code);

		bool first = true;
		foreach (AstNode lowered in Separate(declaration))
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;
			GenerateInternal(lowered, code);
		}
	}

	/// <summary>
	/// Gets the convention this target spells an invented member name in.
	/// </summary>
	/// <remarks>
	/// For the names a generator has to make up rather than the ones it was given. A property
	/// becoming a pair of functions has no name for its setter until somebody invents one, and
	/// inventing it in the wrong convention is how generated code announces itself.
	/// </remarks>
	protected virtual NamingStyle MemberNaming => NamingStyle.Camel;

	/// <summary>
	/// A type's members, with every property separated into the declarations it is made of.
	/// </summary>
	/// <param name="classDecl">The type being emitted.</param>
	/// <returns>The declaration itself when it holds no properties; otherwise a copy of it whose
	/// members are fields and functions.</returns>
	/// <remarks>
	/// Done to the member list rather than at the point each member is written, and that is the
	/// whole of why it works. Three of these generators route a type's members by what they are —
	/// Rust puts the data in a <c>struct</c> and the behaviour in an <c>impl</c>, Go writes the
	/// fields in the type and the methods beside it, C lowers a method to a free function taking
	/// the instance — and every one of those routes reads the member list before anything is
	/// written. A property separated afterwards arrives too late to be routed and comes out
	/// wherever it happened to be standing.
	/// <para>
	/// So it is separated first, and each generator's existing routing then sees an ordinary field
	/// or an ordinary function and needs to know nothing about properties at all.
	/// </para>
	/// </remarks>
	protected ClassDeclaration Separated(ClassDeclaration classDecl)
	{
		Ensure.NotNull(classDecl);

		if (!classDecl.Members.OfType<PropertyDeclaration>().Any())
		{
			return classDecl;
		}

		ClassDeclaration separated = (ClassDeclaration)classDecl.Clone();
		separated.Members.Clear();

		foreach (AstNode member in classDecl.Members)
		{
			if (member is PropertyDeclaration property)
			{
				foreach (AstNode part in Separate(property))
				{
					separated.Members.Add(part);
				}

				continue;
			}

			separated.Members.Add(member);
		}

		return separated;
	}

	/// <summary>
	/// Separates a property into the declarations it is made of.
	/// </summary>
	/// <param name="property">The property to separate.</param>
	/// <returns>A field, or the accessors, in the order they should be written.</returns>
	private IEnumerable<AstNode> Separate(PropertyDeclaration property)
	{
		if (property.IsAutomatic)
		{
			// The one thing the field form cannot carry: a property may be readable and not
			// writable, and a field is neither or both. Said in the field's own documentation
			// rather than in a note beside it, so it travels with the declaration to wherever this
			// target puts it.
			IEnumerable<string> documentation = property.CanWrite
				? property.Documentation
				: [.. property.Documentation, $"{property.Name} is read-only, which a field is not."];

			yield return new FieldDeclaration(property.Name ?? "value", property.Type)
			{
				Visibility = property.Visibility,
				IsStatic = property.IsStatic,
				Documentation = [.. documentation],
				Annotations = [.. property.Annotations.Select(annotation => annotation.Clone())],
			};

			yield break;
		}

		if (property.CanRead)
		{
			FunctionDeclaration getter = new(property.GetterName(MemberNaming))
			{
				ReturnType = property.Type,
				Visibility = property.Visibility,
				IsStatic = property.IsStatic,
				IsReadOnly = true,
				Documentation = [.. property.Documentation],
				Annotations = [.. property.Annotations.Select(annotation => annotation.Clone())],
				Body = [.. property.GetterBody.Select(statement => statement.Clone())],
			};

			yield return getter;
		}

		if (property.CanWrite)
		{
			FunctionDeclaration setter = new(property.SetterName(MemberNaming))
			{
				ReturnType = new TypeReference("void"),
				Visibility = property.Visibility,
				IsStatic = property.IsStatic,
				Body = [.. property.SetterBody.Select(statement => statement.Clone())],
			};

			setter.Parameters.Add(new Parameter("value") { Type = property.Type ?? new TypeReference("object") });
			yield return setter;
		}
	}

	/// <summary>
	/// Emits the program's entry point, and whatever else the language needs in order to run it.
	/// </summary>
	/// <param name="entryPoint">The entry point to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code);

	/// <summary>
	/// Emits the members of a class, one after another.
	/// </summary>
	/// <param name="classDecl">The class whose members to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A member is dispatched like any other node, so a nested class, a method and a field each come
	/// out through the emitter for its own shape and nesting needs no separate code path.
	/// </remarks>
	protected void GenerateClassMembers(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);

		foreach (AstNode member in classDecl.Members)
		{
			GenerateInternal(member, code);
		}
	}

	/// <summary>
	/// Emits a variable declaration as a complete statement.
	/// </summary>
	/// <param name="varDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected abstract void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code);

	/// <summary>
	/// Emits one parameter, without any separator.
	/// </summary>
	/// <param name="parameter">The parameter to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="position">The parameter's position, used to name an unnamed parameter.</param>
	protected abstract void GenerateParameter(Parameter parameter, CodeBlocker code, int position);

	/// <summary>
	/// Appends a parameter's default value, when it has one.
	/// </summary>
	/// <param name="parameter">The parameter whose default to append.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A default value is how every target language expresses <see cref="Parameter.IsOptional"/>;
	/// none of them has separate syntax for it.
	/// </remarks>
	protected static void AppendDefaultValue(Parameter parameter, CodeBlocker code)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" = {parameter.DefaultValue}");
		}
	}

	/// <summary>
	/// Emits a comma-separated parameter list, without the surrounding parentheses.
	/// </summary>
	/// <param name="parameters">The parameters to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	protected void GenerateParameterList(IReadOnlyList<Parameter> parameters, CodeBlocker code)
	{
		Ensure.NotNull(parameters);
		Ensure.NotNull(code);

		for (int i = 0; i < parameters.Count; i++)
		{
			if (i > 0)
			{
				code.Write(", ");
			}

			GenerateParameter(parameters[i], code, i);
		}
	}

	/// <summary>
	/// Writes a list of elements between delimiters, one per line when the elements are themselves
	/// lists.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="open">What opens the list.</param>
	/// <param name="close">What closes it.</param>
	/// <param name="empty">What to write instead when there are no elements at all.</param>
	/// <remarks>
	/// Every language that writes a list of values writes it the same way — the delimiters differ,
	/// and where a value names the member it is for, so does that spelling. Both are the caller's,
	/// through the parameters and through <see cref="WriteDesignator"/>.
	/// <para>
	/// A list of values is a value and belongs on one line; a list whose elements are themselves
	/// lists is a table, and a table written on one line is a row of a diff nobody can read. The test
	/// is the shape of the data rather than a column count, because a generated file has no idea how
	/// wide anyone's editor is and a rule about that would have to be guessed.
	/// </para>
	/// </remarks>
	protected void WriteElementList(
		ConstructionExpression construction,
		CodeBlocker code,
		string open,
		string close,
		string empty)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		if (construction.Arguments.Count == 0)
		{
			code.Write(empty);
			return;
		}

		if (SpansLines(construction))
		{
			WriteStackedList(construction, code, open, close);
			return;
		}

		code.Write($"{open}{ListPadding}");
		for (int index = 0; index < construction.Arguments.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			WriteListElement(construction.Arguments[index], code);
		}

		code.Write($"{ListPadding}{close}");
	}

	/// <summary>
	/// Gets what stands between a list's delimiters and its elements when it is written on one line.
	/// </summary>
	/// <remarks>
	/// A space everywhere but Go, which writes <c>Point{X: 1}</c>. That is not a preference there:
	/// <c>gofmt</c> writes it that way and nobody gets a say, which makes the padding part of the
	/// language's spelling of a list rather than of anyone's taste in them.
	/// </remarks>
	protected virtual string ListPadding => " ";

	/// <summary>
	/// Writes a list one element per line.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="open">What opens the list.</param>
	/// <param name="close">What closes it.</param>
	/// <remarks>
	/// A trailing comma after the last element, which every target language allows in a list and
	/// which keeps adding a row to a generated table from touching the row above it in the diff.
	/// </remarks>
	private void WriteStackedList(ConstructionExpression construction, CodeBlocker code, string open, string close)
	{
		code.WriteLine(open);
		code.Indent();

		foreach (AstNode argument in construction.Arguments)
		{
			WriteListElement(argument, code);
			code.WriteLine(",");
		}

		code.Outdent();
		code.Write(close);
	}

	/// <summary>
	/// Writes one element of a list, which may name the member it is for.
	/// </summary>
	/// <param name="argument">The element to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Overridable for a language with a shorter way to say one of these — Rust's field-init
	/// shorthand is the one that exists here.
	/// </remarks>
	protected virtual void WriteListElement(AstNode argument, CodeBlocker code)
	{
		if (argument is MemberInitialiser designated)
		{
			WriteDesignator(designated.Name ?? string.Empty, code);
			WriteListValue(designated.Value ?? new VariableReference(string.Empty), code);
			return;
		}

		WriteListValue(argument, code);
	}

	/// <summary>
	/// Writes the part of an element that says which member it is for.
	/// </summary>
	/// <param name="name">The member's name.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// <c>name: value</c> is what a language whose lists are keyed writes — Rust's struct literal
	/// and JavaScript's object literal both. C and C++ write a designator instead, and override this.
	/// </remarks>
	protected virtual void WriteDesignator(string name, CodeBlocker code)
	{
		Ensure.NotNull(code);
		code.Write($"{name}: ");
	}

	/// <summary>
	/// Writes what one element of a list is.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Ordinary generation, unless a language has something to say about a value that stands inside
	/// a list rather than on its own — which C does, since only there may it leave the type out.
	/// </remarks>
	protected virtual void WriteListValue(AstNode value, CodeBlocker code) => GenerateInternal(value, code);

	/// <summary>
	/// Reports whether a list is worth breaking across lines.
	/// </summary>
	/// <param name="construction">The expression to judge.</param>
	/// <returns><see langword="true"/> when it should be written one element per line.</returns>
	private static bool SpansLines(ConstructionExpression construction) =>
		construction.Arguments.Any(argument =>
			argument is ConstructionExpression or MemberInitialiser { Value: ConstructionExpression });

	/// <summary>
	/// Names every operator the AST can spell, for a language that cannot overload one and so has to
	/// call it something.
	/// </summary>
	/// <param name="spell">How the language writes a name made of words.</param>
	/// <returns>The name for each symbol the AST can spell.</returns>
	/// <remarks>
	/// The names come from the AST's own operator vocabulary rather than from a table written beside
	/// it, so an operator added to <see cref="BinaryOperator"/> is named without anyone remembering
	/// to name it — and one added without a spelling is left out rather than throwing before anything
	/// has run.
	/// <para>
	/// A symbol both kinds of operator share is named for the binary one, which is what a reader of
	/// <c>a - b</c> means; the unary declaration of it is told apart by taking no operand beside the
	/// instance, which is the caller's to notice.
	/// </para>
	/// </remarks>
	protected static Dictionary<string, string> BuildOperatorNames(Func<string, string> spell)
	{
		Ensure.NotNull(spell);

		Dictionary<string, string> names = new(StringComparer.Ordinal);

		foreach (BinaryOperator op in Enum.GetValues<BinaryOperator>().Where(HasSymbol))
		{
			names[OperatorSymbols.GetSymbol(op)] = spell(op.ToString());
		}

		foreach (UnaryOperator op in Enum.GetValues<UnaryOperator>().Where(HasSymbol))
		{
			names.TryAdd(OperatorSymbols.GetSymbol(op), spell(op.ToString()));
		}

		return names;
	}

	/// <summary>
	/// Names the unary operators alone.
	/// </summary>
	/// <param name="spell">How the language writes a name made of words.</param>
	/// <returns>The name for each symbol a unary operator can be spelled with.</returns>
	/// <remarks>
	/// For a language that tells a unary declaration apart by its arity and so must not name it after
	/// the binary operator sharing its symbol. <c>-</c> is the whole of the problem: a type declaring
	/// both would otherwise declare the same name twice, which is the one outcome worse than an odd
	/// name.
	/// </remarks>
	protected static Dictionary<string, string> BuildUnaryOperatorNames(Func<string, string> spell)
	{
		Ensure.NotNull(spell);

		Dictionary<string, string> names = new(StringComparer.Ordinal);

		foreach (UnaryOperator op in Enum.GetValues<UnaryOperator>().Where(HasSymbol))
		{
			names[OperatorSymbols.GetSymbol(op)] = spell(op.ToString());
		}

		return names;
	}

	/// <summary>
	/// Reports whether the AST can spell an operator at all.
	/// </summary>
	/// <param name="op">The operator to test.</param>
	/// <returns>True when it has a symbol.</returns>
	private static bool HasSymbol(BinaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) && symbol is not null;

	/// <inheritdoc cref="HasSymbol(BinaryOperator)"/>
	/// <param name="op">The operator to test.</param>
	private static bool HasSymbol(UnaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) && symbol is not null;

	/// <summary>
	/// Spells a binary operator in the target language.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Defaults to the C-family set. A language that spells one operator differently overrides this
	/// and defers the rest to <see cref="LanguageGeneratorBase.GetBinaryOperator"/>.
	/// </remarks>
	protected virtual string GetOperatorSpelling(BinaryOperator op) => GetBinaryOperator(op);

	/// <summary>
	/// Spells a unary operator in the target language.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Defaults to the C-family set. A language that spells one operator differently overrides this
	/// and defers the rest to <see cref="LanguageGeneratorBase.GetUnaryOperator"/>. A word operator is
	/// separated from its operand by the emitter, so an override returns the bare word.
	/// </remarks>
	protected virtual string GetUnaryOperatorSpelling(UnaryOperator op) => GetUnaryOperator(op);
}
