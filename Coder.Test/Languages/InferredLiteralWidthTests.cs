// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a Go, C or C++ local whose type is inferred from a <c>long</c> or <c>float</c> literal
/// keeps that width.
/// </summary>
/// <remarks>
/// Go's <c>:=</c> and C++'s <c>auto</c> give an unsuffixed literal the language's default type, so
/// <c>0.1</c> became a <c>float64</c> or a <c>double</c> and <c>5</c> an <c>int</c>. Go then refused the
/// local beside a <c>float32</c>, C++ silently computed in another width, and C, which has no deduced
/// type, declared the local as <c>void*</c> (issue #151). The generated code is compiled and run,
/// because the width is what a consumer gets and is not visible in the text.
/// </remarks>
[TestClass]
public class InferredLiteralWidthTests
{
	private const string Module = """
		module widths

		go 1.21

		""";

	private const string GoDriver = """
		package main

		import "fmt"

		func main() {
			fmt.Println(Half(), Scaled())
		}

		""";

	private const string CDriver = """
		#include <stdio.h>
		#include "widths.h"

		int main(void)
		{
			printf("%g %lld\n", (double)half(), scaled());
			return 0;
		}

		""";

	private const string CppDriver = """
		#include <cstdio>
		#include "widths.h"

		int main()
		{
			std::printf("%g %lld\n", static_cast<double>(half()), scaled());
			return 0;
		}

		""";

	/// <summary>
	/// What every target prints: <c>0.1f * 0.5f</c> in single precision, and <c>3000000 * 3000000</c>,
	/// which overflows a 32-bit integer.
	/// </summary>
	private const string Expected = "0.05 9000000000000";

	private static readonly string[] CCompilers = ["cc", "gcc", "clang"];

	private static readonly string[] CppCompilers = ["c++", "g++", "clang++"];

