// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that integer division and remainder mean the same thing in Python and JavaScript as in the
/// C family.
/// </summary>
/// <remarks>
/// The AST means <c>-7 / 2</c> on two integers as truncating division, which is <c>-3</c> in C, C++,
/// C#, Go and Rust. Python's and JavaScript's <c>/</c> are true division and give <c>-3.5</c>, and
/// Python's <c>%</c> floors, giving <c>1</c> for <c>-7 % 2</c> where the C family gives <c>-1</c>.
/// The generated code is run rather than read, because the spelling of a correct quotient is a choice
/// and its value is not.
/// </remarks>
[TestClass]
public class IntegerDivisionTests
{
	private static readonly (int Left, int Right)[] Operands = [(-7, 2), (7, -2), (-7, -2), (7, 2), (-6, 3), (0, -5)];

	/// <summary>
	/// The functions under test: integer parameters, integer literals, a typed local, and an operand
	/// that is a call, which is the one a remainder must not evaluate twice.
	/// </summary>
	private static IEnumerable<FunctionDeclaration> Functions()
	{
		foreach (BinaryOperator op in new[] { BinaryOperator.Divide, BinaryOperator.Modulo })
		{
			string name = op == BinaryOperator.Divide ? "div" : "mod";

			FunctionDeclaration parameters = new($"{name}_params") { ReturnType = new TypeReference("int") };
			parameters.Parameters.Add(new Parameter("a", "int"));
			parameters.Parameters.Add(new Parameter("b", "int"));
			parameters.Body.Add(new ReturnStatement(new BinaryExpression(
				new VariableReference("a"), op, new VariableReference("b"))));
			yield return parameters;

			FunctionDeclaration literals = new($"{name}_literals") { ReturnType = new TypeReference("int") };
			literals.Body.Add(new ReturnStatement(new BinaryExpression(
				new LiteralExpression<int>(-7), op, new LiteralExpression<int>(2))));
			yield return literals;

			FunctionDeclaration local = new($"{name}_local") { ReturnType = new TypeReference("int") };
			local.Parameters.Add(new Parameter("a", "int"));
			local.Parameters.Add(new Parameter("b", "int"));
			local.Body.Add(new VariableDeclaration("q", "int", new VariableReference("a")));
			local.Body.Add(new ReturnStatement(new BinaryExpression(
				new VariableReference("q"), op, new VariableReference("b"))));
			yield return local;

			FunctionDeclaration call = new($"{name}_call") { ReturnType = new TypeReference("int") };
			call.Parameters.Add(new Parameter("a", "int"));
			call.Parameters.Add(new Parameter("b", "int"));
			CallExpression counted = new("counted") { ExpectedType = "int" };
			counted.Arguments.Add(new VariableReference("a"));
			call.Body.Add(new ReturnStatement(new BinaryExpression(counted, op, new VariableReference("b"))));
			yield return call;
		}
	}

	private static int Expected(string function, int left, int right)
	{
		bool divide = function.StartsWith("div", StringComparison.Ordinal);
		(int a, int b) = function.EndsWith("_literals", StringComparison.Ordinal) ? (-7, 2) : (left, right);
		return divide ? a / b : a % b;
	}

	/// <summary>
	/// Tests that Python computes the C family's quotient and remainder, and calls an operand once.
	/// </summary>
	[TestMethod]
	public void Python_MatchesTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		PythonGenerator generator = new();
		StringBuilder module = new();
		module.AppendLine("calls = 0");
		module.AppendLine("def counted(x):");
		module.AppendLine("    global calls");
		module.AppendLine("    calls += 1");
		module.AppendLine("    return x");
		foreach (FunctionDeclaration function in Functions())
		{
			module.AppendLine(generator.Generate(function));
		}

