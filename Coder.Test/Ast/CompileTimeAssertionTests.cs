// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="CompileTimeAssertion"/>: what a generated type promises that the type itself
/// cannot say.
/// </summary>
/// <remarks>
/// Only C++ and C have anything checked before the program runs, so this is the clearest case of the
/// rule the whole AST follows — say what is true, and let each language say as much of it as it can.
/// The other three write a comment rather than dropping it, because a file that quietly loses a
/// guarantee looks exactly like one that still makes it.
/// </remarks>
[TestClass]
public class CompileTimeAssertionTests
{
	/// <summary>
	/// C++ writes the assertion, with the message on its own line.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesTheAssertion()
	{
		CompileTimeAssertion assertion = new(
			"std::is_trivially_copyable_v<RigidBody>",
			"RigidBody must be trivially copyable: it crosses the C# boundary and the wire as bytes");

		Assert.AreEqual(
			"static_assert(std::is_trivially_copyable_v<RigidBody>,\n"
			+ "    \"RigidBody must be trivially copyable: it crosses the C# boundary and the wire as bytes\");\n",
			new CppGenerator().Generate(assertion).ReplaceLineEndings("\n"));
	}

	/// <summary>
	/// An assertion with nothing to say when it fails is still an assertion.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesAnAssertionWithNoMessage()
	{
		Assert.AreEqual(
			"static_assert(sizeof(Handle) == 8);\n",
			new CppGenerator().Generate(new CompileTimeAssertion("sizeof(Handle) == 8")).ReplaceLineEndings("\n"));
	}

	/// <summary>
	/// A message with a quote in it is escaped rather than ending the string early.
	/// </summary>
	[TestMethod]
	public void Cpp_EscapesTheMessage()
	{
		CompileTimeAssertion assertion = new("sizeof(T) == 8", "a \"T\" is eight bytes");

		Assert.Contains("\\\"T\\\"", new CppGenerator().Generate(assertion), StringComparison.Ordinal);
	}

	/// <summary>
	/// C writes it too, under C11's own spelling — and always with a message, which C11 requires and
	/// which an assertion that carries none is given from its own condition.
	/// </summary>
	[TestMethod]
	public void C_WritesTheAssertionWithAMessageEitherWay()
	{
		Assert.AreEqual(
			"_Static_assert(sizeof(Handle) == 8,\n"
			+ "    \"sizeof(Handle) == 8\");\n",
			new CGenerator().Generate(new CompileTimeAssertion("sizeof(Handle) == 8")).ReplaceLineEndings("\n"));
	}

	/// <summary>
	/// The other three have nothing checked before the program runs, so they say what was asserted
	/// rather than dropping it.
	/// </summary>
	[TestMethod]
	public void OtherLanguages_SayWhatWasAsserted()
	{
		CompileTimeAssertion assertion = new("std::is_standard_layout_v<RigidBody>", "layout must be stable");

		Assert.Contains(
			"// asserted at build time: std::is_standard_layout_v<RigidBody>",
			new CSharpGenerator().Generate(assertion),
			StringComparison.Ordinal);
		Assert.Contains(
			"# asserted at build time: std::is_standard_layout_v<RigidBody>",
			new PythonGenerator().Generate(assertion),
			StringComparison.Ordinal);
		Assert.Contains(
			"// asserted at build time: std::is_standard_layout_v<RigidBody>",
			new JavaScriptGenerator().Generate(assertion),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Assertions about one type stay together, and are separated from the declaration they are about.
	/// </summary>
	/// <remarks>
	/// The same rule the members of a type follow. Two of a kind that say nothing about themselves are
	/// one block; a struct followed by an assertion is two.
	/// </remarks>
	[TestMethod]
	public void Cpp_GroupsAssertionsAboutTheSameType()
	{
		SourceFile file = new("RigidBody.gen.hpp");
		file.Members.Add(new ClassDeclaration("RigidBody") { Kind = TypeDeclarationKind.Struct });
		file.Members.Add(new CompileTimeAssertion("std::is_trivially_copyable_v<RigidBody>", "bytes"));
		file.Members.Add(new CompileTimeAssertion("std::is_standard_layout_v<RigidBody>", "offsets"));

		string code = new CppGenerator().Generate(file).ReplaceLineEndings("\n");

		Assert.Contains("};\n\nstatic_assert(", code, StringComparison.Ordinal);
		Assert.Contains("\"bytes\");\nstatic_assert(", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// An assertion survives a round trip through YAML, and a clone carries it.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsTheAssertion()
	{
		CompileTimeAssertion original = new("sizeof(Handle) == 8", "a handle is eight bytes");

		string yaml = new YamlSerializer().Serialize(original);
		CompileTimeAssertion restored = (CompileTimeAssertion)new YamlDeserializer().Deserialize(yaml)!;

		Assert.AreEqual("sizeof(Handle) == 8", restored.Condition);
		Assert.AreEqual("a handle is eight bytes", restored.Message);

		CompileTimeAssertion clone = (CompileTimeAssertion)original.Clone();

		Assert.AreEqual(original.Condition, clone.Condition);
		Assert.AreEqual(original.Message, clone.Message);
		Assert.AreNotSame(original, clone);
	}

	/// <summary>
	/// A document says nothing for an assertion that asserts nothing.
	/// </summary>
	[TestMethod]
	public void Yaml_WritesNothingForAnEmptyAssertion()
	{
		string yaml = new YamlSerializer().Serialize(new CompileTimeAssertion());

		Assert.DoesNotContain("condition", yaml, StringComparison.Ordinal);
		Assert.DoesNotContain("message", yaml, StringComparison.Ordinal);
	}

	/// <summary>
	/// The editor can place one beside a declaration and edit both of its halves.
	/// </summary>
	[TestMethod]
	public void Graph_AcceptsAndEditsAnAssertion()
	{
		NamespaceDeclaration components = new("holo::components");
		CompileTimeAssertion assertion = new("sizeof(Handle) == 8");

		Assert.IsTrue(AstSchema.TryAttach(components, AstSchema.SlotsOf(components)[0], assertion));
		Assert.IsTrue(AstFields.TryWrite(assertion, "Message", "a handle is eight bytes"));

		Assert.AreEqual("a handle is eight bytes", assertion.Message);
	}
}
