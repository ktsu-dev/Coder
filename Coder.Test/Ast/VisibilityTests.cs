// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="Visibility"/>: what each declaration carries, how each language spells it,
/// and that it survives a round trip through YAML.
/// </summary>
/// <remarks>
/// Visibility is the first thing the AST models that no two languages spell the same way — a C#
/// keyword, a C++ access label, a JavaScript name prefix, and nothing at all in Python — so these
/// cover each spelling rather than asserting one shape four times.
/// </remarks>
[TestClass]
public class VisibilityTests
{
	/// <summary>
	/// Builds a class whose three members ask for three different visibilities.
	/// </summary>
	/// <returns>The class.</returns>
	private static ClassDeclaration SampleClass()
	{
		ClassDeclaration declaration = new("Point");
		declaration.Members.Add(new VariableDeclaration("origin", "int", Literal.Number(0)) { Visibility = Visibility.Public });
		declaration.Members.Add(new VariableDeclaration("x", "int", Literal.Number(0)) { Visibility = Visibility.Private });

		FunctionDeclaration area = new("area") { ReturnType = "int", Visibility = Visibility.Protected };
		area.Body.Add(new ReturnStatement(new VariableReference("x")));
		declaration.Members.Add(area);

		return declaration;
	}

	/// <summary>
	/// Tests that a declaration nobody has given a visibility to carries none, rather than silently
	/// acquiring one the author never asked for.
	/// </summary>
	[TestMethod]
	public void Unspecified_IsWhatADeclarationStartsWith()
	{
		Assert.AreEqual(Visibility.Unspecified, new ClassDeclaration("Point").Visibility);
		Assert.AreEqual(Visibility.Unspecified, new FunctionDeclaration("area").Visibility);
		Assert.AreEqual(Visibility.Unspecified, new VariableDeclaration("x", "int").Visibility);
	}

	/// <summary>
	/// Tests that a clone carries the visibility over, since a copied declaration that quietly became
	/// public would be a copy of something else.
	/// </summary>
	[TestMethod]
	public void Clone_CarriesTheVisibility()
	{
		ClassDeclaration classClone = (ClassDeclaration)new ClassDeclaration("Point") { Visibility = Visibility.Internal }.Clone();
		FunctionDeclaration functionClone = (FunctionDeclaration)new FunctionDeclaration("area") { Visibility = Visibility.Protected }.Clone();
		VariableDeclaration variableClone = (VariableDeclaration)new VariableDeclaration("x", "int") { Visibility = Visibility.Private }.Clone();

		Assert.AreEqual(Visibility.Internal, classClone.Visibility);
		Assert.AreEqual(Visibility.Protected, functionClone.Visibility);
		Assert.AreEqual(Visibility.Private, variableClone.Visibility);
	}

