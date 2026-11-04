// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the bitwise and shift operators on a <c>long</c>, <c>ulong</c> or <c>uint</c> give C#'s
/// answer in JavaScript (#256), and that <c>~</c> and <c>&lt;&lt;</c> on the fixed-width integers give it
/// in Python (#351).
/// </summary>
/// <remarks>
/// JavaScript converts the operands of these operators to a signed 32-bit integer, so
/// <c>1 | 4294967296</c> was <c>1</c> and a <c>uint</c> of three billion shifted right by one was
/// negative. Python's integers are unbounded, so <c>~</c> on a <c>uint</c> was negative and
/// <c>1 &lt;&lt; 31</c> on an <c>int</c> did not wrap. The generated code is run rather than read, since the
/// wrong answer is a number like the right one.
/// </remarks>
[TestClass]
public class WideIntegerBitwiseOperatorTests
{
	private sealed record Case(string Name, string Type, string Input, string Expected, Func<FunctionDeclaration, FunctionDeclaration> Body);

	private static readonly Case[] Cases =
	[
		new("or_long", "long", "1", Invariant(1L | 4294967296L), f => Returning(f, new BinaryExpression(X(), BinaryOperator.BitwiseOr, new LiteralExpression<long>(4294967296L)))),
		new("and_long", "long", "12884901890", Invariant(12884901890L & 8589934591L), f => Returning(f, new BinaryExpression(X(), BinaryOperator.BitwiseAnd, new LiteralExpression<long>(8589934591L)))),
		new("xor_long", "long", "-1", Invariant(-1L ^ 4294967296L), f => Returning(f, new BinaryExpression(X(), BinaryOperator.BitwiseXor, new LiteralExpression<long>(4294967296L)))),
		new("shl_long", "long", "1", Invariant(1L << 40), f => Returning(f, new BinaryExpression(X(), BinaryOperator.LeftShift, new LiteralExpression<int>(40)))),
		new("shr_long", "long", "-8589934592", Invariant(-8589934592L >> 33), f => Returning(f, new BinaryExpression(X(), BinaryOperator.RightShift, new LiteralExpression<int>(33)))),
		new("not_long", "long", "4294967296", Invariant(~4294967296L), f => Returning(f, new UnaryExpression(UnaryOperator.BitwiseNot, X()))),
		new("and_ulong", "ulong", "281474976710655", Invariant(281474976710655UL & 64424509440UL), f => Returning(f, new BinaryExpression(X(), BinaryOperator.BitwiseAnd, new LiteralExpression<long>(64424509440L)))),
		new("shr_ulong", "ulong", "8589934592", Invariant(8589934592UL >> 1), f => Returning(f, new BinaryExpression(X(), BinaryOperator.RightShift, new LiteralExpression<int>(1)))),
		new("shr_uint", "uint", "3000000000", Invariant(3000000000U >> 1), f => Returning(f, new BinaryExpression(X(), BinaryOperator.RightShift, new LiteralExpression<int>(1)))),
		new("shl_uint", "uint", "2147483649", Invariant(2147483649U << 4), f => Returning(f, new BinaryExpression(X(), BinaryOperator.LeftShift, new LiteralExpression<int>(4)))),
		new("or_uint", "uint", "2147483648", Invariant(2147483648U | 1U), f => Returning(f, new BinaryExpression(X(), BinaryOperator.BitwiseOr, new LiteralExpression<int>(1)))),
		new("not_uint", "uint", "0", Invariant(~0U), f => Returning(f, new UnaryExpression(UnaryOperator.BitwiseNot, X()))),
		new("or_assign_long", "long", "1", Invariant(1L | 4294967296L), f => Assigning(f, AssignmentOperator.BitwiseOrAssign, new LiteralExpression<long>(4294967296L))),
		new("shl_assign_long", "long", "3", Invariant(3L << 35), f => Assigning(f, AssignmentOperator.LeftShiftAssign, new LiteralExpression<int>(35))),
		new("shr_assign_uint", "uint", "4294967295", Invariant(4294967295U >> 4), f => Assigning(f, AssignmentOperator.RightShiftAssign, new LiteralExpression<int>(4))),
		new("xor_assign_uint", "uint", "2147483648", Invariant(2147483648U ^ 1U), f => Assigning(f, AssignmentOperator.BitwiseXorAssign, new LiteralExpression<int>(1))),
	];

