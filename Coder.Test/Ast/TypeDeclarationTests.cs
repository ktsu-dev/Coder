// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the declarations a generated type is made of: what kind of type it is, the enumerations
/// and fields it holds, the documentation each carries, and the namespace and file around them.
/// </summary>
/// <remarks>
/// These are the nodes an AST needs before it can describe a header rather than a snippet. Each is
/// covered the same way <see cref="VisibilityTests"/> covers its subject — by what each language
/// spells it as, rather than by asserting one shape four times, because the differences are the
/// reason the AST holds these as structure at all.
/// </remarks>
[TestClass]
public class TypeDeclarationTests
{
	/// <summary>
	/// Builds a struct with a nested enumeration and two fields, one of them initialised.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration SampleStruct()
	{
		EnumDeclaration kind = new("BodyKind") { UnderlyingType = "std::uint8_t" };
		kind.Members.Add(new EnumMember("Static"));
		kind.Members.Add(new EnumMember("Dynamic"));

		ClassDeclaration body = new("RigidBody") { Kind = TypeDeclarationKind.Struct };
		body.Documentation.Add("Physical state, integrated each frame.");
		body.Members.Add(kind);

		FieldDeclaration mass = new("mass", "double")
		{
			InitialValue = new LiteralExpression<double>(1.0),
		};
		mass.Documentation.Add("unit: kg");
		body.Members.Add(mass);

		body.Members.Add(new FieldDeclaration("kind", "BodyKind"));
		return body;
	}

