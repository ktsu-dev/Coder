// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Languages;

using System.Collections.Generic;
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
			default:
				builder.AppendLine($"{indent}// Unsupported node type: {node.GetType().Name}");
				break;
		}
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
}
