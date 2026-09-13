// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates Rust code from AST nodes.
/// </summary>
/// <remarks>
/// Rust is the first target that separates what a type *is* from what it *does* and means it: data
/// goes in a <c>struct</c>, behaviour in an <c>impl</c> block, and a set of members an implementation
/// supplies is a <c>trait</c>. That is the same split C forced, and the opposite of the reason — C
/// had no member functions, while Rust has them and keeps them somewhere else on purpose.
/// <para>
/// It is also the first target with a real answer to most of what the AST says and the others had to
/// approximate. An enumeration's variants are scoped by their enumeration, so nothing needs
/// prefixing; a fixed underlying type is <c>#[repr]</c>; a compile-time assertion is
/// <c>const _: () = assert!(…)</c>; a pure function is <c>#[must_use]</c>; a destructor is
/// <c>impl Drop</c>; an operator is the <c>std::ops</c> trait for it; a conversion is
/// <c>impl From</c>. What is left over is inheritance, which Rust does not have.
/// </para>
/// <para>
/// The AST's type names are the same language-neutral set the other generators consume (<c>str</c>,
/// <c>int</c>, <c>bool</c>, …), so they are mapped to Rust spellings; anything unrecognised is
/// emitted verbatim on the assumption the caller meant a Rust type. Names are written as they are
/// given rather than recased, for the reason every generator here leaves names alone: renaming a
/// declaration would not rename the references to it.
/// </para>
/// </remarks>
public class RustGenerator : StandardLanguageGenerator
{
	/// <summary>
	/// What a declaration that never said what type it is gets.
	/// </summary>
	/// <remarks>
	/// A type is optional on every node that carries one, because a half-built AST is a thing the
	/// editor has to be able to hold. Rust's most general type is a boxed <c>Any</c>, which keeps the
	/// output compiling while making it obvious which declaration was never finished.
	/// </remarks>
	private const string UnknownTypeName = "object";

	/// <summary>
	/// The name a derived struct holds its base in.
	/// </summary>
	/// <remarks>
	/// Rust has no inheritance, and the nearest thing a struct can do is hold one. The member is
	/// named for what it is rather than for the type, so it is the same word whatever it derives
	/// from and renaming the base does not rename the member.
	/// </remarks>
	private const string BaseMemberName = "base";