	/// <summary>
	/// A declaration is a class unless it says otherwise, so nothing that existed before these
	/// changed shape.
	/// </summary>
	[TestMethod]
	public void Kind_DefaultsToClass()
	{
		Assert.AreEqual(TypeDeclarationKind.Class, new ClassDeclaration("Point").Kind);
		Assert.StartsWith(
			"class Point",
			new CppGenerator().Generate(new ClassDeclaration("Point")),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Each language writes the keyword it has for each kind, and invents nothing for the ones it
	/// does not.
	/// </summary>
	[TestMethod]
	public void Kind_IsSpelledByEachLanguage()
	{
		ClassDeclaration interfaceDecl = new("IShape") { Kind = TypeDeclarationKind.Interface };
		ClassDeclaration structDecl = new("Point") { Kind = TypeDeclarationKind.Struct };

		Assert.StartsWith("public struct Point", new CSharpGenerator().Generate(structDecl), StringComparison.Ordinal);
		Assert.StartsWith("public interface IShape", new CSharpGenerator().Generate(interfaceDecl), StringComparison.Ordinal);
		Assert.StartsWith("struct Point", new CppGenerator().Generate(structDecl), StringComparison.Ordinal);

		// C++ has no interface keyword, and Python and JavaScript have neither keyword.
		Assert.StartsWith("class IShape", new CppGenerator().Generate(interfaceDecl), StringComparison.Ordinal);
		Assert.StartsWith("class Point", new PythonGenerator().Generate(structDecl), StringComparison.Ordinal);
		Assert.StartsWith("class Point", new JavaScriptGenerator().Generate(structDecl), StringComparison.Ordinal);
	}

	/// <summary>
	/// A struct's members are public already, so C++ writes no access label above them. A class's
	/// members are not, which is the case the label exists for.
	/// </summary>
	[TestMethod]
	public void Cpp_LabelsAClassButNotAStruct()
	{
		Assert.DoesNotContain("public:", new CppGenerator().Generate(SampleStruct()), StringComparison.Ordinal);

		ClassDeclaration asClass = SampleStruct();
		asClass.Kind = TypeDeclarationKind.Class;

		Assert.Contains("public:", new CppGenerator().Generate(asClass), StringComparison.Ordinal);
	}

	/// <summary>
	/// An enumeration is scoped and sized in the languages that can say either.
	/// </summary>
	[TestMethod]
	public void Enum_IsSpelledByEachLanguage()
	{
		EnumDeclaration kind = new("BodyKind") { UnderlyingType = "std::uint8_t" };
		kind.Members.Add(new EnumMember("Static"));
		kind.Members.Add(new EnumMember("Dynamic") { Value = "7" });

		Assert.Contains(
			"enum class BodyKind : std::uint8_t",
			new CppGenerator().Generate(kind),
			StringComparison.Ordinal);
		Assert.Contains("Dynamic = 7,", new CppGenerator().Generate(kind), StringComparison.Ordinal);

		// Python and JavaScript have no enumeration, so a member with no value of its own is numbered
		// from its position rather than left to a keyword that does not exist.
		Assert.Contains("Static = 0", new PythonGenerator().Generate(kind), StringComparison.Ordinal);
		Assert.Contains("Static: 0,", new JavaScriptGenerator().Generate(kind), StringComparison.Ordinal);
		Assert.Contains("Object.freeze({", new JavaScriptGenerator().Generate(kind), StringComparison.Ordinal);
	}

	/// <summary>
	/// A class body is not a block, so the namespace-scope spelling of an enumeration cannot simply
	/// be nested inside one.
	/// </summary>
	[TestMethod]
	public void JavaScript_NestsAnEnumAsAStaticMember()
	{
		string code = new JavaScriptGenerator().Generate(SampleStruct());

		Assert.Contains("static BodyKind = Object.freeze({", code, StringComparison.Ordinal);
		Assert.DoesNotContain("const BodyKind", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// The difference between a field and a local: C++ initialises the one nobody gave a value to,
	/// so a default-constructed instance is the instance the declaration described.
	/// </summary>
	[TestMethod]
	public void Cpp_ValueInitialisesAFieldWithNoInitialiser()
	{
		string code = new CppGenerator().Generate(SampleStruct());

		Assert.Contains("BodyKind kind{};", code, StringComparison.Ordinal);
		Assert.Contains("double mass = 1", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// Documentation reaches the reader in every language, in whatever comment each has.
	/// </summary>
	[TestMethod]
	public void Documentation_IsWrittenInEachLanguagesComment()
	{
		ClassDeclaration body = SampleStruct();

		Assert.Contains("/// Physical state, integrated each frame.", new CppGenerator().Generate(body), StringComparison.Ordinal);
		Assert.Contains("/// unit: kg", new CSharpGenerator().Generate(body), StringComparison.Ordinal);
		Assert.Contains("# unit: kg", new PythonGenerator().Generate(body), StringComparison.Ordinal);
		Assert.Contains("// unit: kg", new JavaScriptGenerator().Generate(body), StringComparison.Ordinal);
	}

	/// <summary>
	/// A namespace name is written with either separator and comes out with the one its language uses.
	/// </summary>
	[TestMethod]
	public void Namespace_IsRejoinedWithEachLanguagesSeparator()
	{
		NamespaceDeclaration components = new("holo::components");
		components.Members.Add(new ClassDeclaration("Point"));

		Assert.Contains("namespace holo::components", new CppGenerator().Generate(components), StringComparison.Ordinal);
		Assert.Contains("}  // namespace holo::components", new CppGenerator().Generate(components), StringComparison.Ordinal);
		Assert.Contains("namespace holo.components", new CSharpGenerator().Generate(new NamespaceDeclaration("holo.components")), StringComparison.Ordinal);
	}

	/// <summary>
	/// Python and JavaScript have no namespace: the members come out and nothing is wrapped around
	/// them.
	/// </summary>
	[TestMethod]
	public void Namespace_IsNotSpelledWhereALanguageHasNone()
	{
		NamespaceDeclaration components = new("holo::components");
		components.Members.Add(new ClassDeclaration("Point"));

		Assert.AreEqual(
			new PythonGenerator().Generate(new ClassDeclaration("Point")),
			new PythonGenerator().Generate(components));
		Assert.AreEqual(
			new JavaScriptGenerator().Generate(new ClassDeclaration("Point")),
			new JavaScriptGenerator().Generate(components));
	}

	/// <summary>
	/// A header says that including it twice is including it once, and only C++ has anything to say
	/// about that.
	/// </summary>
	[TestMethod]
	public void File_WritesItsDirectivesAndImports()
	{
		SourceFile file = new("Point.hpp") { IsHeader = true };
		file.HeaderComment.Add("Generated. Do not edit.");
		file.Imports.Add("<cstdint>");
		file.Imports.Add("holotype/core/units.hpp");
		file.Members.Add(new ClassDeclaration("Point"));

		string code = new CppGenerator().Generate(file);

		Assert.Contains("// Generated. Do not edit.", code, StringComparison.Ordinal);
		Assert.Contains("#pragma once", code, StringComparison.Ordinal);
		Assert.Contains("#include <cstdint>", code, StringComparison.Ordinal);

		// An import carrying no delimiters of its own is quoted, which is right for a path inside the
		// project being generated.
		Assert.Contains("#include \"holotype/core/units.hpp\"", code, StringComparison.Ordinal);

		file.IsHeader = false;
		Assert.DoesNotContain("#pragma once", new CppGenerator().Generate(file), StringComparison.Ordinal);
	}

	/// <summary>
	/// An import is the one part of a file that does not translate, so each language writes its own
	/// kind of statement for it.
	/// </summary>
	[TestMethod]
	public void File_SpellsAnImportPerLanguage()
	{
		SourceFile file = new("point");
		file.Imports.Add("System.Text");

		Assert.Contains("using System.Text;", new CSharpGenerator().Generate(file), StringComparison.Ordinal);
		Assert.Contains("import System.Text", new PythonGenerator().Generate(file), StringComparison.Ordinal);
		Assert.Contains("import \"System.Text\";", new JavaScriptGenerator().Generate(file), StringComparison.Ordinal);
	}

	/// <summary>
	/// An empty import separates groups rather than importing nothing, which is how the standard
	/// headers are told apart from the project's own.
	/// </summary>
	[TestMethod]
	public void File_TreatsAnEmptyImportAsAGroupSeparator()
	{
		SourceFile file = new("point") { IsHeader = true };
		file.Imports.Add("<cstdint>");
		file.Imports.Add("");
		file.Imports.Add("point.hpp");

		Assert.Contains(
			"#include <cstdint>\n\n#include \"point.hpp\"",
			new CppGenerator().Generate(file).ReplaceLineEndings("\n"),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Every one of these survives a round trip through YAML, which is what makes them a document
	/// rather than something only the generator can see.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsTheWholeFile()
	{
		NamespaceDeclaration components = new("holo::components");
		components.Members.Add(SampleStruct());

		SourceFile original = new("RigidBody.hpp") { IsHeader = true };
		original.HeaderComment.Add("Generated. Do not edit.");
		original.Imports.Add("<cstdint>");
		original.Members.Add(components);

		string yaml = new YamlSerializer().Serialize(original);
		SourceFile restored = (SourceFile)new YamlDeserializer().Deserialize(yaml)!;

		Assert.IsTrue(restored.IsHeader);
		Assert.AreEqual("RigidBody.hpp", restored.Name);
		Assert.AreSequenceEqual(original.HeaderComment, restored.HeaderComment);
		Assert.AreSequenceEqual(original.Imports, restored.Imports);

		NamespaceDeclaration restoredNamespace = (NamespaceDeclaration)restored.Members[0];
		Assert.AreEqual("holo::components", restoredNamespace.Name);

		ClassDeclaration restoredStruct = (ClassDeclaration)restoredNamespace.Members[0];
		Assert.AreEqual(TypeDeclarationKind.Struct, restoredStruct.Kind);
		Assert.AreSequenceEqual(
			["Physical state, integrated each frame."],
			restoredStruct.Documentation);

		EnumDeclaration restoredEnum = (EnumDeclaration)restoredStruct.Members[0];
		Assert.AreEqual("std::uint8_t", restoredEnum.UnderlyingType?.ToString());
		Assert.HasCount(2, restoredEnum.Members);
		Assert.AreEqual("Static", restoredEnum.Members[0].Name);

		FieldDeclaration restoredField = (FieldDeclaration)restoredStruct.Members[1];
		Assert.AreEqual("double", restoredField.Type?.ToString());
		Assert.IsNotNull(restoredField.InitialValue);
		Assert.AreSequenceEqual(["unit: kg"], restoredField.Documentation);
	}

	/// <summary>
	/// A document written before these existed still opens, with a class where it says class and no
	/// documentation where it says nothing.
	/// </summary>
	[TestMethod]
	public void Yaml_WritesNothingForWhatADeclarationDoesNotSay()
	{
		string yaml = new YamlSerializer().Serialize(new ClassDeclaration("Point"));

		Assert.DoesNotContain("kind", yaml, StringComparison.Ordinal);
		Assert.DoesNotContain("documentation", yaml, StringComparison.Ordinal);
	}

	/// <summary>
	/// The editor can walk and edit the new declarations, which is what stops a node type existing
	/// only for whoever builds one in code.
	/// </summary>
	[TestMethod]
	public void Graph_ExposesTheNewDeclarations()
	{
		ClassDeclaration body = SampleStruct();
		EnumDeclaration kind = (EnumDeclaration)body.Members[0];

		Assert.HasCount(2, AstSchema.ChildrenOf(kind, AstSchema.SlotsOf(kind)[0]));
		Assert.IsTrue(AstFields.TryWrite(body, "Kind", "Interface"));
		Assert.AreEqual(TypeDeclarationKind.Interface, body.Kind);

		FieldDeclaration field = (FieldDeclaration)body.Members[2];
		Assert.IsTrue(AstFields.TryWrite(field, "Type", "float"));
		Assert.AreEqual("float", field.Type?.ToString());
	}
}
