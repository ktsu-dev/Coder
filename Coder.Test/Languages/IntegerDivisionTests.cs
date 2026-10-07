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
