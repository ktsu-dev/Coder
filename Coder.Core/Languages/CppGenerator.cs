// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;

/// <summary>
/// Generates C++ code from AST nodes.
/// </summary>
/// <remarks>
/// The AST's type names are the same language-neutral set the other generators consume (<c>str</c>,
/// <c>int</c>, <c>bool</c>, …), so they are mapped to C++ spellings; anything unrecognised is emitted
/// verbatim on the assumption the caller meant a C++ type. A declaration with no type, or one marked
/// type-inferred, becomes <c>auto</c>.
/// </remarks>
public class CppGenerator : LanguageGeneratorBase
{
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
	/// Maps the AST's language-neutral type names onto C++ spellings.
	/// </summary>
	private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "str", "std::string" },
		{ "string", "std::string" },
		{ "int", "int" },
		{ "long", "long long" },
		{ "float", "float" },
		{ "double", "double" },
		{ "bool", "bool" },
		{ "list", "std::vector<std::any>" },
		{ "dict", "std::map<std::string, std::any>" },
		{ "void", "void" },
		{ "object", "std::any" }
	};

	/// <summary>
	/// Generates C++ code with proper indentation.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected override void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(builder);

		switch (node)
		{
			case FunctionDeclaration funcDecl:
				GenerateFunctionDeclaration(funcDecl, builder, indentLevel);
				break;

			case Parameter parameter:
				GenerateParameter(parameter, builder);
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
				builder.Append(boolLit.Value ? "true" : "false");
				break;

			case LiteralExpression<double> doubleLit:
				builder.Append(doubleLit.Value.ToString(CultureInfo.InvariantCulture));
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
				builder.Append(boolLeaf.Value ? "true" : "false");
				break;

			default:
				throw new NotSupportedException($"Unsupported node type for C++ generation: {node.GetNodeTypeName()}");
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
			or AssignmentStatement
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>);
	}

	private void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, StringBuilder builder, int indentLevel)
	{
		Indent(builder, indentLevel);
		builder.Append(MapToCppType(funcDecl.ReturnType ?? "void"));
		builder.Append(' ');
		builder.Append(funcDecl.Name ?? "unnamedFunction");
		builder.Append('(');

		for (int i = 0; i < funcDecl.Parameters.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}

			GenerateParameter(funcDecl.Parameters[i], builder, i);
		}

		builder.AppendLine(")");
		Indent(builder, indentLevel);
		builder.AppendLine("{");

		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, builder, indentLevel + 1);
		}

		Indent(builder, indentLevel);
		builder.AppendLine("}");
	}

	private static void GenerateParameter(Parameter parameter, StringBuilder builder, int position = 0)
	{
		builder.Append(MapToCppType(parameter.Type ?? "object"));
		builder.Append(' ');
		builder.Append(parameter.Name ?? $"param{position}");

		// A C++ default argument is the only way the AST's IsOptional can be expressed.
		if (parameter.IsOptional && !string.IsNullOrEmpty(parameter.DefaultValue))
		{
			builder.Append(" = ");
			builder.Append(parameter.DefaultValue);
		}
	}

	private void GenerateReturnStatement(ReturnStatement returnStmt, StringBuilder builder, int indentLevel)
	{
		Indent(builder, indentLevel);
		builder.Append("return");

		if (returnStmt.Expression is not null)
		{
			builder.Append(' ');
			GenerateInternal(returnStmt.Expression, builder, 0);
		}

		builder.AppendLine(";");
	}

	private void GenerateVariableDeclaration(VariableDeclaration varDecl, StringBuilder builder, int indentLevel)
	{
		Indent(builder, indentLevel);

		if (varDecl.IsConstant)
		{
			builder.Append("const ");
		}

		// `auto` needs an initializer, so an uninitialized inferred declaration falls back to `std::any`.
		builder.Append(varDecl.IsTypeInferred || string.IsNullOrEmpty(varDecl.Type)
			? varDecl.InitialValue is not null ? "auto" : "std::any"
			: MapToCppType(varDecl.Type!));

		builder.Append(' ');
		builder.Append(varDecl.Name);

		if (varDecl.InitialValue is not null)
		{
			builder.Append(" = ");
			GenerateInternal(varDecl.InitialValue, builder, 0);
		}

		builder.AppendLine(";");
	}

	private void GenerateAssignmentStatement(AssignmentStatement assignment, StringBuilder builder, int indentLevel)
	{
		Indent(builder, indentLevel);
		GenerateInternal(assignment.Target, builder, 0);
		builder.Append(' ');
		builder.Append(GetCppAssignmentOperator(assignment.Operator));
		builder.Append(' ');
		GenerateInternal(assignment.Value, builder, 0);
		builder.AppendLine(";");
	}

	private void GenerateBinaryExpression(BinaryExpression binaryExpr, StringBuilder builder)
	{
		// Parenthesised for clarity, as in every other generator: the AST carries no precedence.
		builder.Append('(');
		GenerateInternal(binaryExpr.Left, builder, 0);
		builder.Append(' ');
		builder.Append(GetCppOperator(binaryExpr.Operator));
		builder.Append(' ');
		GenerateInternal(binaryExpr.Right, builder, 0);
		builder.Append(')');
	}

	private static string MapToCppType(string type) =>
		TypeMappings.TryGetValue(type, out string? mapped) ? mapped : type;

	private static string EscapeString(string value)
	{
		return value
			.Replace("\\", "\\\\")
			.Replace("\"", "\\\"")
			.Replace("\n", "\\n")
			.Replace("\r", "\\r")
			.Replace("\t", "\\t");
	}

	private static string GetCppOperator(BinaryOperator op) => op switch
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

	private static string GetCppAssignmentOperator(AssignmentOperator op) => op switch
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
