// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates C++ code from AST nodes.
/// </summary>
/// <remarks>
/// The AST's type names are the same language-neutral set the other generators consume (<c>str</c>,
/// <c>int</c>, <c>bool</c>, …), so they are mapped to C++ spellings; anything unrecognised is emitted
/// verbatim on the assumption the caller meant a C++ type. A declaration with no type, or one marked
/// type-inferred, becomes <c>auto</c>.
/// </remarks>
public class CppGenerator : CFamilyGenerator
{
	/// <summary>
	/// Maps the AST's language-neutral type names onto C++ spellings.
	/// </summary>
	/// <summary>
	/// What a declaration that never said what type it is gets.
	/// </summary>
	/// <remarks>
	/// A type is optional on every node that carries one, because a half-built AST is a thing the
	/// editor has to be able to hold. Emitting the most general type there keeps the output compiling
	/// while making it obvious which declaration was never finished.
	/// </remarks>
	private const string UnknownTypeName = "object";

	/// <summary>
	/// How many type declarations enclose what is being written.
	/// </summary>
	/// <remarks>
	/// A depth rather than a flag, so a type declared inside a type leaves the count right when it
	/// closes. The one thing it decides is whether a constant field says <c>inline</c> or
	/// <c>static</c>; see <see cref="SpellStorage"/>.
	/// </remarks>
	private int insideType;

