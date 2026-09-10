// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using System;
using System.Collections.Generic;
using System.Linq;
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
		{ "list", "std::vector" },
		{ "dict", "std::map" },
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
	/// <remarks>
	/// A pure function is written <c>[[nodiscard]]</c>: discarding the result of a call that does
	/// nothing else is always a mistake, and that is the whole of what the standard can say. The
	/// compiler-specific <c>__attribute__((pure))</c> asserts to the optimiser that the call may be
	/// elided or duplicated, which is a stronger promise than the AST is in a position to make.
	/// </remarks>
	protected override void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, CodeBlocker code)
	{
		Ensure.NotNull(funcDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(funcDecl, code);

		if (funcDecl.IsPure)
		{
			code.Write("[[nodiscard]] ");
		}

		if (funcDecl.IsStatic)
		{
			code.Write("static ");
		}

		code.Write($"{MapToCppType(funcDecl.ReturnType ?? new TypeReference("void"))} {funcDecl.Name ?? "unnamedFunction"}(");
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
	/// <c>#pragma once</c> rather than an include guard. Every compiler this targets supports it, and
	/// a guard needs a macro name unique across the whole program — which the file cannot know it
	/// has, and which a generator picking one would eventually collide on.
	/// </remarks>
	protected override bool WriteFileDirectives(SourceFile file, CodeBlocker code)
	{
		Ensure.NotNull(file);
		Ensure.NotNull(code);

		if (!file.IsHeader)
		{
			return false;
		}

		code.WriteLine("#pragma once");
		return true;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// An import that already carries its own delimiters is written as it stands, because the choice
	/// between <c>&lt;&gt;</c> and <c>""</c> says where the compiler should look and only whoever
	/// wrote the file knows that. One that carries neither is quoted, which is right for a path
	/// within the project being generated.
	/// </remarks>
	protected override string? SpellImport(string import)
	{
		Ensure.NotNull(import);

		bool delimited = (import.StartsWith('<') && import.EndsWith('>'))
			|| (import.StartsWith('"') && import.EndsWith('"'));

		return delimited ? $"#include {import}" : $"#include \"{import}\"";
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The members are not indented. A namespace usually wraps a whole file, so indenting for it
	/// would indent everything and buy nothing; the closing brace names what it closes instead, which
	/// is what tells a reader at the bottom of a long file which one just ended.
	/// </remarks>
	protected override void GenerateNamespaceDeclaration(NamespaceDeclaration namespaceDecl, CodeBlocker code)
	{
		Ensure.NotNull(namespaceDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(namespaceDecl, code);

		string name = string.Join("::", NamespaceDeclaration.Split(namespaceDecl.Name));
		code.WriteLine($"namespace {name}");
		code.WriteLine("{");
		code.NewLine();

		bool first = true;
		foreach (AstNode member in namespaceDecl.Members)
		{
			if (!first)
			{
				code.NewLine();
			}

			first = false;
			GenerateInternal(member, code);
		}

		code.NewLine();
		code.WriteLine($"}}  // namespace {name}");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Members are grouped under the access label each one asks for, and a member with no visibility
	/// of its own lands under <c>public:</c>. A C++ class defaults to private, so a generated class
	/// with no access specifier at all would compile to something nothing outside it could use.
	/// <para>
	/// <see cref="Visibility.Internal"/> becomes <c>public:</c>: C++ has no assembly to be internal
	/// to, and the nearest thing — a friend declaration — names the code it trusts rather than
	/// describing a scope.
	/// </para>
	/// </remarks>
	protected override void GenerateClassDeclaration(ClassDeclaration classDecl, CodeBlocker code)
	{
		Ensure.NotNull(classDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(classDecl, code);

		// A struct's members are public already, so labelling them would be noise. An interface has
		// no keyword in C++ and is a class whose members are all public.
		bool isStruct = classDecl.Kind == TypeDeclarationKind.Struct;

		code.Write($"{(isStruct ? "struct" : "class")} {classDecl.Name ?? "UnnamedClass"}");

		if (classDecl.BaseType is TypeReference baseType)
		{
			code.Write($" : public {MapToCppType(baseType)}");
		}

		code.WriteLine();

		// A C++ class declaration is a statement, so its closing brace takes a semicolon.
		using ScopeWithTrailingSemicolon body = new(code);

		// Unspecified rather than Public, so the first member of a class always writes its label: an
		// unlabelled C++ class body is private, which is the one thing the label has to rule out.
		Visibility current = isStruct ? Visibility.Public : Visibility.Unspecified;
		bool first = true;
		foreach (AstNode member in classDecl.Members)
		{
			// Members are separated by a blank line. A documented one needs it or its first comment
			// line butts against the member above and reads as belonging to that one; an undocumented
			// one needs it to stay in the same column of whitespace as its neighbours, rather than
			// packing tight wherever a comment happens to be missing.
			if (!first)
			{
				// NewLine rather than WriteLine: a separator carrying the current indent is a line of
				// trailing whitespace, which every formatter strips and every diff then shows.
				code.NewLine();
			}

			first = false;

			Visibility access = classDecl.Kind == TypeDeclarationKind.Interface
				? Visibility.Public
				: AccessOf(member);

			if (access != current)
			{
				code.WriteLine($"{SpellVisibility(access)}:");
				current = access;
			}

			if (member is VariableDeclaration field)
			{
				GenerateField(field, code);
			}
			else
			{
				GenerateInternal(member, code);
			}
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Always <c>enum class</c>, never the unscoped form: an unscoped enumeration leaks its members
	/// into the surrounding scope and converts to an integer without being asked, and neither is
	/// something a generated type should do to the code around it.
	/// </remarks>
	protected override void GenerateEnumDeclaration(EnumDeclaration enumDecl, CodeBlocker code)
	{
		Ensure.NotNull(enumDecl);
		Ensure.NotNull(code);

		GenerateDocumentation(enumDecl, code);

		code.Write($"enum class {enumDecl.Name ?? "UnnamedEnum"}");

		if (enumDecl.UnderlyingType is TypeReference underlying)
		{
			code.Write($" : {MapToCppType(underlying)}");
		}

		code.WriteLine();

		using ScopeWithTrailingSemicolon body = new(code);
		foreach (EnumMember member in enumDecl.Members)
		{
			code.Write(member.Name ?? "Unnamed");

			if (member.Value is not null)
			{
				code.Write($" = {member.Value}");
			}

			// A trailing comma on the last member too, so adding one after it is a one-line diff.
			code.WriteLine(",");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A field with no initialiser is written <c>{}</c> rather than left bare. An uninitialised
	/// member holds whatever was in that memory, and a generated type is usually one whose values
	/// come from a file or the wire — so the one place it must be right is the case nobody wrote
	/// anything for.
	/// </remarks>
	protected override void GenerateFieldDeclaration(FieldDeclaration field, CodeBlocker code)
	{
		Ensure.NotNull(field);
		Ensure.NotNull(code);

		GenerateDocumentation(field, code);

		code.Write($"{MapToCppType(field.Type ?? new TypeReference("object"))} {field.Name}");

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}
		else
		{
			code.Write("{}");
		}

		EndStatement(code);
	}

	/// <summary>
	/// Maps a member's visibility onto the access label C++ would put it under.
	/// </summary>
	/// <param name="member">The member to place.</param>
	/// <returns>The visibility whose label the member belongs beneath.</returns>
	private static Visibility AccessOf(AstNode member) => VisibilityOf(member) switch
	{
		Visibility.Protected => Visibility.Protected,
		Visibility.Private => Visibility.Private,
		_ => Visibility.Public,
	};

	/// <summary>
	/// Emits a variable declaration as a class member.
	/// </summary>
	/// <param name="field">The declaration to emit.</param>
	/// <param name="code">The writer to emit into.</param>
	/// <remarks>
	/// A constant member is emitted as <c>static constexpr</c>. A plain <c>const</c> member is a
	/// per-instance value initialised once per object, which is not what a constant means; the
	/// <c>static constexpr</c> spelling is the one that gives the class a single compile-time value,
	/// and it is available because a constant declared here is initialised from a literal.
	/// </remarks>
	private void GenerateField(VariableDeclaration field, CodeBlocker code)
	{
		if (field.IsConstant && field.InitialValue is not null)
		{
			code.Write("static constexpr ");
		}
		else if (field.IsConstant)
		{
			code.Write("const ");
		}

		code.Write($"{GetDeclaredType(field)} {field.Name}");

		if (field.InitialValue is not null)
		{
			code.Write(" = ");
			GenerateInternal(field.InitialValue, code);
		}

		EndStatement(code);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// C++'s <c>main</c> returns <c>int</c> whether or not the program means to hand back an exit
	/// code, so <see cref="EntryPoint.ReturnsExitCode"/> changes nothing in the signature — a program
	/// that returns nothing exits with zero, which the standard supplies by falling off the end.
	/// </remarks>
	protected override void GenerateEntryPoint(EntryPoint entryPoint, CodeBlocker code)
	{
		Ensure.NotNull(entryPoint);
		Ensure.NotNull(code);

		code.Write("int main(");

		if (entryPoint.AcceptsArguments)
		{
			code.Write("int argc, char* argv[]");
		}

		// The line is ended before the scope opens, so C++'s brace lands on its own line.
		code.WriteLine(")");

		using Scope body = new(code);
		foreach (AstNode statement in entryPoint.Body)
		{
			GenerateInternal(statement, code);
		}
	}

	/// <inheritdoc/>
	protected override void GenerateParameter(Parameter parameter, CodeBlocker code, int position)
	{
		Ensure.NotNull(parameter);
		Ensure.NotNull(code);

		code.Write($"{MapToCppType(parameter.Type ?? new TypeReference("object"))} {parameter.Name ?? $"param{position}"}");
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
		if (!varDecl.IsTypeInferred && varDecl.Type is TypeReference declared)
		{
			return MapToCppType(declared);
		}

		return varDecl.InitialValue is not null ? "auto" : "std::any";
	}

	/// <summary>
	/// What a container named without arguments is a container of.
	/// </summary>
	/// <remarks>
	/// <c>list</c> comes from languages that do not say what is in one, and C++ insists. These are
	/// keyed by the name as written rather than by the mapped one, because that is what the schema
	/// said. A <c>list&lt;int&gt;</c> is a <c>std::vector&lt;int&gt;</c> and never reaches here.
	/// </remarks>
	private static readonly Dictionary<string, string> DefaultTypeArguments = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "list", "<std::any>" },
		{ "dict", "<std::string, std::any>" }
	};

	/// <summary>
	/// Spells a type in C++.
	/// </summary>
	/// <param name="type">The type to spell.</param>
	/// <returns>The C++ source for it.</returns>
	/// <remarks>
	/// Only the name is mapped; the shape around it — arguments, <c>const</c>, <c>&amp;</c> and
	/// <c>*</c> — is C++'s own spelling of what the type says, which is what the string form could
	/// not express.
	/// </remarks>
	private static string MapToCppType(TypeReference type)
	{
		string name = TypeMappings.TryGetValue(type.Name, out string? mapped) ? mapped : type.Name;

		string arguments = SpellTypeArguments(type);

		string indirection = type.Indirection switch
		{
			TypeIndirection.Reference => "&",
			TypeIndirection.Pointer => "*",
			_ => string.Empty,
		};

		return $"{(type.IsReadOnly ? "const " : string.Empty)}{name}{arguments}{indirection}";
	}

	/// <summary>
	/// Spells a type's argument list, supplying the one a bare container does not name.
	/// </summary>
	/// <param name="type">The type whose arguments to spell.</param>
	/// <returns>The angle-bracketed list, or nothing when the type takes no arguments.</returns>
	private static string SpellTypeArguments(TypeReference type)
	{
		if (type.TypeArguments.Count > 0)
		{
			return $"<{string.Join(", ", type.TypeArguments.Select(MapToCppType))}>";
		}

		return DefaultTypeArguments.TryGetValue(type.Name, out string? fallback) ? fallback : string.Empty;
	}
}
