// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Globalization;
using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that integer <c>/=</c> and <c>%=</c> mean the same thing in Python and JavaScript as in the C
/// family.
/// </summary>
/// <remarks>
/// The compound-assignment counterpart of <see cref="IntegerDivisionTests"/>. Python's and JavaScript's
/// <c>/=</c> are true division, so <c>a = 5; a /= 2</c> leaves <c>2.5</c>, and Python's <c>%=</c> floors,
/// so <c>a = -7; a %= 2</c> leaves <c>1</c> where the C family leaves <c>-1</c>. The generated code is
/// run rather than read.
/// </remarks>
[TestClass]
public class CompoundIntegerDivisionTests
{
	private static readonly (int Left, int Right)[] Operands = [(-7, 2), (7, -2), (-7, -2), (7, 2), (-6, 3), (0, -5), (5, 2)];

	/// <summary>
	/// The functions under test, each of which applies a compound assignment to an integer and returns
	/// it: to a parameter, to a typed local, with a literal divisor, and with a divisor that is a call,
	/// which must not be made twice.
	/// </summary>
	private static IEnumerable<FunctionDeclaration> Functions()
	{
		foreach (AssignmentOperator op in new[] { AssignmentOperator.DivideAssign, AssignmentOperator.ModuloAssign })
		{
			string name = op == AssignmentOperator.DivideAssign ? "div" : "mod";

			FunctionDeclaration parameters = new($"{name}_params") { ReturnType = new TypeReference("int") };
			parameters.Parameters.Add(new Parameter("a", "int"));
			parameters.Parameters.Add(new Parameter("b", "int"));
			parameters.Body.Add(new AssignmentStatement(new VariableReference("a"), new VariableReference("b"), op));
			parameters.Body.Add(new ReturnStatement(new VariableReference("a")));
			yield return parameters;

			FunctionDeclaration local = new($"{name}_local") { ReturnType = new TypeReference("int") };
			local.Parameters.Add(new Parameter("a", "int"));
			local.Parameters.Add(new Parameter("b", "int"));
			local.Body.Add(new VariableDeclaration("q", "int", new VariableReference("a")));
			local.Body.Add(new AssignmentStatement(new VariableReference("q"), new VariableReference("b"), op));
			local.Body.Add(new ReturnStatement(new VariableReference("q")));
			yield return local;

			FunctionDeclaration literal = new($"{name}_literal") { ReturnType = new TypeReference("int") };
			literal.Parameters.Add(new Parameter("a", "int"));
			literal.Parameters.Add(new Parameter("b", "int"));
			literal.Body.Add(new AssignmentStatement(new VariableReference("a"), new LiteralExpression<int>(2), op));
			literal.Body.Add(new ReturnStatement(new VariableReference("a")));
			yield return literal;

			FunctionDeclaration call = new($"{name}_call") { ReturnType = new TypeReference("int") };
			call.Parameters.Add(new Parameter("a", "int"));
			call.Parameters.Add(new Parameter("b", "int"));
			CallExpression counted = new("counted") { ExpectedType = "int" };
			counted.Arguments.Add(new VariableReference("b"));
			call.Body.Add(new AssignmentStatement(new VariableReference("a"), counted, op));
			call.Body.Add(new ReturnStatement(new VariableReference("a")));
			yield return call;
		}
	}

	private static int Expected(string function, int left, int right)
	{
		int divisor = function.EndsWith("_literal", StringComparison.Ordinal) ? 2 : right;
		return function.StartsWith("div", StringComparison.Ordinal) ? left / divisor : left % divisor;
	}

	/// <summary>
	/// Tests that Python computes the C family's quotient and remainder, as an <c>int</c>, and makes
	/// the divisor's call once.
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
				string arguments = string.Create(CultureInfo.InvariantCulture, $"{left}, {right}");
				int expected = Expected(function.Name!, left, right);
				module.AppendLine("calls = 0");
				module.AppendLine(CultureInfo.InvariantCulture, $"r = {function.Name}({arguments})");
				module.AppendLine(CultureInfo.InvariantCulture, $"assert type(r) is int and r == {expected}, f\"{function.Name}({arguments}) = {{r!r}}, expected {expected}\"");
				if (function.Name!.EndsWith("_call", StringComparison.Ordinal))
				{
					module.AppendLine(CultureInfo.InvariantCulture, $"assert calls == 1, f\"{function.Name}({arguments}) made {{calls}} calls\"");
				}
			}
		}

		Run(python, "check.py", module.ToString());
	}

	/// <summary>
	/// Tests that JavaScript computes the C family's quotient and remainder, and makes the divisor's
	/// call once.
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
		module.AppendLine("let calls = 0;");
		module.AppendLine("function counted(x) { calls++; return x; }");
		foreach (FunctionDeclaration function in Functions())
		{
			module.AppendLine(generator.Generate(function));
		}

		foreach (FunctionDeclaration function in Functions())
		{
			foreach ((int left, int right) in Operands)
			{
				string arguments = string.Create(CultureInfo.InvariantCulture, $"{left}, {right}");
				int expected = Expected(function.Name!, left, right);

				// Strict equality rather than Object.is: JavaScript's -0 is an integer zero to every
				// arithmetic and comparison a consumer would make of it.
				module.AppendLine(CultureInfo.InvariantCulture, $"{{ calls = 0; const r = {function.Name}({arguments}); if (r !== {expected}) {{ throw new Error(`{function.Name}({arguments}) = ${{r}}, expected {expected}`); }} if (calls > 1) {{ throw new Error(`{function.Name}({arguments}) made ${{calls}} calls`); }} }}");
			}
		}

		Run(node, "check.js", module.ToString());
	}

	/// <summary>
	/// Tests that a compound assignment the AST does not know to be on integers, and every other
	/// compound operator, is left as it is.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	[TestMethod]
	[DataRow("python")]
	[DataRow("javascript")]
	public void OtherCompoundAssignments_AreUnchanged(string language)
	{
		ILanguageGenerator generator = language == "python" ? new PythonGenerator() : new JavaScriptGenerator();

		AssignmentStatement unknown = new(new VariableReference("a"), new VariableReference("b"), AssignmentOperator.DivideAssign);
		AssignmentStatement doubles = new(new VariableReference("a"), new LiteralExpression<double>(2.0), AssignmentOperator.DivideAssign);
		AssignmentStatement add = new(new VariableReference("a"), new LiteralExpression<int>(2), AssignmentOperator.AddAssign);
		string end = language == "python" ? string.Empty : ";";

		Assert.AreEqual($"a /= b{end}", generator.Generate(unknown).Trim());
		Assert.AreEqual($"a /= 2.0{end}", generator.Generate(doubles).Trim());
		Assert.AreEqual($"a += 2{end}", generator.Generate(add).Trim());
	}

	private static void Run(string runtime, string fileName, string source) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, fileName), source);
			(int exitCode, string output) = ToolchainHarness.Run(runtime, fileName, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
}