	private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "str", "std::string" },
		{ "string", "std::string" },
		{ "int", "int" },
		{ "long", "long long" },
		{ "float", "float" },
		{ "double", "double" },
		{ "bool", "bool" },
		{ "list", "std::vector" },
		{ "dict", "std::map" },
		{ "void", "void" },
		{ "object", "std::any" }
	};

	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "cpp";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "C++";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "cpp";

	/// <summary>
	/// Writes the template head a declaration written over types needs.
	/// </summary>
	/// <param name="parameters">The declaration's type parameters.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Names only, and <c>typename</c> for each: C++ takes non-type parameters as well, and the AST
	/// models only the ones that are types. What each has to be goes in a note beside the
	/// declaration rather than in a <c>requires</c> clause, for the reason
	/// <see cref="LanguageGeneratorBase.WriteUnaskedConstraints"/> gives.
	/// </remarks>
	private static void WriteTemplateHead(IEnumerable<TypeParameter> parameters, CodeBlocker code)
	{
		string[] names = [.. parameters.Select(parameter => $"typename {parameter.Name}")];

		if (names.Length > 0)
		{
			code.WriteLine($"template <{string.Join(", ", names)}>");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The standard library spells an invented accessor <c>set_value</c>, and that is the convention a reader of any C++ header already has.
	/// </remarks>
	protected override NamingStyle MemberNaming => NamingStyle.Snake;

	/// <inheritdoc/>
	/// <remarks>
	/// Doubled brackets, which is the standard syntax rather than a compiler's own. An attribute
	/// the compiler does not know is ignored with a warning rather than refused, which is what
	/// makes writing a caller's attribute through safe here.
	/// </remarks>
	protected override string? SpellAnnotation(Annotation annotation) => $"[[{annotation}]]";
	/// <inheritdoc/>
	/// <remarks>
	/// A constructor and a destructor are named after the type rather than after themselves, so the
	/// name comes from the class emitter rather than from the declaration. That is what stops the two
	/// desynchronising when the type is renamed — the alternative is holding the type's name twice
	/// and hoping.
	/// <para>
	/// A pure function is written <c>[[nodiscard]]</c>: discarding the result of a call that does
	/// nothing else is always a mistake, and that is the whole of what the standard can say. The
	/// compiler-specific <c>__attribute__((pure))</c> asserts to the optimiser that the call may be
	/// elided or duplicated, which is a stronger promise than the AST is in a position to make.
	/// </para>
	/// </remarks>
	protected override void GenerateFunction(FunctionDeclaration funcDecl, CodeBlocker code, string? enclosingType)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(funcDecl, code);
		WriteAnnotations(funcDecl.Annotations, code);
		WriteUnaskedConstraints(funcDecl.TypeParameters, code);
		WriteTemplateHead(funcDecl.TypeParameters, code);

		// Purity earns [[nodiscard]] on its own: a call that does nothing else and whose result is
		// thrown away did nothing at all.
		if (funcDecl.IsPure || funcDecl.MustUseResult)
		{
			code.Write("[[nodiscard]] ");
		}

		// The order is the one C++ requires and the one it is conventionally written in: what the
		// caller must not ignore, then where the declaration sits, then how it may be called, then
		// when it may be evaluated.
		if (funcDecl.IsFriend)
		{
			code.Write("friend ");
		}

		if (funcDecl.IsExplicit)
		{
			code.Write("explicit ");
		}

		if (funcDecl.IsStatic)
		{
			code.Write("static ");
		}

		// An abstract declaration is virtual whether or not it was asked to be: there is nothing else
		// `= 0` could mean.
		if (funcDecl.IsVirtual || funcDecl.IsAbstract)
		{
			code.Write("virtual ");
		}

		if (funcDecl.IsCompileTimeEvaluable)
		{
			code.Write("constexpr ");
		}

		// A constructor, a destructor and a conversion operator have no return type to write. The
		// first two have none at all, and the third's is part of its name.
		if (funcDecl.Kind is FunctionKind.Method or FunctionKind.Operator)
		{
			code.Write($"{MapToCppType(funcDecl.ReturnType ?? new TypeReference("void"))} ");
		}

		code.Write(SpellFunctionName(funcDecl, enclosingType));
		code.Write("(");
		GenerateParameterList(funcDecl.Parameters, code);
		code.Write(")");

		if (funcDecl.IsReadOnly)
		{
			code.Write(" const");
		}

		if (funcDecl.IsNoThrow)
		{
			code.Write(" noexcept");
		}

		if (funcDecl.IsAbstract)
		{
			code.WriteLine(" = 0;");
			return;
		}

		switch (funcDecl.Definition)
		{
			case FunctionDefinition.Defaulted:
				code.WriteLine(" = default;");
				return;

			case FunctionDefinition.Deleted:
				code.WriteLine(" = delete;");
				return;

			default:
				break;
		}

		// The line is ended before the scope opens, so C++'s brace lands on its own line.
		code.WriteLine();
		WriteInitialiserList(funcDecl, code);

		using Scope body = new(code);
		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <summary>
	/// Writes what the type's members start at, between a constructor's signature and its body.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Initialising rather than assigning is the only way to start a member that cannot be assigned
	/// at all, and is the difference between building a value and building an empty one and then
	/// overwriting it.
	/// </remarks>
	private void WriteInitialiserList(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		if (funcDecl.Initialisers.Count == 0)
		{
			return;
		}

		code.Indent();
		code.Write(": ");

		for (int index = 0; index < funcDecl.Initialisers.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			MemberInitialiser initialiser = funcDecl.Initialisers[index];
			code.Write($"{initialiser.Name}(");

			if (initialiser.Value is not null)
			{
				GenerateInternal(initialiser.Value, code);
			}

			code.Write(")");
		}

		code.WriteLine();
		code.Outdent();
	}

	/// <summary>
	/// Spells the name a declaration is written under.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>The name as C++ writes it.</returns>
	private static string SpellFunctionName(FunctionDeclaration funcDecl, string? enclosingType)
	{
		string typeName = enclosingType ?? funcDecl.Name ?? "UnnamedType";

		return funcDecl.Kind switch
		{
			FunctionKind.Constructor => typeName,
			FunctionKind.Destructor => $"~{typeName}",
			FunctionKind.Operator => $"operator{funcDecl.Name}",
			FunctionKind.ConversionOperator =>
				$"operator {MapToCppType(funcDecl.ReturnType ?? new TypeReference("void"))}",
			_ => funcDecl.Name ?? "unnamedFunction",
		};
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The members are not indented. A namespace usually wraps a whole file, so indenting for it
	/// would indent everything and buy nothing; the closing brace names what it closes instead, which
	/// is what tells a reader at the bottom of a long file which one just ended.
	/// </remarks>
	protected override void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(namespaceDecl, code);

		string name = string.Join("::", NamespaceDeclaration.Split(namespaceDecl.Name));
		code.WriteLine($"namespace {name}");
		code.WriteLine("{");
		code.NewLine();

		// The same rule the members of a type follow: two of a kind that say nothing about themselves
		// stay together, so a run of assertions about one type reads as one block rather than as a
		// paragraph each.
		AstNode? previous = null;
		foreach (AstNode member in namespaceDecl.Members)
		{
			if (previous is not null && NeedsSeparation(previous, member))
			{
				code.NewLine();
			}

			previous = member;
			GenerateInternal(member, code);
		}

		code.NewLine();
		code.WriteLine($"}}  // namespace {name}");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Members are grouped under the access label each one asks for, and a member with no visibility
	/// of its own lands under <c>public:</c>. A C++ class defaults to private, so a generated class
	/// with no access specifier at all would compile to something nothing outside it could use.
	/// <para>
	/// <see cref="Visibility.Internal"/> becomes <c>public:</c>: C++ has no assembly to be internal
	/// to, and the nearest thing — a friend declaration — names the code it trusts rather than
	/// describing a scope.
	/// </para>
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		classDecl = Separated(classDecl);

		GenerateDocumentation(classDecl, code);

		// A struct's members are public already, so labelling them would be noise. An interface has
		// no keyword in C++ and is a class whose members are all public.
		bool isStruct = classDecl.Kind == TypeDeclarationKind.Struct;

		// An explicit specialisation says up front that what follows declares nothing new: the
		// template it specialises is already declared somewhere, and this fills it in for one set
		// of arguments. The empty list is what distinguishes a full specialisation from a partial
		// one, and the generator only writes full ones -- a partial specialisation would need
		// parameters of its own, which is a different thing and not one the AST models.
		if (classDecl.IsSpecialisation)
		{
			code.WriteLine("template <>");
		}

		WriteTemplateHead(classDecl.TypeParameters, code);

		WriteAnnotations(classDecl.Annotations, code);
		WriteTypePromises(classDecl, code);

		// Every constraint. A concept is a predicate over a type and can ask anything at all, so
		// there is no shared idea underneath `struct` and `std::floating_point` to translate
		// between -- the same reason CompileTimeAssertion.Condition is text. The standard ones
		// would also need <concepts> included, which the AST does not carry for a declaration
		// generated on its own.
		WriteUnaskedConstraints(classDecl.TypeParameters, code);

		code.Write($"{(isStruct ? "struct" : "class")} {classDecl.Name ?? "UnnamedClass"}");

		if (classDecl.IsSpecialisation)
		{
			IEnumerable<string> arguments = classDecl.SpecialisationArguments.Select(MapToCppType);
			code.Write($"<{string.Join(", ", arguments)}>");
		}

		// C++ does not distinguish a base class from an interface -- an interface is a class whose
		// members are all pure virtual -- so the two lists join into one. What it does distinguish
		// is public from private inheritance, and the default for a class is private, which would
		// make a base nobody outside could use the base through.
		string[] inherited =
		[
			.. classDecl.BaseType is TypeReference baseType ? (string[])[$"public {MapToCppType(baseType)}"] : [],
			.. classDecl.Interfaces.Select(contract => $"public {MapToCppType(contract)}"),
		];

		if (inherited.Length > 0)
		{
			code.Write($" : {string.Join(", ", inherited)}");
		}

		code.WriteLine();

		// A C++ class declaration is a statement, so its closing brace takes a semicolon.
		using ScopeWithTrailingSemicolon body = new(code);

		// Unspecified rather than Public, so the first member of a class always writes its label: an
		// unlabelled C++ class body is private, which is the one thing the label has to rule out.
		Visibility current = isStruct ? Visibility.Public : Visibility.Unspecified;
		bool first = true;
		AstNode? previous = null;

		insideType++;
		foreach (AstNode member in classDecl.Members)
		{
			Visibility access = classDecl.Kind == TypeDeclarationKind.Interface
				? Visibility.Public
				: AccessOf(member);

			// A blank line goes between two members when either says something about itself, when
			// they are different kinds of thing, or where the access changes. A documented member
			// needs air above it or its first comment line butts against the member before it and
			// reads as belonging to that one; the member after a documented one needs the same, or it
			// is swallowed into that block. Two of the same kind that say nothing stay together,
			// which is what keeps a run of aliases, or of defaulted and deleted declarations, reading
			// as one group rather than as four paragraphs.
			if (!first && (NeedsSeparation(previous!, member) || access != current))
			{
				// NewLine rather than WriteLine: a separator carrying the current indent is a line of
				// trailing whitespace, which every formatter strips and every diff then shows.
				code.NewLine();
			}

			first = false;
			previous = member;

			if (access != current)
			{
				// An access label sits at the class's own indentation rather than the members', which
				// is what makes it read as dividing them rather than as one of them.
				code.Outdent();
				code.WriteLine($"{SpellVisibility(access)}:");
				code.Indent();
				current = access;
			}

			switch (member)
			{
				case VariableDeclaration field:
					GenerateField(field, code);
					break;

				case FunctionDeclaration method:
					GenerateFunction(method, code, classDecl.Name);
					break;

				default:
					GenerateInternal(member, code);
					break;
			}
		}

		insideType--;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The message goes on its own line. These are long by nature — the predicate says what is false
	/// and the message says why anyone cared — and a compiler quoting the whole declaration back is
	/// easier to read when it is two lines rather than one very wide one.
	/// </remarks>
	protected override void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code)
	{
		Ensure.NotNull(assertion);
		Ensure.NotNull(code);

		code.Write($"static_assert({assertion.Condition}");

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
	protected override void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		Ensure.NotNull(usingAlias);
		Ensure.NotNull(code);

		GenerateDocumentation(usingAlias, code);
		code.Write($"using {usingAlias.Name} = {MapToCppType(usingAlias.AliasedType ?? new TypeReference(UnknownTypeName))}");
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Braced rather than parenthesised. Braces will not narrow a value silently, and a construction
	/// with one argument written with parentheses can be read as a declaration instead — which is a
	/// mistake a generator should never be able to make.
	/// <para>
	/// A construction with no type is the braced list on its own, which is what initialises a
	/// declaration that has already said what its type is — an array of rows most of all, where
	/// naming the array's type again would be wrong rather than merely redundant.
	/// </para>
	/// <para>
	/// An argument that is a <see cref="MemberInitialiser"/> is a designated initialiser, so a row
	/// says which member each value is for instead of depending on the order the members happen to
	/// be declared in. C++20 requires designators to appear in declaration order, which is the
	/// caller's business: the generator writes the order it is given.
	/// </para>
	/// </remarks>
	protected override void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		if (construction.Type is not null)
		{
			code.Write(MapToCppType(construction.Type));
		}

		WriteBracedList(construction, code, "{}");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Always <c>enum class</c>, never the unscoped form: an unscoped enumeration leaks its members
	/// into the surrounding scope and converts to an integer without being asked, and neither is
	/// something a generated type should do to the code around it.
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(enumDecl, code);

		code.Write($"enum class {enumDecl.Name ?? "UnnamedEnum"}");

		if (enumDecl.UnderlyingType is TypeReference underlying)
		{
			code.Write($" : {MapToCppType(underlying)}");
		}

		code.WriteLine();

		using ScopeWithTrailingSemicolon body = new(code);
		foreach (EnumMember member in enumDecl.Members)
		{
			code.Write(member.Name ?? "Unnamed");

			if (member.Value is not null)
			{
				code.Write($" = {member.Value}");
			}

			// A trailing comma on the last member too, so adding one after it is a one-line diff.
			code.WriteLine(",");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A field with no initialiser is written <c>{}</c> rather than left bare. An uninitialised
	/// member holds whatever was in that memory, and a generated type is usually one whose values
	/// come from a file or the wire — so the one place it must be right is the case nobody wrote
	/// anything for.
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		GenerateDocumentation(field, code);
		WriteAnnotations(field.Annotations, code);

		code.Write(SpellStorage(field));
		code.Write(SpellDeclarator(field.Type ?? new TypeReference(UnknownTypeName), field.Name ?? string.Empty));

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}
		else
		{
			code.Write("{}");
		}

		EndStatement(code);
	}

	/// <summary>
	/// Maps a member's visibility onto the access label C++ would put it under.
	/// </summary>
	/// <param name="member">The member to place.</param>
	/// <returns>The visibility whose label the member belongs beneath.</returns>
	private static Visibility AccessOf(AstNode member) => VisibilityOf(member) switch
	{
		Visibility.Protected => Visibility.Protected,
		Visibility.Private => Visibility.Private,
		_ => Visibility.Public,
	};

	/// <summary>
	/// Emits a variable declaration as a class member.
	/// </summary>
	/// <param name="field">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A constant member is emitted as <c>static constexpr</c>. A plain <c>const</c> member is a
	/// per-instance value initialised once per object, which is not what a constant means; the
	/// <c>static constexpr</c> spelling is the one that gives the class a single compile-time value,
	/// and it is available because a constant declared here is initialised from a literal.
	/// </remarks>
	private void GenerateField(VariableDeclaration field, CodeBlocker code)
	{
		if (field.IsConstant && field.InitialValue is not null)
		{
			code.Write("static constexpr ");
		}
		else if (field.IsConstant)
		{
			code.Write("const ");
		}

		code.Write(SpellVariableDeclarator(field));

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// C++'s <c>main</c> returns <c>int</c> whether or not the program means to hand back an exit
	/// code, so <see cref="EntryPoint.ReturnsExitCode"/> changes nothing in the signature — a program
	/// that returns nothing exits with zero, which the standard supplies by falling off the end.
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		code.Write("int main(");

		if (entryPoint.AcceptsArguments)
		{
			code.Write("int argc, char* argv[]");
		}

		// The line is ended before the scope opens, so C++'s brace lands on its own line.
		code.WriteLine(")");

		using Scope body = new(code);
		foreach (AstNode statement in entryPoint.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		// An empty name means deliberately unnamed, which C++ allows and a deleted copy constructor
		// wants: the parameter exists to make the signature, and naming it would only invite someone
		// to look for where it is used. A null name means nobody said, so one is invented.
		string name = parameter.Name is "" ? string.Empty : parameter.Name ?? $"param{position}";

		code.Write(SpellDeclarator(parameter.Type ?? new TypeReference(UnknownTypeName), name));

		AppendDefaultValue(parameter, code);
	}

	/// <inheritdoc/>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);

		if (varDecl.IsConstant)
		{
			code.Write("const ");
		}

		code.Write(SpellVariableDeclarator(varDecl));

		if (varDecl.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Spells a local declaration up to its name.
	/// </summary>
	/// <param name="varDecl">The declaration being emitted.</param>
	/// <returns>The type and the name, with an array's brackets where C++ puts them.</returns>
	/// <remarks>
	/// The type and the name are spelled together rather than one after the other, because an array
	/// separates them: <see cref="CFamilyGenerator.SpellDeclarator"/> is the one place that knows it.
	/// <para>
	/// <c>auto</c> needs an initializer to deduce from, so an inferred declaration without one falls
	/// back to <c>std::any</c>. Neither deduces an array, so neither goes through the declarator.
	/// </para>
	/// </remarks>
	private string SpellVariableDeclarator(VariableDeclaration varDecl)
	{
		if (!varDecl.IsTypeInferred && varDecl.Type is TypeReference declared)
		{
			return SpellDeclarator(declared, varDecl.Name);
		}

		return $"{(varDecl.InitialValue is not null ? "auto" : "std::any")} {varDecl.Name}";
	}

	/// <summary>
	/// What a container named without arguments is a container of.
	/// </summary>
	/// <remarks>
	/// <c>list</c> comes from languages that do not say what is in one, and C++ insists. These are
	/// keyed by the name as written rather than by the mapped one, because that is what the schema
	/// said. A <c>list&lt;int&gt;</c> is a <c>std::vector&lt;int&gt;</c> and never reaches here.
	/// </remarks>
	private static readonly Dictionary<string, string> DefaultTypeArguments = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "list", "<std::any>" },
		{ "dict", "<std::string, std::any>" }
	};

	/// <summary>
	/// Spells a type in C++.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The C++ source for it.</returns>
	/// <remarks>
	/// Only the name is mapped; the shape around it — arguments, <c>const</c>, <c>&amp;</c> and
	/// <c>*</c> — is C++'s own spelling of what the type says, which is what the string form could
	/// not express.
	/// <para>
	/// An array's brackets are not among them, because they belong to the declarator rather than to
	/// the type. Everything that declares a name goes through
	/// <see cref="CFamilyGenerator.SpellDeclarator"/> to get them; the positions that spell a type
	/// with no name to put them after — a return type, a base type, an enumeration's underlying type
	/// — are ones C++ does not let an array stand in at all, so writing them there would produce a
	/// compile error wearing the shape of a feature.
	/// </para>
	/// </remarks>
	private static string MapToCppType(TypeReference type)
	{
		string name = TypeMappings.TryGetValue(type.Name, out string? mapped) ? mapped : type.Name;

		string arguments = SpellTypeArguments(type);

		string indirection = type.Indirection switch
		{
			TypeIndirection.Reference => "&",
			TypeIndirection.Pointer => "*",
			_ => string.Empty,
		};

		return $"{(type.IsReadOnly ? "const " : string.Empty)}{name}{arguments}{indirection}";
	}

	/// <inheritdoc/>
	protected override string SpellType(TypeReference type)
	{
		Ensure.NotNull(type);
		return MapToCppType(type);
	}

	/// <summary>
	/// Spells what a field says about where it lives and when its value is fixed.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <returns>The keywords, with a trailing space, or empty when there are none.</returns>
	/// <remarks>
	/// <c>inline</c> is what makes a namespace-scope constant safe to define in a header, which is
	/// the only place a generated one ever appears; a static data member is already implicitly
	/// inline, so saying it inside a class would be noise at best. <see cref="insideType"/> is what
	/// tells the two apart, and it is a depth rather than a flag so a type nested in a type stays
	/// balanced.
	/// </remarks>
	private string SpellStorage(FieldDeclaration field)
	{
		if (field.IsConstant)
		{
			return insideType > 0 ? "static constexpr " : "inline constexpr ";
		}

		return field.IsStatic ? "static " : string.Empty;
	}

	/// <summary>
	/// Spells a type's argument list, supplying the one a bare container does not name.
	/// </summary>
	/// <param name="type">The type whose arguments to spell.</param>
	/// <returns>The angle-bracketed list, or nothing when the type takes no arguments.</returns>
	private static string SpellTypeArguments(TypeReference type)
	{
		if (type.TypeArguments.Count > 0)
		{
			return $"<{string.Join(", ", type.TypeArguments.Select(MapToCppType))}>";
		}

		return DefaultTypeArguments.TryGetValue(type.Name, out string? fallback) ? fallback : string.Empty;
	}
}
