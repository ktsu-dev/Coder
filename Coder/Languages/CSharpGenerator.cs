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
		code.Write($"{SpellVisibility(classDecl.Visibility) ?? "public"} class {classDecl.Name ?? "UnnamedClass"}");

		if (classDecl.BaseType is TypeReference baseType)
		{
			code.Write($" : {MapToCSType(baseType)}");
		}

		// The line is ended before the scope opens, so C#'s brace lands on its own line.
		code.WriteLine();

		using Scope members = new(code);
		foreach (AstNode member in classDecl.Members)
		{
			GenerateInternal(member, code);
		}
	}

	private void GenerateFunction(FunctionDeclaration function, CodeBlocker code)
	{
		// Build method signature. A function nobody has given a visibility to is public: an
		// inaccessible method is not what someone who wrote no modifier meant.
		code.Write($"{SpellVisibility(function.Visibility) ?? "public"} {MapToCSType(function.ReturnType ?? new TypeReference("void"))} {function.Name}(");

		// Add parameters
		for (int i = 0; i < function.Parameters.Count; i++)
		{
			if (i > 0)
			{
				code.Write(", ");
			}

			GenerateParameter(function.Parameters[i], code);
		}

		// The line is ended before the scope opens, so C#'s brace lands on its own line.
		code.WriteLine(")");

		// Add body
		using Scope body = new(code);
		foreach (AstNode statement in function.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	private static void GenerateParameter(Parameter parameter, CodeBlocker code)
	{
		code.Write($"{MapToCSType(parameter.Type ?? new TypeReference("object"))} {parameter.Name}");

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" = {parameter.DefaultValue}");
		}
	}

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
		string name = TypeMappings.TryGetValue(type.Name, out string? mapped)
			? mapped
			: string.Equals(type.Name, "void", StringComparison.OrdinalIgnoreCase) ? "void" : type.Name;

		return type.TypeArguments.Count > 0
			? $"{name}<{string.Join(", ", type.TypeArguments.Select(MapToCSType))}>"
			: DefaultTypeArguments.TryGetValue(type.Name, out string? fallback) ? $"{name}{fallback}" : name;
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
