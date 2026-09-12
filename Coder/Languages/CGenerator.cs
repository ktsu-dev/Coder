// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates C code from AST nodes.
/// </summary>
/// <remarks>
/// C is the one target here with no classes, no namespaces, no overloading and no generics, so this
/// generator spells the AST in the conventions C uses in their place rather than pretending it has
/// them: a type is a <c>typedef struct</c>, a member function is a free function whose first
/// parameter is the instance, an interface is a struct of function pointers, and a namespace is a
/// comment. Each of those is what a C programmer writes by hand for the same declaration.
/// <para>
/// The dialect is C99 with C11's <c>_Static_assert</c> — designated initialisers, compound literals
/// and <c>//</c> comments are C99, and <c>_Static_assert</c> is the only thing asked of C11. Nothing
/// needs C23, which is why <c>[[nodiscard]]</c> is not written for a pure function and a fixed
/// underlying type is not written on an enumeration: both would restrict the output to a dialect
/// most C is still not compiled as, and neither changes what the program means.
/// </para>
/// <para>
/// The AST's type names are the same language-neutral set the other generators consume (<c>str</c>,
/// <c>int</c>, <c>bool</c>, …), so they are mapped to C spellings; anything unrecognised is emitted
/// verbatim on the assumption the caller meant a C type. <c>bool</c> maps to <c>bool</c> rather than
/// to <c>_Bool</c>, so a file using one declares <c>&lt;stdbool.h&gt;</c> among its imports — which
/// is what <see cref="SourceFile.Imports"/> is for, being the one part of the AST that is chosen per
/// language anyway.
/// </para>
/// </remarks>
public class CGenerator : CFamilyGenerator
{
	/// <summary>
	/// What a declaration that never said what type it is gets.
	/// </summary>
	/// <remarks>
	/// A type is optional on every node that carries one, because a half-built AST is a thing the
	/// editor has to be able to hold. C's most general type is an untyped pointer, which keeps the
	/// output compiling while making it obvious which declaration was never finished.
	/// </remarks>
	private const string UnknownTypeName = "object";

	/// <summary>
	/// The name a member function's first parameter is given.
	/// </summary>
	/// <remarks>
	/// C has no implicit receiver, so the instance is an ordinary parameter and needs an ordinary
	/// name. <c>self</c> rather than <c>this</c>, which is a keyword in the languages a caller is
	/// most likely to paste the generated header into.
	/// </remarks>
	private const string SelfParameterName = "self";

	/// <summary>
	/// The name given to the member a derived struct holds its base in.
	/// </summary>
	/// <remarks>
	/// One name rather than the base type's, so the member a caller reaches the base through is the
	/// same word whatever it is derived from — and so that renaming the base does not rename the
	/// member.
	/// </remarks>
	private const string BaseMemberName = "base";

	/// <summary>
	/// What an embedded interface is called as a member.
	/// </summary>
	/// <param name="contract">The interface being embedded.</param>
	/// <returns>The member's name.</returns>
	/// <remarks>
	/// Its own name with the first letter lowered, which is what the member of a C struct usually
	/// looks like and, more to the point, is derivable from the type by anyone reading the header:
	/// a caller passing the object where the interface is wanted has to write
	/// <c>&amp;object-&gt;drawable</c>, and a name it could not have guessed would send it back to
	/// the declaration every time.
	/// </remarks>
	private static string MemberNameOf(TypeReference contract)
	{
		string name = contract.Name;

		return name.Length == 0
			? "implemented"
			: char.ToLowerInvariant(name[0]) + name[1..];
	}

