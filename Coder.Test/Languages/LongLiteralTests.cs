// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a <c>long</c> literal keeps its exact value in JavaScript.
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
