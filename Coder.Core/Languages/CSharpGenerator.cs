// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Languages;

using System.Collections.Generic;
using System.Text;
using ktsu.Coder.Ast;

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
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected override void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel)
	{
		ArgumentNullException.ThrowIfNull(node);
		ArgumentNullException.ThrowIfNull(builder);

		string indent = new(' ', indentLevel * 4);

		switch (node)
		{
			case FunctionDeclaration function:
				GenerateFunction(function, builder, indent);
				break;
			case Parameter parameter:
				GenerateParameter(parameter, builder);
				break;
			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, builder, indent);
				break;
			case BinaryExpression binaryExpr:
				GenerateBinaryExpression(binaryExpr, builder);
				break;
			case VariableReference varRef:
				builder.Append(varRef.Name);
				break;
			case LiteralExpression<string> stringLit:
				builder.Append($"\"{EscapeString(stringLit.Value ?? string.Empty)}\"");
				break;
			case LiteralExpression<int> intLit:
				builder.Append(intLit.Value);
				break;
			case LiteralExpression<bool> boolLit:
				builder.Append(boolLit.Value ? "true" : "false");
				break;
			case LiteralExpression<double> doubleLit:
				builder.Append($"{doubleLit.Value}d");
				break;
			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, builder, indent);
				break;
			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, builder, indent);
				break;
			default:
				builder.AppendLine($"{indent}// Unsupported node type: {node.GetType().Name}");
				break;
		}
	}

	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public override bool CanGenerate(AstNode astNode)
	{
		return astNode is not null and (FunctionDeclaration
			or Parameter
			or ReturnStatement
			or BinaryExpression
			or VariableReference
			or LiteralExpression<string>
			or LiteralExpression<int>
			or LiteralExpression<bool>
			or LiteralExpression<double>
			or VariableDeclaration
			or AssignmentStatement);
	}

	private void GenerateFunction(FunctionDeclaration function, StringBuilder builder, string indent)
	{
		// Build method signature
		builder.Append(indent);
		builder.Append("public ");

		// Add return type
		string returnType = MapToCSType(function.ReturnType ?? "void");
		builder.Append(returnType);
		builder.Append(' ');

		// Add method name
		builder.Append(function.Name);
		builder.Append('(');

		// Add parameters
		for (int i = 0; i < function.Parameters.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}
			GenerateParameter(function.Parameters[i], builder);
		}

		builder.AppendLine(")");
		builder.AppendLine($"{indent}{{");

		// Add body
		foreach (AstNode statement in function.Body)
		{
			GenerateInternal(statement, builder, 1);
		}

		builder.AppendLine($"{indent}}}");
	}

	private static void GenerateParameter(Parameter parameter, StringBuilder builder)
	{
		string type = MapToCSType(parameter.Type ?? "object");
		builder.Append(type);
		builder.Append(' ');
		builder.Append(parameter.Name);

		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			builder.Append(" = ");
			builder.Append(parameter.DefaultValue);
		}
	}

	private static void GenerateReturnStatement(ReturnStatement returnStmt, StringBuilder builder, string indent)
	{
		builder.Append(indent);
		builder.Append("return");

		if (returnStmt.Expression != null)
		{
			builder.Append(' ');
			// For now, just convert to string - in a full implementation,
			// we'd recursively generate the expression
			builder.Append(returnStmt.Expression.ToString());
		}

		builder.AppendLine(";");
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

	private void GenerateBinaryExpression(BinaryExpression binaryExpr, StringBuilder builder)
	{
		// Add parentheses for clarity
		builder.Append('(');
		GenerateInternal(binaryExpr.Left, builder, 0);
		builder.Append(' ');
		builder.Append(GetCSharpOperator(binaryExpr.Operator));
		builder.Append(' ');
		GenerateInternal(binaryExpr.Right, builder, 0);
		builder.Append(')');
	}

	private void GenerateVariableDeclaration(VariableDeclaration varDecl, StringBuilder builder, string indent)
	{
		builder.Append(indent);

		// Use type or var for type inference
		if (varDecl.IsTypeInferred || string.IsNullOrEmpty(varDecl.Type))
		{
			builder.Append("var ");
		}
		else
		{
			builder.Append(MapToCSType(varDecl.Type));
			builder.Append(' ');
		}

		builder.Append(varDecl.Name);

		if (varDecl.InitialValue != null)
		{
			builder.Append(" = ");
			GenerateInternal(varDecl.InitialValue, builder, 0);
		}

		builder.AppendLine(";");
	}

	private void GenerateAssignmentStatement(AssignmentStatement assignment, StringBuilder builder, string indent)
	{
		builder.Append(indent);
		GenerateInternal(assignment.Target, builder, 0);
		builder.Append(' ');
		builder.Append(GetCSharpAssignmentOperator(assignment.Operator));
		builder.Append(' ');
		GenerateInternal(assignment.Value, builder, 0);
		builder.AppendLine(";");
	}

	private static string EscapeString(string value)
	{
		return value
			.Replace("\\", "\\\\")
			.Replace("\"", "\\\"")
			.Replace("\n", "\\n")
			.Replace("\r", "\\r")
			.Replace("\t", "\\t");
	}

	private static string GetCSharpOperator(BinaryOperator op) => op switch
	{
		BinaryOperator.Add => "+",
		BinaryOperator.Subtract => "-",
		BinaryOperator.Multiply => "*",
		BinaryOperator.Divide => "/",
		BinaryOperator.Modulo => "%",
		BinaryOperator.Equal => "==",
		BinaryOperator.NotEqual => "!=",
		BinaryOperator.LessThan => "<",
		BinaryOperator.LessThanOrEqual => "<=",
		BinaryOperator.GreaterThan => ">",
		BinaryOperator.GreaterThanOrEqual => ">=",
		BinaryOperator.LogicalAnd => "&&",
		BinaryOperator.LogicalOr => "||",
		BinaryOperator.BitwiseAnd => "&",
		BinaryOperator.BitwiseOr => "|",
		BinaryOperator.BitwiseXor => "^",
		BinaryOperator.LeftShift => "<<",
		BinaryOperator.RightShift => ">>",
		_ => throw new NotSupportedException($"Unsupported binary operator: {op}")
	};

	private static string GetCSharpAssignmentOperator(AssignmentOperator op) => op switch
	{
		AssignmentOperator.Assign => "=",
		AssignmentOperator.AddAssign => "+=",
		AssignmentOperator.SubtractAssign => "-=",
		AssignmentOperator.MultiplyAssign => "*=",
		AssignmentOperator.DivideAssign => "/=",
		AssignmentOperator.ModuloAssign => "%=",
		AssignmentOperator.BitwiseAndAssign => "&=",
		AssignmentOperator.BitwiseOrAssign => "|=",
		AssignmentOperator.BitwiseXorAssign => "^=",
		AssignmentOperator.LeftShiftAssign => "<<=",
		AssignmentOperator.RightShiftAssign => ">>=",
		_ => throw new NotSupportedException($"Unsupported assignment operator: {op}")
	};
}
