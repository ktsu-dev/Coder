// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Cli;

using ktsu.Coder.Ast;
using ktsu.Coder.Cli;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests the sample function <see cref="CoderCLI"/> demonstrates, which the README shows as the
/// canonical usage example.
/// </summary>
[TestClass]
public class SampleFunctionTests
{
	/// <summary>
	/// Tests that the sample returns the sum of its arguments, not the text <c>"a + b"</c>, in every
	/// language and after a YAML round-trip.
	/// </summary>
	[TestMethod]
	public void SampleFunctionReturnsTheSumNotAString()
	{
		FunctionDeclaration function = CoderCLI.CreateSampleFunction();
		YamlSerializer serializer = new();
		AstNode? reloaded = new YamlDeserializer().Deserialize(serializer.Serialize(function));
		Assert.IsNotNull(reloaded);

		ILanguageGenerator[] generators =
		[
			new PythonGenerator(),
			new CSharpGenerator(),
			new CppGenerator(),
			new JavaScriptGenerator(),
			new RustGenerator(),
			new GoGenerator(),
			new CGenerator(),
		];

		foreach (ILanguageGenerator generator in generators)
		{
			foreach (AstNode node in new[] { function, reloaded })
			{
				string code = generator.Generate(node);
				StringAssert.Contains(code, "a + b", $"{generator.GetType().Name} does not add the arguments:\n{code}");
				Assert.DoesNotContain("\"a + b\"", code, $"{generator.GetType().Name} returns a string literal:\n{code}");
			}
		}
	}

	/// <summary>
	/// Tests the Python the README shows for the sample.
	/// </summary>
	[TestMethod]
	public void SamplePythonReturnsTheSum()
	{
		string code = new PythonGenerator().Generate(CoderCLI.CreateSampleFunction());

		StringAssert.Contains(code, "def calculate_sum(a: int, b: int, include_debug: bool = False) -> int:", StringComparison.Ordinal);
		StringAssert.Contains(code, "    return (a + b)", StringComparison.Ordinal);
	}
}
