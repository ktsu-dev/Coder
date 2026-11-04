// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the file extension each generator gives the source generated from a node (issue #367).
/// </summary>
[TestClass]
public sealed class GeneratorFileExtensionTests
{
	private static ILanguageGenerator[] AllGenerators() =>
		[new CSharpGenerator(), new PythonGenerator(), new CppGenerator(), new CGenerator(), new RustGenerator(), new GoGenerator(), new JavaScriptGenerator()];

	/// <summary>
	/// Tests that C and C++ name a header source file as a header.
	/// </summary>
	[TestMethod]
	public void CFamily_NamesAHeaderSourceFileAsAHeader()
	{
		SourceFile header = new("Physics") { IsHeader = true };

		Assert.AreEqual("hpp", new CppGenerator().FileExtensionFor(header));
		Assert.AreEqual("h", new CGenerator().FileExtensionFor(header));
	}

	/// <summary>
	/// Tests that a source file that is not a header, and a node that is not a source file at all,
	/// keep each generator's ordinary extension.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_KeepsItsExtensionForANonHeader()
	{
		AstNode[] nodes = [new SourceFile("Physics"), new ClassDeclaration("Body")];

		foreach (ILanguageGenerator generator in AllGenerators())
		{
			foreach (AstNode node in nodes)
			{
				Assert.AreEqual(generator.FileExtension, generator.FileExtensionFor(node), $"{generator.LanguageId} on {node.GetType().Name}");
			}
		}
	}

	/// <summary>
	/// Tests that a header flag means nothing to the languages that do not split a file in two.
	/// </summary>
	[TestMethod]
	public void NonCFamily_IgnoresTheHeaderFlag()
	{
		SourceFile header = new("Physics") { IsHeader = true };

		foreach (ILanguageGenerator generator in AllGenerators())
		{
			if (generator is CFamilyGenerator)
			{
				continue;
			}

			Assert.AreEqual(generator.FileExtension, generator.FileExtensionFor(header), generator.LanguageId);
		}
	}
}
