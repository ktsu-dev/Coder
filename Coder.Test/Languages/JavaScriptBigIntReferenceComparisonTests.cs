// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that comparing a <c>long</c> with a constant, a local or a call that holds a BigInt gives the
/// same answer in JavaScript as in every other target (#481).
/// </summary>
/// <remarks>
/// #424 converted the other operand with <c>BigInt(...)</c> only when one side was a BigInt literal
/// itself. A BigInt that reached the comparison through a name or a call was compared with
/// <c>===</c> against a number, which is always <c>false</c>, so a sentinel check never held. The
/// module is run rather than read, because the answer is what a consumer gets.
/// </remarks>
[TestClass]
public class JavaScriptBigIntReferenceComparisonTests
{
	private const long TwoToThe60 = 1L << 60;

	private const string Checks = """

		const check = (actual, expected, what) => {
		  if (actual !== expected) {
		    throw new Error(`${what}: expected ${expected}, got ${actual}`);
		  }
		};

		check(isLimit(2 ** 60), true, "2^60 == LIMIT");
		check(isLimit(1152921504606846976n), true, "2^60 as a BigInt == LIMIT");
		check(isLimit(1), false, "1 == LIMIT");
		check(notLimit(2 ** 60), false, "2^60 != LIMIT");
		check(notLimit(1), true, "1 != LIMIT");
		check(notNeg(-(2 ** 60)), false, "-2^60 != limit");
		check(notNeg(0), true, "0 != limit");
		check(isBig(2 ** 60), true, "2^60 == big()");
		check(isBig(2), false, "2 == big()");
		check(isSmall(5), true, "5 == small()");
		check(isSmall(6), false, "6 == small()");
		check(isLimitAsDouble(2 ** 60), true, "(double)2^60 == (double)LIMIT");

		""";

	/// <summary>
	/// Tests that the issue's constant, local and call comparisons hold exactly when the values are
	/// equal.
	/// </summary>
	[TestMethod]
	public void LongComparedWithANamedOrReturnedBigInt_ComparesValues()
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
	/// Tests that a comparison between two <c>long</c> names that hold no BigInt is left as <c>===</c>.
	/// </summary>
	[TestMethod]
	public void LongComparedWithANumberValuedLocal_IsUnchanged()
	{
		FunctionDeclaration function = new("same") { ReturnType = new TypeReference("bool") };
		function.Parameters.Add(new Parameter("x", "long"));
		function.Body.Add(new VariableDeclaration("y", "long", new LiteralExpression<long>(5)));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), BinaryOperator.Equal, new VariableReference("y"))));

		string code = new JavaScriptGenerator().Generate(function);

		StringAssert.Contains(code, "(x === y)", StringComparison.Ordinal);
		Assert.DoesNotContain("BigInt(", code, StringComparison.Ordinal, code);
	}

	private static SourceFile Comparisons()
	{
		SourceFile file = new("comparisons");
		file.Members.Add(new VariableDeclaration("LIMIT", "long", new LiteralExpression<long>(TwoToThe60)) { IsConstant = true });
		file.Members.Add(Compare("isLimit", "long", BinaryOperator.Equal, new VariableReference("LIMIT")));
		file.Members.Add(Compare("notLimit", "long", BinaryOperator.NotEqual, new VariableReference("LIMIT")));
		file.Members.Add(Compare("isLimitAsDouble", "double", BinaryOperator.Equal, new VariableReference("LIMIT")));

		FunctionDeclaration notNeg = Compare("notNeg", "long", BinaryOperator.NotEqual, new VariableReference("limit"));
		notNeg.Body.Insert(0, new VariableDeclaration("limit", "long", new UnaryExpression(UnaryOperator.Negate, new LiteralExpression<long>(TwoToThe60))));
		file.Members.Add(notNeg);

		file.Members.Add(Returning("big", TwoToThe60));
		file.Members.Add(Compare("isBig", "long", BinaryOperator.Equal, new CallExpression("big")));
		file.Members.Add(Returning("small", 5));
		file.Members.Add(Compare("isSmall", "long", BinaryOperator.Equal, new CallExpression("small")));
		return file;
	}

	private static FunctionDeclaration Compare(string name, string type, BinaryOperator op, Expression right)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference("bool") };
		function.Parameters.Add(new Parameter("x", type));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), op, right)));
		return function;
	}

	private static FunctionDeclaration Returning(string name, long value)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference("long") };
		function.Body.Add(new ReturnStatement(new LiteralExpression<long>(value)));
		return function;
	}
}
