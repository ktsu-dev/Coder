// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using ktsu.Coder.Ast;
using ktsu.CodeBlocker;

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
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		code.Write($"{MapToCppType(funcDecl.ReturnType ?? "void")} {funcDecl.Name ?? "unnamedFunction"}(");
		GenerateParameterList(funcDecl.Parameters, code);

		// The line is ended before the scope opens, so C++'s brace lands on its own line.
		code.WriteLine(")");

		using Scope body = new(code);
		foreach (AstNode statement in funcDecl.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Every member is public. The AST carries no per-member visibility, and a C++ class defaults to
	/// private, so a generated class with no access specifier would compile to something nothing
	/// outside it could use.
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		code.Write($"class {classDecl.Name ?? "UnnamedClass"}");

		if (!string.IsNullOrEmpty(classDecl.BaseType))
		{
			code.Write($" : public {MapToCppType(classDecl.BaseType!)}");
		}

		code.WriteLine();

		// A C++ class declaration is a statement, so its closing brace takes a semicolon.
		using ScopeWithTrailingSemicolon body = new(code);
		code.WriteLine("public:");
		GenerateClassMembers(classDecl, code);
	}

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		code.Write($"{MapToCppType(parameter.Type ?? "object")} {parameter.Name ?? $"param{position}"}");
		AppendDefaultValue(parameter, code);
	}

	/// <inheritdoc/>
	protected override void GenerateVariableDeclaration(VariableDeclaration varDecl, CodeBlocker code)
	{
		Ensure.NotNull(varDecl);
		Ensure.NotNull(code);

		if (varDecl.IsConstant)
		{
			code.Write("const ");
		}

		code.Write($"{GetDeclaredType(varDecl)} {varDecl.Name}");

		if (varDecl.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(varDecl.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <summary>
	/// Spells the type a declaration is introduced with.
	/// </summary>
	/// <param name="varDecl">The declaration being emitted.</param>
	/// <returns>The C++ type name, or a deduced placeholder.</returns>
	/// <remarks>
	/// <c>auto</c> needs an initializer to deduce from, so an inferred declaration without one falls
	/// back to <c>std::any</c>.
	/// </remarks>
	private static string GetDeclaredType(VariableDeclaration varDecl)
	{
		if (!varDecl.IsTypeInferred && !string.IsNullOrEmpty(varDecl.Type))
		{
			return MapToCppType(varDecl.Type!);
		}

		return varDecl.InitialValue is not null ? "auto" : "std::any";
	}

	private static string MapToCppType(string type) =>
		TypeMappings.TryGetValue(type, out string? mapped) ? mapped : type;
}
