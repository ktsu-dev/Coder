// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using System;
using System.IO;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using ktsu.Coder.Test.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="Parameter.DefaultValue"/>: that it is spelled by each language rather than
/// pasted, and that the text form older documents hold still reads (issue #182).
/// </summary>
/// <remarks>
/// A default used to be target-language source text, so the README's own <c>DefaultValue = "False"</c>
/// compiled in Python and nowhere else, and no spelling of a flag worked in every target.
/// </remarks>
[TestClass]
public class ParameterDefaultValueTests
{
	/// <summary>
	/// Builds <c>describe(include_debug: bool = false) -> bool</c>, which returns its parameter.
	/// </summary>
	/// <returns>The function.</returns>
	private static FunctionDeclaration Describe()
	{
		FunctionDeclaration function = new("describe") { ReturnType = "bool" };
		function.Parameters.Add(new Parameter("include_debug", "bool") { IsOptional = true, DefaultValue = Literal.Bool(false) });
		function.Body.Add(new ReturnStatement(new VariableReference("include_debug")));
		return function;
	}

	/// <summary>
	/// Tests that a <c>false</c> default is spelled each language's own way.
	/// </summary>
	/// <param name="language">The language, which names the generator.</param>
	/// <param name="expected">What the parameter has to come out as.</param>
	[TestMethod]
	[DataRow("csharp", "bool include_debug = false")]
	[DataRow("cpp", "bool include_debug = false")]
	[DataRow("javascript", "include_debug = false")]
	[DataRow("python", "include_debug: bool = False")]
	[DataRow("c", "/* = false */")]
	[DataRow("go", "/* = false */")]
	[DataRow("rust", "/* = false */")]
	public void AFlagDefaultIsSpelledByEachLanguage(string language, string expected)
	{
		ILanguageGenerator generator = language switch
		{
			"csharp" => new CSharpGenerator(),
			"cpp" => new CppGenerator(),
			"javascript" => new JavaScriptGenerator(),
			"python" => new PythonGenerator(),
			"c" => new CGenerator(),
			"go" => new GoGenerator(),
			"rust" => new RustGenerator(),
			_ => throw new ArgumentOutOfRangeException(nameof(language)),
		};

		string generated = generator.Generate(Describe());

		Assert.Contains(expected, generated, StringComparison.Ordinal, generated);
		Assert.DoesNotContain("False", language == "python" ? string.Empty : generated, StringComparison.Ordinal, generated);
	}