	/// <summary>
	/// Python only: results past 2^53 are rounded by JavaScript's number conversion, which #256 accepted.
	/// </summary>
	private static readonly Case[] FixedWidthCases =
	[
		new("not_uint_5", "uint", "5", Invariant(~5U), f => Returning(f, new UnaryExpression(UnaryOperator.BitwiseNot, X()))),
		new("not_ulong", "ulong", "5", Invariant(~5UL), f => Returning(f, new UnaryExpression(UnaryOperator.BitwiseNot, X()))),
		new("not_int", "int", "5", Invariant(~5), f => Returning(f, new UnaryExpression(UnaryOperator.BitwiseNot, X()))),
		new("shl_int", "int", "1", Invariant(1 << 31), f => Returning(f, new BinaryExpression(X(), BinaryOperator.LeftShift, new LiteralExpression<int>(31)))),
		new("shl_int_overflow", "int", "3", Invariant(3 << 31), f => Returning(f, new BinaryExpression(X(), BinaryOperator.LeftShift, new LiteralExpression<int>(31)))),
		new("shl_long_wrap", "long", "1", Invariant(1L << 63), f => Returning(f, new BinaryExpression(X(), BinaryOperator.LeftShift, new LiteralExpression<int>(63)))),
		new("shl_ulong", "ulong", "3", Invariant(3UL << 63), f => Returning(f, new BinaryExpression(X(), BinaryOperator.LeftShift, new LiteralExpression<int>(63)))),
		new("shl_assign_int", "int", "1", Invariant(1 << 31), f => Assigning(f, AssignmentOperator.LeftShiftAssign, new LiteralExpression<int>(31))),
		new("shl_assign_uint", "uint", "2147483649", Invariant(2147483649U << 1), f => Assigning(f, AssignmentOperator.LeftShiftAssign, new LiteralExpression<int>(1))),
	];

	private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

	private static string Invariant(ulong value) => value.ToString(CultureInfo.InvariantCulture);

	private static VariableReference X() => new("x");

	private static FunctionDeclaration Returning(FunctionDeclaration function, Expression value)
	{
		function.Body.Add(new ReturnStatement(value));
		return function;
	}

	private static FunctionDeclaration Assigning(FunctionDeclaration function, AssignmentOperator op, Expression value)
	{
		function.Body.Add(new AssignmentStatement(X(), value, op));
		function.Body.Add(new ReturnStatement(X()));
		return function;
	}

	private static SourceFile File() => File(Cases);

	private static SourceFile File(IEnumerable<Case> cases)
	{
		SourceFile file = new("main");
		foreach (Case entry in cases)
		{
			FunctionDeclaration function = new(entry.Name) { ReturnType = new TypeReference(entry.Type) };
			function.Parameters.Add(new Parameter("x", entry.Type));
			file.Members.Add(entry.Body(function));
		}

		return file;
	}

