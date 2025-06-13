// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Languages;

using System.Text;
using ktsu.Coder.Core.Ast;

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
	/// Internal method to generate code with proper indentation.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected override void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel)
	{
		switch (node)
		{
			case FunctionDeclaration func:
				GenerateFunctionDeclaration(func, builder, indentLevel);
				break;
			case ReturnStatement ret:
				GenerateReturnStatement(ret, builder, indentLevel);
				break;
			case AstLeafNode<string> leafStr:
				builder.Append(leafStr.Value ?? "null");
				break;
			case AstLeafNode<int> leafInt:
				builder.Append(leafInt.Value.ToString());
				break;
			case AstLeafNode<bool> leafBool:
				builder.Append(leafBool.Value ? "true" : "false");
				break;
			default:
				builder.Append($"/* Unsupported node type: {node.GetType().Name} */");
				break;
		}
	}

	private void GenerateFunctionDeclaration(FunctionDeclaration function, StringBuilder builder, int indentLevel)
	{
		ArgumentNullException.ThrowIfNull(function);
		ArgumentNullException.ThrowIfNull(builder);

		Indent(builder, indentLevel);

		// Access modifier and method signature
		string accessModifier = function.Metadata.ContainsKey("access") ?
			function.Metadata["access"]?.ToString() ?? "public" : "public";

		string staticModifier = function.Metadata.ContainsKey("static") &&
			Convert.ToBoolean(function.Metadata["static"]) ? "static " : "";

		string returnType = function.ReturnType ?? "void";

		builder.Append($"{accessModifier} {staticModifier}{returnType} {function.Name}(");

		// Parameters
		for (int i = 0; i < function.Parameters.Count; i++)
		{
			Parameter param = function.Parameters[i];
			GenerateParameter(param, builder);

			if (i < function.Parameters.Count - 1)
			{
				builder.Append(", ");
			}
		}

		builder.AppendLine(")");
		Indent(builder, indentLevel);
		builder.AppendLine("{");

		// Function body
		foreach (AstNode statement in function.Body)
		{
			GenerateInternal(statement, builder, indentLevel + 1);
			builder.AppendLine();
		}

		Indent(builder, indentLevel);
		builder.AppendLine("}");
	}

	private void GenerateParameter(Parameter parameter, StringBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(parameter);
		ArgumentNullException.ThrowIfNull(builder);

		string paramType = parameter.Type ?? "object";
		builder.Append($"{paramType} {parameter.Name}");

		if (parameter.IsOptional && parameter.DefaultValue != null)
		{
			string defaultValue = parameter.DefaultValue;

			// Handle common default value conversions
			if (paramType.ToLowerInvariant() == "bool")
			{
				defaultValue = defaultValue.ToLowerInvariant() == "true" ? "true" : "false";
			}
			else if (paramType.ToLowerInvariant() == "string")
			{
				if (!defaultValue.StartsWith('"'))
				{
					defaultValue = $"\"{defaultValue}\"";
				}
			}

			builder.Append($" = {defaultValue}");
		}
	}

	private void GenerateReturnStatement(ReturnStatement returnStatement, StringBuilder builder, int indentLevel)
	{
		ArgumentNullException.ThrowIfNull(returnStatement);
		ArgumentNullException.ThrowIfNull(builder);

		Indent(builder, indentLevel);

		if (returnStatement.Expression == null)
		{
			builder.Append("return;");
		}
		else
		{
			builder.Append("return ");
			GenerateInternal(returnStatement.Expression, builder, 0);
			builder.Append(";");
		}
	}
}
