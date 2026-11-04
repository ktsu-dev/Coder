// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a floating-point quotient or remainder by zero gives the IEEE 754 result in Python, as
/// it does in every other target (#425).
/// </summary>
/// <remarks>
/// Python's <c>/</c> raises <c>ZeroDivisionError</c> for a zero divisor, and <c>math.fmod</c> raises
/// <c>ValueError</c> for a zero divisor or an infinite dividend. C#, C, C++, JavaScript, Go and Rust
/// return an infinity or NaN for the same expressions, so code that checks for one crashes only in
/// the Python output.
/// </remarks>
[TestClass]
public class PythonFloatingDivisionByZeroTests
{
	/// <summary>
	/// What the generated functions should return, checked by the interpreter.
	/// </summary>
	/// <remarks>
	/// <c>ratio_of_calls</c> divides two calls, which the generator binds once rather than reading
	/// twice, so the bound form is run as well as the inline one.
	/// </remarks>
	private const string Checks = """

		import math

		assert ratio(1.0, 0.0) == math.inf, ratio(1.0, 0.0)
		assert ratio(-1.0, 0.0) == -math.inf, ratio(-1.0, 0.0)
		assert ratio(1.0, -0.0) == -math.inf, ratio(1.0, -0.0)
		assert math.isnan(ratio(0.0, 0.0)), ratio(0.0, 0.0)
		assert math.isnan(ratio(math.nan, 0.0)), ratio(math.nan, 0.0)
		assert ratio(7.0, 2.0) == 3.5, ratio(7.0, 2.0)
		assert ratio_of_calls(1.0, 0.0) == math.inf, ratio_of_calls(1.0, 0.0)
		assert ratio_of_calls(7.0, 2.0) == 3.5, ratio_of_calls(7.0, 2.0)
		assert halve_into(1.0, 0.0) == math.inf, halve_into(1.0, 0.0)
		assert halve_into(7.0, 2.0) == 3.5, halve_into(7.0, 2.0)
		assert math.isnan(rem(1.0, 0.0)), rem(1.0, 0.0)
		assert math.isnan(rem(math.inf, 2.0)), rem(math.inf, 2.0)
		assert rem(-7.5, 2.0) == -1.5, rem(-7.5, 2.0)
		assert rem(7.5, math.inf) == 7.5, rem(7.5, math.inf)
		assert math.isnan(rem_into(1.0, 0.0)), rem_into(1.0, 0.0)
		assert whole(7, 2) == 3, whole(7, 2)

		""";

	/// <summary>
	/// Tests that <c>1.0 / 0.0</c>, <c>-1.0 / 0.0</c>, <c>0.0 / 0.0</c>, <c>1.0 % 0.0</c> and
	/// <c>inf % 2.0</c> give <c>inf</c>, <c>-inf</c>, <c>nan</c>, <c>nan</c> and <c>nan</c>, and that
	/// a quotient or remainder that is not by zero is unchanged.
	/// </summary>
	[TestMethod]
	public void FloatingDivisionAndRemainderByZero_GiveIeeeResults()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		string source = new PythonGenerator().Generate(FileOfQuotients()) + Checks;

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "check.py"), source);
			(int exitCode, string output) = ToolchainHarness.Run(python, "check.py", directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a file whose only floating operation is a quotient imports <c>math</c> once, and
	/// that integer division neither imports it nor changes.
	/// </summary>
	[TestMethod]
	public void FloatingDivision_ImportsMathOnce()
	{
		SourceFile floating = new("quotients");
		floating.Members.Add(Quotient("ratio", "double", new VariableReference("a"), new VariableReference("b")));
		SourceFile integer = new("quotients");
		integer.Members.Add(Quotient("whole", "int", new VariableReference("a"), new VariableReference("b")));

		string floatingCode = new PythonGenerator().Generate(floating);
		string integerCode = new PythonGenerator().Generate(integer);

		Assert.AreEqual(1, floatingCode.Split("import math").Length - 1, floatingCode);
		Assert.DoesNotContain("import math", integerCode, StringComparison.Ordinal, integerCode);
		Assert.DoesNotContain("math.", integerCode, StringComparison.Ordinal, integerCode);
	}

	private static SourceFile FileOfQuotients()
	{
		SourceFile file = new("quotients");
		file.Members.Add(Identity());
		file.Members.Add(Quotient("ratio", "double", new VariableReference("a"), new VariableReference("b")));
		file.Members.Add(Quotient("ratio_of_calls", "double", Call("identity", "a"), Call("identity", "b")));
		file.Members.Add(Compound("halve_into", "double", AssignmentOperator.DivideAssign));
		file.Members.Add(Remainder("rem", "double"));
		file.Members.Add(Compound("rem_into", "double", AssignmentOperator.ModuloAssign));
		file.Members.Add(Quotient("whole", "int", new VariableReference("a"), new VariableReference("b")));
		return file;
	}

	private static FunctionDeclaration Identity()
	{
		FunctionDeclaration function = new("identity") { ReturnType = new TypeReference("double") };
		function.Parameters.Add(new Parameter("x", "double"));
		function.Body.Add(new ReturnStatement(new VariableReference("x")));
		return function;
	}

	private static CallExpression Call(string callee, string argument)
	{
		CallExpression call = new(callee);
		call.Arguments.Add(new VariableReference(argument));
		return call;
	}

	private static FunctionDeclaration Quotient(string name, string type, Expression left, Expression right)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference(type) };
		function.Parameters.Add(new Parameter("a", type));
		function.Parameters.Add(new Parameter("b", type));
		function.Body.Add(new ReturnStatement(new BinaryExpression(left, BinaryOperator.Divide, right)));
		return function;
	}

	private static FunctionDeclaration Remainder(string name, string type)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference(type) };
		function.Parameters.Add(new Parameter("a", type));
		function.Parameters.Add(new Parameter("b", type));
		function.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("a"), BinaryOperator.Modulo, new VariableReference("b"))));
		return function;
	}

	private static FunctionDeclaration Compound(string name, string type, AssignmentOperator op)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference(type) };
		function.Parameters.Add(new Parameter("a", type));
		function.Parameters.Add(new Parameter("b", type));
		function.Body.Add(new VariableDeclaration("x", type, new VariableReference("a")));
		function.Body.Add(new AssignmentStatement(new VariableReference("x"), new VariableReference("b"), op));
		function.Body.Add(new ReturnStatement(new VariableReference("x")));
		return function;
	}
}
