// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Languages;

using System;
using System.Text;
using ktsu.Coder.Core.Ast;

/// <summary>
/// Generates Python code from AST nodes.
/// </summary>
public class PythonGenerator : LanguageGeneratorBase
{
	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "python";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "Python";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "py";

	/// <summary>
	/// Generates Python code with proper indentation.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected override void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel)
	{
		ArgumentNullException.ThrowIfNull(node);
		ArgumentNullException.ThrowIfNull(builder);

		switch (node)
		{
			case FunctionDeclaration funcDecl:
				GenerateFunctionDeclaration(funcDecl, builder, indentLevel);
				break;

			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, builder, indentLevel);
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
				builder.Append(boolLit.Value ? "True" : "False");
				break;

			case LiteralExpression<double> doubleLit:
				builder.Append(doubleLit.Value);
				break;

			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, builder, indentLevel);
				break;

			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, builder, indentLevel);
				break;

			// Legacy support for AstLeafNode types
			case AstLeafNode<string> strLeaf:
				builder.Append($"\"{EscapeString(strLeaf.Value ?? string.Empty)}\"");
				break;

			case AstLeafNode<int> intLeaf:
				builder.Append(intLeaf.Value);
				break;

			case AstLeafNode<bool> boolLeaf:
				builder.Append(boolLeaf.Value ? "True" : "False");
				break;

			default:
				throw new NotSupportedException($"Unsupported node type for Python generation: {node.GetNodeTypeName()}");
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
			or ReturnStatement
			or BinaryExpression
			or VariableReference
			or LiteralExpression<string>
			or LiteralExpression<int>
			or LiteralExpression<bool>
			or LiteralExpression<double>
			or VariableDeclaration
			or AssignmentStatement
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>);
	}

	private void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, StringBuilder builder, int indentLevel)
	{
		// Function signature
		Indent(builder, indentLevel);
		builder.Append("def ");
		builder.Append(funcDecl.Name ?? "unnamed_function");
		builder.Append('(');

		// Parameters
		for (int i = 0; i < funcDecl.Parameters.Count; i++)
		{
			Parameter param = funcDecl.Parameters[i];

			if (i > 0)
			{
				builder.Append(", ");
			}

			builder.Append(param.Name ?? $"param{i}");

			// Add type hints if available
			if (param.Type != null)
			{
				builder.Append(": ");
				builder.Append(PythonTypeFromGenericType(param.Type));
			}

			// Add default value if optional
			if (param.IsOptional && param.DefaultValue != null)
			{
				builder.Append(" = ");
				builder.Append(param.DefaultValue);
			}
		}

		builder.Append(')');

		// Add return type hint if available
		if (funcDecl.ReturnType != null)
		{
			builder.Append(" -> ");
			builder.Append(PythonTypeFromGenericType(funcDecl.ReturnType));
		}

		builder.AppendLine(":");

		// Function body
		if (funcDecl.Body.Count == 0)
		{
			// Empty function body needs a pass statement
			Indent(builder, indentLevel + 1);
			builder.AppendLine("pass");
		}
		else
		{
			// Generate each statement in the body
			foreach (AstNode statement in funcDecl.Body)
			{
				GenerateInternal(statement, builder, indentLevel + 1);
				builder.AppendLine();
			}
		}
	}

	private void GenerateReturnStatement(ReturnStatement returnStmt, StringBuilder builder, int indentLevel)
	{
		Indent(builder, indentLevel);
		builder.Append("return");

		if (returnStmt.Expression != null)
		{
			builder.Append(' ');
			GenerateInternal(returnStmt.Expression, builder, 0); // No indentation for the expression
		}
	}

	private static string PythonTypeFromGenericType(string genericType)
	{
		return genericType.ToLowerInvariant() switch
		{
			"int" => "int",
			"string" => "str",
			"bool" => "bool",
			"float" => "float",
			"double" => "float",
			"void" => "None",
			_ => genericType
		};
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

	private static void Indent(StringBuilder builder, int level) => builder.Append(new string(' ', level * 4));

	private void GenerateBinaryExpression(BinaryExpression binaryExpr, StringBuilder builder)
	{
		// Add parentheses for clarity
		builder.Append('(');
		GenerateInternal(binaryExpr.Left, builder, 0);
		builder.Append(' ');
		builder.Append(GetPythonOperator(binaryExpr.Operator));
		builder.Append(' ');
		GenerateInternal(binaryExpr.Right, builder, 0);
		builder.Append(')');
	}

	private void GenerateVariableDeclaration(VariableDeclaration varDecl, StringBuilder builder, int indentLevel)
	{
		Indent(builder, indentLevel);
		builder.Append(varDecl.Name);

		if (varDecl.InitialValue != null)
		{
			builder.Append(" = ");
			GenerateInternal(varDecl.InitialValue, builder, 0);
		}
		else
		{
			// Python requires initialization, so use None for uninitialized variables
			builder.Append(" = None");
		}
	}

	private void GenerateAssignmentStatement(AssignmentStatement assignment, StringBuilder builder, int indentLevel)
	{
		Indent(builder, indentLevel);
		GenerateInternal(assignment.Target, builder, 0);
		builder.Append(' ');
		builder.Append(GetPythonAssignmentOperator(assignment.Operator));
		builder.Append(' ');
		GenerateInternal(assignment.Value, builder, 0);
	}

	private static string GetPythonOperator(BinaryOperator op) => op switch
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
		BinaryOperator.LogicalAnd => "and",
		BinaryOperator.LogicalOr => "or",
		BinaryOperator.BitwiseAnd => "&",
		BinaryOperator.BitwiseOr => "|",
		BinaryOperator.BitwiseXor => "^",
		BinaryOperator.LeftShift => "<<",
		BinaryOperator.RightShift => ">>",
		_ => throw new NotSupportedException($"Unsupported binary operator: {op}")
	};

	private static string GetPythonAssignmentOperator(AssignmentOperator op) => op switch
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
