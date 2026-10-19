// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a <c>long</c> literal keeps its exact value in JavaScript, and that the minimum
/// <c>long</c> and <c>int</c> compile in C and C++.
/// </summary>
/// <remarks>
/// A JavaScript number is a double, which rounds any integer past <c>Number.MAX_SAFE_INTEGER</c>
/// (2^53 − 1). The other targets hold a 64-bit integer exactly. The generated code is run rather than
/// read, because the value is what a consumer gets.
/// </remarks>
[TestClass]
public class LongLiteralTests
{
	private const long MaxSafeInteger = 9007199254740991;

	private static readonly long[] Values =
	[
		0,
		MaxSafeInteger,
		-MaxSafeInteger,
		MaxSafeInteger + 1,
		MaxSafeInteger + 2,
		-(MaxSafeInteger + 2),
		long.MaxValue,
		long.MinValue,
	];

	/// <summary>
	/// Tests that a literal past the safe range is written as a BigInt and one inside it is not.
	/// </summary>
	/// <param name="value">The literal's value.</param>
	/// <param name="expected">What JavaScript should be given.</param>
	[TestMethod]
	[DataRow(0L, "0")]
	[DataRow(MaxSafeInteger, "9007199254740991")]
	[DataRow(-MaxSafeInteger, "-9007199254740991")]
	[DataRow(MaxSafeInteger + 1, "9007199254740992n")]
	[DataRow(-(MaxSafeInteger + 2), "-9007199254740993n")]
	[DataRow(long.MaxValue, "9223372036854775807n")]
	[DataRow(long.MinValue, "-9223372036854775808n")]
	public void JavaScript_SpellsAnUnsafeLongAsABigInt(long value, string expected) =>
		Assert.AreEqual(expected, new JavaScriptGenerator().Generate(new LiteralExpression<long>(value)).Trim());

	/// <summary>
	/// Tests that the other targets still write the digits exactly, since each holds them exactly.
	/// C and C++ add <c>LL</c>, so a deduced or promoted type is the 64-bit one (issue #151).
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <param name="expected">What the target should be given.</param>
	[TestMethod]
	[DataRow("python", "9007199254740993")]
	[DataRow("go", "9007199254740993")]
	[DataRow("rust", "9007199254740993")]
	[DataRow("cpp", "9007199254740993LL")]
	[DataRow("c", "9007199254740993LL")]
	public void OtherTargets_KeepTheExactDigits(string language, string expected)
	{
		ILanguageGenerator generator = language switch
		{
			"python" => new PythonGenerator(),
			"go" => new GoGenerator(),
			"rust" => new RustGenerator(),
			"cpp" => new CppGenerator(),
			_ => new CGenerator(),
		};

		Assert.AreEqual(expected, generator.Generate(new LiteralExpression<long>(MaxSafeInteger + 2)).Trim());
	}

	/// <summary>
	/// Tests that C and C++ write the minimum <c>long</c> and <c>int</c> as a subtraction, since neither
	/// language has a negative literal and the negated digits do not fit the type (issue #247).
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	[TestMethod]
	[DataRow("c")]
	[DataRow("cpp")]
	public void CFamily_SpellsTheMinimumValuesAsASubtraction(string language)
	{
		ILanguageGenerator generator = language == "c" ? new CGenerator() : new CppGenerator();

		Assert.AreEqual("(-9223372036854775807LL - 1)", generator.Generate(new LiteralExpression<long>(long.MinValue)).Trim());
		Assert.AreEqual("(-2147483647 - 1)", generator.Generate(new LiteralExpression<int>(int.MinValue)).Trim());
		Assert.AreEqual("-9223372036854775807LL", generator.Generate(new LiteralExpression<long>(long.MinValue + 1)).Trim());
		Assert.AreEqual("-2147483647", generator.Generate(new LiteralExpression<int>(int.MinValue + 1)).Trim());
	}

