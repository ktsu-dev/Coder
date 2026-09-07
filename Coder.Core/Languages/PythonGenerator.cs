// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System.Text;
using ktsu.Coder.Ast;

/// <summary>
/// Generates Python code from AST nodes.
/// </summary>
public class PythonGenerator : StandardLanguageGenerator
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
	/// Spells a boolean literal. Python capitalizes them.
	/// </summary>
	/// <param name="value">The literal's value.</param>
	/// <returns>The keyword Python uses.</returns>
	protected override string FormatBoolean(bool value) => value ? "True" : "False";

	/// <summary>
	/// Ends a statement. Python has no terminator, and the function body emits the line breaks, so
	/// this deliberately writes nothing.
	/// </summary>
	/// <param name="builder">The string builder, left untouched.</param>
	protected override void EndStatement(StringBuilder builder)
	{
		// Python statements end at the newline the caller writes.
	}

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(builder);

		// Function signature
		Indent(builder, indentLevel);
		builder.Append("def ");
		builder.Append(funcDecl.Name ?? "unnamed_function");
		builder.Append('(');
		GenerateParameterList(funcDecl.Parameters, builder);
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

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, StringBuilder builder, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(builder);

		builder.Append(parameter.Name ?? $"param{position}");

		// Type hints are optional in Python, so they are emitted only when the AST carries one.
		if (parameter.Type is not null)
		{
			builder.Append(": ");
			builder.Append(PythonTypeFromGenericType(parameter.Type));
		}

		AppendDefaultValue(parameter, builder);
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

	/// <inheritdoc/>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(builder);

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

	/// <summary>
	/// Maps a binary operator to its Python spelling.
	/// </summary>
	/// <param name="op">The operator to map.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <remarks>Only the logical operators differ from the C-family set: Python spells them as words.</remarks>
	protected override string GetOperatorSpelling(BinaryOperator op) => op switch
	{
		BinaryOperator.LogicalAnd => "and",
		BinaryOperator.LogicalOr => "or",
		_ => GetBinaryOperator(op)
	};
}