	/// <summary>
	/// Tests that Go writes an inferred long or float local with its width, and still leaves an
	/// <c>int</c> or <c>double</c> one to <c>:=</c>.
	/// </summary>
	[TestMethod]
	public void Go_AnInferredLongOrFloatLocal_SaysItsWidth()
	{
		string code = new GoGenerator().Generate(Locals());

		StringAssert.Contains(code, "var narrow float32 = 0.1", StringComparison.Ordinal);
		StringAssert.Contains(code, "var big int64 = 5", StringComparison.Ordinal);
		StringAssert.Contains(code, "small := 5", StringComparison.Ordinal);
		StringAssert.Contains(code, "wide := 0.1", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that C declares an inferred local with its initialiser's type rather than <c>void*</c>,
	/// and suffixes a float or long literal.
	/// </summary>
	[TestMethod]
	public void C_AnInferredLocal_TakesItsInitialisersType()
	{
		string code = new CGenerator().Generate(Locals());

		StringAssert.Contains(code, "float narrow = 0.1f;", StringComparison.Ordinal);
		StringAssert.Contains(code, "long long big = 5LL;", StringComparison.Ordinal);
		StringAssert.Contains(code, "int small = 5;", StringComparison.Ordinal);
		StringAssert.Contains(code, "double wide = 0.1;", StringComparison.Ordinal);
		Assert.DoesNotContain("void*", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that C++ suffixes a float or long literal, so <c>auto</c> deduces the declared width.
	/// </summary>
	[TestMethod]
	public void Cpp_AnInferredLongOrFloatLocal_DeducesItsWidth()
	{
		string code = new CppGenerator().Generate(Locals());

		StringAssert.Contains(code, "auto narrow = 0.1f;", StringComparison.Ordinal);
		StringAssert.Contains(code, "auto big = 5LL;", StringComparison.Ordinal);
		StringAssert.Contains(code, "auto small = 5;", StringComparison.Ordinal);
		StringAssert.Contains(code, "auto wide = 0.1;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the Go output compiles, is what <c>gofmt</c> would write, and computes in the
	/// declared widths.
	/// </summary>
	[TestMethod]
	public void Go_InferredWidths_CompileAndRun()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new GoGenerator().Generate(Arithmetic("main", "Half", "Scaled"));
			File.WriteAllText(Path.Combine(directory, "go.mod"), Module);
			File.WriteAllText(Path.Combine(directory, "driver.go"), GoDriver);
			File.WriteAllText(Path.Combine(directory, "widths.go"), source);

			(int exitCode, string output) = ToolchainHarness.Run("go", "run .", directory);
			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
			Assert.AreEqual(Expected, output.Trim(), source);

			(int formatted, string differs) = ToolchainHarness.Run("gofmt", "-l widths.go", directory);
			Assert.AreEqual(0, formatted, $"gofmt did not run:{Environment.NewLine}{differs}");
			Assert.AreEqual(string.Empty, differs.Trim(), "gofmt would rewrite the generated source.");
		});
	}

	/// <summary>
	/// Tests that the C output compiles as strict C and computes in the declared widths.
	/// </summary>
	[TestMethod]
	public void C_InferredWidths_CompileAndRun() =>
		CompileAndRun(CCompilers, new CGenerator(), "driver.c", CDriver, "-std=c11 -Wall -Wextra -pedantic -Werror");

	/// <summary>
	/// Tests that the C++ output compiles with float conversions treated as errors, and computes in
	/// the declared widths.
	/// </summary>
	[TestMethod]
	public void Cpp_InferredWidths_CompileAndRun() =>
		CompileAndRun(CppCompilers, new CppGenerator(), "driver.cpp", CppDriver, "-std=c++20 -Wall -Wextra -pedantic -Wfloat-conversion -Werror");

	private static void CompileAndRun(string[] compilers, ILanguageGenerator generator, string driverName, string driver, string flags)
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", compilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = generator.Generate(Arithmetic("widths", "half", "scaled"));
			File.WriteAllText(Path.Combine(directory, "widths.h"), source);
			File.WriteAllText(Path.Combine(directory, driverName), driver);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, $"{flags} {driverName} -o widths", directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "widths"), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual(Expected, printed.Trim(), source);
		});
	}

	/// <summary>
	/// Inferred locals initialised from each literal width, nothing reading them.
	/// </summary>
	private static SourceFile Locals()
	{
		FunctionDeclaration function = new("widths");
		function.Body.Add(new VariableDeclaration("narrow", null, new LiteralExpression<float>(0.1f)));
		function.Body.Add(new VariableDeclaration("big", null, new LiteralExpression<long>(5)));
		function.Body.Add(new VariableDeclaration("small", null, new LiteralExpression<int>(5)));
		function.Body.Add(new VariableDeclaration("wide", null, new LiteralExpression<double>(0.1)));

		SourceFile file = new("widths");
		file.Members.Add(function);
		return file;
	}

	/// <summary>
	/// Inferred float and long locals, then used in arithmetic whose result is declared with the same
	/// width — the issue's reproduction.
	/// </summary>
	private static SourceFile Arithmetic(string fileName, string halfName, string scaledName)
	{
		FunctionDeclaration half = new(halfName) { ReturnType = new TypeReference("float") };
		half.Body.Add(new VariableDeclaration("a", null, new LiteralExpression<float>(0.1f)));
		half.Body.Add(new VariableDeclaration("d", "float", new BinaryExpression(
			new VariableReference("a"),
			BinaryOperator.Multiply,
			new LiteralExpression<float>(0.5f))));
		half.Body.Add(new ReturnStatement(new VariableReference("d")));

		FunctionDeclaration scaled = new(scaledName) { ReturnType = new TypeReference("long") };
		scaled.Body.Add(new VariableDeclaration("b", null, new LiteralExpression<long>(3_000_000)));
		scaled.Body.Add(new VariableDeclaration("e", "long", new BinaryExpression(
			new VariableReference("b"),
			BinaryOperator.Multiply,
			new VariableReference("b"))));
		scaled.Body.Add(new ReturnStatement(new VariableReference("e")));

		SourceFile file = new(fileName);
		file.Members.Add(half);
		file.Members.Add(scaled);
		return file;
	}
}