	/// <summary>
	/// Tests that C# writes the keyword for each visibility, and that a member without one is public
	/// rather than carrying no modifier at all.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesTheKeyword()
	{
		string code = new CSharpGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "public int origin = 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "private int x = 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "protected int area()", StringComparison.Ordinal);
		StringAssert.Contains(new CSharpGenerator().Generate(new FunctionDeclaration("area") { ReturnType = "int" }), "public int area()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a local variable — one with no visibility — is emitted without a modifier, since a
	/// modifier on a local does not compile.
	/// </summary>
	[TestMethod]
	public void CSharp_LeavesALocalUnmodified()
	{
		FunctionDeclaration function = new("run") { ReturnType = "void" };
		function.Body.Add(new VariableDeclaration("total", "int", Literal.Number(0)));

		StringAssert.Contains(new CSharpGenerator().Generate(function), "int total = 0;", StringComparison.Ordinal);
		Assert.IsFalse(new CSharpGenerator().Generate(function).Contains("public int total", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that C++ groups the members under the access label each one asks for, which is how C++
	/// spells visibility.
	/// </summary>
	[TestMethod]
	public void Cpp_GroupsMembersUnderAccessLabels()
	{
		string code = new CppGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "public:", StringComparison.Ordinal);
		StringAssert.Contains(code, "private:", StringComparison.Ordinal);
		StringAssert.Contains(code, "protected:", StringComparison.Ordinal);

		// The label has to come before the member it governs, not after it.
		Assert.IsTrue(code.IndexOf("private:", StringComparison.Ordinal) < code.IndexOf("int x = 0;", StringComparison.Ordinal));
		Assert.IsTrue(code.IndexOf("protected:", StringComparison.Ordinal) < code.IndexOf("int area()", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a member with no visibility of its own lands under <c>public:</c>, since an
	/// unlabelled C++ class body is private and nothing outside the class could reach it.
	/// </summary>
	[TestMethod]
	public void Cpp_PutsAnUnspecifiedMemberUnderPublic()
	{
		ClassDeclaration declaration = new("Point");
		declaration.Members.Add(new VariableDeclaration("x", "int", Literal.Number(0)));

		string code = new CppGenerator().Generate(declaration);

		Assert.IsTrue(code.IndexOf("public:", StringComparison.Ordinal) < code.IndexOf("int x = 0;", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that C++ puts an internal member under <c>public:</c>, since C++ has no assembly for a
	/// declaration to be internal to.
	/// </summary>
	[TestMethod]
	public void Cpp_TreatsInternalAsPublic()
	{
		ClassDeclaration declaration = new("Point");
		declaration.Members.Add(new VariableDeclaration("x", "int", Literal.Number(0)) { Visibility = Visibility.Internal });

		string code = new CppGenerator().Generate(declaration);

		StringAssert.Contains(code, "public:", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("internal", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that JavaScript spells a private member with the <c>#</c> prefix its own private syntax
	/// uses, and leaves the others as ordinary members.
	/// </summary>
	[TestMethod]
	public void JavaScript_SpellsAPrivateMemberWithAHash()
	{
		string code = new JavaScriptGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "#x = 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "origin = 0;", StringComparison.Ordinal);

		// protected has no JavaScript spelling, so the method is an ordinary one.
		StringAssert.Contains(code, "area() {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a private method is spelled with the prefix too, since JavaScript's <c>#</c> applies
	/// to any class member rather than to fields alone.
	/// </summary>
	[TestMethod]
	public void JavaScript_SpellsAPrivateMethodWithAHash()
	{
		ClassDeclaration declaration = new("Point");
		declaration.Members.Add(new FunctionDeclaration("recompute") { ReturnType = "void", Visibility = Visibility.Private });

		StringAssert.Contains(new JavaScriptGenerator().Generate(declaration), "#recompute() {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that Python drops visibility rather than renaming the declaration, since its convention
	/// for a non-public member is a spelling of the identifier and renaming one here would leave every
	/// reference to it naming something that no longer exists.
	/// </summary>
	[TestMethod]
	public void Python_DropsVisibilityRatherThanRenaming()
	{
		string code = new PythonGenerator().Generate(SampleClass());

		StringAssert.Contains(code, "x = 0", StringComparison.Ordinal);
		StringAssert.Contains(code, "def area(self) -> int:", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("_x", StringComparison.Ordinal));
		Assert.IsFalse(code.Contains("private", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that visibility survives a round trip through YAML, on each of the declarations that can
	/// carry one.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsVisibility()
	{
		ClassDeclaration original = SampleClass();
		original.Visibility = Visibility.Internal;

		string yaml = new YamlSerializer().Serialize(original);
		ClassDeclaration restored = (ClassDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.AreEqual(Visibility.Internal, restored.Visibility);
		Assert.AreEqual(Visibility.Public, ((VariableDeclaration)restored.Members[0]).Visibility);
		Assert.AreEqual(Visibility.Private, ((VariableDeclaration)restored.Members[1]).Visibility);
		Assert.AreEqual(Visibility.Protected, ((FunctionDeclaration)restored.Members[2]).Visibility);
	}

	/// <summary>
	/// Tests that a declaration with no visibility writes no key at all, so the document says nothing
	/// rather than saying "unspecified".
	/// </summary>
	[TestMethod]
	public void Yaml_WritesNothingForAnUnspecifiedVisibility()
	{
		string yaml = new YamlSerializer().Serialize(new ClassDeclaration("Point"));

		Assert.IsFalse(yaml.Contains("visibility", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a document written before visibility became an enumeration still opens, since a
	/// saved document should not stop loading because the library changed how it models the same idea.
	/// </summary>
	[TestMethod]
	public void Yaml_ReadsTheOlderAccessModifierSpelling()
	{
		const string yaml = """
			classDeclaration:
			  name: Point
			  accessModifier: internal
			""";

		ClassDeclaration restored = (ClassDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.AreEqual(Visibility.Internal, restored.Visibility);
	}

	/// <summary>
	/// Tests that the inspector offers visibility on every declaration that can carry one, and writes
	/// the choice the user picked.
	/// </summary>
	[TestMethod]
	public void Fields_OfferVisibilityOnEveryDeclaration()
	{
		ClassDeclaration classDecl = new("Point");
		FunctionDeclaration function = new("area") { ReturnType = "int" };
		VariableDeclaration variable = new("x", "int");

		Assert.IsTrue(AstFields.TryWrite(classDecl, "Visibility", "Internal"));
		Assert.IsTrue(AstFields.TryWrite(function, "Visibility", "Protected"));
		Assert.IsTrue(AstFields.TryWrite(variable, "Visibility", "Private"));

		Assert.AreEqual(Visibility.Internal, classDecl.Visibility);
		Assert.AreEqual(Visibility.Protected, function.Visibility);
		Assert.AreEqual(Visibility.Private, variable.Visibility);
	}

	/// <summary>
	/// Tests that the inspector offers each visibility as a choice, so it is picked from a menu rather
	/// than typed and misspelled.
	/// </summary>
	[TestMethod]
	public void Fields_ListEveryVisibilityAsAChoice()
	{
		AstField field = AstFields.Of(new ClassDeclaration("Point")).Single(f => f.Name == "Visibility");

		Assert.AreEqual(AstFieldKind.Choice, field.Kind);
		Assert.AreEqual(Enum.GetValues<Visibility>().Length, field.Choices.Count);
		Assert.IsTrue(field.Choices.Any(choice => choice.Value == nameof(Visibility.Internal)));
	}

	/// <summary>
	/// Tests that a value that names no visibility is refused, leaving the document alone rather than
	/// writing a default over what the user had.
	/// </summary>
	[TestMethod]
	public void Fields_RefuseAValueThatNamesNoVisibility()
	{
		ClassDeclaration classDecl = new("Point") { Visibility = Visibility.Internal };

		Assert.IsFalse(AstFields.TryWrite(classDecl, "Visibility", "friendly"));
		Assert.AreEqual(Visibility.Internal, classDecl.Visibility);
	}
}
