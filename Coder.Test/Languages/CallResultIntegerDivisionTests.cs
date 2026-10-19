// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a call to a function declared beside it is known to return what that function says, so
/// Python and JavaScript divide its result the way the C family does (#273).
/// </summary>
/// <remarks>
/// <c>twice(a) / 4</c> with <c>twice</c> declared <c>int</c> is <c>-3</c> for <c>a = -7</c> in C, C++,
/// C#, Go and Rust. Without the callee's return type, Python's and JavaScript's <c>/</c> are true
/// division and give <c>-3.5</c>, which still runs, so the generated code is run rather than read.
/// </remarks>
[TestClass]
public class CallResultIntegerDivisionTests
{
	/// <summary>
	/// A file holding <c>int twice(int a)</c> and, for <c>/</c> and <c>%</c>, a function that divides
	/// its result once into a typed local and once in a return.
	/// </summary>
	private static SourceFile File()
	{
		SourceFile file = new("calls");

		FunctionDeclaration twice = new("twice") { ReturnType = new TypeReference("int") };
		twice.Parameters.Add(new Parameter("a", "int"));
		twice.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("a"), BinaryOperator.Multiply, Literal.Number(2))));
		file.Members.Add(twice);

		foreach ((string name, BinaryOperator op) in new[] { ("half", BinaryOperator.Divide), ("rest", BinaryOperator.Modulo) })
		{
			FunctionDeclaration local = new($"{name}_local") { ReturnType = new TypeReference("int") };
			local.Parameters.Add(new Parameter("a", "int"));
			local.Body.Add(new VariableDeclaration("t", "int", Twice()));
			local.Body.Add(new AssignmentStatement(new VariableReference("t"), new BinaryExpression(Twice(), op, Literal.Number(4))));
			local.Body.Add(new ReturnStatement(new VariableReference("t")));
			file.Members.Add(local);

			FunctionDeclaration returned = new($"{name}_return") { ReturnType = new TypeReference("int") };
			returned.Parameters.Add(new Parameter("a", "int"));
			returned.Body.Add(new ReturnStatement(new BinaryExpression(Twice(), op, Literal.Number(4))));
			file.Members.Add(returned);
		}

		return file;

		static CallExpression Twice()
		{
			CallExpression call = new("twice");
			call.Arguments.Add(new VariableReference("a"));
			return call;
		}
	}

	private static readonly int[] Arguments = [-7, 7, -9, 9, 0];

	private static IEnumerable<(string Function, int Argument, int Expected)> Cases()
	{
		foreach (int a in Arguments)
		{
			int twice = a * 2;
			yield return ("half_local", a, twice / 4);
			yield return ("half_return", a, twice / 4);
			yield return ("rest_local", a, twice % 4);
			yield return ("rest_return", a, twice % 4);
		}
	}

	/// <summary>
	/// Tests that Python truncates a division and a remainder of a sibling function's <c>int</c> result.
	/// </summary>
	[TestMethod]
	public void Python_CallResultOfADeclaredIntFunction_MatchesTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new PythonGenerator().Generate(File()));
		module.AppendLine();
		foreach ((string function, int argument, int expected) in Cases())
		{
			module.AppendLine(FormattableString.Invariant($"r = {function}({argument})"));
			module.AppendLine(FormattableString.Invariant($"assert type(r) is int and r == {expected}, f\"{function}({argument}) = {{r!r}}, expected {expected}\""));
		}

		Run(python, "check.py", module.ToString());
	}

	/// <summary>
	/// Tests that JavaScript truncates a division and a remainder of a sibling function's <c>int</c> result.
	/// </summary>
	[TestMethod]
	public void JavaScript_CallResultOfADeclaredIntFunction_MatchesTheCFamily()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new JavaScriptGenerator().Generate(File()));
		module.AppendLine();
		foreach ((string function, int argument, int expected) in Cases())
		{
			module.AppendLine(FormattableString.Invariant($"{{ const r = {function}({argument}); if (r !== {expected}) {{ throw new Error(`{function}({argument}) = ${{r}}, expected {expected}`); }} }}"));
		}

		Run(node, "check.js", module.ToString());
	}

	/// <summary>
	/// Tests that a call whose callee is not declared in the tree, or is declared more than once, is
	/// still of unknown type, so its division is left as the target's own.
	/// </summary>
	[TestMethod]
	public void UnknownOrOverloadedCallee_StaysTrueDivision()
	{
		SourceFile file = new("calls");

		FunctionDeclaration first = new("twice") { ReturnType = new TypeReference("int") };
		first.Parameters.Add(new Parameter("a", "int"));
		first.Body.Add(new ReturnStatement(new VariableReference("a")));
		file.Members.Add(first);

		FunctionDeclaration second = new("twice") { ReturnType = new TypeReference("double") };
		second.Parameters.Add(new Parameter("a", "double"));
		second.Body.Add(new ReturnStatement(new VariableReference("a")));
		file.Members.Add(second);

		FunctionDeclaration divides = new("divides") { ReturnType = new TypeReference("int") };
		divides.Body.Add(new ReturnStatement(new BinaryExpression(new CallExpression("twice"), BinaryOperator.Divide, Literal.Number(4))));
		divides.Body.Add(new ReturnStatement(new BinaryExpression(new CallExpression("elsewhere"), BinaryOperator.Divide, Literal.Number(4))));
		file.Members.Add(divides);

		string python = new PythonGenerator().Generate(file);
		Assert.Contains("(twice() / 4)", python, StringComparison.Ordinal, python);
		Assert.Contains("(elsewhere() / 4)", python, StringComparison.Ordinal, python);

		string javascript = new JavaScriptGenerator().Generate(file);
		Assert.DoesNotContain("Math.trunc", javascript, StringComparison.Ordinal, javascript);
	}

	private static void Run(string runtime, string fileName, string source) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, fileName), source);
			(int exitCode, string output) = ToolchainHarness.Run(runtime, fileName, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
}
