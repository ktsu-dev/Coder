// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Builds the reflection table Holotype's <c>generate_reflection.cpp</c> emits and checks that each
/// generator writes it in its own language.
/// </summary>
/// <remarks>
/// <c>docs/generated-cpp-target.md</c> says the other backends "become the same exercise once the
/// vocabulary exists". They did not: a table of rows needs four things a header of structs does not
/// — an array type, a field whose value is fixed where it is written, a braced list with no type
/// name in front of it, and an element that says which member it is for. This is the test that says
/// the AST can express all four, and it is written against the real table rather than a toy so that
/// it fails when a generated table would.
/// <para>
/// What it does not cover is the <c>template&lt;&gt; struct Describe&lt;T&gt;</c> specialisation that
/// sits below the table in that file. An explicit specialisation is C++ and only C++, and whether
/// this AST should learn it or the generator should write it — the same question
/// <c>static_assert</c> raised and the same answer available — is open.
/// </para>
/// </remarks>
[TestClass]
public class ExemplarReflectionTableTests
{
	/// <summary>The table as C++ wants it: a namespace-scope constant array of designated rows.</summary>
	private const string ExpectedCpp =
		"""
		#include <cstddef>
		#include "holotype/core/reflect.hpp"

		namespace holo::components::reflection
		{

		/// Every field RigidBody declares, in declaration order.
		inline constexpr holo::reflect::FieldInfo kRigidBodyFields[] = {
		    holo::reflect::FieldInfo{ .name = "velocity", .offset = offsetof(holo::components::RigidBody, velocity), .lerp = true },
		    holo::reflect::FieldInfo{ .name = "mass", .offset = offsetof(holo::components::RigidBody, mass), .lerp = false },
		};

		}  // namespace holo::components::reflection
		""";

	/// <summary>
	/// Builds the table.
	/// </summary>
	/// <returns>The file holding it.</returns>
	private static SourceFile ReflectionTable()
	{
		SourceFile file = new() { Name = "RigidBody.reflect.gen" };
		file.Imports.Add("<cstddef>");
		file.Imports.Add("holotype/core/reflect.hpp");

		ConstructionExpression table = new();
		table.Arguments.Add(Row("velocity", true));
		table.Arguments.Add(Row("mass", false));

		NamespaceDeclaration declaration = new("holo::components::reflection");
		declaration.Members.Add(new FieldDeclaration(
			"kRigidBodyFields",
			new TypeReference("holo::reflect::FieldInfo") { IsArray = true })
		{
			IsConstant = true,
			InitialValue = table,
			Documentation = { "Every field RigidBody declares, in declaration order." },
		});

		file.Members.Add(declaration);
		return file;
	}

	/// <summary>
	/// Builds one row of the table.
	/// </summary>
	/// <param name="name">The field the row describes.</param>
	/// <param name="lerp">Whether that field is interpolated between states.</param>
	/// <returns>The row.</returns>
	private static ConstructionExpression Row(string name, bool lerp)
	{
		ConstructionExpression row = new() { Type = new TypeReference("holo::reflect::FieldInfo") };
		row.Arguments.Add(new MemberInitialiser("name", Literal.Text(name)));
		row.Arguments.Add(new MemberInitialiser("offset",
			new VariableReference($"offsetof(holo::components::RigidBody, {name})")));
		row.Arguments.Add(new MemberInitialiser("lerp", Literal.Bool(lerp)));
		return row;
	}

	/// <summary>
	/// The whole table, which is the point of all four additions at once.
	/// </summary>
	[TestMethod]
	public void Cpp_GeneratesTheTableTheReflectionGeneratorEmits() =>
		Assert.AreEqual(
			ExpectedCpp.ReplaceLineEndings("\n").TrimEnd(),
			new CppGenerator().Generate(ReflectionTable()).ReplaceLineEndings("\n").TrimEnd());

	/// <summary>
	/// A constant at namespace scope has to say <c>inline</c> or every translation unit including
	/// the header defines it again.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesANamespaceScopeConstantInline() =>
		Assert.Contains("inline constexpr", new CppGenerator().Generate(ReflectionTable()));

	/// <summary>
	/// A static data member is already implicitly inline, so inside a type the same field says
	/// <c>static</c> instead — which is the only reason the generator tracks where it is.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesAMemberConstantStatic()
	{
		ClassDeclaration type = new() { Name = "Registry", Kind = TypeDeclarationKind.Struct };
		type.Members.Add(new FieldDeclaration("kCount", new TypeReference("int"))
		{
			IsConstant = true,
			InitialValue = Literal.Number(2),
		});

		string code = new CppGenerator().Generate(type);

		Assert.Contains("static constexpr", code);
		Assert.DoesNotContain("inline", code);
	}

	/// <summary>
	/// C++ puts an array's brackets on the name being declared, not on the type. Everything else
	/// here puts them on the type, which is why the AST says only that it is an array.
	/// </summary>
	[TestMethod]
	public void ArrayBracketsGoWhereEachLanguagePutsThem()
	{
		Assert.Contains("FieldInfo kRigidBodyFields[]", new CppGenerator().Generate(ReflectionTable()));
		Assert.Contains("FieldInfo[] kRigidBodyFields", new CSharpGenerator().Generate(ReflectionTable()));
		Assert.Contains("list[", new PythonGenerator().Generate(ReflectionTable()));
	}

	/// <summary>
	/// C# has no designated initialiser; an object initialiser is the same idea and the same order
	/// freedom, and <c>static readonly</c> is what <c>constexpr</c> means where <c>const</c> is
	/// reserved for primitives.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesAnObjectInitialiserAndAStaticReadonlyField()
	{
		string code = new CSharpGenerator().Generate(ReflectionTable());

		Assert.Contains("static readonly", code);
		Assert.Contains("new holo::reflect::FieldInfo { name = \"velocity\"", code);
	}

	/// <summary>
	/// Python's keyword argument is the designated initialiser, so the names survive the trip
	/// rather than collapsing into position.
	/// </summary>
	[TestMethod]
	public void Python_WritesKeywordArguments() =>
		Assert.Contains("name=\"velocity\"", new PythonGenerator().Generate(ReflectionTable()));

	/// <summary>
	/// JavaScript has nothing that names an argument's member, so the names go into an object
	/// passed to the constructor. That is a convention rather than a translation, and it is the
	/// only shape here that keeps them at all.
	/// </summary>
	[TestMethod]
	public void JavaScript_WritesAnObjectLiteral() =>
		Assert.Contains("{ name: \"velocity\"", new JavaScriptGenerator().Generate(ReflectionTable()));

	/// <summary>
	/// A list of values is a value; a list of lists is a table. Only the second is worth breaking
	/// across lines, and a table that is not is a diff nobody can read.
	/// </summary>
	[TestMethod]
	public void ABracedListOfPlainValuesStaysOnOneLine()
	{
		ConstructionExpression list = new();
		list.Arguments.Add(Literal.Number(1));
		list.Arguments.Add(Literal.Number(2));

		NamespaceDeclaration declaration = new("holo");
		declaration.Members.Add(new FieldDeclaration("kSteps", new TypeReference("int") { IsArray = true })
		{
			IsConstant = true,
			InitialValue = list,
		});

		Assert.Contains("kSteps[] = { 1, 2 }", new CppGenerator().Generate(declaration));
	}
}
