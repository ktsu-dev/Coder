// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that <c>&amp;</c>, <c>|</c> and <c>^</c> and their compound assignments on two booleans give
/// a boolean with C#'s truth table in Go and JavaScript (#268).
/// </summary>
/// <remarks>
/// Go defines the operators on integers only, so its output did not build, and JavaScript coerces the
/// operands to numbers, so its output ran and returned <c>0</c> or <c>1</c>. The generated code is run
/// rather than read, since a JavaScript number reads like the boolean it should have been.
/// </remarks>
[TestClass]
public class BooleanBitwiseOperatorTests
{
	private static readonly (string Name, Func<bool, bool, bool> Expected)[] Functions =
	[
		("mix", (a, b) => (a ^ b) & !a),
		("and_of", (a, b) => a & b),
		("or_of", (a, b) => a | b),
		("xor_of", (a, b) => a ^ b),
		("and_call", (a, b) => a & b),
		("or_call", (a, b) => a | b),
		("and_assign", (a, b) => a & b),
		("or_assign", (a, b) => a | b),
		("xor_assign", (a, b) => a ^ b),
	];

	private static readonly (bool A, bool B)[] Inputs = [(false, false), (false, true), (true, false), (true, true)];

	/// <summary>
	/// A file holding <c>bool same(bool v)</c> and one <c>bool f(bool a, bool b)</c> per entry of
	/// <see cref="Functions"/>.
	/// </summary>
	private static SourceFile File()
	{
		SourceFile file = new("main");

		FunctionDeclaration same = new("same") { ReturnType = new TypeReference("bool") };
		same.Parameters.Add(new Parameter("v", "bool"));
		same.Body.Add(new ReturnStatement(new VariableReference("v")));
		file.Members.Add(same);

		file.Members.Add(Returning("mix", new BinaryExpression(
			new BinaryExpression(A(), BinaryOperator.BitwiseXor, B()),
			BinaryOperator.BitwiseAnd,
			new UnaryExpression(UnaryOperator.LogicalNot, A()))));
		file.Members.Add(Returning("and_of", new BinaryExpression(A(), BinaryOperator.BitwiseAnd, B())));
		file.Members.Add(Returning("or_of", new BinaryExpression(A(), BinaryOperator.BitwiseOr, B())));
		file.Members.Add(Returning("xor_of", new BinaryExpression(A(), BinaryOperator.BitwiseXor, B())));
		file.Members.Add(Returning("and_call", new BinaryExpression(A(), BinaryOperator.BitwiseAnd, Same())));
		file.Members.Add(Returning("or_call", new BinaryExpression(A(), BinaryOperator.BitwiseOr, Same())));
		file.Members.Add(Assigning("and_assign", AssignmentOperator.BitwiseAndAssign));
		file.Members.Add(Assigning("or_assign", AssignmentOperator.BitwiseOrAssign));
		file.Members.Add(Assigning("xor_assign", AssignmentOperator.BitwiseXorAssign));
		return file;

		static VariableReference A() => new("a");
		static VariableReference B() => new("b");

		static CallExpression Same()
		{
			CallExpression call = new("same");
			call.Arguments.Add(B());
			return call;
		}

		static FunctionDeclaration Signature(string name)
		{
			FunctionDeclaration function = new(name) { ReturnType = new TypeReference("bool") };
			function.Parameters.Add(new Parameter("a", "bool"));
			function.Parameters.Add(new Parameter("b", "bool"));
			return function;
		}

		static FunctionDeclaration Returning(string name, Expression value)
		{
			FunctionDeclaration function = Signature(name);
			function.Body.Add(new ReturnStatement(value));
			return function;
		}

		static FunctionDeclaration Assigning(string name, AssignmentOperator op)
		{
			FunctionDeclaration function = Signature(name);
			function.Body.Add(new VariableDeclaration("x", "bool", A()));
			function.Body.Add(new AssignmentStatement(new VariableReference("x"), B(), op));
			function.Body.Add(new ReturnStatement(new VariableReference("x")));
			return function;
		}
	}