	/// <summary>
	/// Tests that the JavaScript output computes each operator at the width the operands declare.
	/// </summary>
	[TestMethod]
	public void JavaScript_WideIntegerBitwiseOperators_MatchCSharp()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		string source = new JavaScriptGenerator().Generate(File());
		StringBuilder module = new(source);
		module.AppendLine();
		StringBuilder expected = new();
		foreach (Case entry in Cases)
		{
			module.AppendLine(FormattableString.Invariant($"console.log(`{entry.Name} ${{{entry.Name}({entry.Input})}}`);"));
			expected.Append(FormattableString.Invariant($"{entry.Name} {entry.Expected}\n"));
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, "check.js"), module.ToString());
			(int exitCode, string output) = ToolchainHarness.Run(node, "check.js", directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
			Assert.AreEqual(expected.ToString().Trim(), output.Trim().ReplaceLineEndings("\n"), source);
		});
	}

	/// <summary>
	/// Tests that the Python output computes <c>~</c> and <c>&lt;&lt;</c> at the width the operands declare.
	/// </summary>
	[TestMethod]
	public void Python_FixedWidthBitwiseOperators_MatchCSharp()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		Case[] cases = [.. Cases, .. FixedWidthCases];
		string source = new PythonGenerator().Generate(File(cases));
		StringBuilder module = new(source);
		module.AppendLine();
		StringBuilder expected = new();
		foreach (Case entry in cases)
		{
			module.AppendLine(FormattableString.Invariant($"print(f\"{entry.Name} {{{entry.Name}({entry.Input})}}\")"));
			expected.Append(FormattableString.Invariant($"{entry.Name} {entry.Expected}\n"));
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, "check.py"), module.ToString());
			(int exitCode, string output) = ToolchainHarness.Run(python, "check.py", directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
			Assert.AreEqual(expected.ToString().Trim(), output.Trim().ReplaceLineEndings("\n"), source);
		});
	}

	/// <summary>
	/// Tests that <c>~</c> on a signed integer, and the operators other than <c>&lt;&lt;</c>, are written as before in Python.
	/// </summary>
	[TestMethod]
	public void Python_SignedNotAndOtherOperators_AreNotWrapped()
	{
		SourceFile file = new("main");
		FunctionDeclaration function = new("mask") { ReturnType = new TypeReference("int") };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("a"), BinaryOperator.RightShift, new LiteralExpression<int>(1))));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("u"), BinaryOperator.LeftShift, new VariableReference("a"))));
		function.Body.Add(new ReturnStatement(new UnaryExpression(UnaryOperator.BitwiseNot, new VariableReference("a"))));
		file.Members.Add(function);

		string python = new PythonGenerator().Generate(file);
		Assert.Contains("(a >> 1)", python, StringComparison.Ordinal, python);
		Assert.Contains("(u << a)", python, StringComparison.Ordinal, python);
		Assert.Contains("(~a)", python, StringComparison.Ordinal, python);
		Assert.DoesNotContain("0xFFFFFFFF", python, StringComparison.Ordinal, python);
	}

	/// <summary>
	/// Tests that a 64-bit operation is written in BigInt and a <c>uint</c> one is read back unsigned.
	/// </summary>
	[TestMethod]
	public void JavaScript_WideIntegerBitwiseOperators_AreWrittenAtTheirWidth()
	{
		string javascript = new JavaScriptGenerator().Generate(File());
		Assert.Contains("Number(BigInt.asIntN(64, BigInt(x) | BigInt(4294967296)))", javascript, StringComparison.Ordinal, javascript);
		Assert.Contains("Number(BigInt.asUintN(64, BigInt(x) & BigInt(64424509440)))", javascript, StringComparison.Ordinal, javascript);
		Assert.Contains("(x >>> 1)", javascript, StringComparison.Ordinal, javascript);
		Assert.Contains("((x << 4) >>> 0)", javascript, StringComparison.Ordinal, javascript);
		Assert.Contains("x = Number(BigInt.asIntN(64, BigInt(x) | BigInt(4294967296)));", javascript, StringComparison.Ordinal, javascript);
	}

	/// <summary>
	/// Tests that an operator on <c>int</c> operands, or ones of unknown type, is written as before.
	/// </summary>
	[TestMethod]
	public void JavaScript_IntOrUnknownOperands_KeepTheirOperator()
	{
		SourceFile file = new("main");
		FunctionDeclaration function = new("mask") { ReturnType = new TypeReference("int") };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("a"), BinaryOperator.RightShift, new LiteralExpression<int>(1))));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("u"), BinaryOperator.BitwiseOr, new VariableReference("a"))));
		function.Body.Add(new ReturnStatement(new UnaryExpression(UnaryOperator.BitwiseNot, new VariableReference("a"))));
		file.Members.Add(function);

		string javascript = new JavaScriptGenerator().Generate(file);
		Assert.Contains("(a >> 1)", javascript, StringComparison.Ordinal, javascript);
		Assert.Contains("(u | a)", javascript, StringComparison.Ordinal, javascript);
		Assert.Contains("(~a)", javascript, StringComparison.Ordinal, javascript);
		Assert.DoesNotContain("BigInt", javascript, StringComparison.Ordinal, javascript);
		Assert.DoesNotContain(">>>", javascript, StringComparison.Ordinal, javascript);
	}
}
