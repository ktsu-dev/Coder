// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="ClassDeclaration"/>: what it holds, how each language spells it, and that it
/// survives a round trip through YAML.
/// </summary>
/// <remarks>
/// A class is the first AST node that holds declarations rather than statements, so these cover the
/// whole path a new node type has to work along — the schema the editor walks it with, the four
/// generators, and the serializer the editor saves through.
/// </remarks>
[TestClass]
public class ClassDeclarationTests
{
	/// <summary>
	/// Builds a class with a field and a method, which is the shape every test here uses.
	/// </summary>
	/// <returns>The class.</returns>
	private static ClassDeclaration SampleClass()
	{
		ClassDeclaration declaration = new("Point") { BaseType = "Shape" };
		declaration.Members.Add(new VariableDeclaration("x", "int", Literal.Number(0)));

		FunctionDeclaration area = new("area") { ReturnType = "int" };
		area.Parameters.Add(new Parameter("scale", "int"));
		area.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("x"), BinaryOperator.Multiply, new VariableReference("scale"))));
		declaration.Members.Add(area);

		return declaration;
	}

	/// <summary>
	/// Tests that a clone copies the class and everything in it, rather than sharing the members.
	/// </summary>
	[TestMethod]
	public void Clone_CopiesMembersRatherThanSharingThem()
	{
		ClassDeclaration original = SampleClass();

		ClassDeclaration clone = (ClassDeclaration)original.Clone();

		Assert.AreEqual("Point", clone.Name);
		Assert.AreEqual("Shape", clone.BaseType);
		Assert.AreEqual(original.Members.Count, clone.Members.Count);
		Assert.AreNotSame(original.Members[0], clone.Members[0]);

		((VariableDeclaration)clone.Members[0]).Name = "y";
		Assert.AreEqual("x", ((VariableDeclaration)original.Members[0]).Name);
	}

	/// <summary>
	/// Tests that the node reports the type name the serializer writes and reads it back under.
	/// </summary>
	[TestMethod]
	public void NodeTypeName_IsClassDeclaration() =>
		Assert.AreEqual("ClassDeclaration", new ClassDeclaration("Point").GetNodeTypeName());

	/// <summary>
	/// Tests that the editor sees a class as one slot holding its members, and that the slot takes
	/// the declarations a class can hold and refuses what it cannot.
	/// </summary>
	[TestMethod]
	public void Schema_ExposesMembersAsOneSlot()
	{
		ClassDeclaration declaration = SampleClass();
		AstSlot members = AstSchema.SlotsOf(declaration).Single();

		Assert.AreEqual("Members", members.Name);
		Assert.AreEqual(AstSlotCardinality.Many, members.Cardinality);
		Assert.AreEqual(2, AstSchema.ChildrenOf(declaration, members).Count);

		Assert.IsTrue(AstSchema.Accepts(members, new FunctionDeclaration("method")));
		Assert.IsTrue(AstSchema.Accepts(members, new VariableDeclaration("field", "int")));
		Assert.IsTrue(AstSchema.Accepts(members, new ClassDeclaration("Nested")));
		Assert.IsFalse(AstSchema.Accepts(members, Literal.Number(1)), "a literal is not a member");
		Assert.IsFalse(AstSchema.Accepts(members, new Parameter("value", "int")), "a parameter is not a member");
	}

	/// <summary>
	/// Tests that a class can be edited through the graph: a member is attached, detached, and its
	/// position within the sequence is kept.
	/// </summary>
	[TestMethod]
	public void Schema_AttachesAndDetachesMembers()
	{
		ClassDeclaration declaration = new("Point");
		AstSlot members = AstSchema.SlotsOf(declaration).Single();
		FunctionDeclaration first = new("first");
		FunctionDeclaration second = new("second");

		Assert.IsTrue(AstSchema.TryAttach(declaration, members, first));
		Assert.IsTrue(AstSchema.TryAttach(declaration, members, second));
		Assert.AreEqual(2, declaration.Members.Count);

		// Attaching inside the sequence replaces in place rather than appending, which is what keeps a
		// class's members in the order the user arranged them.
		FunctionDeclaration replacement = new("replacement");
		Assert.IsTrue(AstSchema.TryAttachAt(declaration, members, 0, replacement));
		Assert.AreSame(replacement, declaration.Members[0]);
		Assert.AreSame(second, declaration.Members[1]);

		Assert.IsTrue(AstSchema.TryDetachAt(declaration, members, 1));
		Assert.AreEqual(1, declaration.Members.Count);
	}

	/// <summary>
	/// Tests the caption the editor draws on a class node.
	/// </summary>
	[TestMethod]
	public void Describe_NamesTheClass()
	{
		Assert.AreEqual("class Point", AstSchema.Describe(new ClassDeclaration("Point")));
		Assert.AreEqual("class <unnamed>", AstSchema.Describe(new ClassDeclaration()));
	}

	/// <summary>
	/// Tests that C# spells a class with its base type and members.
	/// </summary>
	[TestMethod]
	public void CSharp_GeneratesAClass()
	{
		string code = new CSharpGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "public class Point : Shape", StringComparison.Ordinal);
		StringAssert.Contains(code, "    int x = 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "    public int area(int scale)", StringComparison.Ordinal);
		StringAssert.Contains(code, "        return (x * scale);", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the access modifier a class carries is the one emitted, and that a class without one
	/// is public rather than nothing at all.
	/// </summary>
	[TestMethod]
	public void CSharp_UsesTheDeclaredAccessModifier()
	{
		ClassDeclaration declaration = new("Point") { AccessModifier = "internal" };

		StringAssert.StartsWith(new CSharpGenerator().Generate(declaration), "internal class Point", StringComparison.Ordinal);
		StringAssert.StartsWith(new CSharpGenerator().Generate(new ClassDeclaration("Point")), "public class Point", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that C++ derives publicly and terminates the declaration with a semicolon, without which
	/// the output would not compile.
	/// </summary>
	[TestMethod]
	public void Cpp_GeneratesAClass()
	{
		string code = new CppGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "class Point : public Shape", StringComparison.Ordinal);
		StringAssert.Contains(code, "public:", StringComparison.Ordinal);
		StringAssert.Contains(code, "};", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that Python names the base class in parentheses and gives each method its receiver.
	/// </summary>
	[TestMethod]
	public void Python_GeneratesAClass()
	{
		string code = new PythonGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "class Point(Shape):", StringComparison.Ordinal);
		StringAssert.Contains(code, "def area(self, scale: int) -> int:", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an empty Python class is given a body, since a suite may not be empty.
	/// </summary>
	[TestMethod]
	public void Python_GivesAnEmptyClassAPass()
	{
		string code = new PythonGenerator().Generate(new ClassDeclaration("Empty"));

		StringAssert.Contains(code, "class Empty:", StringComparison.Ordinal);
		StringAssert.Contains(code, "pass", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that JavaScript extends its base class and writes members as class syntax rather than as
	/// the statements they are elsewhere.
	/// </summary>
	[TestMethod]
	public void JavaScript_GeneratesAClass()
	{
		string code = new JavaScriptGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "class Point extends Shape", StringComparison.Ordinal);
		StringAssert.Contains(code, "    area(scale)", StringComparison.Ordinal);

		// `function` and `let` are both syntax errors inside a class body.
		Assert.IsFalse(code.Contains("function area", StringComparison.Ordinal), code);
		Assert.IsFalse(code.Contains("let x", StringComparison.Ordinal), code);
	}

	/// <summary>
	/// Tests that every generator accepts a class, so choosing a language in the editor never turns
	/// the preview into a refusal.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_AcceptsAClass()
	{
		ILanguageGenerator[] generators =
			[new CSharpGenerator(), new PythonGenerator(), new CppGenerator(), new JavaScriptGenerator()];

		foreach (ILanguageGenerator generator in generators)
		{
			Assert.IsTrue(generator.CanGenerate(SampleClass()), generator.DisplayName);
		}
	}

	/// <summary>
	/// Tests that a class survives being written to YAML and read back, which is what saving and
	/// opening a class document does.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsAClass()
	{
		ClassDeclaration original = SampleClass();

		string yaml = new YamlSerializer().Serialize(original);
		AstNode? restored = new YamlDeserializer().Deserialize(yaml);

		Assert.IsInstanceOfType<ClassDeclaration>(restored);
		ClassDeclaration declaration = (ClassDeclaration)restored;

		Assert.AreEqual("Point", declaration.Name);
		Assert.AreEqual("Shape", declaration.BaseType);
		Assert.AreEqual(2, declaration.Members.Count);
		Assert.IsInstanceOfType<VariableDeclaration>(declaration.Members[0]);
		Assert.IsInstanceOfType<FunctionDeclaration>(declaration.Members[1]);

		// The strongest statement of a round trip: the same source comes out the other side.
		Assert.AreEqual(new CSharpGenerator().Generate(original), new CSharpGenerator().Generate(declaration));
	}

	/// <summary>
	/// Tests that a nested class is carried through as well, since the members slot takes one.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsANestedClass()
	{
		ClassDeclaration outer = new("Outer");
		outer.Members.Add(new ClassDeclaration("Inner") { AccessModifier = "private" });

		string yaml = new YamlSerializer().Serialize(outer);
		ClassDeclaration restored = (ClassDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.IsInstanceOfType<ClassDeclaration>(restored.Members.Single());
		Assert.AreEqual("Inner", ((ClassDeclaration)restored.Members[0]).Name);
		Assert.AreEqual("private", ((ClassDeclaration)restored.Members[0]).AccessModifier);
	}
}
