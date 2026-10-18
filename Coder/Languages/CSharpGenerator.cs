// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

/// <summary>
/// Generates C# code from AST nodes.
/// </summary>
public class CSharpGenerator : LanguageGeneratorBase
{
	/// <summary>
	/// Gets the unique identifier for the C# language generator.
	/// </summary>
	public override string LanguageId => "csharp";

	/// <summary>
	/// Gets the display name for the C# language generator.
	/// </summary>
	public override string DisplayName => "C#";

	/// <summary>
	/// Gets the file extension for C# files.
	/// </summary>
	public override string FileExtension => "cs";

	/// <summary>
	/// Generates code for the specified AST node.
	/// </summary>
	/// <param name="node">The AST node to generate code for.</param>
	/// <param name="code">The writer to emit into.</param>
	protected override void GenerateInternal(AstNode node, CodeBlocker code)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(code);

		switch (node)
		{
			case ClassDeclaration classDecl:
				GenerateClass(classDecl, code);
				break;
			case FunctionDeclaration function:
				GenerateFunction(function, code);
				break;
			case EntryPoint entryPoint:
				GenerateEntryPoint(entryPoint, code);
				break;
			case Parameter parameter:
				GenerateParameter(parameter, code);
				break;
			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, code);
				break;
			case BinaryExpression binaryExpr:
				GenerateBinaryExpression(binaryExpr, code, GetBinaryOperator(binaryExpr.Operator));
				break;
			case UnaryExpression unaryExpr:
				GenerateUnaryExpression(unaryExpr, code, GetUnaryOperator(unaryExpr.Operator));
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
			default:
				GenerateExpressionOrLeaf(node, code);
				break;
		}
	}

	/// <summary>
	/// Emits an expression or a leaf.
	/// </summary>
	/// <param name="node">The node to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Split from the declarations only because one switch over every node the AST has is more
	/// branches than the analyzer accepts. The line is the same one the AST already draws.
	/// </remarks>
	private void GenerateExpressionOrLeaf(AstNode node, CodeBlocker code)
	{
		switch (node)
		{
			case VariableReference varRef:
				code.Write(varRef.Name);
				break;
			case LiteralExpression<string> stringLit:
				code.Write($"\"{EscapeString(stringLit.Value ?? string.Empty)}\"");
				break;
			case LiteralExpression<int> intLit:
				code.Write(intLit.Value.ToString(CultureInfo.InvariantCulture));
				break;
			case LiteralExpression<bool> boolLit:
				code.Write(boolLit.Value ? "true" : "false");
				break;
			case LiteralExpression<double> doubleLit:
				code.Write(double.IsFinite(doubleLit.Value)
					? $"{doubleLit.Value.ToString(CultureInfo.InvariantCulture)}d"
					: SpellNonFiniteDouble(doubleLit.Value));
				break;
			case LiteralExpression<float> floatLit:
				code.Write(float.IsFinite(floatLit.Value)
					? $"{floatLit.Value.ToString(CultureInfo.InvariantCulture)}f"
					: SpellNonFiniteSingle(floatLit.Value));
				break;
			case LiteralExpression<long> longLit:
				code.Write($"{longLit.Value.ToString(CultureInfo.InvariantCulture)}L");
				break;
			case SourceFile file:
				GenerateSourceFile(file, code);
				break;
			case NamespaceDeclaration namespaceDecl:
				GenerateNamespace(namespaceDecl, code);
				break;
			case CompileTimeAssertion assertion:
				WriteInexpressible(code, $"asserted at build time: {assertion.Condition}");
				break;
			case UsingAlias usingAlias:
				GenerateUsingAlias(usingAlias, code);
				break;
			case ConstructionExpression construction:
				GenerateConstruction(construction, code);
				break;
			case EnumDeclaration enumDecl:
				GenerateEnum(enumDecl, code);
				break;
			case PropertyDeclaration property:
				GenerateProperty(property, code);
				break;
			case FieldDeclaration field:
				GenerateField(field, code);
				break;
			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, code);
				break;
			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, code);
				break;
			default:
				// The legacy AstLeafNode shapes are spelled the same in every language, so they come
				// from the shared path; anything else is genuinely unrecognised.
				if (!TryGenerateCommonNode(node, code))
				{
					WriteComment(code, "//", $"Unsupported node type: {node.GetType().Name}");
				}

				break;
		}
	}

	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public override bool CanGenerate(AstNode astNode) => CanGenerateStandardNodes(astNode);

	/// <summary>
	/// Emits a class declaration and its members.
	/// </summary>
	/// <param name="classDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A member is emitted through the same dispatch as any other node, so a nested class, a method
	/// and a field each come out through the emitter for their own shape.
	/// </remarks>
	private void GenerateClass(ClassDeclaration classDecl, CodeBlocker code)
	{
		bool aliasesWereHoisted = memberAliasesHoisted;
		if (!aliasesWereHoisted)
		{
			WriteHoistedAliases(MemberAliases(classDecl), code);
		}

		WritePreamble(classDecl, code, PreambleSite.Type);

		// C++ can attach a declaration to a type it does not own, by specialising a template on it.
		// Nothing here can, so the fact is written down rather than lost: what follows is an
		// ordinary declaration, and the comment says which type it was the declaration for.
		if (classDecl.IsSpecialisation)
		{
			WriteInexpressible(code, $"specialised for {string.Join(", ", classDecl.SpecialisationArguments)}");
		}

		// readonly is a modifier only a struct may carry (CS0106). A class or interface still makes the
		// claim, so it is written down the way the targets with no word for it write it down.
		bool isReadOnlyStruct = classDecl.IsReadOnly && classDecl.Kind == TypeDeclarationKind.Struct;
		if (classDecl.IsReadOnly && !isReadOnlyStruct)
		{
			WriteTypePromises(classDecl, code, recordIsSpelled: true);
		}

		string keyword = classDecl.Kind switch
		{
			TypeDeclarationKind.Struct => "struct",
			TypeDeclarationKind.Interface => "interface",
			_ => "class",
		};

		// In the order C# takes them: access, then whether it can be instantiated, then the promise
		// the type makes about itself, then the permission to declare the rest of it elsewhere, then
		// what kind of type it is. A record is written before the keyword rather than instead of it,
		// which is what makes `record struct` reachable.
		string[] modifiers =
		[
			SpellVisibility(classDecl.Visibility) ?? DefaultVisibility,
			.. IsAbstractClass(classDecl) ? (string[])["abstract"] : [],
			.. isReadOnlyStruct ? (string[])["readonly"] : [],
			.. classDecl.IsPartial ? (string[])["partial"] : [],
			.. classDecl.IsRecord ? (string[])["record"] : [],
			keyword,
		];

		code.Write($"{string.Join(" ", modifiers)} {classDecl.Name ?? "UnnamedClass"}{SpellTypeParameters(classDecl.TypeParameters)}");

		// One list, base first. C# takes at most one class in it and puts it first, which is why
		// the AST keeps the two apart: the order is not something a generator could recover.
		string[] inherited =
		[
			.. classDecl.BaseType is TypeReference baseType ? (string[])[MapToCSType(baseType)] : [],
			.. classDecl.Interfaces.Select(MapToCSType),
		];

		if (inherited.Length > 0)
		{
			code.Write($" : {string.Join(", ", inherited)}");
		}

		// The line is ended before the scope opens, so C#'s brace lands on its own line.
		code.WriteLine();
		WriteConstraintClauses(classDecl.TypeParameters, code);

		using Scope members = new(code);
		bool enclosingIsReadOnlyStruct = inReadOnlyStruct;
		inReadOnlyStruct = isReadOnlyStruct;
		memberAliasesHoisted = true;
		try
		{
			foreach (AstNode member in classDecl.Members)
			{
				if (member is FunctionDeclaration method)
				{
					GenerateFunction(method, code, classDecl);
				}
				else if (member is not UsingAlias)
				{
					GenerateInternal(member, code);
				}
			}
		}
		finally
		{
			inReadOnlyStruct = enclosingIsReadOnlyStruct;
			memberAliasesHoisted = aliasesWereHoisted;
		}
	}

	/// <summary>
	/// Collects the aliases a type and the types nested in it declare as members.
	/// </summary>
	/// <param name="classDecl">The type.</param>
	/// <returns>The aliases, outermost type first and each type's in the order declared.</returns>
	/// <remarks>
	/// C# has a using alias only at file or namespace scope, never inside a type (CS1513), so these are
	/// written in the scope that encloses the type instead. The name then resolves everywhere the type
	/// used it, which is what declaring it as a member was for.
	/// </remarks>
	private static IEnumerable<UsingAlias> MemberAliases(ClassDeclaration classDecl) =>
		classDecl.Members.SelectMany(member => member switch
		{
			UsingAlias alias => [alias],
			ClassDeclaration nested => MemberAliases(nested),
			_ => [],
		});

	/// <summary>
	/// Writes aliases ahead of the declarations they were lifted out of.
	/// </summary>
	/// <param name="aliases">The aliases to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <returns>Whether anything was written.</returns>
	private bool WriteHoistedAliases(IEnumerable<UsingAlias> aliases, CodeBlocker code)
	{
		bool any = false;
		foreach (UsingAlias alias in aliases)
		{
			GenerateUsingAlias(alias, code);
			any = true;
		}

		return any;
	}

	/// <summary>
	/// Spells a declaration's type parameters, or nothing when it has none.
	/// </summary>
	/// <param name="parameters">The declaration's type parameters.</param>
	/// <returns>The list, angle brackets and all, or an empty string.</returns>
	/// <remarks>
	/// Names only. C# writes the requirements in a <c>where</c> clause of their own rather than
	/// beside the name, which is what <see cref="WriteConstraintClauses"/> is for.
	/// </remarks>
	private static string SpellTypeParameters(IEnumerable<TypeParameter> parameters)
	{
		string[] names = [.. parameters.Select(parameter => parameter.Name)];

		return names.Length == 0 ? string.Empty : $"<{string.Join(", ", names)}>";
	}

	/// <summary>
	/// Writes one <c>where</c> clause per constrained parameter, indented under the declaration.
	/// </summary>
	/// <param name="parameters">The declaration's type parameters.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The order within a clause is the language's rather than the declaration's, and putting it
	/// right is this generator's job: C# requires the class or struct constraint first and
	/// <c>new()</c> last, and rejects any other order. The AST has no reason to know that, and a
	/// caller listing them as they think of them should still get a file that compiles.
	/// </remarks>
	private static void WriteConstraintClauses(IEnumerable<TypeParameter> parameters, CodeBlocker code)
	{
		using IndentScope clauses = new(code);

		foreach (TypeParameter parameter in parameters.Where(parameter => parameter.Constraints.Count > 0))
		{
			IEnumerable<string> written = InDeclarationOrder(parameter.Constraints)
				.Select(constraint => constraint.ToString());

			code.WriteLine($"where {parameter.Name} : {string.Join(", ", written)}");
		}
	}

	/// <summary>
	/// Puts a parameter's constraints into the order C# accepts them in.
	/// </summary>
	/// <param name="constraints">The constraints as the declaration lists them.</param>
	/// <returns>The constraints, reordered.</returns>
	private static IEnumerable<TypeConstraint> InDeclarationOrder(IEnumerable<TypeConstraint> constraints) =>
		constraints.OrderBy(constraint => constraint.Kind switch
		{
			TypeConstraintKind.ValueType or TypeConstraintKind.ReferenceType => 0,
			TypeConstraintKind.Constructible => 2,
			_ => 1,
		});

	/// <inheritdoc/>
	/// <remarks>
	/// Square brackets, and one attribute per line rather than several in one pair of them: a
	/// declaration with four of them reads down the page, and a diff that adds one touches one line.
	/// </remarks>
	protected override string? SpellAnnotation(Annotation annotation, PreambleSite site) => $"[{annotation}]";

	/// <inheritdoc/>
	protected override string? SpellImport(string import) => $"using {import};";

	/// <inheritdoc/>
	/// <remarks>
	/// The constants on <see cref="double"/>, which every file can name without a <c>using</c>.
	/// </remarks>
	protected override string SpellNonFiniteDouble(double value) =>
		double.IsNaN(value) ? "double.NaN" : value > 0 ? "double.PositiveInfinity" : "double.NegativeInfinity";

	/// <inheritdoc/>
	/// <remarks>
	/// The constants on <see cref="float"/>, because C# does not narrow a <c>double</c> to a
	/// <c>float</c> implicitly.
	/// </remarks>
	protected override string SpellNonFiniteSingle(float value) =>
		float.IsNaN(value) ? "float.NaN" : value > 0 ? "float.PositiveInfinity" : "float.NegativeInfinity";

	/// <summary>
	/// Emits a namespace and its members.
	/// </summary>
	/// <param name="namespaceDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Braced rather than file-scoped. A file-scoped namespace has to be the only one in its file and
	/// cannot contain another, and the AST puts no such restriction on where a namespace may appear —
	/// so the braced form is the one that is always correct for whatever it is handed.
	/// </remarks>
	private void GenerateNamespace(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		WritePreamble(namespaceDecl, code, PreambleSite.Type);

		IReadOnlyList<string> segments = NamespaceDeclaration.Split(namespaceDecl.Name);
		code.WriteLine($"namespace {string.Join(".", segments)}");

		using Scope members = new(code);
		GenerateScopeMembers(namespaceDecl.Members, segments.Count > 0 ? segments[^1] : GlobalsClassName, code);
	}

	/// <inheritdoc/>
	protected override void GenerateFileMembers(SourceFile file, CodeBlocker code)
	{
		Ensure.NotNull(file);
		Ensure.NotNull(code);
		GenerateScopeMembers(file.Members, GlobalsClassName, code);
	}

	/// <summary>
	/// The name of the class that holds a file's fields and functions.
	/// </summary>
	private const string GlobalsClassName = "Globals";

	/// <summary>
	/// Emits the members of a namespace or a file, gathering the ones C# cannot declare there into a
	/// static class.
	/// </summary>
	/// <param name="members">The members, in the order declared.</param>
	/// <param name="holderName">What to call the class, unless a type beside it already has the name.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// C# has no field, function or variable outside a type: one in a namespace fails with CS0116,
	/// and one at file scope with CS0106. Every other target has somewhere to put them, so the AST
	/// allows them, and a static class is what C# has in their place — a reader names
	/// <c>Geometry.twice</c> where C++ names <c>Geometry::twice</c>. The class is written where the
	/// first of them was declared, and types, enums and nested namespaces stay where they are. Aliases
	/// come first, those of the scope's types with them, because C# takes them nowhere else.
	/// </remarks>
	private void GenerateScopeMembers(IEnumerable<AstNode> members, string holderName, CodeBlocker code)
	{
		AstNode[] declared = [.. members];
		AstNode[] typeless = [.. declared.Where(IsTypelessMember)];
		bool holderWritten = false;

		// Every alias the scope holds goes first, its own and those lifted out of its types: C# takes
		// a using alias only ahead of everything else declared in a namespace or file (CS1529).
		IEnumerable<UsingAlias> aliases = declared.SelectMany(member => member switch
		{
			UsingAlias alias => [alias],
			ClassDeclaration type => MemberAliases(type),
			_ => [],
		});

		bool first = !WriteHoistedAliases(aliases, code);

		bool aliasesWereHoisted = memberAliasesHoisted;
		memberAliasesHoisted = true;
		try
		{
			foreach (AstNode member in declared)
			{
				bool isTypeless = IsTypelessMember(member);
				if ((isTypeless && holderWritten) || member is UsingAlias)
				{
					continue;
				}

				if (!first)
				{
					code.NewLine();
				}

				first = false;

				if (isTypeless)
				{
					holderWritten = true;
					GenerateMemberHolder(UnclaimedName(holderName, declared), typeless, code);
				}
				else
				{
					GenerateInternal(member, code);
				}
			}
		}
		finally
		{
			memberAliasesHoisted = aliasesWereHoisted;
		}
	}

	/// <summary>
	/// Emits the static class that holds a scope's fields, functions and variables.
	/// </summary>
	/// <param name="name">The class's name.</param>
	/// <param name="members">The members it holds, in the order declared.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateMemberHolder(string name, IEnumerable<AstNode> members, CodeBlocker code)
	{
		code.WriteLine($"{DefaultVisibility} static class {name}");

		using Scope body = new(code);
		bool first = true;
		foreach (AstNode member in members)
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;

			// A static class may hold only static members (CS0708), and the AST's member is static in
			// every sense that matters already: there is no instance of a namespace for it to belong to.
			heldMember = member;
			try
			{
				GenerateInternal(member, code);
			}
			finally
			{
				heldMember = null;
			}
		}
	}

	/// <summary>
	/// Determines whether a member of a namespace or file is one C# can declare only inside a type.
	/// </summary>
	/// <param name="member">The member.</param>
	/// <returns>True for a field, a function or a variable.</returns>
	private static bool IsTypelessMember(AstNode member) =>
		member is FieldDeclaration or FunctionDeclaration or VariableDeclaration;

	/// <summary>
	/// Picks a name for the class holding a scope's members that no type beside it already has.
	/// </summary>
	/// <param name="name">The name wanted.</param>
	/// <param name="siblings">Everything else declared in the scope.</param>
	/// <returns>The name, or the name with a suffix when a sibling has it (CS0101).</returns>
	private static string UnclaimedName(string name, IEnumerable<AstNode> siblings)
	{
		bool claimed = siblings.Any(sibling => sibling switch
		{
			ClassDeclaration type => string.Equals(type.Name, name, StringComparison.Ordinal),
			EnumDeclaration type => string.Equals(type.Name, name, StringComparison.Ordinal),
			UsingAlias alias => string.Equals(alias.Name, name, StringComparison.Ordinal),
			_ => false,
		});

		return claimed ? $"{name}{GlobalsClassName}" : name;
	}

	/// <summary>
	/// Emits an alias giving a type a second name.
	/// </summary>
	/// <param name="usingAlias">The alias to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// C# has a using alias but only at file or namespace scope, never inside a type. One declared as
	/// a member is lifted to the scope enclosing its type rather than written where it stands; see
	/// <see cref="MemberAliases"/>.
	/// </remarks>
	private void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		WritePreamble(usingAlias, code, PreambleSite.Type);
		code.WriteLine($"using {usingAlias.Name} = {MapToCSType(usingAlias.AliasedType ?? new TypeReference(UnknownTypeName))};");
	}

	/// <summary>
	/// Emits an expression that builds a value.
	/// </summary>
	/// <param name="construction">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Three shapes, decided by what the expression holds rather than by a flag. Arguments that name
	/// the member they are for become an object initialiser — <c>new T { Name = value }</c> — which
	/// is what C# has in place of a designated initialiser, and it is written in the order given
	/// because C# does not care about declaration order the way C++ does. A construction with no
	/// type at all is the braced list on its own, which C# accepts where the declaration has
	/// already named an array type. Everything else is a constructor call.
	/// </remarks>
	private void GenerateConstruction(ConstructionExpression construction, CodeBlocker code)
	{
		bool designated = construction.Arguments.Any(argument => argument is MemberInitialiser);

		if (construction.Type is null)
		{
			WriteBracedList(construction, code);
			return;
		}

		code.Write($"new {MapToCSType(construction.Type)}");

		if (designated)
		{
			code.Write(" ");
			WriteBracedList(construction, code);
			return;
		}

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

	/// <summary>
	/// Emits a construction's arguments as a braced list.
	/// </summary>
	/// <param name="construction">The expression whose arguments to write.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A list whose elements are themselves lists is a table, and a table on one line is a diff
	/// nobody can read, so it goes one element per line with a trailing comma. A list of plain
	/// values is a value and stays where it is.
	/// </remarks>
	private void WriteBracedList(ConstructionExpression construction, CodeBlocker code)
	{
		IReadOnlyList<AstNode> elements = WrittenElements(construction);

		if (elements.Count == 0)
		{
			code.Write("{ }");
			return;
		}

		bool stacked = construction.Arguments.Any(argument =>
			argument is ConstructionExpression or MemberInitialiser { Value: ConstructionExpression });

		if (stacked)
		{
			code.WriteLine("{");
			code.Indent();

			foreach (AstNode argument in elements)
			{
				WriteListElement(argument, code);
				code.WriteLine(",");
			}

			code.Outdent();
			code.Write("}");
			return;
		}

		code.Write("{ ");

		for (int index = 0; index < elements.Count; index++)
		{
			if (index > 0)
			{
				code.Write(", ");
			}

			WriteListElement(elements[index], code);
		}

		code.Write(" }");
	}

	/// <summary>
	/// Emits one element of a braced list, which may name the member it is for.
	/// </summary>
	/// <param name="argument">The element to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteListElement(AstNode argument, CodeBlocker code)
	{
		if (argument is MemberInitialiser designated)
		{
			code.Write($"{designated.Name} = ");
			GenerateInternal(designated.Value!, code);
			return;
		}

		GenerateInternal(argument, code);
	}

	/// <summary>
	/// Emits an enumeration and its members.
	/// </summary>
	/// <param name="enumDecl">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The underlying type is written only when the declaration names one, because C#'s default is
	/// <c>int</c> and saying so adds nothing. A member with no value of its own is left unnumbered,
	/// which is how C# spells "take it from the position" and keeps the declaration readable.
	/// </remarks>
	private void GenerateEnum(EnumDeclaration enumDecl, CodeBlocker code)
	{
		WritePreamble(enumDecl, code, PreambleSite.Enum);

		code.Write($"{SpellVisibility(enumDecl.Visibility) ?? DefaultVisibility} enum {enumDecl.Name ?? "UnnamedEnum"}");

		if (enumDecl.UnderlyingType is TypeReference underlying)
		{
			code.Write($" : {MapToCSType(underlying)}");
		}

		code.WriteLine();

		using Scope members = new(code);
		foreach (EnumMember member in enumDecl.Members)
		{
			code.Write(member.Name ?? "Unnamed");

			if (member.Value is not null)
			{
				code.Write($" = {member.Value}");
			}

			code.WriteLine(",");
		}
	}

	/// <summary>
	/// Emits a property, which C# has a word for.
	/// </summary>
	/// <param name="property">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// The accessors go on one line when the language supplies them and in a block when they have
	/// bodies, which is how a person writes the two and how every C# formatter will put them back
	/// if a generator chooses otherwise.
	/// </remarks>
	private void GenerateProperty(PropertyDeclaration property, CodeBlocker code)
	{
		WritePreamble(property, code, PreambleSite.Property);

		string type = property.Type is TypeReference declared ? MapToCSType(declared) : "object";
		string modifiers = property.IsStatic ? " static" : string.Empty;

		code.Write($"{SpellVisibility(property.Visibility) ?? DefaultVisibility}{modifiers} {type} {property.Name ?? "Value"}");

		// init rather than set where the declaration asked for it: the two differ only in when the
		// call is legal, and nothing else here has a word for the difference. A readonly struct asks
		// for it too, for an instance property: nothing may modify the instance after it is made.
		bool initOnlyForStruct = inReadOnlyStruct && !property.IsStatic && !property.SetterIsInitOnly && property.CanWrite;
		string setter = property.SetterIsInitOnly || initOnlyForStruct ? "init" : "set";

		if (property.IsAutomatic)
		{
			string accessors = property.CanWrite ? $"get; {setter};" : "get;";
			code.WriteLine($" {{ {accessors} }}");
			return;
		}

		code.WriteLine();

		using Scope accessorBlock = new(code);

		if (property.CanRead)
		{
			code.Write("get");
			WriteAccessorBody(property.GetterBody, code);
		}

		if (property.CanWrite)
		{
			if (initOnlyForStruct)
			{
				WriteInexpressible(code, "readonly: set only while the instance is initialised");
			}

			code.Write(setter);
			WriteAccessorBody(property.SetterBody, code);
		}
	}

	/// <summary>
	/// Writes an accessor's statements, in a brace scope.
	/// </summary>
	/// <param name="body">The statements to write.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteAccessorBody(Collection<AstNode> body, CodeBlocker code)
	{
		code.WriteLine();

		using Scope statements = new(code);

		foreach (AstNode statement in body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <summary>
	/// Emits a field of a type.
	/// </summary>
	/// <param name="field">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Public unless the declaration says otherwise, matching what every other declaration here does
	/// with an unset visibility. C#'s own default for a field is private, which is not what someone
	/// who wrote no modifier on a generated type meant.
	/// </remarks>
	private void GenerateField(FieldDeclaration field, CodeBlocker code)
	{
		WritePreamble(field, code, PreambleSite.Field);

		code.Write($"{SpellVisibility(field.Visibility) ?? DefaultVisibility} ");

		// static readonly rather than const: const is legal only for the primitives and strings,
		// and a generated table is neither. The two mean the same thing to a reader and only one of
		// them is always available.
		if (field.IsConstant)
		{
			code.Write("static readonly ");
		}
		else if (field.IsStatic || ReferenceEquals(field, heldMember))
		{
			code.Write("static ");
		}
		else if (inReadOnlyStruct)
		{
			code.Write("readonly ");
		}

		code.Write($"{MapToCSType(field.Type ?? new TypeReference(UnknownTypeName))} {field.Name}");

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Emits a function.
	/// </summary>
	/// <param name="function">The function to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A pure function carries <c>[Pure]</c>, written out in full because the AST has no import to
	/// hang a <c>using</c> on: a generated file is a fragment, and a short name in it would be one
	/// the reader has to arrange for.
	/// </remarks>
	private void GenerateFunction(FunctionDeclaration function, CodeBlocker code) =>
		GenerateFunction(function, code, null);

	/// <summary>
	/// Emits a function, which may be a member of a type.
	/// </summary>
	/// <param name="function">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="enclosingType">The type it belongs to, when it belongs to one.</param>
	/// <remarks>
	/// C# expresses a defaulted or deleted member by not declaring it: the compiler supplies the one
	/// and the absence of the other is what makes a call fail to compile. So neither is emitted, and
	/// a note says which member went and why — a generated file that silently drops one looks
	/// complete and is not.
	/// <para>
	/// A defaulted constructor is the exception, because a type that declares any other constructor
	/// stops getting one for free. It is written with an empty body, which is exactly what
	/// <c>= default</c> means there.
	/// </para>
	/// </remarks>
	private void GenerateFunction(FunctionDeclaration function, CodeBlocker code, ClassDeclaration? enclosingType)
	{
		WritePreamble(function, code, enclosingType is null ? PreambleSite.Function : PreambleSite.Method);

		string name = SpellFunctionName(function, enclosingType?.Name);

		if (function.Definition != FunctionDefinition.Provided
			&& !(function.Kind == FunctionKind.Constructor && function.Definition == FunctionDefinition.Defaulted))
		{
			string state = function.Definition == FunctionDefinition.Defaulted ? "supplied by the language" : "deleted";
			WriteInexpressible(code, $"{name} is {state}, which C# expresses by not declaring it.");
			return;
		}

		if (!WriteMemberNotes(function, name, enclosingType, code))
		{
			return;
		}

		WriteFunctionAttributes(function, code);
		WriteFunctionModifiers(function, code, enclosingType);

		code.Write(name);
		code.Write(SpellTypeParameters(function.TypeParameters));
		code.Write("(");

		bool takesOperand = TakesImplicitOperand(function, enclosingType);
		if (takesOperand)
		{
			code.Write($"{enclosingType!.Name}{SpellTypeParameters(enclosingType.TypeParameters)} {OperandParameterName}");
		}

		for (int i = 0; i < function.Parameters.Count; i++)
		{
			if (i > 0 || takesOperand)
			{
				code.Write(", ");
			}

			GenerateParameter(function.Parameters[i], code);
		}

		code.Write(")");

		if (function.IsAbstract)
		{
			code.WriteLine(";");
			WriteConstraintClauses(function.TypeParameters, code);
			return;
		}

		// The line is ended before the scope opens, so C#'s brace lands on its own line.
		code.WriteLine();
		WriteConstraintClauses(function.TypeParameters, code);

		using Scope body = new(code);
		WriteInitialisers(function, code);

		foreach (AstNode statement in function.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <summary>
	/// Writes a constructor's member initialisers as the assignments that open its body.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	private void WriteInitialisers(FunctionDeclaration function, CodeBlocker code)
	{
		// C# assigns where C++ initialises. Written before the body's own statements and in the order
		// declared, which is what the initialiser means where there is no initialiser list. One with no
		// value value-initialises the member, as C++'s `x()` does, and `default` is C#'s spelling.
		foreach (MemberInitialiser initialiser in function.Initialisers)
		{
			code.Write($"this.{initialiser.Name} = ");

			if (initialiser.Value is not null)
			{
				GenerateInternal(initialiser.Value, code);
			}
			else
			{
				code.Write("default");
			}

			EndStatement(code);
		}
	}

	/// <summary>
	/// Writes down what a member asks for that C# cannot say where the member is.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <param name="name">The name it is written under.</param>
	/// <param name="enclosingType">The type it belongs to, when it belongs to one.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <returns>False when the note stands in for the whole declaration, so nothing else is written.</returns>
	private bool WriteMemberNotes(FunctionDeclaration function, string name, ClassDeclaration? enclosingType, CodeBlocker code)
	{
		// A struct cannot have a finalizer (CS0575), so the destructor is written down rather than
		// declared, the way a member C# expresses by not declaring it is.
		if (function.Kind == FunctionKind.Destructor && enclosingType?.Kind == TypeDeclarationKind.Struct)
		{
			WriteInexpressible(code, $"{name} is a destructor, which a C# struct cannot declare.");
			return false;
		}

		// readonly is a modifier only an instance member of a struct may carry (CS0106). Anywhere else
		// the member still makes the claim, so it is written down the way the type's own claim is.
		if (function.IsReadOnly && !function.IsStatic && !IsReadOnlySpelled(function, enclosingType))
		{
			WriteInexpressible(code, $"readonly: {name} does not modify its instance");
		}

		// C++ lets a member operator be private, protected or virtual; a C# operator is public and
		// static and nothing else, so what it asked for is written down rather than lost.
		if (IsOperator(function))
		{
			if (function.Visibility is not (Visibility.Unspecified or Visibility.Public))
			{
				WriteInexpressible(code, $"{SpellVisibility(function.Visibility)}: {name} is public, as every C# operator is");
			}

			if (enclosingType?.Kind != TypeDeclarationKind.Interface && (function.IsVirtual || function.IsAbstract))
			{
				string modifier = function.IsAbstract ? "abstract" : "virtual";
				WriteInexpressible(code, $"{modifier}: {name} is static, which a C# operator outside an interface cannot override");
			}
		}

		return true;
	}

	/// <summary>
	/// Gets whether a declaration is one C# requires to be <c>public static</c>.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <returns>True for an operator or a conversion operator.</returns>
	private static bool IsOperator(FunctionDeclaration function) =>
		function.Kind is FunctionKind.Operator or FunctionKind.ConversionOperator;

	/// <summary>
	/// Writes the attributes a declaration earns.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Purity already says the result must be used, so a declaration carrying both gets the one
	/// attribute that says the stronger thing rather than two saying overlapping ones.
	/// </remarks>
	private static void WriteFunctionAttributes(FunctionDeclaration function, CodeBlocker code)
	{
		if (function.IsPure)
		{
			code.WriteLine("[System.Diagnostics.Contracts.Pure]");
			return;
		}

		if (function.MustUseResult)
		{
			code.WriteLine("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Usage\", \"CA1806\", Justification = \"The result must be used.\")]");
		}
	}

	/// <summary>
	/// Writes the modifiers in front of a declaration, up to and including its return type.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <param name="enclosingType">The type it belongs to, when it belongs to one.</param>
	private void WriteFunctionModifiers(FunctionDeclaration function, CodeBlocker code, ClassDeclaration? enclosingType)
	{
		// A function nobody has given a visibility to is public: an inaccessible method is not what
		// someone who wrote no modifier meant. A finalizer is the exception, because it takes no
		// access modifier at all (CS0106): only the runtime calls it.
		// A user-defined operator must be public as well as static (CS0558), whatever it declared.
		if (IsOperator(function))
		{
			code.Write("public ");
		}
		else if (function.Kind != FunctionKind.Destructor)
		{
			code.Write($"{SpellVisibility(function.Visibility) ?? DefaultVisibility} ");
		}

		if (function.IsStatic
			|| ReferenceEquals(function, heldMember)
			|| IsOperator(function))
		{
			// A C# operator is always static, whether or not the declaration thought to say so.
			code.Write("static ");
		}

		// An operator outside an interface can be neither virtual nor abstract (CS0106); the note
		// written above the declaration says what was dropped.
		bool canOverride = !IsOperator(function) || enclosingType?.Kind == TypeDeclarationKind.Interface;

		// An interface member with no body is abstract already, and says so by having none.
		if (function.IsAbstract)
		{
			if (canOverride && enclosingType?.Kind != TypeDeclarationKind.Interface)
			{
				code.Write("abstract ");
			}
		}
		else if (function.IsVirtual && canOverride)
		{
			code.Write("virtual ");
		}

		if (IsReadOnlySpelled(function, enclosingType))
		{
			code.Write("readonly ");
		}

		// A constructor, a destructor and a conversion operator have no return type to write.
		if (function.Kind is FunctionKind.Method or FunctionKind.Operator)
		{
			code.Write($"{MapToCSType(function.ReturnType ?? new TypeReference("void"))} ");
		}
	}

	/// <summary>
	/// What the operand an operator acts on is called, once C# has to name it.
	/// </summary>
	/// <remarks>
	/// <c>self</c>, as the C lowering names its receiver, so a body that reads <c>self.x</c> means the
	/// same thing in both.
	/// </remarks>
	private const string OperandParameterName = "self";

	/// <summary>
	/// Determines whether a declaration's operand is one C# has to write as a parameter.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <param name="enclosingType">The type it belongs to, when it belongs to one.</param>
	/// <returns>True when it is an operator or a conversion that belongs to a type.</returns>
	/// <remarks>
	/// The AST's operators are members, as C++'s and Rust's are: <c>operator+(rhs)</c> is binary, and
	/// the instance it belongs to is the left-hand operand. A C# operator is static and takes every
	/// operand explicitly, so written as declared a binary operator would become a unary one and a
	/// unary operator or a conversion would take nothing at all (CS1019).
	/// </remarks>
	private static bool TakesImplicitOperand(FunctionDeclaration function, ClassDeclaration? enclosingType) =>
		enclosingType is not null && function.Kind is FunctionKind.Operator or FunctionKind.ConversionOperator;

	/// <summary>
	/// Determines whether a declaration's <see cref="FunctionDeclaration.IsReadOnly"/> is spelled as
	/// C#'s <c>readonly</c>.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <param name="enclosingType">The type it belongs to, when it belongs to one.</param>
	/// <returns>True when the declaration is an instance member of a struct that claims it.</returns>
	/// <remarks>
	/// The claim is C++'s trailing <c>const</c>, which any member function may make. C# lets only an
	/// instance member of a struct make it, so everywhere else <c>readonly</c> fails with CS0106. An
	/// operator is static in C# whether or not the declaration says so, so it never can.
	/// </remarks>
	private static bool IsReadOnlySpelled(FunctionDeclaration function, ClassDeclaration? enclosingType) =>
		function.IsReadOnly
		&& !function.IsStatic
		&& function.Kind is not (FunctionKind.Operator or FunctionKind.ConversionOperator)
		&& enclosingType?.Kind == TypeDeclarationKind.Struct;

	/// <summary>
	/// Determines whether a class is written <c>abstract</c>.
	/// </summary>
	/// <param name="classDecl">The declaration being emitted.</param>
	/// <returns>True when it is a class with a member that has no definition.</returns>
	/// <remarks>
	/// The AST says abstract of a member, as C++ does, and a C++ class with a pure virtual member is
	/// abstract without saying so. C# requires the class to say it (CS0513).
	/// </remarks>
	private static bool IsAbstractClass(ClassDeclaration classDecl) =>
		classDecl.Kind == TypeDeclarationKind.Class
		&& classDecl.Members.OfType<FunctionDeclaration>().Any(member => member.IsAbstract);

	/// <summary>
	/// Spells the name a declaration is written under.
	/// </summary>
	/// <param name="function">The declaration being emitted.</param>
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
	/// <returns>The name as C# writes it.</returns>
	private static string SpellFunctionName(FunctionDeclaration function, string? enclosingType)
	{
		string typeName = enclosingType ?? function.Name ?? "UnnamedType";

		return function.Kind switch
		{
			FunctionKind.Constructor => typeName,
			FunctionKind.Destructor => $"~{typeName}",
			FunctionKind.Operator => $"operator {function.Name}",
			FunctionKind.ConversionOperator =>
				$"{(function.IsExplicit ? "explicit" : "implicit")} operator {MapToCSType(function.ReturnType ?? new TypeReference(UnknownTypeName))}",
			_ => function.Name ?? "UnnamedFunction",
		};
	}

	private static void GenerateParameter(Parameter parameter, CodeBlocker code)
	{
		code.Write($"{MapToCSType(parameter.Type ?? new TypeReference(UnknownTypeName))} {parameter.Name}");

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" = {parameter.DefaultValue}");
		}
	}

	/// <summary>
	/// What a declaration nobody gave a visibility to gets.
	/// </summary>
	/// <remarks>
	/// C#'s own default is private for a member and internal for a type. Neither is what someone who
	/// wrote no modifier on a generated declaration meant: an inaccessible one is not a declaration
	/// anybody asked for.
	/// </remarks>
	private const string DefaultVisibility = "public";

	/// <summary>
	/// Whether the members being written belong directly to a <c>readonly struct</c>.
	/// </summary>
	/// <remarks>
	/// C# requires every instance field and automatic property of a readonly struct to be readonly
	/// itself (CS8340, CS8341), so a member's spelling depends on the type it stands in. Set for the
	/// members of one type at a time and restored afterwards, so a nested type answers for itself.
	/// </remarks>
	private bool inReadOnlyStruct;

	/// <summary>
	/// Whether the aliases declared in the types being written have already been written in front of
	/// them, by the scope that encloses those types or by an enclosing type.
	/// </summary>
	/// <remarks>
	/// A type generated on its own has no enclosing scope to lift its aliases into, so it writes them
	/// in front of itself; one inside a namespace, a file or another type leaves them to that.
	/// </remarks>
	private bool memberAliasesHoisted;

	/// <summary>
	/// The member of a namespace or file being written into the static class that holds it, which
	/// is written static whether or not it says so.
	/// </summary>
	/// <remarks>
	/// The member itself rather than a flag, because a function's body declares locals and a flag
	/// would make them static too.
	/// </remarks>
	private AstNode? heldMember;

	/// <summary>
	/// What a declaration that never said what type it is gets.
	/// </summary>
	/// <remarks>
	/// A type is optional on every node that carries one, because a half-built AST is a thing the
	/// editor has to be able to hold. Emitting the most general type there keeps the output compiling
	/// while making it obvious which declaration was never finished.
	/// </remarks>
	private const string UnknownTypeName = "object";

	private static readonly Dictionary<string, string> TypeMappings = new()
	{
		{ "str", "string" },
		{ "int", "int" },
		{ "float", "float" },
		{ "double", "double" },
		{ "bool", "bool" },
		{ "list", "List" },
		{ "dict", "Dictionary" },
		{ "void", "void" }
	};

	/// <summary>
	/// What a container named without arguments is a container of.
	/// </summary>
	/// <remarks>
	/// Keyed by the name as written rather than by the mapped one, because that is what the schema
	/// said. A <c>list&lt;int&gt;</c> is a <c>List&lt;int&gt;</c> and never reaches here.
	/// </remarks>
	private static readonly Dictionary<string, string> DefaultTypeArguments = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "list", "<object>" },
		{ "dict", "<string, object>" }
	};

	/// <summary>
	/// Spells a type in C#.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The C# source for it.</returns>
	/// <remarks>
	/// C# has no <c>const</c> on a type and no pointer outside unsafe code, so
	/// <see cref="TypeReference.IsReadOnly"/> and <see cref="TypeReference.Indirection"/> have no
	/// spelling here. Read-only-ness of an argument is <c>in</c>, which belongs on the parameter
	/// rather than on the type, and is not something the AST can say yet.
	/// </remarks>
	private static string MapToCSType(TypeReference type)
	{
		string name = MapTypeName(type.Name);

		string array = type.IsArray ? "[]" : string.Empty;

		if (type.TypeArguments.Count > 0)
		{
			return $"{name}<{string.Join(", ", type.TypeArguments.Select(MapToCSType))}>{array}";
		}

		return DefaultTypeArguments.TryGetValue(type.Name, out string? fallback)
			? $"{name}{fallback}{array}"
			: $"{name}{array}";
	}

	/// <summary>
	/// Spells a type's name in C#, leaving any arguments to the caller.
	/// </summary>
	/// <param name="name">The name as the AST holds it.</param>
	/// <returns>The C# name.</returns>
	/// <remarks>
	/// <c>void</c> is matched case-insensitively where the rest of the table is not, because it is
	/// the one name a caller reaches for without knowing which language's casing the AST was written
	/// in — a return type left unset is spelled <c>void</c> by the generator itself.
	/// </remarks>
	private static string MapTypeName(string name)
	{
		if (TypeMappings.TryGetValue(name, out string? mapped))
		{
			return mapped;
		}

		return string.Equals(name, "void", StringComparison.OrdinalIgnoreCase) ? "void" : name;
	}

	private void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		// Use type or var for type inference
		string type = varDecl.IsTypeInferred || varDecl.Type is null
			? "var"
			: MapToCSType(varDecl.Type);

		// A local has no access modifier and a field usually does, and the AST distinguishes the two
		// by whether one was set: emitting it only when present keeps both correct. A variable of a
		// namespace or file is a field of the class holding it, and as visible as the scope it was in.
		bool isHeld = ReferenceEquals(varDecl, heldMember);
		if ((isHeld ? SpellVisibility(varDecl.Visibility) ?? DefaultVisibility : SpellVisibility(varDecl.Visibility)) is string modifier)
		{
			code.Write($"{modifier} ");
		}

		// A C# constant must name its type and hold a compile-time value, so an inferred one, or one
		// computed from a parameter or a call, stays a plain declaration rather than becoming source
		// that does not compile. C# has no readonly local to say the rest with.
		bool isConst = varDecl.IsConstant
			&& !string.Equals(type, "var", StringComparison.Ordinal)
			&& IsCompileTimeValue(varDecl.InitialValue);
		if (isConst)
		{
			code.Write("const ");
		}
		else if (isHeld)
		{
			// A const is static already, and saying so as well is CS0504.
			code.Write("static ");
		}

		code.Write($"{type} {varDecl.Name}");

		if (varDecl.InitialValue != null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}

		code.WriteLine(";");
	}

	/// <summary>
	/// Emits the program's entry point as C#'s <c>Main</c> method.
	/// </summary>
	/// <param name="entryPoint">The entry point to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// Always <c>static</c>, and named with the capital C# gives it. The method is emitted on its own
	/// rather than wrapped in a class, because a C# entry point is an ordinary member of whatever
	/// class the document puts it in — including one this generator emits around it.
	/// </remarks>
	private void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		code.Write($"public static {(entryPoint.ReturnsExitCode ? "int" : "void")} Main(");

		if (entryPoint.AcceptsArguments)
		{
			code.Write("string[] args");
		}

		// The line is ended before the scope opens, so C#'s brace lands on its own line.
		code.WriteLine(")");

		using Scope body = new(code);
		foreach (AstNode statement in entryPoint.Body)
		{
			GenerateInternal(statement, code);
		}
	}
}
