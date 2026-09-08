// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for a constant: a <see cref="VariableDeclaration"/> marked
/// <see cref="VariableDeclaration.IsConstant"/> and initialised from a literal.
/// </summary>
/// <remarks>
/// A constant is spelled two ways in each language that has one — as a local and as a class member —
/// and they are not the same spelling, so both are covered here rather than only the local one.
/// </remarks>
[TestClass]
public class ConstantDeclarationTests
{
	/// <summary>
	/// Builds a constant with a literal value.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static VariableDeclaration SampleConstant() =>
		new("MAX", "int", Literal.Number(10)) { IsConstant = true };

	/// <summary>
	/// Builds a class holding one constant member.
	/// </summary>
	/// <returns>The class.</returns>
	private static ClassDeclaration SampleClass()
	{
		ClassDeclaration declaration = new("Limits");
		declaration.Members.Add(SampleConstant());
		return declaration;
	}

	/// <summary>
	/// Tests that a clone stays constant, since a copy that quietly became writable would be a copy
	/// of something else.
	/// </summary>
	[TestMethod]
	public void Clone_StaysConstant()
	{
		VariableDeclaration clone = (VariableDeclaration)SampleConstant().Clone();

		Assert.IsTrue(clone.IsConstant);
		Assert.AreEqual(10, ((LiteralExpression<int>)clone.InitialValue!).Value);
	}

	/// <summary>
	/// Tests that C# writes <c>const</c> in front of the declaration, with the type a C# constant has
	/// to name.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesConst()
	{
		StringAssert.Contains(new CSharpGenerator().Generate(SampleConstant()), "const int MAX = 10;", StringComparison.Ordinal);
		StringAssert.Contains(new CSharpGenerator().Generate(SampleClass()), "const int MAX = 10;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that C++ writes <c>const</c> for a local and <c>static constexpr</c> for a class member,
	/// which is the spelling that gives the class one compile-time value rather than one per object.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesConstexprForAMember()
	{
		StringAssert.Contains(new CppGenerator().Generate(SampleConstant()), "const int MAX = 10;", StringComparison.Ordinal);
		StringAssert.Contains(new CppGenerator().Generate(SampleClass()), "static constexpr int MAX = 10;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constant member with nothing to initialise it stays a plain <c>const</c>, since
	/// <c>constexpr</c> without an initialiser does not compile.
	/// </summary>
	[TestMethod]
	public void Cpp_LeavesAnUninitialisedMemberAsConst()
	{
		ClassDeclaration declaration = new("Limits");
		declaration.Members.Add(new VariableDeclaration("MAX", "int") { IsConstant = true });

		string code = new CppGenerator().Generate(declaration);

		StringAssert.Contains(code, "const int MAX;", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("constexpr", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that JavaScript writes <c>const</c> for a local and <c>static</c> for a class member,
	/// since <c>const</c> declares a binding in a scope and a class body is not one.
	/// </summary>
	[TestMethod]
	public void JavaScript_WritesStaticForAMember()
	{
		StringAssert.Contains(new JavaScriptGenerator().Generate(SampleConstant()), "const MAX = 10;", StringComparison.Ordinal);
		StringAssert.Contains(new JavaScriptGenerator().Generate(SampleClass()), "static MAX = 10;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a private constant member is spelled with both, since the two are independent.
	/// </summary>
	[TestMethod]
	public void JavaScript_CombinesStaticWithAPrivateName()
	{
		ClassDeclaration declaration = new("Limits");
		declaration.Members.Add(new VariableDeclaration("MAX", "int", Literal.Number(10))
		{
			IsConstant = true,
			Visibility = Visibility.Private,
		});

		StringAssert.Contains(new JavaScriptGenerator().Generate(declaration), "static #MAX = 10;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that Python emits an ordinary assignment, since it has no constant declaration and the
	/// upper-case naming that stands in for one is a convention about the identifier rather than
	/// something the declaration can say.
	/// </summary>
	[TestMethod]
	public void Python_EmitsAnOrdinaryAssignment()
	{
		string code = new PythonGenerator().Generate(SampleConstant());

		StringAssert.Contains(code, "MAX = 10", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("const", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a constant survives a round trip through YAML, value and all.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsAConstant()
	{
		string yaml = new YamlSerializer().Serialize(SampleConstant());
		VariableDeclaration restored = (VariableDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.IsTrue(restored.IsConstant);
		Assert.AreEqual("MAX", restored.Name);
		Assert.AreEqual(10, ((LiteralExpression<int>)restored.InitialValue!).Value);
	}

	/// <summary>
	/// Tests that the palette offers a constant already holding a literal, so it is a node the user
	/// edits rather than one they have to wire a value into first.
	/// </summary>
	[TestMethod]
	public void Catalog_OffersAConstantHoldingALiteral()
	{
		AstNode created = AstNodeCatalog.Templates
			.Single(template => string.Equals(template.Label, "Constant", StringComparison.Ordinal))
			.Create();

		VariableDeclaration constant = (VariableDeclaration)created;

		Assert.IsTrue(constant.IsConstant);
		Assert.IsInstanceOfType<LiteralExpression<int>>(constant.InitialValue);
	}

	/// <summary>
	/// Tests that a constant with nothing in it is reported rather than generated, since a constant
	/// is the one declaration whose value is not optional.
	/// </summary>
	[TestMethod]
	public void Graph_ReportsAConstantWithNoValue()
	{
		VariableDeclaration constant = new("MAX", "int") { IsConstant = true };
		AstGraph graph = new(constant);

		Assert.IsTrue(graph.Validate().Any(problem => ReferenceEquals(problem.Node, constant)));
	}
}
