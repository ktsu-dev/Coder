// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.CodeBlocker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that generated source is byte-identical whichever platform produced it.
/// </summary>
/// <remarks>
/// Generators emit through <see cref="CodeBlocker"/>, whose
/// <see cref="CodeBlocker.DefaultNewLineString"/> is LF rather than <c>Environment.NewLine</c>.
/// Generated code is committed, diffed and compared against golden files, all of which want
/// reproducibility over the local convention. On Linux the two agree, so a test asserting
/// <c>Environment.NewLine</c> would pass for the wrong reason and hide a Windows break; these assert
/// the absence of a carriage return directly.
/// </remarks>
[TestClass]
public class GeneratedLineEndingTests
{
	/// <summary>
	/// Builds a function whose body spans several lines, so the output carries line breaks from the
	/// signature, the body and the closing delimiter alike.
	/// </summary>
	/// <returns>The function declaration under test.</returns>
	private static FunctionDeclaration MultiLineFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Parameters.Add(new Parameter("b", "int"));
		function.Body.Add(new VariableDeclaration("sum", "int",
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));
		function.Body.Add(new ReturnStatement(new VariableReference("sum")));
		return function;
	}

	/// <summary>
	/// Tests that no generator emits a carriage return, on any platform.
	/// </summary>
	/// <param name="languageId">The generator under test, named for the failure message.</param>
	[TestMethod]
	[DataRow("python")]
	[DataRow("csharp")]
	[DataRow("cpp")]
	[DataRow("c")]
	[DataRow("rust")]
	[DataRow("javascript")]
	public void Generators_EmitLineFeedsOnly(string languageId)
	{
		ILanguageGenerator generator = languageId switch
		{
			"python" => new PythonGenerator(),
			"csharp" => new CSharpGenerator(),
			"cpp" => new CppGenerator(),
			"c" => new CGenerator(),
			"rust" => new RustGenerator(),
			"javascript" => new JavaScriptGenerator(),
			_ => throw new ArgumentOutOfRangeException(nameof(languageId))
		};

		string code = generator.Generate(MultiLineFunction());

		StringAssert.Contains(code, "\n", StringComparison.Ordinal, $"{languageId} should produce a multi-line result");
		Assert.IsFalse(
			code.Contains('\r', StringComparison.Ordinal),
			$"{languageId} should terminate lines with LF alone, not CRLF, so output does not vary by platform");
	}

	/// <summary>
	/// Tests that the terminator generators use is the one <see cref="CodeBlocker"/> documents, so a
	/// change to that default is caught here rather than in every generator's expectations.
	/// </summary>
	[TestMethod]
	public void GeneratedNewLine_IsCodeBlockersDefault()
	{
		string code = new CppGenerator().Generate(MultiLineFunction());

		StringAssert.Contains(code, CodeBlocker.DefaultNewLineString, StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a nested body is indented by one level per depth, which is the responsibility this
	/// change handed to <see cref="CodeBlocker"/>.
	/// </summary>
	[TestMethod]
	public void NestedBody_IsIndentedOneLevelPerDepth()
	{
		string code = new CppGenerator().Generate(MultiLineFunction());

		StringAssert.Contains(code, "\n    int sum = (a + b);\n", StringComparison.Ordinal);
		StringAssert.Contains(code, "\n    return sum;\n", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests the whole of a generated C# function, so the shape this refactor preserved is pinned
	/// rather than only sampled. The C# generator was the one that indented with a precomputed
	/// string and hardcoded its body to one level, so it had the most to lose.
	/// </summary>
	[TestMethod]
	public void CSharpFunction_GeneratesTheExpectedText()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new VariableDeclaration("sum", "int", Literal.Number(1)));
		function.Body.Add(new ReturnStatement(new VariableReference("sum")));

		Assert.AreEqual(
			"public int total(int a)\n{\n    int sum = 1;\n    return sum;\n}\n",
			new CSharpGenerator().Generate(function));
	}

	/// <summary>
	/// Tests that Python's body is indented without braces, since it opens an indent scope rather
	/// than a brace scope.
	/// </summary>
	[TestMethod]
	public void PythonBody_IsIndentedWithoutBraces()
	{
		string code = new PythonGenerator().Generate(MultiLineFunction());

		StringAssert.Contains(code, "\n    sum = (a + b)\n", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains('{', StringComparison.Ordinal), "Python output should carry no braces");
	}
}
