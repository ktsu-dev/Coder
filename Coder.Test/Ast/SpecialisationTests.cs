// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="ClassDeclaration.SpecialisationArguments"/>: a declaration that attaches
/// itself to a type rather than introducing one.
/// </summary>
/// <remarks>
/// The second case of the rule <see cref="CompileTimeAssertion"/> established -- say what is true,
/// and let each language say as much of it as it can. It is here because a generated table has to
/// be reachable from the type it describes, and <c>Describe&lt;RigidBody&gt;</c> is how C++ does
/// that without touching <c>RigidBody</c>.
/// </remarks>
[TestClass]
public class SpecialisationTests
{
	/// <summary>
	/// C++ writes the empty parameter list above the declaration and the arguments after its name.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesTheSpecialisation()
	{
		ClassDeclaration describe = new("Describe")
		{
			Kind = TypeDeclarationKind.Struct,
			SpecialisationArguments = { new TypeReference("holo::components::RigidBody") },
		};

		describe.Members.Add(new FieldDeclaration("info", "ComponentInfo") { IsStatic = true, IsConstant = true });

		Assert.AreEqual(
			"template <>\n"
			+ "struct Describe<holo::components::RigidBody>\n"
			+ "{\n"
			+ "    static constexpr ComponentInfo info{};\n"
			+ "};\n",
			new CppGenerator().Generate(describe).ReplaceLineEndings("\n"));
	}

	/// <summary>
	/// A declaration with no arguments is an ordinary one, and says nothing about templates.
	/// </summary>
	/// <remarks>
	/// The property is what distinguishes the two, so the absence has to be tested as well as the
	/// presence: a generator that wrote <c>template &lt;&gt;</c> unconditionally would break every
	/// type this library has ever emitted.
	/// </remarks>
	[TestMethod]
	public void Cpp_WritesAnOrdinaryDeclarationWithNone()
	{
		ClassDeclaration body = new("RigidBody") { Kind = TypeDeclarationKind.Struct };

		string code = new CppGenerator().Generate(body).ReplaceLineEndings("\n");

		Assert.DoesNotContain("template", code, StringComparison.Ordinal);
		Assert.Contains("struct RigidBody\n", code, StringComparison.Ordinal);
		Assert.IsFalse(body.IsSpecialisation);
	}

	/// <summary>
	/// Several arguments are written in the order they were given.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesEveryArgumentInOrder()
	{
		ClassDeclaration converter = new("Convert")
		{
			Kind = TypeDeclarationKind.Struct,
			SpecialisationArguments =
			{
				new TypeReference("Metres"),
				new TypeReference("Feet"),
			},
		};

		Assert.Contains(
			"struct Convert<Metres, Feet>",
			new CppGenerator().Generate(converter),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// An argument that is itself a generic type keeps its own arguments.
	/// </summary>
	/// <remarks>
	/// The reason the arguments are <see cref="TypeReference"/> rather than text: a type argument
	/// has structure, and the comma in <c>Result&lt;Handle, Error&gt;</c> belongs to that type
	/// rather than separating two of them.
	/// </remarks>
	[TestMethod]
	public void Cpp_WritesANestedArgumentWhole()
	{
		ClassDeclaration describe = new("Describe")
		{
			Kind = TypeDeclarationKind.Struct,
			SpecialisationArguments = { TypeReference.Parse("holo::Result<holo::BodyHandle, holo::Error>") },
		};

		Assert.Contains(
			"struct Describe<holo::Result<holo::BodyHandle, holo::Error>>",
			new CppGenerator().Generate(describe),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// The other three cannot attach a declaration to a type, so they say which type it was for
	/// rather than emitting something that reads as an unrelated class.
	/// </summary>
	[TestMethod]
	public void OtherLanguages_SayWhatItWasSpecialisedFor()
	{
		ClassDeclaration describe = new("Describe")
		{
			Kind = TypeDeclarationKind.Struct,
			SpecialisationArguments = { new TypeReference("holo::components::RigidBody") },
		};

		Assert.Contains(
			"// specialised for holo::components::RigidBody",
			new CSharpGenerator().Generate(describe),
			StringComparison.Ordinal);
		Assert.Contains(
			"# specialised for holo::components::RigidBody",
			new PythonGenerator().Generate(describe),
			StringComparison.Ordinal);
		Assert.Contains(
			"// specialised for holo::components::RigidBody",
			new JavaScriptGenerator().Generate(describe),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// The arguments survive a round trip through YAML, nested ones included, and a clone carries
	/// them without sharing them.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsTheArguments()
	{
		ClassDeclaration original = new("Describe")
		{
			Kind = TypeDeclarationKind.Struct,
			SpecialisationArguments =
			{
				new TypeReference("holo::components::RigidBody"),
				TypeReference.Parse("holo::Result<holo::BodyHandle, holo::Error>"),
			},
		};

		string yaml = new YamlSerializer().Serialize(original);
		ClassDeclaration restored = (ClassDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.HasCount(2, restored.SpecialisationArguments);
		Assert.AreEqual("holo::components::RigidBody", restored.SpecialisationArguments[0].ToString());
		Assert.AreEqual("holo::Result<holo::BodyHandle, holo::Error>", restored.SpecialisationArguments[1].ToString());

		ClassDeclaration clone = (ClassDeclaration)original.Clone();

		Assert.HasCount(2, clone.SpecialisationArguments);
		Assert.AreEqual(original.SpecialisationArguments[1].ToString(), clone.SpecialisationArguments[1].ToString());
		Assert.AreNotSame(original.SpecialisationArguments[0], clone.SpecialisationArguments[0]);
	}

	/// <summary>
	/// A document says nothing for a declaration that specialises nothing.
	/// </summary>
	[TestMethod]
	public void Yaml_WritesNothingForAnOrdinaryDeclaration()
	{
		string yaml = new YamlSerializer().Serialize(new ClassDeclaration("RigidBody"));

		Assert.DoesNotContain("specialisationArguments", yaml, StringComparison.Ordinal);
	}
}