	/// <summary>
	/// Tests that C++ deduces an <c>int</c> for an inferred local holding <see cref="int.MinValue"/>,
	/// rather than the <c>long</c> that <c>-2147483648</c> is.
	/// </summary>
	[TestMethod]
	public void Cpp_AnInferredMinimumInt_StaysAnInt() =>
		StringAssert.Contains(new CppGenerator().Generate(Minimums()), "auto low = (-2147483647 - 1);", StringComparison.Ordinal);

	/// <summary>
	/// Tests that C compiles the minimum values with pedantic diagnostics as errors, and reads them
	/// back exactly.
	/// </summary>
	[TestMethod]
	public void C_MinimumValues_CompileAndRun() =>
		CompileAndRun(["cc", "gcc", "clang"], new CGenerator(), "driver.c", CDriver, "-std=c11 -Wall -Wextra -pedantic-errors -Wconversion -Werror");

	/// <summary>
	/// Tests that C++ compiles the minimum values with narrowing treated as an error, which is what an
	/// <c>auto</c> local deduced as <c>long</c> and returned as an <c>int</c> would be, and reads them
	/// back exactly.
	/// </summary>
	[TestMethod]
	public void Cpp_MinimumValues_CompileAndRun() =>
		CompileAndRun(["c++", "g++", "clang++"], new CppGenerator(), "driver.cpp", CppDriver, "-std=c++20 -Wall -Wextra -pedantic -Wconversion -Werror");

	private const string CDriver = """
		#include <stdio.h>
		#include "minimums.h"

		int main(void)
		{
			printf("%lld %d\n", lowest_long(), lowest_int());
			return 0;
		}

		""";

	private const string CppDriver = """
		#include <cstdio>
		#include "minimums.h"

		int main()
		{
			std::printf("%lld %d\n", lowest_long(), lowest_int());
			return 0;
		}

		""";

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
			string source = generator.Generate(Minimums());
			File.WriteAllText(Path.Combine(directory, "minimums.h"), source);
			File.WriteAllText(Path.Combine(directory, driverName), driver);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, $"{flags} {driverName} -o minimums", directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "minimums"), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("-9223372036854775808 -2147483648", printed.Trim(), source);
		});
	}

	/// <summary>
	/// A function returning each minimum: the <c>long</c> through a declared local, the issue's
	/// reproduction, and the <c>int</c> through an inferred one.
	/// </summary>
	private static SourceFile Minimums()
	{
		FunctionDeclaration lowestLong = new("lowest_long") { ReturnType = new TypeReference("long") };
		lowestLong.Body.Add(new VariableDeclaration("v", "long", new LiteralExpression<long>(long.MinValue)));
		lowestLong.Body.Add(new ReturnStatement(new VariableReference("v")));

		FunctionDeclaration lowestInt = new("lowest_int") { ReturnType = new TypeReference("int") };
		lowestInt.Body.Add(new VariableDeclaration("low", null, new LiteralExpression<int>(int.MinValue)));
		lowestInt.Body.Add(new ReturnStatement(new VariableReference("low")));

		SourceFile file = new("minimums");
		file.Members.Add(lowestLong);
		file.Members.Add(lowestInt);
		return file;
	}

	/// <summary>
	/// Tests that Node reads back the exact value of every literal.
	/// </summary>
	[TestMethod]
	public void JavaScript_KeepsTheExactValue()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		JavaScriptGenerator generator = new();
		StringBuilder module = new();
		foreach (long value in Values)
		{
			string digits = value.ToString(CultureInfo.InvariantCulture);
			string declaration = generator.Generate(new VariableDeclaration("big", "long", new LiteralExpression<long>(value))).Trim();
			module.AppendLine(CultureInfo.InvariantCulture, $"{{ {declaration} if (String(big) !== \"{digits}\") {{ throw new Error(`{digits} read back as ${{big}}`); }} }}");
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "check.js"), module.ToString());
			(int exitCode, string output) = ToolchainHarness.Run(node, "check.js", directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{module}");
		});
	}
}
