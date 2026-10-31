// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that comparing a <c>long</c> against a literal past <c>Number.MAX_SAFE_INTEGER</c> gives the
/// same answer in JavaScript as in every other target (#424).
/// </summary>
/// <remarks>
/// Such a literal is a BigInt in the generated JavaScript, and <c>===</c> between a BigInt and a number
/// is always <c>false</c>, so <c>==</c> never held and <c>!=</c> always did. The module is run rather
/// than read, because the answer is what a consumer gets.
/// </remarks>
[TestClass]
public class JavaScriptBigIntComparisonTests
{
	private const long TwoToThe53 = 9007199254740992;

	private const string Checks = """

		const check = (actual, expected, what) => {
		  if (actual !== expected) {
		    throw new Error(`${what}: expected ${expected}, got ${actual}`);
		  }
		};

		check(isLimit(2 ** 53), true, "2^53 == 2^53");
		check(isLimit(2 ** 53 - 2), false, "2^53 - 2 == 2^53");
		check(notLimit(2 ** 53), false, "2^53 != 2^53");
		check(isPast(2 ** 53), false, "2^53 == 2^53 + 1");
		check(isPast(9007199254740993n), true, "2^53 + 1 == 2^53 + 1");
		check(isPastFlipped(9007199254740993n), true, "2^53 + 1 == 2^53 + 1, literal first");
		check(notPast(2 ** 53), true, "2^53 != 2^53 + 1");
		check(notPast(9007199254740993n), false, "2^53 + 1 != 2^53 + 1");
		check(belowPast(2 ** 53), true, "2^53 < 2^53 + 1");
		check(abovePast(2 ** 53), false, "2^53 > 2^53 + 1");
		check(isPastAsDouble(2 ** 53), true, "(double)2^53 == (double)(2^53 + 1)");
		check(isTwoPast(2 ** 53 + 2), true, "2^53 + 2, held as a number, == 2^53 + 2");
		check(notTwoPast(2 ** 53 + 2), false, "2^53 + 2, held as a number, != 2^53 + 2");

		""";

	/// <summary>
	/// Tests that <c>x == 9007199254740992L</c> holds for <c>x = 2**53</c>, and that comparisons against
	/// <c>9007199254740993L</c> compare values.
	/// </summary>
	[TestMethod]
	public void LongComparedWithAnUnsafeLiteral_ComparesValues()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		string source = new JavaScriptGenerator().Generate(Comparisons()) + Checks;

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "check.js"), source);
			(int exitCode, string output) = ToolchainHarness.Run(node, "check.js", directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a comparison with a literal a number holds exactly is left as <c>===</c>.
	/// </summary>
	[TestMethod]
	public void LongComparedWithASafeLiteral_IsUnchanged()
	{
		string code = new JavaScriptGenerator().Generate(Comparison("isLimit", "long", BinaryOperator.Equal, TwoToThe53, literalFirst: false));
		StringAssert.Contains(code, "(x === 9007199254740992)", StringComparison.Ordinal);
		Assert.DoesNotContain("BigInt(", code, StringComparison.Ordinal, code);
	}

	private static SourceFile Comparisons()
	{
		SourceFile file = new("comparisons");
		file.Members.Add(Comparison("isLimit", "long", BinaryOperator.Equal, TwoToThe53, literalFirst: false));
		file.Members.Add(Comparison("notLimit", "long", BinaryOperator.NotEqual, TwoToThe53, literalFirst: false));
		file.Members.Add(Comparison("isPast", "long", BinaryOperator.Equal, TwoToThe53 + 1, literalFirst: false));
		file.Members.Add(Comparison("isPastFlipped", "long", BinaryOperator.Equal, TwoToThe53 + 1, literalFirst: true));
		file.Members.Add(Comparison("notPast", "long", BinaryOperator.NotEqual, TwoToThe53 + 1, literalFirst: false));
		file.Members.Add(Comparison("belowPast", "long", BinaryOperator.LessThan, TwoToThe53 + 1, literalFirst: false));
		file.Members.Add(Comparison("abovePast", "long", BinaryOperator.GreaterThan, TwoToThe53 + 1, literalFirst: false));
		file.Members.Add(Comparison("isTwoPast", "long", BinaryOperator.Equal, TwoToThe53 + 2, literalFirst: false));
		file.Members.Add(Comparison("notTwoPast", "long", BinaryOperator.NotEqual, TwoToThe53 + 2, literalFirst: false));
		file.Members.Add(Comparison("isPastAsDouble", "double", BinaryOperator.Equal, TwoToThe53 + 1, literalFirst: false));
		return file;
	}

	private static FunctionDeclaration Comparison(string name, string type, BinaryOperator op, long value, bool literalFirst)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference("bool") };
		function.Parameters.Add(new Parameter("x", type));
		Expression variable = new VariableReference("x");
		Expression literal = new LiteralExpression<long>(value);
		function.Body.Add(new ReturnStatement(literalFirst
			? new BinaryExpression(literal, op, variable)
			: new BinaryExpression(variable, op, literal)));
		return function;
	}
}
