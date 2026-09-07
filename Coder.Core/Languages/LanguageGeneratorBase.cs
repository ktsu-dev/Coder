// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;

/// <summary>
/// Provides a base implementation for language generators with common functionality.
/// </summary>
public abstract class LanguageGeneratorBase : ILanguageGenerator
{
	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public abstract string LanguageId { get; }

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public abstract string DisplayName { get; }

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public abstract string FileExtension { get; }

	/// <summary>
	/// Generates code in the target language from an AST node.
	/// </summary>
	/// <param name="astNode">The AST node to generate code from.</param>
	/// <returns>A string containing the generated code in the target language.</returns>
	public string Generate(AstNode astNode)
	{
		Ensure.NotNull(astNode);

		if (!CanGenerate(astNode))
		{
			throw new NotSupportedException($"Cannot generate code for node type: {astNode.GetNodeTypeName()}");
		}

		StringBuilder builder = new();
		GenerateInternal(astNode, builder, 0);
		return builder.ToString();
	}

	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public virtual bool CanGenerate(AstNode astNode)
	{
		// No null check: a type pattern never matches null.
		return astNode is FunctionDeclaration
			or ReturnStatement
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>;
	}

	/// <summary>
	/// Internal method to generate code with proper indentation.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected abstract void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel);

	/// <summary>
	/// Adds indentation to the code based on the current indentation level.
	/// </summary>
	/// <param name="builder">The string builder to append indentation to.</param>
	/// <param name="indentLevel">The indentation level.</param>
	/// <param name="indentSize">The number of spaces per indent level. Default is 4.</param>
	protected static void Indent(StringBuilder builder, int indentLevel, int indentSize = 4)
	{
		Ensure.NotNull(builder);
		builder.Append(' ', indentLevel * indentSize);
	}

	/// <summary>
	/// Emits the nodes whose spelling is the same in every target language: variable references,
	/// literals, and the legacy <see cref="AstLeafNode{T}"/> shapes.
	/// </summary>
	/// <param name="node">The node to emit.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <returns>True if the node was handled; false if it is the caller's to emit.</returns>
	/// <remarks>
	/// A generator's dispatch handles its structural nodes and defers the rest here, so the literal
	/// cases exist once. Where a language does differ — Python's capitalized booleans — it overrides
	/// <see cref="FormatBoolean"/> rather than repeating the whole set.
	/// </remarks>
	protected bool TryGenerateCommonNode(AstNode node, StringBuilder builder)
	{
		Ensure.NotNull(builder);

		switch (node)
		{
			case VariableReference varRef:
				builder.Append(varRef.Name);
				return true;

			case LiteralExpression<string> stringLit:
				builder.Append($"\"{EscapeString(stringLit.Value ?? string.Empty)}\"");
				return true;

			case LiteralExpression<int> intLit:
				builder.Append(intLit.Value);
				return true;

			case LiteralExpression<bool> boolLit:
				builder.Append(FormatBoolean(boolLit.Value));
				return true;

			case LiteralExpression<double> doubleLit:
				builder.Append(doubleLit.Value.ToString(CultureInfo.InvariantCulture));
				return true;

			// Legacy support for AstLeafNode types
			case AstLeafNode<string> strLeaf:
				builder.Append($"\"{EscapeString(strLeaf.Value ?? string.Empty)}\"");
				return true;

			case AstLeafNode<int> intLeaf:
				builder.Append(intLeaf.Value);
				return true;

			case AstLeafNode<bool> boolLeaf:
				builder.Append(FormatBoolean(boolLeaf.Value));
				return true;

			default:
				return false;
		}
	}

	/// <summary>
	/// Spells a boolean literal.
	/// </summary>
	/// <param name="value">The literal's value.</param>
	/// <returns>The keyword the language uses.</returns>
	protected virtual string FormatBoolean(bool value) => value ? "true" : "false";