	/// <summary>
	/// Tests that the generated Python runs when called without the argument.
	/// </summary>
	[TestMethod]
	public void TheFlagDefaultRunsInPython()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python on the path, so nothing was run.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "driver.py"),
				new PythonGenerator().Generate(Describe()) + "\nassert describe() is False\nassert describe(True) is True\n");

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);

			Assert.AreEqual(0, exitCode, output);
		});
	}

	/// <summary>
	/// Tests that the generated JavaScript runs when called without the argument, rather than throwing
	/// a <c>ReferenceError</c> for a name it has never heard of.
	/// </summary>
	[TestMethod]
	public void TheFlagDefaultRunsInJavaScript()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No Node.js on the path, so nothing was run.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "driver.js"),
				new JavaScriptGenerator().Generate(Describe())
					+ "\nif (describe() !== false || describe(true) !== true) { process.exit(1); }\n");

			(int exitCode, string output) = ToolchainHarness.Run(node, "driver.js", directory);

			Assert.AreEqual(0, exitCode, output);
		});
	}

	/// <summary>
	/// Tests that the generated C++ compiles when called without the argument.
	/// </summary>
	[TestMethod]
	public void TheFlagDefaultCompilesInCpp()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "g++", "clang++");
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "driver.cpp"),
				new CppGenerator().Generate(Describe()) + "\nint main()\n{\n\treturn describe() ? 1 : 0;\n}\n");

			(int exitCode, string output) = ToolchainHarness.Run(compiler, "-std=c++20 -Wall -Wextra -pedantic -c driver.cpp -o driver.o", directory);

			Assert.AreEqual(0, exitCode, output);
		});
	}

	/// <summary>
	/// Tests that a default whose spelling holds <c>*/</c> does not end the comment a language with no
	/// default arguments writes it into.
	/// </summary>
	/// <param name="language">The language, which names the generator.</param>
	[TestMethod]
	[DataRow("c")]
	[DataRow("go")]
	[DataRow("rust")]
	public void ACommentedDefaultCannotEndItsComment(string language)
	{
		ILanguageGenerator generator = language switch
		{
			"c" => new CGenerator(),
			"go" => new GoGenerator(),
			_ => new RustGenerator(),
		};

		FunctionDeclaration function = new("greet") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("text", "string") { IsOptional = true, DefaultValue = Literal.Text("a */ b") });
		function.Body.Add(new ReturnStatement(Literal.Number(0)));

		string generated = generator.Generate(function);

		Assert.Contains("/* = \"a * / b\" */", generated, StringComparison.Ordinal, generated);
	}

	/// <summary>
	/// Tests that the text form reads into literals, and that anything else is a name.
	/// </summary>
	[TestMethod]
	public void ParseDefaultValue_ReadsLiteralsAndNames()
	{
		Assert.IsFalse(((LiteralExpression<bool>)Parameter.ParseDefaultValue("False")!).Value);
		Assert.IsTrue(((LiteralExpression<bool>)Parameter.ParseDefaultValue("true")!).Value);
		Assert.AreEqual(-3, ((LiteralExpression<int>)Parameter.ParseDefaultValue("-3")!).Value);
		Assert.AreEqual(5_000_000_000L, ((LiteralExpression<long>)Parameter.ParseDefaultValue("5000000000")!).Value);
		Assert.AreEqual(1.5, ((LiteralExpression<double>)Parameter.ParseDefaultValue("1.5")!).Value);
		Assert.AreEqual("say \"hi\"", ((LiteralExpression<string>)Parameter.ParseDefaultValue("\"say \\\"hi\\\"\"")!).Value);
		Assert.AreEqual("Color.Red", ((VariableReference)Parameter.ParseDefaultValue("Color.Red")!).Name);
		Assert.IsNull(Parameter.ParseDefaultValue(" "));
		Assert.IsNull(Parameter.ParseDefaultValue(null));
	}

	/// <summary>
	/// Tests that every text form reads back as the value it was written from.
	/// </summary>
	/// <param name="text">The text form.</param>
	[TestMethod]
	[DataRow("true")]
	[DataRow("false")]
	[DataRow("42")]
	[DataRow("-7")]
	[DataRow("5000000000")]
	[DataRow("1.0")]
	[DataRow("0.25")]
	[DataRow("\"default\"")]
	[DataRow("\"back\\\\slash \\\"quoted\\\"\"")]
	[DataRow("Color.Red")]
	public void FormatDefaultValue_RoundTrips(string text) =>
		Assert.AreEqual(text, Parameter.FormatDefaultValue(Parameter.ParseDefaultValue(text)));

	/// <summary>
	/// Tests that a document written while defaults were text still reads, including the README's own
	/// Python spelling of a flag.
	/// </summary>
	[TestMethod]
	public void AnOldDocumentsTextDefaultReadsAsALiteral()
	{
		const string Yaml = """
			functionDeclaration:
			  name: describe
			  parameters:
			  - name: include_debug
			    type: bool
			    isOptional: true
			    defaultValue: False
			""";

		FunctionDeclaration function = (FunctionDeclaration)new YamlDeserializer().Deserialize(Yaml)!;

		Assert.IsFalse(((LiteralExpression<bool>)function.Parameters[0].DefaultValue!).Value);
	}

	/// <summary>
	/// Tests that a default with no text form survives a round trip through YAML as the node it is.
	/// </summary>
	[TestMethod]
	public void ADefaultWithNoTextFormRoundTripsThroughYaml()
	{
		FunctionDeclaration function = new("scale") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("by", "int")
		{
			IsOptional = true,
			DefaultValue = new BinaryExpression(Literal.Number(2), BinaryOperator.Multiply, Literal.Number(3)),
		});

		string yaml = new YamlSerializer().Serialize(function)!;
		FunctionDeclaration read = (FunctionDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		BinaryExpression value = (BinaryExpression)read.Parameters[0].DefaultValue!;
		Assert.AreEqual(BinaryOperator.Multiply, value.Operator, yaml);
	}

	/// <summary>
	/// Tests that a clone holds its own copy of the default rather than sharing the original's node.
	/// </summary>
	[TestMethod]
	public void Clone_CopiesTheDefault()
	{
		Parameter original = new("n", "int") { IsOptional = true, DefaultValue = Literal.Number(1) };

		Parameter clone = (Parameter)original.Clone();

		Assert.AreNotSame(original.DefaultValue, clone.DefaultValue);
		Assert.AreEqual(1, ((LiteralExpression<int>)clone.DefaultValue!).Value);
	}
}
