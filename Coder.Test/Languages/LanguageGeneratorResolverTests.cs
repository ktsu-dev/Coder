// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System;
using ktsu.Coder;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="LanguageGeneratorResolver"/>: one lookup by language id or file extension,
/// so consumers agree on case, the leading dot, and which generator wins a tie.
/// </summary>
[TestClass]
public class LanguageGeneratorResolverTests
{
	private static ILanguageGeneratorResolver RegisteredResolver()
	{
		ServiceCollection services = new();
		services.AddLanguageGenerators();
		return services.BuildServiceProvider().GetRequiredService<ILanguageGeneratorResolver>();
	}

	[TestMethod]
	[DataRow("python", typeof(PythonGenerator))]
	[DataRow("csharp", typeof(CSharpGenerator))]
	[DataRow("javascript", typeof(JavaScriptGenerator))]
	[DataRow("cpp", typeof(CppGenerator))]
	[DataRow("c", typeof(CGenerator))]
	[DataRow("rust", typeof(RustGenerator))]
	[DataRow("go", typeof(GoGenerator))]
	[DataRow("Python", typeof(PythonGenerator))]
	[DataRow("CSHARP", typeof(CSharpGenerator))]
	public void TryGetByLanguageId_ResolvesEachBuiltInIdIgnoringCase(string languageId, Type expected)
	{
		Assert.IsTrue(RegisteredResolver().TryGetByLanguageId(languageId, out ILanguageGenerator? generator));
		Assert.IsInstanceOfType(generator, expected);
	}

	[TestMethod]
	[DataRow("py", typeof(PythonGenerator))]
	[DataRow("cs", typeof(CSharpGenerator))]
	[DataRow("js", typeof(JavaScriptGenerator))]
	[DataRow("cpp", typeof(CppGenerator))]
	[DataRow("c", typeof(CGenerator))]
	[DataRow("rs", typeof(RustGenerator))]
	[DataRow("go", typeof(GoGenerator))]
	[DataRow(".py", typeof(PythonGenerator))]
	[DataRow(".RS", typeof(RustGenerator))]
	[DataRow("model.py", typeof(PythonGenerator))]
	[DataRow("out/model.CS", typeof(CSharpGenerator))]
	[DataRow(@"out\nested.dir\model.go", typeof(GoGenerator))]
	public void TryGetByFileExtension_AcceptsABareOrDottedExtensionOrAPath(string extensionOrPath, Type expected)
	{
		Assert.IsTrue(RegisteredResolver().TryGetByFileExtension(extensionOrPath, out ILanguageGenerator? generator));
		Assert.IsInstanceOfType(generator, expected);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("cobol")]
	[DataRow("py ")]
	[DataRow("pyt")]
	public void TryGetByLanguageId_ReturnsFalseForAnUnknownId(string languageId)
	{
		Assert.IsFalse(RegisteredResolver().TryGetByLanguageId(languageId, out ILanguageGenerator? generator));
		Assert.IsNull(generator);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow(".")]
	[DataRow("cob")]
	[DataRow("model")]
	[DataRow("out/model")]
	[DataRow("out.py/model")]
	[DataRow("model.py.bak")]
	public void TryGetByFileExtension_ReturnsFalseForAnUnknownExtension(string extensionOrPath)
	{
		Assert.IsFalse(RegisteredResolver().TryGetByFileExtension(extensionOrPath, out ILanguageGenerator? generator));
		Assert.IsNull(generator);
	}

	[TestMethod]
	public void Generators_ListsEveryRegisteredGenerator()
	{
		Assert.HasCount(7, RegisteredResolver().Generators);
	}

	[TestMethod]
	public void AClaimedIdOrExtension_ResolvesToTheFirstGeneratorGiven()
	{
		ILanguageGenerator first = new StubGenerator("python", "py");
		ILanguageGenerator second = new StubGenerator("PYTHON", ".PY");
		LanguageGeneratorResolver resolver = new([first, second]);

		Assert.IsTrue(resolver.TryGetByLanguageId("python", out ILanguageGenerator? byId));
		Assert.AreSame(first, byId);
		Assert.IsTrue(resolver.TryGetByFileExtension("py", out ILanguageGenerator? byExtension));
		Assert.AreSame(first, byExtension);
	}

	[TestMethod]
	public void AGeneratorDeclaringItsExtensionWithADot_StillResolves()
	{
		ILanguageGenerator dotted = new StubGenerator("dotted", ".dt");
		LanguageGeneratorResolver resolver = new([dotted]);

		Assert.IsTrue(resolver.TryGetByFileExtension("dt", out ILanguageGenerator? generator));
		Assert.AreSame(dotted, generator);
	}

	private sealed class StubGenerator(string languageId, string fileExtension) : ILanguageGenerator
	{
		public string LanguageId { get; } = languageId;

		public string DisplayName => LanguageId;

		public string FileExtension { get; } = fileExtension;

		public string Generate(AstNode astNode) => string.Empty;

		public bool CanGenerate(AstNode astNode) => false;
	}
}