	/// <summary>
	/// Ends a statement with the terminator and line break the language uses.
	/// </summary>
	/// <param name="builder">The string builder to append to.</param>
	/// <remarks>
	/// Python has neither, and its function body writes its own line breaks, so it overrides this
	/// with an empty body rather than each statement emitter growing a special case.
	/// </remarks>
	protected virtual void EndStatement(StringBuilder builder)
	{
		Ensure.NotNull(builder);
		builder.AppendLine(";");
	}

	/// <summary>
	/// Emits a return statement, recursing into its expression.
	/// </summary>
	/// <param name="returnStmt">The statement to emit.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected void GenerateReturnStatement(ReturnStatement returnStmt, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(returnStmt);
		Ensure.NotNull(builder);

		Indent(builder, indentLevel);
		builder.Append("return");

		if (returnStmt.Expression is not null)
		{
			builder.Append(' ');
			GenerateInternal(returnStmt.Expression, builder, 0);
		}

		EndStatement(builder);
	}

	/// <summary>
	/// Emits an assignment statement, recursing into both sides.
	/// </summary>
	/// <param name="assignment">The statement to emit.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected void GenerateAssignmentStatement(AssignmentStatement assignment, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(assignment);
		Ensure.NotNull(builder);

		Indent(builder, indentLevel);
		GenerateInternal(assignment.Target, builder, 0);
		builder.Append(' ');
		builder.Append(GetAssignmentOperator(assignment.Operator));
		builder.Append(' ');
		GenerateInternal(assignment.Value, builder, 0);
		EndStatement(builder);
	}

	/// <summary>
	/// Emits a parenthesised binary expression, recursing into both operands.
	/// </summary>
	/// <param name="binaryExpr">The expression to emit.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="operatorSpelling">The operator's spelling in the target language.</param>
	/// <remarks>Always parenthesised: the AST carries no precedence, so nesting would be ambiguous otherwise.</remarks>
	protected void GenerateBinaryExpression(BinaryExpression binaryExpr, StringBuilder builder, string operatorSpelling)
	{
		Ensure.NotNull(binaryExpr);
		Ensure.NotNull(builder);

		builder.Append('(');
		GenerateInternal(binaryExpr.Left, builder, 0);
		builder.Append(' ');
		builder.Append(operatorSpelling);
		builder.Append(' ');
		GenerateInternal(binaryExpr.Right, builder, 0);
		builder.Append(')');
	}

	/// <summary>
	/// Reports whether a node is one of the standard shapes a generator built on these helpers accepts.
	/// </summary>
	/// <param name="astNode">The node to check.</param>
	/// <returns>True if the node is generatable.</returns>
	protected static bool CanGenerateStandardNodes(AstNode astNode)
	{
		// No null check: a type pattern never matches null.
		return astNode is FunctionDeclaration
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
			or AstLeafNode<bool>;
	}

	/// <summary>
	/// Escapes a string literal's contents for a target language using C-style backslash escapes.
	/// </summary>
	/// <param name="value">The raw string value.</param>
	/// <returns>The escaped value, without surrounding quotes.</returns>
	/// <remarks>Every language the generators target uses these escapes, Python included.</remarks>
	protected static string EscapeString(string value)
	{
		Ensure.NotNull(value);

		return value
			.Replace("\\", "\\\\")
			.Replace("\"", "\\\"")
			.Replace("\n", "\\n")
			.Replace("\r", "\\r")
			.Replace("\t", "\\t");
	}

	/// <summary>
	/// Maps a binary operator to its C-family spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException">The operator has no mapping.</exception>
	/// <remarks>
	/// C# and C++ use this set unchanged. A generator whose language spells one operator
	/// differently — Python's <c>and</c>/<c>or</c>, JavaScript's strict <c>===</c>/<c>!==</c> —
	/// handles just that operator and defers the rest here.
	/// </remarks>
	protected static string GetBinaryOperator(BinaryOperator op) => OperatorSymbols.GetSymbol(op);

	/// <summary>
	/// Maps an assignment operator to its source spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException">The operator has no mapping.</exception>
	/// <remarks>Every language the generators target spells these identically.</remarks>
	protected static string GetAssignmentOperator(AssignmentOperator op) => OperatorSymbols.GetSymbol(op);
}
