// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
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

			case VariableDeclaration varDecl:
				GenerateVariableDeclaration(varDecl, builder, indentLevel);
				break;

			case BinaryExpression binaryExpr:
				GenerateBinaryExpression(binaryExpr, builder, GetJavaScriptOperator(binaryExpr.Operator));
				break;

			case ReturnStatement returnStmt:
				GenerateReturnStatement(returnStmt, builder, indentLevel);
				break;

			case AssignmentStatement assignment:
				GenerateAssignmentStatement(assignment, builder, indentLevel);
				break;

			default:
				if (!TryGenerateCommonNode(node, builder))
				{
					throw new NotSupportedException($"Unsupported node type for JavaScript generation: {node.GetNodeTypeName()}");
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

		EndStatement(builder);
	}

	/// <summary>
	/// Maps a binary operator to its JavaScript spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>
	/// Only equality differs from the C-family set: the AST's <see cref="BinaryOperator.Equal"/>
	/// means value equality, which is <c>===</c> in JavaScript. <c>==</c> coerces and would not.
	/// </remarks>
	private static string GetJavaScriptOperator(BinaryOperator op) => op switch
	{
		BinaryOperator.Equal => "===",
		BinaryOperator.NotEqual => "!==",
		_ => GetBinaryOperator(op)
	};
}
