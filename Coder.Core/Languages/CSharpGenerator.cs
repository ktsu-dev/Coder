// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Collections.Generic;
using System.Globalization;
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
			case FunctionDeclaration function:
				GenerateFunction(function, code);
				break;
			case Parameter parameter:
				GenerateParameter(parameter, code);
				break;
			case ReturnStatement returnStmt:
				GenerateStringifiedReturnStatement(returnStmt, code);
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

	private void GenerateFunction(FunctionDeclaration function, CodeBlocker code)
	{
		// Build method signature
		code.Write($"public {MapToCSType(function.ReturnType ?? "void")} {function.Name}(");

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
		code.Write($"{MapToCSType(parameter.Type ?? "object")} {parameter.Name}");

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			code.Write($" = {parameter.DefaultValue}");
		}
	}

	/// <summary>
	/// Emits a return statement, stringifying its expression rather than recursing into it.
	/// </summary>
	/// <param name="returnStmt">The statement to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// This is not what <see cref="LanguageGeneratorBase.GenerateReturnStatement"/> does, and the
	/// difference is a defect rather than a dialect: an expression with no <c>ToString</c> override
	/// emits its type name, and a string literal loses its quotes. It is preserved here so that
	/// adopting <c>CodeBlocker</c> changes no generated output; the distinct name keeps it from
	/// silently hiding the base member. Correcting it belongs in its own change.
	/// </remarks>
	private static void GenerateStringifiedReturnStatement(ReturnStatement returnStmt, CodeBlocker code)
	{
		code.Write("return");

		if (returnStmt.Expression != null)
		{
			code.Write($" {returnStmt.Expression}");
		}

		code.WriteLine(";");
	}

	private static readonly Dictionary<string, string> TypeMappings = new()
	{
		{ "str", "string" },
		{ "int", "int" },
		{ "float", "float" },
		{ "double", "double" },
		{ "bool", "bool" },
		{ "list", "List<object>" },
		{ "dict", "Dictionary<string, object>" },
		{ "void", "void" }
	};

	private static string MapToCSType(string pythonType) =>
		TypeMappings.TryGetValue(pythonType, out string? mapped)
			? mapped
			: string.Equals(pythonType, "void", StringComparison.OrdinalIgnoreCase) ? "void" : pythonType;

	private void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		// Use type or var for type inference
		string type = varDecl.IsTypeInferred || string.IsNullOrEmpty(varDecl.Type)
			? "var"
			: MapToCSType(varDecl.Type);

		code.Write($"{type} {varDecl.Name}");

		if (varDecl.InitialValue != null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}

		code.WriteLine(";");
	}
}
