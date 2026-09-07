// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

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
public class JavaScriptGenerator : StandardLanguageGenerator
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

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(builder);

		Indent(builder, indentLevel);
		builder.Append("function ");
		builder.Append(funcDecl.Name ?? "unnamedFunction");
		builder.Append('(');
		GenerateParameterList(funcDecl.Parameters, builder);
		builder.AppendLine(") {");

		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, builder, indentLevel + 1);
		}

		Indent(builder, indentLevel);
		builder.AppendLine("}");
	}

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, StringBuilder builder, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(builder);

		builder.Append(parameter.Name ?? $"param{position}");
		AppendDefaultValue(parameter, builder);
	}

	/// <inheritdoc/>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(builder);

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
	protected override string GetOperatorSpelling(BinaryOperator op) => op switch
	{
		BinaryOperator.Equal => "===",
		BinaryOperator.NotEqual => "!==",
		_ => GetBinaryOperator(op)
	};
}
