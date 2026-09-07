// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;

/// <summary>
/// Generates JavaScript code from AST nodes.
/// </summary>
/// <remarks>
/// JavaScript is dynamically typed, so the <see cref="Parameter.Type"/> and
/// <see cref="FunctionDeclaration.ReturnType"/> carried by the AST have no place in the output and
/// are deliberately dropped. Equality maps to the strict operators (<c>===</c>, <c>!==</c>) rather
/// than the coercing ones, because the AST's <see cref="BinaryOperator.Equal"/> means value equality
/// in every other generator and <c>==</c> would not.
/// </remarks>
public class JavaScriptGenerator : LanguageGeneratorBase
{
	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public override string LanguageId => "javascript";

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public override string DisplayName => "JavaScript";

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public override string FileExtension => "js";

	/// <summary>
	/// Generates JavaScript code with proper indentation.
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
				throw new NotSupportedException($"Unsupported node type for JavaScript generation: {node.GetNodeTypeName()}");
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
		builder.Append("function ");
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

		builder.AppendLine(") {");

		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, builder, indentLevel + 1);
		}

		Indent(builder, indentLevel);
		builder.AppendLine("}");
	}

	private static void GenerateParameter(Parameter parameter, StringBuilder builder, int position = 0)
	{
		builder.Append(parameter.Name ?? $"param{position}");

		// A JavaScript default value makes the parameter optional; there is no other way to say so.
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

		// `const` needs an initializer, so an uninitialized constant has to be declared with `let`.
		builder.Append(varDecl.IsConstant && varDecl.InitialValue is not null ? "const " : "let ");
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
		builder.Append(GetJavaScriptAssignmentOperator(assignment.Operator));
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
		builder.Append(GetJavaScriptOperator(binaryExpr.Operator));
		builder.Append(' ');
		GenerateInternal(binaryExpr.Right, builder, 0);
		builder.Append(')');
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

	private static string GetJavaScriptOperator(BinaryOperator op) => op switch
	{
		BinaryOperator.Add => "+",
		BinaryOperator.Subtract => "-",
		BinaryOperator.Multiply => "*",
		BinaryOperator.Divide => "/",
		BinaryOperator.Modulo => "%",
		BinaryOperator.Equal => "===",
		BinaryOperator.NotEqual => "!==",
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

	private static string GetJavaScriptAssignmentOperator(AssignmentOperator op) => op switch
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
