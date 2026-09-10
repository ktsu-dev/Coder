// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Collections.Generic;
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
				code.Write($"{doubleLit.Value.ToString(CultureInfo.InvariantCulture)}d");
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
					code.WriteLine($"// Unsupported node type: {node.GetType().Name}");
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
		GenerateDocumentation(classDecl, code);

		string keyword = classDecl.Kind switch
		{
			TypeDeclarationKind.Struct => "struct",
			TypeDeclarationKind.Interface => "interface",
			_ => "class",
		};

		code.Write($"{SpellVisibility(classDecl.Visibility) ?? DefaultVisibility} {keyword} {classDecl.Name ?? "UnnamedClass"}");

		if (classDecl.BaseType is TypeReference baseType)
		{
			code.Write($" : {MapToCSType(baseType)}");
		}

		// The line is ended before the scope opens, so C#'s brace lands on its own line.
		code.WriteLine();

		using Scope members = new(code);
		foreach (AstNode member in classDecl.Members)
		{
			if (member is FunctionDeclaration method)
			{
				GenerateFunction(method, code, classDecl.Name);
			}
			else
			{
				GenerateInternal(member, code);
			}
		}
	}

	/// <inheritdoc/>
	protected override string? SpellImport(string import) => $"using {import};";

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
		GenerateDocumentation(namespaceDecl, code);

		code.WriteLine($"namespace {string.Join(".", NamespaceDeclaration.Split(namespaceDecl.Name))}");

		using Scope members = new(code);
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

	/// <summary>
	/// Emits an alias giving a type a second name.
	/// </summary>
	/// <param name="usingAlias">The alias to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// C# has a using alias but only at file or namespace scope, never inside a type. One declared as
	/// a member is therefore said rather than written, since a generated file that silently drops it
	/// looks complete and is not.
	/// </remarks>
	private void GenerateUsingAlias(UsingAlias usingAlias, CodeBlocker code)
	{
		GenerateDocumentation(usingAlias, code);
		code.WriteLine($"using {usingAlias.Name} = {MapToCSType(usingAlias.AliasedType ?? new TypeReference(UnknownTypeName))};");
	}

	/// <summary>
	/// Emits an expression that builds a value.
	/// </summary>
	/// <param name="construction">The expression to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	private void GenerateConstruction(ConstructionExpression construction, CodeBlocker code)
	{
		code.Write($"new {MapToCSType(construction.Type ?? new TypeReference(UnknownTypeName))}(");

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
		GenerateDocumentation(enumDecl, code);

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
		GenerateDocumentation(field, code);

		code.Write($"{SpellVisibility(field.Visibility) ?? DefaultVisibility} ");
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
	/// <param name="enclosingType">The name of the type it belongs to, when it belongs to one.</param>
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
	private void GenerateFunction(FunctionDeclaration function, CodeBlocker code, string? enclosingType)
	{
		GenerateDocumentation(function, code);

		if (function.Definition != FunctionDefinition.Provided
			&& !(function.Kind == FunctionKind.Constructor && function.Definition == FunctionDefinition.Defaulted))
		{
			string state = function.Definition == FunctionDefinition.Defaulted ? "supplied by the language" : "deleted";
			WriteInexpressible(code, $"{SpellFunctionName(function, enclosingType)} is {state}, which C# expresses by not declaring it.");
			return;
		}

		WriteFunctionAttributes(function, code);
		WriteFunctionModifiers(function, code);

		code.Write(SpellFunctionName(function, enclosingType));
		code.Write("(");

		for (int i = 0; i < function.Parameters.Count; i++)
		{
			if (i > 0)
			{
				code.Write(", ");
			}

			GenerateParameter(function.Parameters[i], code);
		}

		code.Write(")");

		if (function.IsAbstract)
		{
			code.WriteLine(";");
			return;
		}

		// The line is ended before the scope opens, so C#'s brace lands on its own line.
		code.WriteLine();

		using Scope body = new(code);

		// C# assigns where C++ initialises. Written before the body's own statements and in the order
		// declared, which is what the initialiser means where there is no initialiser list.
		foreach (MemberInitialiser initialiser in function.Initialisers)
		{
			code.Write($"this.{initialiser.Name} = ");

			if (initialiser.Value is not null)
			{
				GenerateInternal(initialiser.Value, code);
			}

			EndStatement(code);
		}

		foreach (AstNode statement in function.Body)
		{
			GenerateInternal(statement, code);
		}
	}

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
	private static void WriteFunctionModifiers(FunctionDeclaration function, CodeBlocker code)
	{
		// A function nobody has given a visibility to is public: an inaccessible method is not what
		// someone who wrote no modifier meant.
		code.Write($"{SpellVisibility(function.Visibility) ?? DefaultVisibility} ");

		if (function.IsStatic || function.Kind is FunctionKind.Operator or FunctionKind.ConversionOperator)
		{
			// A C# operator is always static, whether or not the declaration thought to say so.
			code.Write("static ");
		}

		if (function.IsAbstract)
		{
			code.Write("abstract ");
		}
		else if (function.IsVirtual)
		{
			code.Write("virtual ");
		}

		if (function.IsReadOnly)
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

		if (type.TypeArguments.Count > 0)
		{
			return $"{name}<{string.Join(", ", type.TypeArguments.Select(MapToCSType))}>";
		}

		return DefaultTypeArguments.TryGetValue(type.Name, out string? fallback) ? $"{name}{fallback}" : name;
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
		// by whether one was set: emitting it only when present keeps both correct.
		if (SpellVisibility(varDecl.Visibility) is string modifier)
		{
			code.Write($"{modifier} ");
		}

		// A C# constant must name its type, so an inferred one stays a plain declaration rather than
		// becoming source that does not compile.
		if (varDecl.IsConstant && !string.Equals(type, "var", StringComparison.Ordinal))
		{
			code.Write("const ");
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