	private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "str", "String" },
		{ "string", "String" },
		{ "int", "i32" },
		{ "long", "i64" },
		{ "float", "f32" },
		{ "double", "f64" },
		{ "bool", "bool" },
		{ "void", "()" },
		{ "object", "Box<dyn std::any::Any>" }
	};

	/// <summary>
	/// The owned types whose borrowed form is a different type rather than a reference to them.
	/// </summary>
	/// <remarks>
	/// <c>&amp;String</c> and <c>&amp;Vec&lt;T&gt;</c> are the two types Rust asks you not to write:
	/// a borrow of a string is a <c>&amp;str</c> and a borrow of a sequence is a <c>&amp;[T]</c>,
	/// which every caller of the owned form can also produce. Keyed by the owned spelling, since
	/// that is what the mapping has already produced by the time a borrow is being written.
	/// </remarks>
	private static readonly Dictionary<string, string> BorrowedForms = new(StringComparer.Ordinal)
	{
		{ "String", "str" }
	};

	/// <summary>
	/// How many type declarations enclose what is being written.
	/// </summary>
	/// <remarks>
	/// A depth rather than a flag, so a type declared inside a type leaves the count right when it
	/// closes. It decides what a field is: inside a struct it is a member, and at module scope it is
	/// a <c>const</c> or a <c>static</c>, which Rust spells differently and requires a value for.
	/// </remarks>
	private int insideType;

	/// <summary>
	/// Whether what is being written is a member of a trait declaration.
	/// </summary>
	/// <remarks>
	/// A member with no body is a requirement rather than a mistake, which is the whole point of
	/// declaring it there.
	/// </remarks>
	private bool insideTrait;

	/// <summary>
	/// Whether what is being written is a member of a trait implementation.
	/// </summary>
	private bool insideTraitImplementation;

	/// <summary>
	/// The bounded parameter list every <c>impl</c> block for the type being written needs.
	/// </summary>
	/// <remarks>
	/// Held rather than passed because the four places that open an <c>impl</c> — the inherent
	/// block, an operator, a conversion and <c>Drop</c> — are reached by three different routes
	/// from the type that owns them, and every one of them has to say the same thing. A generic
	/// type whose <c>impl</c> block forgot the parameter does not compile, which makes this the one
	/// piece of state here that is load-bearing rather than a convenience.
	/// </remarks>
	private string implBounds = string.Empty;

	/// <summary>
	/// Spells type parameters where they are being declared, bounds and all.
	/// </summary>
	/// <param name="parameters">The declaration's type parameters.</param>
	/// <returns>Something like <c>&lt;T: INumber&lt;T&gt;&gt;</c>, or an empty string.</returns>
	/// <remarks>
	/// A trait bound is exactly what <see cref="TypeConstraintKind.Implements"/> means, and
	/// <c>Default</c> is exactly what <see cref="TypeConstraintKind.Constructible"/> means. The
	/// other two have no bound at all: every Rust type is a value, and whether one is referenced
	/// is a property of the binding rather than of the type.
	/// </remarks>
	private static string SpellParameterDeclarations(IEnumerable<TypeParameter> parameters)
	{
		string[] declared = [.. parameters.Select(SpellOneParameter)];

		return declared.Length == 0 ? string.Empty : $"<{string.Join(", ", declared)}>";
	}

	private static string SpellOneParameter(TypeParameter parameter)
	{
		string[] bounds =
		[
			.. parameter.Constraints
				.Select(SpellBound)
				.Where(bound => bound.Length > 0),
		];

		return bounds.Length == 0 ? parameter.Name : $"{parameter.Name}: {string.Join(" + ", bounds)}";
	}

	private static string SpellBound(TypeConstraint constraint) => constraint.Kind switch
	{
		TypeConstraintKind.Implements => SpellType(constraint.Type ?? new TypeReference(UnknownTypeName)),
		TypeConstraintKind.Constructible => "Default",
		_ => string.Empty,
	};

	/// <summary>
	/// Spells type parameters where they are being used, names only.
	/// </summary>
	/// <param name="parameters">The declaration's type parameters.</param>
	/// <returns>Something like <c>&lt;T&gt;</c>, or an empty string.</returns>
	/// <remarks>
	/// The bounds belong to the declaration and are a repetition anywhere else, which Rust warns
	/// about: <c>impl&lt;T: Bound&gt; Mass&lt;T&gt;</c> declares the parameter once and then names
	/// it.
	/// </remarks>
	private static string SpellParameterArguments(IEnumerable<TypeParameter> parameters)
	{
		string[] names = [.. parameters.Select(parameter => parameter.Name)];

		return names.Length == 0 ? string.Empty : $"<{string.Join(", ", names)}>";
	}

	/// <summary>
	/// Whether a member should say nothing about who may see it.
	/// </summary>
	/// <remarks>
	/// An associated item is exactly as visible as the trait it belongs to, and Rust rejects a
	/// visibility on one rather than ignoring it — which makes this the difference between a file
	/// that compiles and one that does not, not a matter of style.
	/// </remarks>
	private bool IsAssociatedItem => insideTrait || insideTraitImplementation;

	/// <summary>
	/// The <c>std::ops</c> trait each binary operator is spelled through.
	/// </summary>
	/// <remarks>
	/// Rust has operator overloading and spells it as a trait implementation, so an operator
	/// declaration is not a function with an odd name — it is <c>impl Add for Point</c> with a method
	/// called <c>add</c>. Each of these is one operator to one trait method, which is what makes them
	/// mechanical; see <see cref="ComparisonOperators"/> for the ones that are not.
	/// </remarks>
	private static readonly Dictionary<string, OperatorTrait> BinaryOperatorTraits = new(StringComparer.Ordinal)
	{
		{ "+", new("std::ops::Add", "add") },
		{ "-", new("std::ops::Sub", "sub") },
		{ "*", new("std::ops::Mul", "mul") },
		{ "/", new("std::ops::Div", "div") },
		{ "%", new("std::ops::Rem", "rem") },
		{ "&", new("std::ops::BitAnd", "bitand") },
		{ "|", new("std::ops::BitOr", "bitor") },
		{ "^", new("std::ops::BitXor", "bitxor") },
		{ "<<", new("std::ops::Shl", "shl") },
		{ ">>", new("std::ops::Shr", "shr") },
		{ "==", new("PartialEq", "eq", ReturnsBool: true) },
	};

	/// <summary>
	/// The <c>std::ops</c> trait each unary operator is spelled through.
	/// </summary>
	/// <remarks>
	/// Kept apart from the binary ones because <c>-</c> is in both, and which one a declaration means
	/// is decided by whether it takes an operand beside the instance.
	/// </remarks>
	private static readonly Dictionary<string, OperatorTrait> UnaryOperatorTraits = new(StringComparer.Ordinal)
	{
		{ "-", new("std::ops::Neg", "neg") },
		{ "!", new("std::ops::Not", "not") },
	};

	/// <summary>
	/// The operators Rust supplies from another one and will not let a type define by itself.
	/// </summary>
	/// <remarks>
	/// <c>!=</c> comes from <c>PartialEq</c> and the four orderings from <c>PartialOrd</c>'s
	/// <c>partial_cmp</c>; <c>&amp;&amp;</c> and <c>||</c> short-circuit and are not overloadable at
	/// all. A declaration of one of these is not dropped — it is written as a note naming the trait
	/// the type should implement instead, because the alternative is a generated file that silently
	/// lost a comparison somebody asked for.
	/// </remarks>
	private static readonly Dictionary<string, string> ComparisonOperators = new(StringComparer.Ordinal)
	{
		{ "!=", "PartialEq, which supplies != from ==" },
		{ "<", Ordering },
		{ "<=", Ordering },
		{ ">", Ordering },
		{ ">=", Ordering },
		{ "&&", ShortCircuit },
		{ "||", ShortCircuit },
	};

	/// <summary>
	/// What to implement instead of one of the orderings, which are four spellings of one decision.
	/// </summary>
	private const string Ordering = "PartialOrd, which supplies the orderings from partial_cmp";

	/// <summary>
	/// What to implement instead of a short-circuiting operator, which is nothing.
	/// </summary>
	private const string ShortCircuit = "nothing: a short-circuiting operator is not overloadable";

	/// <summary>
	/// A trait an operator is spelled through.
	/// </summary>
	/// <param name="Name">The trait's path.</param>
	/// <param name="Method">The method the operator calls.</param>
	/// <param name="ReturnsBool">
	/// Whether the method answers <c>bool</c> and borrows its operands, as <c>PartialEq::eq</c> does,
	/// rather than consuming them and naming an <c>Output</c>, as the arithmetic traits do.
	/// </param>
	private sealed record OperatorTrait(string Name, string Method, bool ReturnsBool = false);

	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "rust";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "Rust";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "rs";

	/// <inheritdoc/>
	/// <remarks>
	/// Enforced rather than conventional: rustc warns on a member name that is not snake case, so an invented one in any other convention makes the generated file noisy to build.
	/// </remarks>
	protected override NamingStyle MemberNaming => NamingStyle.Snake;

	/// <inheritdoc/>
	protected override string? SpellAnnotation(Annotation annotation) => $"#[{annotation}]";
	/// <inheritdoc/>
	/// <remarks>
	/// <c>use</c>, which is what Rust writes where the others write an include or an import. A path
	/// that already ends in a semicolon is left alone, so a caller who wrote the whole item — a
	/// grouped <c>use a::{b, c};</c>, or an attribute — gets what they wrote.
	/// </remarks>
	protected override string? SpellImport(string import)
	{
		Ensure.NotNull(import);
		return import.EndsWith(';') ? import : $"use {import};";
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Rust has modules, so a namespace is one — and a dotted name is nested modules rather than one
	/// module with a long name, because that is what the name says and what a caller reaching into it
	/// would have to write either way.
	/// </remarks>
	protected override void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(namespaceDecl, code);
		WriteModule([.. NamespaceDeclaration.Split(namespaceDecl.Name)], namespaceDecl.Members, code);
	}

	/// <summary>
	/// Writes one module, and whatever modules are nested inside it.
	/// </summary>
	/// <param name="path">The names left to open, outermost first.</param>
	/// <param name="members">What the innermost module holds.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteModule(IReadOnlyList<string> path, IReadOnlyCollection<AstNode> members, CodeBlocker code)
	{
		if (path.Count == 0)
		{
			WriteMembers(members, code);
			return;
		}

		// The line is left open, so the scope's brace lands on it: Rust braces hang.
		code.Write($"pub mod {path[0]} ");

		using Scope module = new(code);
		WriteModule([.. path.Skip(1)], members, code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Two of a kind that say nothing about themselves stay together, which keeps a run of type
	/// aliases or of constants reading as one block rather than as a paragraph each.
	/// </remarks>
	protected override bool NeedsSeparation(AstNode previous, AstNode member) => !GroupsWith(previous, member);

	/// <inheritdoc/>
	/// <remarks>
	/// A type declaration becomes a <c>struct</c> holding the data and an <c>impl</c> block holding
	/// the behaviour, which is the split Rust insists on. What does not go in the inherent block goes
	/// in a trait implementation of its own: a destructor is <c>impl Drop</c>, an operator is the
	/// <c>std::ops</c> trait for it, and a conversion is <c>impl From</c>.
	/// <para>
	/// An interface is a <c>trait</c> instead, which is the one mapping here that needs no
	/// explaining: a set of members an implementation supplies is what a trait is, and a base type is
	/// a supertrait.
	/// </para>
	/// <para>
	/// <see cref="ClassDeclaration.BaseType"/> on a struct is a field holding the base, and that is
	/// the one place Rust has nothing better. Inheritance is what Rust deliberately left out, so the
	/// field is written with a note rather than pretending <c>Deref</c> makes it inheritance.
	/// </para>
	/// <para>
	/// Nothing is derived. <c>#[derive(Clone, Debug)]</c> would be the obvious thing to write on a
	/// generated data type and would stop it compiling the moment one field does not implement them,
	/// which is a decision about the type rather than about the declaration.
	/// </para>
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		classDecl = Separated(classDecl);

		string name = classDecl.Name ?? "UnnamedStruct";

		foreach (AstNode nested in classDecl.Members.Where(IsTypeDeclaration))
		{
			GenerateInternal(nested, code);
			code.NewLine();
		}

		if (classDecl.IsSpecialisation)
		{
			GenerateSpecialisation(classDecl, name, code);
			return;
		}

		if (classDecl.Kind == TypeDeclarationKind.Interface)
		{
			GenerateTrait(classDecl, name, code);
			return;
		}

		// Everything below writes `Name<T>` where it names the type and `impl<T: Bound>` where it
		// opens a block, which is the one shape that compiles: the parameter is declared once, on
		// the impl, and named everywhere else.
		implBounds = SpellParameterDeclarations(classDecl.TypeParameters);
		string applied = $"{name}{SpellParameterArguments(classDecl.TypeParameters)}";

		GenerateStruct(classDecl, name, code);

		List<FunctionDeclaration> functions =
		[
			.. classDecl.Members.OfType<FunctionDeclaration>().Where(member => member.Kind is not FunctionKind.Destructor),
		];

		List<FunctionDeclaration> inherent =
		[
			.. functions.Where(member => member.Kind is FunctionKind.Method or FunctionKind.Constructor),
		];

		if (inherent.Count > 0)
		{
			code.NewLine();
			code.Write($"impl{implBounds} {applied} ");

			using Scope block = new(code);
			bool first = true;
			foreach (FunctionDeclaration function in inherent)
			{
				if (!first)
				{
					code.NewLine();
				}

				first = false;
				GenerateFunction(function, code, name);
			}
		}

		foreach (FunctionDeclaration function in functions.Except(inherent))
		{
			code.NewLine();
			GenerateTraitImplementation(function, applied, code);
		}

		foreach (FunctionDeclaration destructor in classDecl.Members
			.OfType<FunctionDeclaration>()
			.Where(member => member.Kind == FunctionKind.Destructor))
		{
			code.NewLine();
			GenerateDrop(destructor, applied, code);
		}

		implBounds = string.Empty;
	}

	/// <summary>
	/// Writes a declaration that is for a type rather than of one.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="name">The name being specialised.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// This is the one place Rust answers C++ exactly. An explicit specialisation attaches facts to a
	/// type without touching the type, and <c>impl Describe for RigidBody</c> is how Rust does that —
	/// so a specialisation is a trait implementation, its fields are associated constants and its
	/// functions are its methods.
	/// <para>
	/// Rust's own <c>specialization</c> feature is a different thing and is not what this needs: that
	/// one is about a more specific implementation overriding a blanket one, and is unstable. This
	/// needs no feature at all.
	/// </para>
	/// </remarks>
	private void GenerateSpecialisation(ClassDeclaration classDecl, string name, CodeBlocker code)
	{
		GenerateDocumentation(classDecl, code);

		string arguments = string.Join(", ", classDecl.SpecialisationArguments.Select(SpellType));
		code.Write($"impl {name} for {arguments} ");

		using Scope body = new(code);

		insideTraitImplementation = true;
		WriteMembers(classDecl.Members, code);
		insideTraitImplementation = false;
	}

	/// <summary>
	/// Reports whether a member declares a type rather than data or behaviour.
	/// </summary>
	/// <param name="member">The member to test.</param>
	/// <returns>True when it declares a type.</returns>
	/// <remarks>
	/// A type declared inside another is written beside it. Rust nests modules rather than types, so
	/// a type declared within a type has nowhere to go except the module they are both in.
	/// </remarks>
	private static bool IsTypeDeclaration(AstNode member) =>
		member is ClassDeclaration or EnumDeclaration or UsingAlias;

	/// <summary>
	/// Writes the data half of a type declaration.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="name">The name it is written under.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateStruct(ClassDeclaration classDecl, string name, CodeBlocker code)
	{
		GenerateDocumentation(classDecl, code);
		WriteAnnotations(classDecl.Annotations, code);

		// A trait a struct implements needs an impl block, and an impl block needs the bodies of
		// the methods it supplies, which the declaration does not have: the members here belong to
		// the struct rather than to any one of the traits. So it is written down instead.
		if (classDecl.Interfaces.Count > 0)
		{
			WriteInexpressible(
				code,
				$"implements {string.Join(", ", classDecl.Interfaces.Select(SpellType))}: "
					+ "each needs an impl block of its own, which this declaration does not say how to fill");
		}

		WriteTypePromises(classDecl, code, recordIsSpelled: true);
		WriteUnaskedConstraints(
			classDecl.TypeParameters,
			code,
			TypeConstraintKind.Implements,
			TypeConstraintKind.Constructible);

		// What a record asks for is exactly what derive supplies, which makes this the one target
		// besides C# that has a word for it rather than a comment about it. Clone is the copy,
		// PartialEq the comparison, Debug the readable form. Written last of the lines above the
		// struct, so that it sits against the item it applies to rather than behind the notes.
		if (classDecl.IsRecord)
		{
			code.WriteLine("#[derive(Clone, Debug, PartialEq)]");
		}

		code.Write($"{SpellVisibilityOf(classDecl)}struct {name}{SpellParameterDeclarations(classDecl.TypeParameters)} ");

		using Scope body = new(code);

		insideType++;

		if (classDecl.BaseType is TypeReference baseType)
		{
			WriteInexpressible(code, "the base: Rust has no inheritance, so it is held rather than derived from");
			code.WriteLine($"pub {BaseMemberName}: {SpellType(baseType)},");
		}

		foreach (AstNode member in classDecl.Members)
		{
			switch (member)
			{
				case VariableDeclaration field:
					WriteStructMember(field.Name, field.Type, VisibilityOf(field), code);
					break;

				case FieldDeclaration field:
					GenerateDocumentation(field, code);
					WriteStructMember(field.Name, field.Type, field.Visibility, code);
					break;

				default:
					break;
			}
		}

		insideType--;
	}

	/// <summary>
	/// Writes one field of a struct.
	/// </summary>
	/// <param name="name">The field's name.</param>
	/// <param name="type">The field's type.</param>
	/// <param name="visibility">What the declaration said about who may see it.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A field's initial value is not written, and unlike C that is not a gap to apologise for: a
	/// Rust struct is built by naming every field at once, so what a field starts at is the
	/// constructor's business rather than the declaration's.
	/// </remarks>
	private static void WriteStructMember(string? name, TypeReference? type, Visibility visibility, CodeBlocker code) =>
		code.WriteLine($"{SpellVisibilityKeyword(visibility)}{name}: {SpellType(type ?? new TypeReference(UnknownTypeName))},");

	/// <summary>
	/// Writes an interface as the trait it is.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="name">The name it is written under.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateTrait(ClassDeclaration classDecl, string name, CodeBlocker code)
	{
		GenerateDocumentation(classDecl, code);
		WriteAnnotations(classDecl.Annotations, code);
		WriteTypePromises(classDecl, code);
		WriteUnaskedConstraints(
			classDecl.TypeParameters,
			code,
			TypeConstraintKind.Implements,
			TypeConstraintKind.Constructible);
		code.Write($"{SpellVisibilityOf(classDecl)}trait {name}{SpellParameterDeclarations(classDecl.TypeParameters)}");

		// A supertrait: something every implementation of this one must also be. A base type and an
		// interface are the same thing to a trait, which is the one place Rust answers the
		// distinction exactly rather than working around it -- the reason the two are kept apart in
		// the AST is the struct below, where only one of them has an answer at all.
		string[] supertraits =
		[
			.. classDecl.BaseType is TypeReference baseType ? (string[])[SpellType(baseType)] : [],
			.. classDecl.Interfaces.Select(SpellType),
		];

		if (supertraits.Length > 0)
		{
			code.Write($": {string.Join(" + ", supertraits)}");
		}

		code.Write(" ");

		using Scope body = new(code);

		insideTrait = true;

		bool first = true;
		foreach (AstNode member in classDecl.Members.Where(member => member is FunctionDeclaration or FieldDeclaration))
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;

			if (member is FunctionDeclaration function)
			{
				GenerateFunction(function, code, name);
				continue;
			}

			GenerateInternal(member, code);
		}

		insideTrait = false;
	}

	/// <summary>
	/// Writes an operator or a conversion as the trait implementation Rust spells it through.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="typeName">The type it belongs to.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateTraitImplementation(FunctionDeclaration funcDecl, string typeName, CodeBlocker code)
	{
		if (funcDecl.Kind == FunctionKind.ConversionOperator)
		{
			GenerateConversion(funcDecl, typeName, code);
			return;
		}

		string symbol = funcDecl.Name ?? string.Empty;
		bool unary = funcDecl.Parameters.Count == 0;
		Dictionary<string, OperatorTrait> traits = unary ? UnaryOperatorTraits : BinaryOperatorTraits;

		if (!traits.TryGetValue(symbol, out OperatorTrait? op))
		{
			string reason = ComparisonOperators.TryGetValue(symbol, out string? supplied)
				? supplied
				: "no std::ops trait";
			GenerateDocumentation(funcDecl, code);
			WriteInexpressible(code, $"operator{symbol} on {typeName}: implement {reason}");
			return;
		}

		GenerateDocumentation(funcDecl, code);
		code.Write($"impl{implBounds} {op.Name} for {typeName} ");

		using Scope block = new(code);

		string result = SpellType(funcDecl.ReturnType ?? new TypeReference(typeName));

		if (!op.ReturnsBool)
		{
			code.WriteLine($"type Output = {result};");
			code.NewLine();
		}

		// A comparison borrows what it compares and answers a bool; an arithmetic operator consumes
		// its operands and answers the type it named as its Output.
		string self = op.ReturnsBool ? "&self" : "self";
		code.Write($"fn {op.Method}({self}");

		foreach (Parameter parameter in funcDecl.Parameters)
		{
			code.Write(", ");
			code.Write($"{parameter.Name ?? "rhs"}: {SpellOperandType(parameter, op, typeName)}");
		}

		code.Write($") -> {(op.ReturnsBool ? "bool" : result)} ");

		using Scope body = new(code);
		WriteBody(funcDecl.Body, code);
	}

	/// <summary>
	/// Spells the type of the operand beside the instance.
	/// </summary>
	/// <param name="parameter">The operand as the declaration carries it.</param>
	/// <param name="op">The trait being implemented.</param>
	/// <param name="typeName">The type the operator belongs to.</param>
	/// <returns>The type as Rust writes it.</returns>
	/// <remarks>
	/// A comparison borrows its operand for the same reason it borrows the instance — answering
	/// whether two values are equal is not a reason to consume either of them.
	/// </remarks>
	private static string SpellOperandType(Parameter parameter, OperatorTrait op, string typeName)
	{
		string spelled = SpellType(parameter.Type ?? new TypeReference(typeName));
		return op.ReturnsBool ? $"&{spelled}" : spelled;
	}

	/// <summary>
	/// Writes a conversion as the <c>From</c> implementation Rust spells it through.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="typeName">The type being converted from.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// <c>From</c> rather than <c>Into</c>: implementing one gives the other for free, and this is
	/// the direction the standard library asks to be implemented.
	/// </remarks>
	private void GenerateConversion(FunctionDeclaration funcDecl, string typeName, CodeBlocker code)
	{
		string target = SpellType(funcDecl.ReturnType ?? new TypeReference(UnknownTypeName));

		GenerateDocumentation(funcDecl, code);
		code.Write($"impl{implBounds} From<{typeName}> for {target} ");

		using Scope block = new(code);
		code.Write($"fn from(value: {typeName}) -> {target} ");

		using Scope body = new(code);
		WriteBody(funcDecl.Body, code);
	}

	/// <summary>
	/// Writes a destructor as the <c>Drop</c> implementation Rust spells it through.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="typeName">The type being dropped.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A destructor is the one member whose name Rust fixes: <c>drop</c>, taking <c>&amp;mut self</c>,
	/// called for you. It cannot go in the inherent block, because a type that both implements
	/// <c>Drop</c> and has its own <c>drop</c> has two of them.
	/// </remarks>
	private void GenerateDrop(FunctionDeclaration funcDecl, string typeName, CodeBlocker code)
	{
		GenerateDocumentation(funcDecl, code);
		code.Write($"impl{implBounds} Drop for {typeName} ");

		using Scope block = new(code);
		code.Write("fn drop(&mut self) ");

		using Scope body = new(code);
		WriteBody(funcDecl.Body, code);
	}

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code) =>
		GenerateFunction(funcDecl, code, null);

	/// <summary>
	/// Emits a function, which may have been declared as a member of a type.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <remarks>
	/// A member takes a receiver, and which one it takes is what
	/// <see cref="FunctionDeclaration.IsReadOnly"/> decides: a member that promises not to modify
	/// what it is called on takes <c>&amp;self</c> and one that does not takes <c>&amp;mut self</c>.
	/// That is the same promise C++ spells as a trailing <c>const</c>, and the one modifier here that
	/// Rust states more strongly than the language it came from.
	/// <para>
	/// Nothing is written for <see cref="FunctionDeclaration.IsNoThrow"/>,
	/// <see cref="FunctionDeclaration.IsExplicit"/> or <see cref="FunctionDeclaration.IsFriend"/>:
	/// Rust has no exceptions, no converting constructors, and no way to make an exception to privacy
	/// for one named friend.
	/// </para>
	/// </remarks>
	private void GenerateFunction(FunctionDeclaration funcDecl, CodeBlocker code, string? enclosingType)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(funcDecl, code);

		// A deleted declaration exists to make a call illegal, and Rust has no way to say that of one
		// member. Writing the signature would do the opposite of what it asks for.
		if (funcDecl.Definition == FunctionDefinition.Deleted)
		{
			WriteInexpressible(code, $"{SpellFunctionName(funcDecl)} is deleted: Rust cannot refuse a call");
			return;
		}

		// A defaulted declaration is one the language supplies. Rust supplies it too, through a
		// derive rather than through the declaration, which is the caller's line to write.
		if (funcDecl.Definition == FunctionDefinition.Defaulted)
		{
			WriteInexpressible(code, $"{SpellFunctionName(funcDecl)} is defaulted: derive it, or implement Default");
			return;
		}

		WriteAnnotations(funcDecl.Annotations, code);

		// #[must_use] says what a pure function's purity means to a caller, and is worth nothing on
		// one that answers nothing.
		if ((funcDecl.IsPure || funcDecl.MustUseResult) && ReturnsAValue(funcDecl))
		{
			code.WriteLine("#[must_use]");
		}

		if (!IsAssociatedItem)
		{
			code.Write(SpellVisibilityOf(funcDecl));
		}

		if (funcDecl.IsCompileTimeEvaluable)
		{
			code.Write("const ");
		}

		WriteUnaskedConstraints(
			funcDecl.TypeParameters,
			code,
			TypeConstraintKind.Implements,
			TypeConstraintKind.Constructible);

		code.Write($"fn {SpellFunctionName(funcDecl)}{SpellParameterDeclarations(funcDecl.TypeParameters)}(");
		WriteReceiver(funcDecl, enclosingType, code);
		GenerateParameterList(funcDecl.Parameters, code);
		code.Write(")");

		if (funcDecl.Kind == FunctionKind.Constructor)
		{
			code.Write(" -> Self");
		}
		else if (ReturnsAValue(funcDecl))
		{
			code.Write($" -> {SpellType(funcDecl.ReturnType!)}");
		}

		// A declaration with no definition is a requirement, which is a thing only a trait can hold:
		// an abstract member of a struct has nowhere to be implemented.
		if (funcDecl.IsAbstract || (insideTrait && funcDecl.Body.Count == 0))
		{
			code.WriteLine(";");
			return;
		}

		// The line is left open, so the scope's brace lands on it: Rust braces hang.
		code.Write(" ");

		using Scope body = new(code);

		if (funcDecl.Kind == FunctionKind.Constructor)
		{
			WriteConstructorBody(funcDecl, code);
			return;
		}

		WriteBody(funcDecl.Body, code);
	}

	/// <summary>
	/// Writes a function's statements.
	/// </summary>
	/// <param name="statements">The statements to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteBody(IEnumerable<AstNode> statements, CodeBlocker code)
	{
		foreach (AstNode statement in statements)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <summary>
	/// Writes what a constructor builds.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A Rust value is built by naming every field at once, so the initialiser list is the whole of
	/// the constructor rather than a preamble to it — and it is the function's tail expression, which
	/// is how Rust returns without saying so.
	/// <para>
	/// A field initialised from a name of its own spelling is written as that name alone, which is
	/// Rust's field-init shorthand and what anybody would have written by hand.
	/// </para>
	/// </remarks>
	private void WriteConstructorBody(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		WriteBody(funcDecl.Body, code);

		if (funcDecl.Initialisers.Count == 0)
		{
			// There is nothing to build from, and Rust has no zero value to fall back on. Default is
			// the one thing a type can ask for without naming its fields.
			code.WriteLine("Self::default()");
			return;
		}

		ConstructionExpression value = new(new TypeReference("Self"));
		foreach (MemberInitialiser initialiser in funcDecl.Initialisers)
		{
			value.Arguments.Add(initialiser);
		}

		GenerateConstructionExpression(value, code);
		code.WriteLine();
	}

	/// <summary>
	/// Writes the instance a member function acts on, when it acts on one.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <param name="code">The writer to emit into.</param>
	private static void WriteReceiver(FunctionDeclaration funcDecl, string? enclosingType, CodeBlocker code)
	{
		// A static member acts on no instance, and a constructor has none to act on yet.
		if (enclosingType is null || funcDecl.IsStatic || funcDecl.Kind == FunctionKind.Constructor)
		{
			return;
		}

		code.Write(funcDecl.IsReadOnly ? "&self" : "&mut self");

		if (funcDecl.Parameters.Count > 0)
		{
			code.Write(", ");
		}
	}

	/// <summary>
	/// Reports whether a function answers with something.
	/// </summary>
	/// <param name="funcDecl">The declaration to test.</param>
	/// <returns>True when it has a return type that is not the unit.</returns>
	/// <remarks>
	/// Rust writes no return type for a function that answers nothing, rather than naming the unit:
	/// <c>-&gt; ()</c> is legal and nobody writes it.
	/// </remarks>
	private static bool ReturnsAValue(FunctionDeclaration funcDecl) =>
		funcDecl.ReturnType is TypeReference declared && SpellType(declared) != "()";

	/// <summary>
	/// Spells the name a declaration is written under.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <returns>The name as Rust writes it.</returns>
	/// <remarks>
	/// A constructor is <c>new</c>, which is a convention rather than a keyword — Rust has no
	/// constructors, and an associated function returning <c>Self</c> is what every type writes
	/// instead.
	/// </remarks>
	private static string SpellFunctionName(FunctionDeclaration funcDecl) => funcDecl.Kind switch
	{
		FunctionKind.Constructor => "new",
		FunctionKind.Destructor => "drop",
		_ => funcDecl.Name ?? "unnamed_function",
	};

	/// <inheritdoc/>
	/// <remarks>
	/// A parameter's default value is written beside it as a comment. Rust has no default arguments,
	/// so the caller has to pass one — and the value the declaration chose is exactly what they need
	/// in order to pass the same thing.
	/// </remarks>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		code.Write($"{parameter.Name ?? $"param{position}"}: ");
		code.Write(SpellType(parameter.Type ?? new TypeReference(UnknownTypeName)));

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" /* = {parameter.DefaultValue} */");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <c>let</c> binds immutably in Rust, which is the opposite of every other target here, so an
	/// ordinary declaration is written <c>let mut</c> and a constant one plain <c>let</c>. The
	/// warning an unneeded <c>mut</c> earns is a better outcome than the error a missing one causes,
	/// and the AST says whether a value is constant rather than whether it is ever assigned again.
	/// <para>
	/// An inferred declaration writes no type at all, which is the one thing on this list Rust does
	/// better than the language the node was modelled on: <c>let x = 5;</c> needs no <c>auto</c> and
	/// no <c>var</c>.
	/// </para>
	/// </remarks>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);

		code.Write(varDecl.IsConstant ? "let " : "let mut ");
		code.Write(varDecl.Name);

		bool inferred = varDecl.IsTypeInferred || varDecl.Type is null;
		if (!inferred || varDecl.InitialValue is null)
		{
			code.Write($": {SpellType(varDecl.Type ?? new TypeReference(UnknownTypeName))}");
		}

		if (varDecl.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Inside a struct this is a field; at module scope it is a <c>const</c> when the declaration
	/// says its value never changes and a <c>static</c> otherwise. The difference is real rather than
	/// stylistic: a <c>const</c> is substituted wherever it is named and a <c>static</c> is one
	/// object with an address, which is what a large table wants to be.
	/// <para>
	/// Both require a value, and an array one requires a length that
	/// <see cref="TypeReference.IsArray"/> deliberately does not carry — so a constant array is
	/// written as a borrowed slice, <c>&amp;[T]</c>, which is the bound-free spelling Rust does have
	/// and the one a generated table wants anyway.
	/// </para>
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		if (insideType > 0)
		{
			GenerateDocumentation(field, code);
			WriteAnnotations(field.Annotations, code);
			WriteStructMember(field.Name, field.Type, field.Visibility, code);
			return;
		}

		GenerateDocumentation(field, code);

		TypeReference type = field.Type ?? new TypeReference(UnknownTypeName);

		// A trait's constant may say only what it is, leaving every implementation to say what it
		// holds. That is a requirement rather than a value with something missing.
		if (insideTrait && field.InitialValue is null)
		{
			code.Write($"const {field.Name}: {SpellStorageType(type)}");
			EndStatement(code);
			return;
		}

		if (field.InitialValue is null)
		{
			WriteInexpressible(code, $"{field.Name}: Rust gives a const or a static no value of its own");
			return;
		}

		string visibility = IsAssociatedItem ? string.Empty : SpellVisibilityKeyword(field.Visibility);
		string storage = IsAssociatedItem || field.IsConstant ? "const" : "static";

		code.Write($"{visibility}{storage} ");
		code.Write($"{field.Name}: {SpellStorageType(type)} = ");

		// A slice borrows the array literal it is written from, which is what makes a table with no
		// length a constant at all.
		if (type.IsArray)
		{
			code.Write("&");
		}

		GenerateInternal(field.InitialValue, code);
		EndStatement(code);
	}

	/// <summary>
	/// Spells the type a constant or a static is declared with.
	/// </summary>
	/// <param name="type">The declared type.</param>
	/// <returns>The type as Rust writes it in that position.</returns>
	private static string SpellStorageType(TypeReference type)
	{
		if (!type.IsArray)
		{
			return SpellType(type);
		}

		TypeReference element = type.Clone();
		element.IsArray = false;
		return $"&[{SpellType(element)}]";
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An enumeration's variants are named through it — <c>Colour::Red</c> — so nothing has to be
	/// prefixed to keep two enumerations with a <c>None</c> each apart, which is the thing C cannot
	/// do. A fixed underlying type is <c>#[repr]</c>, which is the guarantee rather than a comment
	/// about one.
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(enumDecl, code);

		if (enumDecl.UnderlyingType is TypeReference underlying)
		{
			code.WriteLine($"#[repr({SpellType(underlying)})]");
		}

		code.Write($"{SpellVisibilityOf(enumDecl)}enum {enumDecl.Name ?? "UnnamedEnum"} ");

		using Scope body = new(code);
		foreach (EnumMember member in enumDecl.Members)
		{
			code.Write(member.Name ?? "Unnamed");

			if (member.Value is not null)
			{
				code.Write($" = {member.Value}");
			}

			// A trailing comma on the last variant too, so adding one after it is a one-line diff.
			code.WriteLine(",");
		}
	}

	/// <inheritdoc/>
	protected override void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		Ensure.NotNull(usingAlias);
		Ensure.NotNull(code);

		GenerateDocumentation(usingAlias, code);
		code.Write($"{SpellVisibilityOf(usingAlias)}type {usingAlias.Name} = ");
		code.Write(SpellType(usingAlias.AliasedType ?? new TypeReference(UnknownTypeName)));
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <c>const _: () = assert!(…)</c>, which is Rust's own compile-time assertion and needs no
	/// macro crate: a constant nobody names still has to be evaluated for the program to build, and
	/// a failed <c>assert!</c> in that position is a compile error naming the message.
	/// </remarks>
	protected override void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code)
	{
		Ensure.NotNull(assertion);
		Ensure.NotNull(code);

		string condition = assertion.Condition ?? "false";

		code.Write($"const _: () = assert!({condition}");

		if (assertion.Message is not null)
		{
			code.WriteLine(",");
			code.Indent();
			code.Write($"\"{EscapeString(assertion.Message)}\"");
			code.Outdent();
		}

		code.Write(")");
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Rust has no ternary operator at all, so the inherited <c>?:</c> would not be a different
	/// spelling of this — it would not compile. What it has instead is an <c>if</c> that is an
	/// expression rather than a statement, which is the same idea reached from the other side: the
	/// branches yield the value rather than assigning one.
	/// <para>
	/// Parenthesised, for a reason the braces do not already cover. An <c>if</c> at the start of a
	/// statement is parsed as a statement, so a conditional used for its effect alone would have its
	/// branches' values silently discarded; wrapping it keeps it an expression wherever it stands.
	/// </para>
	/// </remarks>
	protected override void GenerateConditionalExpression(ConditionalExpression conditional, CodeBlocker code)
	{
		Ensure.NotNull(conditional);
		Ensure.NotNull(code);

		code.Write("(if ");
		GenerateInternal(conditional.Condition, code);
		code.Write(" { ");
		GenerateInternal(conditional.WhenTrue, code);
		code.Write(" } else { ");
		GenerateInternal(conditional.WhenFalse, code);
		code.Write(" })");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Three shapes, and which one is written depends on what the expression is rather than on where
	/// it stands: a construction naming its members is a struct literal, one that does not is a call,
	/// and one with no type at all is an array literal — which is what initialises a declaration that
	/// has already said its type, a constant table most of all.
	/// <para>
	/// Rust needs no distinction between an initialiser and a value the way C does. A struct literal
	/// is an expression everywhere, including in the constant that a table has to be.
	/// </para>
	/// </remarks>
	protected override void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		if (construction.Type is null)
		{
			WriteElementList(construction, code, "[", "]", "[]");
			return;
		}

		string type = SpellType(construction.Type);

		if (construction.Arguments.Any(argument => argument is MemberInitialiser))
		{
			code.Write($"{type} ");
			WriteElementList(construction, code, "{", "}", "{}");
			return;
		}

		code.Write(type);
		WriteCallArguments(construction, code);
	}

	/// <summary>
	/// Writes the arguments of a construction that names none of its members.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A call rather than a list: a tuple struct and a function are built the same way, and neither
	/// is a table, so the arguments stay on one line however many there are.
	/// </remarks>
	private void WriteCallArguments(ConstructionExpression construction, CodeBlocker code)
	{
		code.Write("(");

		for (int index = 0; index < construction.Arguments.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			GenerateInternal(construction.Arguments[index], code);
		}

		code.Write(")");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A field initialised from a variable of its own name is written as that name alone, which is
	/// Rust's field-init shorthand and what the same line would look like written by hand. It is not
	/// only shorter: <c>x: x</c> is what clippy's <c>redundant_field_names</c> exists to complain
	/// about, so writing it would make every generated constructor a lint.
	/// </remarks>
	protected override void WriteListElement(AstNode argument, CodeBlocker code)
	{
		Ensure.NotNull(code);

		if (argument is MemberInitialiser { Value: VariableReference reference } designated
			&& string.Equals(designated.Name, reference.Name, StringComparison.Ordinal))
		{
			code.Write(reference.Name);
			return;
		}

		base.WriteListElement(argument, code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Rust's <c>main</c> takes no arguments and returns nothing, so a program that wants either
	/// reaches for the standard library: the arguments come from <c>std::env::args</c>, and an exit
	/// code is handed to <c>std::process::exit</c>.
	/// <para>
	/// A program that returns an exit code is written as a <c>run</c> answering one and a
	/// <c>main</c> exiting with what it answered — the same shape Python's <c>__main__</c> guard
	/// takes here, and for the same reason: it is what keeps the body's own <c>return</c> meaning
	/// what it says.
	/// </para>
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		string arguments = entryPoint.AcceptsArguments ? "args: Vec<String>" : string.Empty;
		string collect = entryPoint.AcceptsArguments ? "std::env::args().collect()" : string.Empty;

		if (entryPoint.ReturnsExitCode)
		{
			code.Write($"fn run({arguments}) -> i32 ");

			using (Scope run = new(code))
			{
				WriteBody(entryPoint.Body, code);
			}

			code.NewLine();
			code.Write("fn main() ");

			using Scope main = new(code);
			code.WriteLine($"std::process::exit(run({collect}));");
			return;
		}

		code.Write("fn main() ");

		using Scope body = new(code);

		if (entryPoint.AcceptsArguments)
		{
			code.WriteLine($"let args: Vec<String> = {collect};");
		}

		WriteBody(entryPoint.Body, code);
	}

	/// <summary>
	/// Spells what a declaration said about who may see it, with a trailing space.
	/// </summary>
	/// <param name="node">The declaration to read.</param>
	/// <returns>The keyword and a space, or nothing.</returns>
	private static string SpellVisibilityOf(AstNode node) => SpellVisibilityKeyword(VisibilityOf(node));

	/// <summary>
	/// Spells a visibility, with a trailing space.
	/// </summary>
	/// <param name="visibility">The visibility to spell.</param>
	/// <returns>The keyword and a space, or nothing.</returns>
	/// <remarks>
	/// Rust's default is private to the module, so <c>pub</c> has to be written wherever anything
	/// outside is meant to reach the declaration — including on every field, which is what makes a
	/// generated data type usable at all. That is why <see cref="Visibility.Unspecified"/> is public
	/// here: a type nothing can read is not what a declaration that said nothing asked for.
	/// <para>
	/// <see cref="Visibility.Protected"/> is <c>pub(crate)</c>, which is the nearest Rust has and is
	/// not very near. Protected means visible to whatever derives from this, and nothing derives from
	/// anything here.
	/// </para>
	/// </remarks>
	private static string SpellVisibilityKeyword(Visibility visibility) => visibility switch
	{
		Visibility.Private => string.Empty,
		Visibility.Internal or Visibility.Protected => "pub(crate) ",
		_ => "pub ",
	};

	/// <summary>
	/// Spells a type in Rust.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The Rust source for it.</returns>
	/// <remarks>
	/// A reference is <c>&amp;</c>, and whether it is a <c>&amp;mut</c> is what
	/// <see cref="TypeReference.IsReadOnly"/> decides — which is the same question C++ answers with
	/// <c>const T&amp;</c> against <c>T&amp;</c>, asked in the other order. A pointer is a raw
	/// pointer, which is the only thing Rust has that a pointer is, and is <c>*const</c> or
	/// <c>*mut</c> for the same reason.
	/// <para>
	/// Borrowing an owned type gives the borrowed one rather than a reference to the owned one:
	/// <c>&amp;str</c> and <c>&amp;[T]</c>, never <c>&amp;String</c> or <c>&amp;Vec&lt;T&gt;</c>.
	/// </para>
	/// </remarks>
	private static string SpellType(TypeReference type)
	{
		string core = SpellCoreType(type);

		if (type.Indirection == TypeIndirection.Pointer)
		{
			return $"*{(type.IsReadOnly ? "const" : "mut")} {core}";
		}

		if (type.Indirection == TypeIndirection.Reference)
		{
			return $"&{(type.IsReadOnly ? string.Empty : "mut ")}{Borrow(core)}";
		}

		return core;
	}

	/// <summary>
	/// Spells a type without saying how it is reached.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The owned form.</returns>
	private static string SpellCoreType(TypeReference type)
	{
		string core = SpellTypeName(type);
		return type.IsArray ? $"Vec<{core}>" : core;
	}

	/// <summary>
	/// Spells a type's name and its arguments.
	/// </summary>
	/// <param name="type">The type whose name to spell.</param>
	/// <returns>The name as Rust writes it.</returns>
	/// <remarks>
	/// <c>list</c> and <c>dict</c> are the two names the AST has that Rust spells from the standard
	/// library rather than from the name. A container named without arguments is a container of the
	/// most general thing there is, which is what the caller left unsaid.
	/// </remarks>
	private static string SpellTypeName(TypeReference type)
	{
		string unknown = TypeMappings[UnknownTypeName];

		if (string.Equals(type.Name, "list", StringComparison.OrdinalIgnoreCase))
		{
			return $"Vec<{(type.TypeArguments.Count == 1 ? SpellType(type.TypeArguments[0]) : unknown)}>";
		}

		if (string.Equals(type.Name, "dict", StringComparison.OrdinalIgnoreCase))
		{
			return type.TypeArguments.Count == 2
				? $"std::collections::HashMap<{SpellType(type.TypeArguments[0])}, {SpellType(type.TypeArguments[1])}>"
				: $"std::collections::HashMap<String, {unknown}>";
		}

		string name = TypeMappings.TryGetValue(type.Name, out string? mapped) ? mapped : type.Name;

		return type.TypeArguments.Count == 0
			? name
			: $"{name}<{string.Join(", ", type.TypeArguments.Select(SpellType))}>";
	}

	/// <summary>
	/// Gives the form a type takes when it is borrowed rather than owned.
	/// </summary>
	/// <param name="owned">The owned spelling.</param>
	/// <returns>The borrowed spelling, which is usually the same one.</returns>
	private static string Borrow(string owned)
	{
		if (BorrowedForms.TryGetValue(owned, out string? borrowed))
		{
			return borrowed;
		}

		// A borrowed sequence is a slice, whatever it is a sequence of.
		return owned.StartsWith("Vec<", StringComparison.Ordinal) && owned.EndsWith('>')
			? $"[{owned[4..^1]}]"
			: owned;
	}
}