	private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "str", "const char*" },
		{ "string", "const char*" },
		{ "int", "int" },
		{ "long", "long long" },
		{ "float", "float" },
		{ "double", "double" },
		{ "bool", "bool" },
		{ "void", "void" },
		{ "object", "void*" },
		{ "dict", "void*" }
	};

	/// <summary>
	/// How many type declarations enclose what is being written.
	/// </summary>
	/// <remarks>
	/// A depth rather than a flag, so a type declared inside a type leaves the count right when it
	/// closes. It decides what a field may say about itself: at file scope a constant is
	/// <c>static const</c>, and inside a struct there is no such thing — C has neither static data
	/// members nor default member initialisers.
	/// </remarks>
	private int insideType;

	/// <summary>
	/// The word each operator symbol is named by, where C has to spell an operator as a function.
	/// </summary>
	/// <remarks>
	/// C has no operator overloading, so an operator declaration becomes an ordinary function and
	/// needs an ordinary name. The names are the AST's own — <see cref="BinaryOperator.Add"/> is
	/// <c>add</c> and <see cref="BinaryOperator.LessThanOrEqual"/> is <c>less_than_or_equal</c> —
	/// derived from the vocabulary rather than listed beside it, so an operator added to the AST is
	/// named here without anybody remembering to. A symbol the AST does not have keeps the symbol,
	/// spelled out of the way of the identifier grammar by <see cref="SpellOperatorName"/>.
	/// <para>
	/// A symbol that is both a binary and a unary operator — <c>-</c> is subtraction and negation —
	/// is named for the binary one, which is what a declaration taking an operand beside the instance
	/// means.
	/// </para>
	/// </remarks>
	private static readonly Dictionary<string, string> OperatorNames = BuildOperatorNames(SnakeCase);

	/// <summary>
	/// Writes a name the way C names things.
	/// </summary>
	/// <param name="name">The name, as the AST spells it.</param>
	/// <returns>The same name in lower case, with an underscore where each word begins.</returns>
	private static string SnakeCase(string name) =>
		string.Concat(name.Select((character, index) =>
			char.IsUpper(character) && index > 0
				? $"_{char.ToLowerInvariant(character)}"
				: char.ToLowerInvariant(character).ToString()));

	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "c";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "C";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "c";

	/// <inheritdoc/>
	/// <remarks>
	/// C has no member functions, so a member becomes a free function named for the type it belongs
	/// to and taking the instance as its first parameter — <c>Point_translate(Point* self, …)</c>.
	/// That is what C code written by hand does.
	/// <para>
	/// Nothing is written for <see cref="FunctionDeclaration.IsPure"/>,
	/// <see cref="FunctionDeclaration.MustUseResult"/>,
	/// <see cref="FunctionDeclaration.IsCompileTimeEvaluable"/>,
	/// <see cref="FunctionDeclaration.IsNoThrow"/>, <see cref="FunctionDeclaration.IsExplicit"/> or
	/// <see cref="FunctionDeclaration.IsFriend"/>. The first three have no spelling before C23, and
	/// the last three describe things C does not have — exceptions, converting constructors, and an
	/// access control to be excepted from.
	/// </para>
	/// <para>
	/// Nothing is written for <see cref="FunctionDeclaration.IsVirtual"/> either, and that one is not
	/// a gap: C dispatches dynamically through a struct of function pointers, which is what an
	/// interface becomes here, so a virtual member is either already reached through one or is an
	/// ordinary function that happens to be overridable in a language that is not C.
	/// </para>
	/// </remarks>
	protected override void GenerateFunction(FunctionDeclaration funcDecl, CodeBlocker code, string? enclosingType)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		// A deleted declaration exists to make a call illegal, and C has no way to say that. Writing
		// the prototype would do the opposite of what it asks for, so what is written is the reason
		// the function is missing — which is what someone looking for it needs.
		if (funcDecl.Definition == FunctionDefinition.Deleted)
		{
			GenerateDocumentation(funcDecl, code);
			WriteInexpressible(code, $"{SpellFunctionName(funcDecl, enclosingType)} is deleted: C cannot refuse a call");
			return;
		}

		GenerateDocumentation(funcDecl, code);

		// Internal linkage is the only privacy C has, and it is what a private function wants: the
		// declaration is visible to this translation unit and to nothing else.
		if (funcDecl.Visibility == Visibility.Private)
		{
			code.Write("static ");
		}

		// A constructor is the only member function that returns the type rather than acting on an
		// instance of it; a destructor returns nothing. Neither has a return type of its own to read.
		code.Write($"{SpellReturnType(funcDecl, enclosingType)} ");
		code.Write(SpellFunctionName(funcDecl, enclosingType));
		WriteParameterList(ParametersOf(funcDecl, enclosingType), code);

		// A declaration with no definition is a prototype, which is all C has to offer for either of
		// them: an abstract declaration is one an implementation must supply, and a defaulted one is
		// one the language would have supplied had it been C++.
		if (funcDecl.IsAbstract || funcDecl.Definition == FunctionDefinition.Defaulted)
		{
			code.WriteLine(";");
			return;
		}

		// The line is ended before the scope opens, so C's brace lands on its own line.
		code.WriteLine();

		using Scope body = new(code);

		if (funcDecl.Kind == FunctionKind.Constructor)
		{
			GenerateConstructorBody(funcDecl, code, enclosingType);
			return;
		}

		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <summary>
	/// Writes a constructor's body: build the value, run what the constructor asked for, hand it back.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="enclosingType">The name of the type being constructed.</param>
	/// <remarks>
	/// C constructs nothing on its own, so a constructor is a function that returns a value it built.
	/// The initialiser list becomes the designated initialiser it already is — which is the one place
	/// C is the better fit, since C's designators need not be in declaration order and C++20's do —
	/// and a constructor with no initialisers starts from <c>{0}</c>, so every member of the returned
	/// value has a value whether or not anyone named it.
	/// </remarks>
	private void GenerateConstructorBody(FunctionDeclaration funcDecl, CodeBlocker code, string? enclosingType)
	{
		string typeName = enclosingType ?? funcDecl.Name ?? "UnnamedType";

		code.Write($"{typeName} {SelfParameterName} = ");

		if (funcDecl.Initialisers.Count == 0)
		{
			// Not `{}`, which C only allows from C23. `{0}` is the spelling that zeroes a whole
			// object in every dialect, whatever the first member's type is.
			code.WriteLine("{0};");
		}
		else
		{
			ConstructionExpression initialiser = new(type: null);
			foreach (MemberInitialiser member in funcDecl.Initialisers)
			{
				initialiser.Arguments.Add(member);
			}

			WriteInitialiser(initialiser, code);
			code.WriteLine(";");
		}

		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, code);
		}

		code.WriteLine($"return {SelfParameterName};");
	}

	/// <summary>
	/// Spells what a function hands back.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>The return type as C writes it.</returns>
	private static string SpellReturnType(FunctionDeclaration funcDecl, string? enclosingType) => funcDecl.Kind switch
	{
		FunctionKind.Constructor => enclosingType ?? funcDecl.Name ?? "void",
		FunctionKind.Destructor => "void",
		_ => MapToCType(funcDecl.ReturnType ?? new TypeReference("void")),
	};

	/// <summary>
	/// Spells the name a declaration is written under.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>The name as C writes it.</returns>
	/// <remarks>
	/// C has one namespace for functions and no overloading, so a member's name has to carry the
	/// type's: two types with a <c>reset</c> each would otherwise be one function declared twice.
	/// </remarks>
	private static string SpellFunctionName(FunctionDeclaration funcDecl, string? enclosingType)
	{
		string bare = funcDecl.Kind switch
		{
			FunctionKind.Constructor => "create",
			FunctionKind.Destructor => "destroy",
			FunctionKind.Operator => SpellOperatorName(funcDecl.Name),
			FunctionKind.ConversionOperator =>
				$"to_{Identifier(MapToCType(funcDecl.ReturnType ?? new TypeReference("void")))}",
			_ => funcDecl.Name ?? "unnamedFunction",
		};

		return enclosingType is null ? bare : $"{enclosingType}_{bare}";
	}

	/// <summary>
	/// Names an operator, which C can only declare as a function.
	/// </summary>
	/// <param name="symbol">The operator's symbol, as the declaration carries it.</param>
	/// <returns>The function's name, without the type it belongs to.</returns>
	private static string SpellOperatorName(string? symbol)
	{
		if (symbol is null)
		{
			return "operator";
		}

		return OperatorNames.TryGetValue(symbol, out string? word)
			? word
			: $"operator_{Identifier(symbol)}";
	}

	/// <summary>
	/// Turns text into something that can be part of a C identifier.
	/// </summary>
	/// <param name="text">The text to fold.</param>
	/// <returns>The text with everything C would not accept replaced by an underscore.</returns>
	private static string Identifier(string text)
	{
		char[] folded = [.. text.Select(character => char.IsLetterOrDigit(character) ? character : '_')];
		return new string(folded).Trim('_');
	}

	/// <summary>
	/// The parameters a function is written with, including the instance a member acts on.
	/// </summary>
	/// <param name="funcDecl">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>The full parameter list.</returns>
	/// <remarks>
	/// A static member acts on no instance and a constructor has none to act on yet, so neither takes
	/// one. <see cref="FunctionDeclaration.IsReadOnly"/> — a member function that does not modify
	/// what it is called on — becomes a pointer to <c>const</c>, which is exactly what it says and is
	/// the only place C has to say it.
	/// </remarks>
	private static List<Parameter> ParametersOf(FunctionDeclaration funcDecl, string? enclosingType)
	{
		List<Parameter> parameters = [];

		if (enclosingType is not null && !funcDecl.IsStatic && funcDecl.Kind != FunctionKind.Constructor)
		{
			parameters.Add(SelfParameter(enclosingType, funcDecl.IsReadOnly));
		}

		parameters.AddRange(funcDecl.Parameters);
		return parameters;
	}

	/// <summary>
	/// Builds the parameter a member function receives its instance through.
	/// </summary>
	/// <param name="typeName">The type the function belongs to, or null for an interface's own.</param>
	/// <param name="isReadOnly">Whether the function promises not to modify the instance.</param>
	/// <returns>The parameter.</returns>
	private static Parameter SelfParameter(string? typeName, bool isReadOnly) =>
		new(SelfParameterName)
		{
			// An interface does not know what implements it, so its receiver is an untyped pointer.
			Type = new TypeReference(typeName ?? "void")
			{
				Indirection = TypeIndirection.Pointer,
				IsReadOnly = isReadOnly,
			},
		};

	/// <summary>
	/// Writes a parenthesised parameter list, saying <c>void</c> where there are none.
	/// </summary>
	/// <param name="parameters">The parameters to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// An empty list in C declares a function whose parameters are unspecified rather than one that
	/// takes none, so a call passing three arguments to it is legal and unchecked. <c>(void)</c> is
	/// the spelling that means what every other language here means by writing nothing.
	/// </remarks>
	private void WriteParameterList(List<Parameter> parameters, CodeBlocker code)
	{
		code.Write("(");

		if (parameters.Count == 0)
		{
			code.Write("void");
		}
		else
		{
			GenerateParameterList(parameters, code);
		}

		code.Write(")");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// C has no namespaces and no way to add one, so the members are written flat and the name is
	/// written above them as a comment. The alternative — folding the name into every declaration's
	/// name, which is what a C library does by hand — would rename the declarations without renaming
	/// the references to them elsewhere in the AST, and a generator that silently breaks the code it
	/// writes is worse than one that says what the language cannot do.
	/// </remarks>
	protected override void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(namespaceDecl, code);

		string name = string.Join("_", NamespaceDeclaration.Split(namespaceDecl.Name));
		WriteInexpressible(code, $"namespace {name}: C has none, so these declarations are not nested in one");
		code.NewLine();

		bool first = true;
		foreach (AstNode member in namespaceDecl.Members)
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;
			GenerateInternal(member, code);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Every kind of type declaration becomes a <c>typedef struct</c>, so a caller writes
	/// <c>Point</c> rather than <c>struct Point</c> — the tag is kept as well as the alias, because a
	/// struct that names itself is the only way to declare one that points at its own kind.
	/// <para>
	/// A C struct holds data and nothing else, so the members are written in three groups rather than
	/// in the order they were declared: the types a member might be declared with first, then the
	/// struct, then the functions. Anything else would reference a type the compiler has not seen.
	/// </para>
	/// <para>
	/// An interface is the exception, and is where C is more interesting than it looks: a set of
	/// members an implementation supplies is a struct of function pointers, each taking the instance
	/// as an untyped pointer. That is what every C library that dispatches dynamically does, and it
	/// is a real translation of the declaration rather than a comment apologising for one.
	/// </para>
	/// <para>
	/// <see cref="ClassDeclaration.BaseType"/> becomes a first member holding the base. A struct
	/// whose first member is another struct is layout-compatible with it, so a pointer to the derived
	/// one may be used as a pointer to the base — which is what C has instead of inheritance, and why
	/// the member has to come first rather than merely be present.
	/// </para>
	/// <para>
	/// A data member's visibility is not written at all: C has no access control within a struct, and
	/// its one privacy — internal linkage — is a property of a declaration rather than of a member of
	/// one. A member function is a declaration once it is lifted out of the struct, so a private one
	/// does take internal linkage; that is <see cref="GenerateFunction"/>'s business rather than this
	/// method's.
	/// </para>
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		string name = classDecl.Name ?? "UnnamedStruct";
		bool isInterface = classDecl.Kind == TypeDeclarationKind.Interface;

		// C has no templates, so it has nothing a declaration for one type rather than of one could
		// be. Said rather than dropped, the same as every other target that cannot spell it.
		if (classDecl.IsSpecialisation)
		{
			WriteInexpressible(code, $"specialised for {string.Join(", ", classDecl.SpecialisationArguments)}");
		}

		List<AstNode> nestedTypes = [.. classDecl.Members.Where(IsTypeDeclaration)];
		List<FunctionDeclaration> functions = isInterface
			? []
			: [.. classDecl.Members.OfType<FunctionDeclaration>()];
		List<AstNode> fields =
		[
			.. classDecl.Members.Where(member =>
				!IsTypeDeclaration(member) && (isInterface || member is not FunctionDeclaration)),
		];

		foreach (AstNode nested in nestedTypes)
		{
			GenerateInternal(nested, code);
			code.NewLine();
		}

		GenerateDocumentation(classDecl, code);
		WriteTypePromises(classDecl, code);

		code.WriteLine($"typedef struct {name}");
		code.WriteLine("{");
		code.Indent();

		insideType++;

		// A base and an interface are the same thing here: a struct embedded as a member, whose
		// own members are reached through it. What the first position buys is that a pointer to the
		// whole is a pointer to that member, so the two are interchangeable without a cast -- and C
		// has exactly one first position to give, so the base takes it and an interface after it is
		// reached by taking its address instead.
		List<(TypeReference Type, string Member)> embedded =
		[
			.. classDecl.BaseType is TypeReference baseType ? (List<(TypeReference, string)>)[(baseType, BaseMemberName)] : [],
			.. classDecl.Interfaces.Select(contract => (contract, MemberNameOf(contract))),
		];

		if (embedded.Count > 0)
		{
			WriteInexpressible(code, embedded.Count == 1
				? $"{embedded[0].Member} is first, so that a pointer to this is a pointer to it"
				: $"{embedded[0].Member} is first, so that a pointer to this is a pointer to it; the rest are reached by taking their address");

			foreach ((TypeReference embeddedType, string member) in embedded)
			{
				code.WriteLine($"{SpellDeclarator(embeddedType, member)};");
			}

			if (fields.Count > 0)
			{
				code.NewLine();
			}
		}

		AstNode? previous = null;
		foreach (AstNode member in fields)
		{
			if (previous is not null && NeedsSeparation(previous, member))
			{
				code.NewLine();
			}

			previous = member;

			switch (member)
			{
				case FunctionDeclaration method:
					GenerateFunctionPointer(method, code);
					break;

				case VariableDeclaration field:
					GenerateStructMember(field.Name, field.Type, field.InitialValue, field.IsConstant, code);
					break;

				default:
					GenerateInternal(member, code);
					break;
			}
		}

		insideType--;

		code.Outdent();
		code.WriteLine($"}} {name};");

		foreach (FunctionDeclaration function in functions)
		{
			code.NewLine();
			GenerateFunction(function, code, name);
		}
	}

	/// <summary>
	/// Reports whether a member declares a type rather than data or behaviour.
	/// </summary>
	/// <param name="member">The member to test.</param>
	/// <returns>True when it declares a type.</returns>
	private static bool IsTypeDeclaration(AstNode member) =>
		member is ClassDeclaration or EnumDeclaration or UsingAlias;

	/// <summary>
	/// Emits one member of a struct.
	/// </summary>
	/// <param name="name">The member's name.</param>
	/// <param name="type">The member's type.</param>
	/// <param name="initialValue">What the declaration said it starts at, if anything.</param>
	/// <param name="isConstant">Whether the declaration said its value never changes.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// C has no default member initialisers and no static data members, so neither an initial value
	/// nor a constant survives as syntax — but both are written as a comment rather than dropped,
	/// because whoever writes the initialiser for this struct is the one who needs to know them.
	/// A <c>const</c> member is not written either: it would make the whole struct unassignable,
	/// which is a much larger claim than the declaration made.
	/// </remarks>
	private void GenerateStructMember(string? name, TypeReference? type, Expression? initialValue, bool isConstant, CodeBlocker code)
	{
		code.Write(SpellDeclarator(type ?? new TypeReference(UnknownTypeName), name ?? string.Empty));
		code.Write(";");

		if (initialValue is not null || isConstant)
		{
			code.Write($"  {CommentPrefix} {DescribeMemberIntent(initialValue, isConstant)}");
		}

		code.WriteLine();
	}

	/// <summary>
	/// Says what a struct member asked for that C cannot write.
	/// </summary>
	/// <param name="initialValue">What the declaration said it starts at, if anything.</param>
	/// <param name="isConstant">Whether the declaration said its value never changes.</param>
	/// <returns>The note to write after the member.</returns>
	private string DescribeMemberIntent(Expression? initialValue, bool isConstant)
	{
		string? value = initialValue is null ? null : GenerateExpression(initialValue);

		return (value, isConstant) switch
		{
			(not null, true) => $"constant, {value}",
			(not null, false) => $"defaults to {value}",
			_ => "constant",
		};
	}

	/// <summary>
	/// Emits an interface's member: a pointer to the function an implementation supplies.
	/// </summary>
	/// <param name="funcDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The receiver is <c>void*</c> rather than the interface's own type, because what implements an
	/// interface is not the interface: a pointer to the implementation is what the caller has, and a
	/// struct of function pointers is what it is reached through.
	/// </remarks>
	private void GenerateFunctionPointer(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		GenerateDocumentation(funcDecl, code);

		List<Parameter> parameters = [];
		if (!funcDecl.IsStatic)
		{
			parameters.Add(SelfParameter(null, funcDecl.IsReadOnly));
		}

		parameters.AddRange(funcDecl.Parameters);

		code.Write($"{MapToCType(funcDecl.ReturnType ?? new TypeReference("void"))} ");
		code.Write($"(*{funcDecl.Name ?? "unnamedFunction"})");
		WriteParameterList(parameters, code);
		code.WriteLine(";");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <c>_Static_assert</c> rather than <c>static_assert</c>: the underscored spelling is C11's own
	/// and needs no header, while the other is a macro in <c>&lt;assert.h&gt;</c> that the file would
	/// have to have asked for. C11 also requires a message, so an assertion with none is given its
	/// own condition — which is what a reader wants anyway when nobody wrote a better one.
	/// </remarks>
	protected override void GenerateCompileTimeAssertion(CompileTimeAssertion assertion, CodeBlocker code)
	{
		Ensure.NotNull(assertion);
		Ensure.NotNull(code);

		string condition = assertion.Condition ?? "0";

		code.Write($"_Static_assert({condition},");
		code.WriteLine();
		code.Indent();
		code.Write($"\"{EscapeString(assertion.Message ?? condition)}\"");
		code.Outdent();
		code.Write(")");
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A typedef, which is what C has where another language has an alias — and it is written through
	/// <see cref="CFamilyGenerator.SpellDeclarator"/> rather than by naming the type and then the name, because a
	/// typedef declares a name the same way a variable declaration does and an alias for an array
	/// puts its brackets after the name.
	/// </remarks>
	protected override void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		Ensure.NotNull(usingAlias);
		Ensure.NotNull(code);

		GenerateDocumentation(usingAlias, code);
		code.Write($"typedef {SpellDeclarator(usingAlias.AliasedType ?? new TypeReference(UnknownTypeName), usingAlias.Name ?? string.Empty)}");
		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A compound literal — <c>(Point){ .x = 1 }</c> — which is C's way of writing a value of a named
	/// type where one is wanted, and the reason a constructor can return the thing it built in one
	/// statement. With no type it is the braced list alone, which is what initialises a declaration
	/// that has already said its type.
	/// <para>
	/// A <see cref="MemberInitialiser"/> among the arguments is a designated initialiser, which C
	/// invented and does not require to be in declaration order. A list whose own elements are lists
	/// is a table and is written one row per line; a list of plain values stays on one.
	/// </para>
	/// </remarks>
	protected override void GenerateConstructionExpression(ConstructionExpression construction, CodeBlocker code) =>
		WriteList(construction, code, asExpression: true);

	/// <inheritdoc/>
	/// <remarks>
	/// C has no member functions, so a receiver is not written in front of the callee: it becomes the
	/// first argument, which is the same lowering <see cref="GenerateFunction"/> already performs on
	/// the declaration — <c>Point_translate(Point* self, …)</c>. A call site that kept the dot would
	/// not reach the function this generator emitted for it.
	/// <para>
	/// Its address is taken, because that <c>self</c> parameter is a pointer. That assumes the
	/// receiver is an instance rather than already a pointer to one, which is an assumption rather
	/// than a deduction: a <see cref="CallExpression"/> knows the receiver's spelling and not its
	/// type. It is the assumption worth making, because taking the address is the only one of the two
	/// a caller cannot write for itself — the AST has no address-of operator — and because what this
	/// generator emits elsewhere is instances. A caller holding a pointer spells the call as a free
	/// function and passes the pointer as an ordinary argument.
	/// </para>
	/// <para>
	/// What is not done is mangling the name: the declaration's is built from the type it belongs to,
	/// and the receiver's type is exactly what is not known here, so
	/// <see cref="CallExpression.Callee"/> is written verbatim and choosing it stays the caller's —
	/// which is what the node says it is everywhere else too.
	/// </para>
	/// </remarks>
	protected override void GenerateCallExpression(CallExpression callExpr, CodeBlocker code)
	{
		Ensure.NotNull(callExpr);
		Ensure.NotNull(code);

		code.Write(callExpr.Callee);
		code.Write("(");

		if (callExpr.Receiver is not null)
		{
			code.Write("&");
			GenerateInternal(callExpr.Receiver, code);

			if (callExpr.Arguments.Count > 0)
			{
				code.Write(", ");
			}
		}

		GenerateArgumentList(callExpr.Arguments, code);
		code.Write(")");
	}

	/// <summary>
	/// Writes what a declaration starts at.
	/// </summary>
	/// <param name="initialValue">The value the declaration was given.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A braced list, not a compound literal: C distinguishes the two by where they appear, and only
	/// the braced form is a constant expression, which is what an object with static storage duration
	/// has to be initialised by. Naming the type again would be redundant in the best case and
	/// rejected at file scope in the ordinary one.
	/// </remarks>
	private void WriteInitialiser(Expression initialValue, CodeBlocker code) =>
		WriteListValue(initialValue, code);

	/// <summary>
	/// Writes a braced list, as either an initialiser or a value in its own right.
	/// </summary>
	/// <param name="construction">The expression to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="asExpression">
	/// Whether the list stands where a value is wanted, and so needs its type in front of it.
	/// </param>
	private void WriteList(ConstructionExpression construction, CodeBlocker code, bool asExpression)
	{
		Ensure.NotNull(construction);
		Ensure.NotNull(code);

		if (asExpression && construction.Type is not null)
		{
			code.Write($"({MapToCType(construction.Type)})");
		}

		// `{0}` rather than `{}`, which C only allows from C23, and which zeroes a whole object
		// whatever the type of its first member is.
		WriteBracedList(construction, code, "{0}");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An element that is itself a braced list is written without its type. The enclosing list has
	/// already said what each element is, so a row of a table is <c>{ .x = 1 }</c> rather than
	/// <c>(Point){ .x = 1 }</c> — and at file scope the second is not a constant expression, which
	/// is what a constant table has to be initialised by.
	/// </remarks>
	protected override void WriteListValue(AstNode value, CodeBlocker code)
	{
		if (value is ConstructionExpression nested)
		{
			WriteList(nested, code, asExpression: false);
			return;
		}

		GenerateInternal(value, code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A C enumeration is unscoped: its members are names in the scope around it, so two enumerations
	/// with a <c>None</c> each would be one name declared twice. Prefixing each member with the
	/// enumeration's name is what C code does instead, and it is the same thing C++'s <c>enum
	/// class</c> does by requiring the name at the use site — the difference is only that C has to
	/// spell it into the declaration.
	/// <para>
	/// A fixed underlying type is written as a comment rather than as syntax. C says only that the
	/// type is one capable of holding every member, and the spelling that pins it is C23's; a file
	/// that needs the guarantee can assert it with a <see cref="CompileTimeAssertion"/>, which is a
	/// thing the AST can already say.
	/// </para>
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		string name = enumDecl.Name ?? "UnnamedEnum";

		// Above the documentation rather than below it, so the comment block in front of the
		// declaration stays one block rather than the note splitting it in two.
		if (enumDecl.UnderlyingType is TypeReference underlying)
		{
			WriteInexpressible(code, $"underlying type {MapToCType(underlying)}: C chooses one that fits the members");
		}

		GenerateDocumentation(enumDecl, code);

		code.WriteLine($"typedef enum {name}");
		code.WriteLine("{");
		code.Indent();

		foreach (EnumMember member in enumDecl.Members)
		{
			code.Write(SpellEnumMember(name, member.Name));

			if (member.Value is not null)
			{
				code.Write($" = {member.Value}");
			}

			// A trailing comma on the last member too, so adding one after it is a one-line diff.
			code.WriteLine(",");
		}

		code.Outdent();
		code.WriteLine($"}} {name};");
	}

	/// <summary>
	/// Spells an enumeration member's name, qualified by the enumeration it belongs to.
	/// </summary>
	/// <param name="enumName">The enumeration's name.</param>
	/// <param name="memberName">The member's name.</param>
	/// <returns>The name as C declares it.</returns>
	/// <remarks>
	/// A member already named for its enumeration is left alone: <c>Color_Red</c> in a <c>Color</c>
	/// is a caller who has already done this, and <c>Color_Color_Red</c> would be the generator
	/// doing it twice.
	/// </remarks>
	private static string SpellEnumMember(string enumName, string? memberName)
	{
		string bare = memberName ?? "Unnamed";
		return bare.StartsWith($"{enumName}_", StringComparison.Ordinal) ? bare : $"{enumName}_{bare}";
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A constant is <c>static const</c>, not <c>const</c>: a file-scope <c>const</c> in C has
	/// external linkage, so a header declaring one and included twice is the same object defined
	/// twice and does not link. The <c>static</c> spelling gives each translation unit its own,
	/// which is what a constant in a header means and what C has in place of C++'s <c>inline</c>.
	/// <para>
	/// Not <c>#define</c>, although that is what much C does: a macro has no type and no scope, it
	/// is not visible to a debugger, and it would substitute itself into every later use of the same
	/// word anywhere in the translation unit — including ones that are not this constant at all.
	/// </para>
	/// <para>
	/// A field with no initialiser is left bare rather than zeroed. An object with static storage
	/// duration is zero-initialised by the standard, so writing it would say nothing the language
	/// does not already promise.
	/// </para>
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		if (insideType > 0)
		{
			GenerateDocumentation(field, code);
			GenerateStructMember(field.Name, field.Type, field.InitialValue, field.IsConstant, code);
			return;
		}

		GenerateDocumentation(field, code);

		TypeReference type = field.Type ?? new TypeReference(UnknownTypeName);

		if (field.IsConstant)
		{
			code.Write("static const ");

			// The keyword has just been written, so a type that also says const would say it twice
			// — and `const const T` is not a type.
			if (type.IsReadOnly)
			{
				type = type.Clone();
				type.IsReadOnly = false;
			}
		}
		else if (field.IsStatic || field.Visibility == Visibility.Private)
		{
			code.Write("static ");
		}

		code.Write(SpellDeclarator(type, field.Name ?? string.Empty));

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			WriteInitialiser(field.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// C's <c>main</c> returns <c>int</c> whether or not the program means to hand back an exit code,
	/// so <see cref="EntryPoint.ReturnsExitCode"/> changes nothing in the signature — a program that
	/// returns nothing exits with zero, which C99 supplies by falling off the end.
	/// <para>
	/// <c>main(void)</c>, not <c>main()</c>. An empty parameter list in C declares a function whose
	/// parameters are unspecified rather than one that takes none, which is the opposite of what the
	/// entry point of a program with no arguments means.
	/// </para>
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		code.Write("int main(");
		code.Write(entryPoint.AcceptsArguments ? "int argc, char* argv[]" : "void");

		// The line is ended before the scope opens, so C's brace lands on its own line.
		code.WriteLine(")");

		using Scope body = new(code);
		foreach (AstNode statement in entryPoint.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A parameter's default value is written as a comment beside it. C has no default arguments, so
	/// the caller has to pass one — and the value the declaration chose is exactly what they need to
	/// know in order to pass the same thing.
	/// </remarks>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		TypeReference type = parameter.Type ?? new TypeReference(UnknownTypeName);

		// An empty name means deliberately unnamed, which C allows in a prototype. A null name means
		// nobody said, so one is invented.
		string name = parameter.Name is "" ? string.Empty : parameter.Name ?? $"param{position}";

		code.Write(name.Length == 0 ? MapToCType(type) : SpellDeclarator(type, name));

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" /* = {parameter.DefaultValue} */");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A constant local is <c>const</c>, which is all it needs to be: unlike a file-scope one it has
	/// no linkage to collide with. There is no <c>auto</c> to fall back on — C's is a storage class,
	/// not a deduced type — so an inferred declaration is written with the type it was given, or an
	/// untyped pointer when it was given none.
	/// </remarks>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);

		if (varDecl.IsConstant)
		{
			code.Write("const ");
		}

		code.Write(SpellDeclarator(varDecl.Type ?? new TypeReference(UnknownTypeName), varDecl.Name));

		if (varDecl.InitialValue is not null)
		{
			code.Write(" = ");
			WriteInitialiser(varDecl.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Writes an expression to a string, for the places a comment has to quote one.
	/// </summary>
	/// <param name="expression">The expression to write.</param>
	/// <returns>Its C source.</returns>
	private string GenerateExpression(Expression expression)
	{
		using CodeBlocker inline = CodeBlocker.Create(IndentString);
		GenerateInternal(expression, inline);
		return inline.ToString().TrimEnd('\r', '\n');
	}

	/// <summary>
	/// Spells a type in C.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The C source for it.</returns>
	/// <remarks>
	/// A reference and a pointer are both <c>*</c>. C has no references, and the one thing the
	/// distinction carries — that a reference is never null — is not something C can say about a
	/// parameter either way.
	/// <para>
	/// C has no generic types, so a type's arguments are folded into its name:
	/// <c>Vector&lt;int&gt;</c> is <c>Vector_int</c>, which is what the macro that generated such a
	/// type would have called it. A <c>list</c> is the one the AST names without C having it, and it
	/// becomes a pointer to its element — the count travels separately, because in C it always does.
	/// </para>
	/// </remarks>
	private static string MapToCType(TypeReference type)
	{
		string spelled = SpellTypeName(type);

		string indirection = type.Indirection is TypeIndirection.Reference or TypeIndirection.Pointer
			? "*"
			: string.Empty;

		// A mapped spelling that is already const — `str` is `const char*` — is left alone rather
		// than written `const const char*`, which is not a type.
		string qualifier = type.IsReadOnly && !spelled.StartsWith("const ", StringComparison.Ordinal)
			? "const "
			: string.Empty;

		return $"{qualifier}{spelled}{indirection}{(type.IsArray ? "[]" : string.Empty)}";
	}

	/// <summary>
	/// Spells a type's name, arguments and all.
	/// </summary>
	/// <param name="type">The type whose name to spell.</param>
	/// <returns>The name as C writes it.</returns>
	private static string SpellTypeName(TypeReference type)
	{
		// A list is a pointer to its elements, which is what C has: there is no container type, and
		// the length is a second thing the program carries beside it.
		if (string.Equals(type.Name, "list", StringComparison.OrdinalIgnoreCase))
		{
			return type.TypeArguments.Count == 1
				? $"{SpellTypeName(type.TypeArguments[0])}*"
				: "void*";
		}

		if (TypeMappings.TryGetValue(type.Name, out string? mapped))
		{
			return mapped;
		}

		return type.TypeArguments.Count == 0
			? type.Name
			: $"{type.Name}_{string.Join("_", type.TypeArguments.Select(argument => Identifier(SpellTypeName(argument))))}";
	}

	/// <inheritdoc/>
	protected override string SpellType(TypeReference type)
	{
		Ensure.NotNull(type);
		return MapToCType(type);
	}
}
