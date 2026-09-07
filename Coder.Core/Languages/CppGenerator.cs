// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
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
public class CppGenerator : StandardLanguageGenerator
{
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

	/// <inheritdoc/>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(builder);

		Indent(builder, indentLevel);
		builder.Append(MapToCppType(funcDecl.ReturnType ?? "void"));
		builder.Append(' ');
		builder.Append(funcDecl.Name ?? "unnamedFunction");
		builder.Append('(');
		GenerateParameterList(funcDecl.Parameters, builder);
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

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, StringBuilder builder, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(builder);

		builder.Append(MapToCppType(parameter.Type ?? "object"));
		builder.Append(' ');
		builder.Append(parameter.Name ?? $"param{position}");
		AppendDefaultValue(parameter, builder);
	}

	/// <inheritdoc/>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, StringBuilder builder, int indentLevel)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(builder);

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

		EndStatement(builder);
	}

	private static string MapToCppType(string type) =>
		TypeMappings.TryGetValue(type, out string? mapped) ? mapped : type;
}