	private static string Expected()
	{
		StringBuilder expected = new();
		foreach ((string name, Func<bool, bool, bool> function) in Functions)
		{
			foreach ((bool a, bool b) in Inputs)
			{
				expected.AppendLine(FormattableString.Invariant($"{name} {Spell(a)} {Spell(b)} {Spell(function(a, b))}"));
			}
		}

		return expected.ToString().Trim();
	}

	private static string Spell(bool value) => value ? "true" : "false";

	/// <summary>
	/// Tests that the Go output builds, is gofmt-clean, and gives C#'s truth table.
	/// </summary>
	[TestMethod]
	public void Go_BooleanBitwiseOperators_BuildAndMatchCSharp()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		string source = new GoGenerator().Generate(File());
		Assert.Contains("func(l, r bool) bool { return l && r }(", source, StringComparison.Ordinal, source);

		StringBuilder driver = new();
		driver.AppendLine("package main");
		driver.AppendLine();
		driver.AppendLine("import \"fmt\"");
		driver.AppendLine();
		driver.AppendLine("func main() {");
		foreach ((string name, _) in Functions)
		{
			foreach ((bool a, bool b) in Inputs)
			{
				driver.AppendLine(FormattableString.Invariant($"\tfmt.Println(\"{name}\", {Spell(a)}, {Spell(b)}, {name}({Spell(a)}, {Spell(b)}))"));
			}
		}

		driver.AppendLine("}");

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, "go.mod"), "module bitwise\n\ngo 1.21\n");
			System.IO.File.WriteAllText(Path.Combine(directory, "driver.go"), driver.ToString());
			System.IO.File.WriteAllText(Path.Combine(directory, "bitwise.go"), source);

			(int exitCode, string output) = ToolchainHarness.Run("go", "run .", directory);
			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
			Assert.AreEqual(Expected(), output.Trim().ReplaceLineEndings("\n"), source);

			(int formatted, string differs) = ToolchainHarness.Run("gofmt", "-l bitwise.go", directory);
			Assert.AreEqual(0, formatted, $"gofmt did not run:{Environment.NewLine}{differs}");
			Assert.AreEqual(string.Empty, differs.Trim(), $"gofmt would rewrite the generated source.{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that the JavaScript output returns a boolean, not a number, with C#'s truth table.
	/// </summary>
	[TestMethod]
	public void JavaScript_BooleanBitwiseOperators_ReturnBooleansMatchingCSharp()
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
		foreach ((string name, _) in Functions)
		{
			foreach ((bool a, bool b) in Inputs)
			{
				module.AppendLine(FormattableString.Invariant($"{{ const r = {name}({Spell(a)}, {Spell(b)}); if (typeof r !== \"boolean\") {{ throw new Error(`{name}({Spell(a)}, {Spell(b)}) = ${{r}} is a ${{typeof r}}`); }} console.log(`{name} {Spell(a)} {Spell(b)} ${{r}}`); }}"));
			}
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, "check.js"), module.ToString());
			(int exitCode, string output) = ToolchainHarness.Run(node, "check.js", directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
			Assert.AreEqual(Expected(), output.Trim().ReplaceLineEndings("\n"), source);
		});
	}

	/// <summary>
	/// Tests that a bitwise operator on operands not known to be <c>bool</c> is written as before.
	/// </summary>
	[TestMethod]
	public void IntegerOrUnknownOperands_KeepTheirBitwiseOperator()
	{
		SourceFile file = new("main");
		FunctionDeclaration function = new("mask") { ReturnType = new TypeReference("int") };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Parameters.Add(new Parameter("b", "int"));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("a"), BinaryOperator.BitwiseXor, new VariableReference("u"))));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("a"), BinaryOperator.BitwiseAnd, new VariableReference("b"))));
		file.Members.Add(function);

		string go = new GoGenerator().Generate(file);
		Assert.Contains("(a ^ u)", go, StringComparison.Ordinal, go);
		Assert.Contains("(a & b)", go, StringComparison.Ordinal, go);

		string javascript = new JavaScriptGenerator().Generate(file);
		Assert.Contains("(a ^ u)", javascript, StringComparison.Ordinal, javascript);
		Assert.DoesNotContain("Boolean(", javascript, StringComparison.Ordinal, javascript);
	}
}