		foreach (FunctionDeclaration function in Functions())
		{
			foreach ((int left, int right) in Operands)
			{
				string arguments = function.Parameters.Count == 0
					? string.Empty
					: string.Create(CultureInfo.InvariantCulture, $"{left}, {right}");
				int expected = Expected(function.Name!, left, right);
				module.AppendLine(CultureInfo.InvariantCulture, $"calls = 0");
				module.AppendLine(CultureInfo.InvariantCulture, $"r = {function.Name}({arguments})");
				module.AppendLine(CultureInfo.InvariantCulture, $"assert type(r) is int and r == {expected}, f\"{function.Name}({arguments}) = {{r!r}}, expected {expected}\"");
				if (function.Name!.EndsWith("_call", StringComparison.Ordinal))
				{
					module.AppendLine(CultureInfo.InvariantCulture, $"assert calls == 1, f\"{function.Name} called its operand {{calls}} times\"");
				}
			}
		}

		Run(python, "check.py", module.ToString());
	}

	/// <summary>
	/// Tests that Python computes the C family's quotient and remainder for <c>long</c> operands too
	/// large for a float to hold exactly, where going through true division rounds the quotient.
	/// </summary>
	[TestMethod]
	public void Python_LongOperandsAbove2To53_MatchTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		(long Left, long Right)[] operands =
		[
			(1700000000123456789L, 7L),
			(-1700000000123456789L, 7L),
			(1700000000123456789L, -7L),
			(-1700000000123456789L, -7L),
			(9007199254740993L, 1L),
			(-9007199254740993L, 2L),
			(long.MaxValue, 3L),
			(long.MinValue + 1, 10L),
		];

		PythonGenerator generator = new();
		StringBuilder module = new();

		// The generator still annotates a long as the undefined name `long` (#109); binding it here
		// keeps this test about the arithmetic.
		module.AppendLine("long = int");
		module.AppendLine("def counted(x):");
		module.AppendLine("    return x");

		foreach (BinaryOperator op in new[] { BinaryOperator.Divide, BinaryOperator.Modulo })
		{
			string name = op == BinaryOperator.Divide ? "div" : "mod";

			FunctionDeclaration parameters = new($"{name}_long") { ReturnType = new TypeReference("long") };
			parameters.Parameters.Add(new Parameter("a", "long"));
			parameters.Parameters.Add(new Parameter("b", "long"));
			parameters.Body.Add(new ReturnStatement(new BinaryExpression(
				new VariableReference("a"), op, new VariableReference("b"))));
			module.AppendLine(generator.Generate(parameters));

			FunctionDeclaration call = new($"{name}_long_call") { ReturnType = new TypeReference("long") };
			call.Parameters.Add(new Parameter("a", "long"));
			call.Parameters.Add(new Parameter("b", "long"));
			CallExpression counted = new("counted") { ExpectedType = "long" };
			counted.Arguments.Add(new VariableReference("a"));
			call.Body.Add(new ReturnStatement(new BinaryExpression(counted, op, new VariableReference("b"))));
			module.AppendLine(generator.Generate(call));

			FunctionDeclaration literal = new($"{name}_long_literal") { ReturnType = new TypeReference("long") };
			literal.Body.Add(new VariableDeclaration("n", "long", new LiteralExpression<long>(1700000000123456789L)));
			literal.Body.Add(new ReturnStatement(new BinaryExpression(
				new VariableReference("n"), op, new LiteralExpression<int>(7))));
			module.AppendLine(generator.Generate(literal));

			long literalExpected = op == BinaryOperator.Divide ? 1700000000123456789L / 7 : 1700000000123456789L % 7;
			module.AppendLine(CultureInfo.InvariantCulture, $"r = {name}_long_literal()");
			module.AppendLine(CultureInfo.InvariantCulture, $"assert type(r) is int and r == {literalExpected}, f\"{name}_long_literal() = {{r!r}}, expected {literalExpected}\"");

			foreach ((long left, long right) in operands)
			{
				long expected = op == BinaryOperator.Divide ? left / right : left % right;
				foreach (string function in new[] { $"{name}_long", $"{name}_long_call" })
				{
					string arguments = string.Create(CultureInfo.InvariantCulture, $"{left}, {right}");
					module.AppendLine(CultureInfo.InvariantCulture, $"r = {function}({arguments})");
					module.AppendLine(CultureInfo.InvariantCulture, $"assert type(r) is int and r == {expected}, f\"{function}({arguments}) = {{r!r}}, expected {expected}\"");
				}
			}
		}

		Run(python, "check_long.py", module.ToString());
	}

	/// <summary>
	/// Tests that JavaScript computes the C family's quotient and remainder.
	/// </summary>
	[TestMethod]
	public void JavaScript_MatchesTheCFamily()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		JavaScriptGenerator generator = new();
		StringBuilder module = new();
		module.AppendLine("function counted(x) { return x; }");
		foreach (FunctionDeclaration function in Functions())
		{
			module.AppendLine(generator.Generate(function));
		}

		foreach (FunctionDeclaration function in Functions())
		{
			foreach ((int left, int right) in Operands)
			{
				string arguments = function.Parameters.Count == 0
					? string.Empty
					: string.Create(CultureInfo.InvariantCulture, $"{left}, {right}");
				int expected = Expected(function.Name!, left, right);

				// Strict equality rather than Object.is: JavaScript's -0 is an integer zero to every
				// arithmetic and comparison a consumer would make of it.
				module.AppendLine(CultureInfo.InvariantCulture, $"{{ const r = {function.Name}({arguments}); if (r !== {expected}) {{ throw new Error(`{function.Name}({arguments}) = ${{r}}, expected {expected}`); }} }}");
			}
		}

		Run(node, "check.js", module.ToString());
	}

	/// <summary>
	/// A class whose property accessors divide: the setter records <c>value / 2</c> and
	/// <c>value % 2</c>, and the getter returns a typed local divided by 2.
	/// </summary>
	private static ClassDeclaration AccessorBox()
	{
		PropertyDeclaration half = new("half", new TypeReference("int"));
		half.GetterBody.Add(new VariableDeclaration("a", "int", new CallExpression("seed")));
		half.GetterBody.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("a"), BinaryOperator.Divide, new LiteralExpression<int>(2))));
		foreach (BinaryOperator op in new[] { BinaryOperator.Divide, BinaryOperator.Modulo })
		{
			CallExpression record = new("record");
			record.Arguments.Add(new BinaryExpression(new VariableReference("value"), op, new LiteralExpression<int>(2)));
			half.SetterBody.Add(new ExpressionStatement(record));
		}

		ClassDeclaration box = new("Box");
		box.Members.Add(half);
		return box;
	}

	/// <summary>
	/// An entry point that records a typed local divided by 2, and its remainder.
	/// </summary>
	private static EntryPoint DividingEntryPoint()
	{
		EntryPoint entryPoint = new();
		entryPoint.Body.Add(new VariableDeclaration("a", "int", new CallExpression("seed")));
		foreach (BinaryOperator op in new[] { BinaryOperator.Divide, BinaryOperator.Modulo })
		{
			CallExpression record = new("record");
			record.Arguments.Add(new BinaryExpression(new VariableReference("a"), op, new LiteralExpression<int>(2)));
			entryPoint.Body.Add(new ExpressionStatement(record));
		}

		return entryPoint;
	}

	/// <summary>
	/// Tests that Python truncates integer division in a property's setter and getter, and in an
	/// entry point, the way it does in a function (#191).
	/// </summary>
	[TestMethod]
	public void Python_AccessorAndEntryPointBodies_MatchTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		PythonGenerator generator = new();
		StringBuilder module = new();
		module.AppendLine("recorded = []");
		module.AppendLine("SEED = -7");
		module.AppendLine("def record(x):");
		module.AppendLine("    recorded.append(x)");
		module.AppendLine("def seed():");
		module.AppendLine("    return SEED");
		module.AppendLine(generator.Generate(AccessorBox()));
		module.AppendLine("b = Box()");
		foreach (int value in new[] { -7, 7 })
		{
			string expected = string.Create(CultureInfo.InvariantCulture, $"[{value / 2}, {value % 2}]");
			module.AppendLine("recorded.clear()");
			module.AppendLine(CultureInfo.InvariantCulture, $"b.half = {value}");
			module.AppendLine(CultureInfo.InvariantCulture, $"assert recorded == {expected}, f\"setter({value}) recorded {{recorded!r}}, expected {expected}\"");
		}

		module.AppendLine(CultureInfo.InvariantCulture, $"assert b.half == {-7 / 2}, f\"getter = {{b.half!r}}, expected {-7 / 2}\"");
		module.AppendLine("recorded.clear()");
		module.AppendLine(generator.Generate(DividingEntryPoint()));
		string entryExpected = string.Create(CultureInfo.InvariantCulture, $"[{-7 / 2}, {-7 % 2}]");
		module.AppendLine(CultureInfo.InvariantCulture, $"assert recorded == {entryExpected}, f\"entry point recorded {{recorded!r}}, expected {entryExpected}\"");

		Run(python, "check_accessors.py", module.ToString());
	}

	/// <summary>
	/// Tests that JavaScript truncates integer division in a property's setter and getter, and in an
	/// entry point, the way it does in a function (#191).
	/// </summary>
	[TestMethod]
	public void JavaScript_AccessorAndEntryPointBodies_MatchTheCFamily()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		JavaScriptGenerator generator = new();
		StringBuilder module = new();
		module.AppendLine("const recorded = [];");
		module.AppendLine("function record(x) { recorded.push(x); }");
		module.AppendLine("function seed() { return -7; }");
		module.AppendLine("function check(actual, expected, what) { if (JSON.stringify(actual) !== JSON.stringify(expected)) { throw new Error(`${what} = ${JSON.stringify(actual)}, expected ${JSON.stringify(expected)}`); } }");
		module.AppendLine(generator.Generate(AccessorBox()));
		module.AppendLine("const b = new Box();");
		foreach (int value in new[] { -7, 7 })
		{
			string expected = string.Create(CultureInfo.InvariantCulture, $"[{value / 2}, {value % 2}]");
			module.AppendLine("recorded.length = 0;");
			module.AppendLine(CultureInfo.InvariantCulture, $"b.half = {value};");
			module.AppendLine(CultureInfo.InvariantCulture, $"check(recorded, {expected}, \"setter({value})\");");
		}

		module.AppendLine(CultureInfo.InvariantCulture, $"check(b.half, {-7 / 2}, \"getter\");");
		module.AppendLine("recorded.length = 0;");
		module.AppendLine(generator.Generate(DividingEntryPoint()));
		string entryExpected = string.Create(CultureInfo.InvariantCulture, $"[{-7 / 2}, {-7 % 2}]");
		module.AppendLine(CultureInfo.InvariantCulture, $"check(recorded, {entryExpected}, \"entry point\");");

		Run(node, "check_accessors.js", module.ToString());
	}

	/// <summary>
	/// Tests that a division the AST does not know to be on integers is left as true division.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	[TestMethod]
	[DataRow("python")]
	[DataRow("javascript")]
	public void NonIntegerDivision_IsUnchanged(string language)
	{
		ILanguageGenerator generator = language == "python" ? new PythonGenerator() : new JavaScriptGenerator();

		BinaryExpression doubles = new(new LiteralExpression<double>(7.0), BinaryOperator.Divide, new LiteralExpression<int>(2));
		BinaryExpression unknown = new(new VariableReference("a"), BinaryOperator.Divide, new VariableReference("b"));

		Assert.AreEqual("(7.0 / 2)", generator.Generate(doubles).Trim());
		Assert.AreEqual("(a / b)", generator.Generate(unknown).Trim());
	}

	private static void Run(string runtime, string fileName, string source) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, fileName), source);
			(int exitCode, string output) = ToolchainHarness.Run(runtime, fileName, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
}
