// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that two values of an enumeration can be ordered, and in the order of their numbers, in the
/// two targets that used to refuse it (#295).
/// </summary>
/// <remarks>
/// C, C++, C#, Go and JavaScript compare an enumeration's values as the integers they are. A plain
/// Python <c>Enum</c> defines no ordering, so <c>a &lt; b</c> raised <c>TypeError</c> at run time, and a
/// Rust enumeration that does not derive <c>PartialOrd</c> is E0369. Both are run rather than read,
/// with members declared out of numeric order so a target ordering by declaration would fail.
/// </remarks>
[TestClass]
public class EnumOrderingTests
{
	/// <summary>
	/// A file holding <c>enum Level { High = 3, Low = 1, Middle = 2 }</c> and a function for each
	/// ordering operator over two of its values.
	/// </summary>
	private static SourceFile File()
	{
		SourceFile file = new("levels");

		EnumDeclaration level = new("Level") { UnderlyingType = "int" };
		level.Members.Add(new EnumMember("High") { Value = "3" });
		level.Members.Add(new EnumMember("Low") { Value = "1" });
		level.Members.Add(new EnumMember("Middle") { Value = "2" });
		file.Members.Add(level);

		foreach ((string name, BinaryOperator op) in Operators)
		{
			FunctionDeclaration compare = new(name) { ReturnType = "bool" };
			compare.Parameters.Add(new Parameter("a", "Level"));
			compare.Parameters.Add(new Parameter("b", "Level"));
			compare.Body.Add(new ReturnStatement(
				new BinaryExpression(new VariableReference("a"), op, new VariableReference("b"))));
			file.Members.Add(compare);
		}

		return file;
	}

	private static readonly (string Name, BinaryOperator Op)[] Operators =
	[
		("less", BinaryOperator.LessThan),
		("at_most", BinaryOperator.LessThanOrEqual),
		("greater", BinaryOperator.GreaterThan),
		("at_least", BinaryOperator.GreaterThanOrEqual),
	];

	private static readonly (string Name, int Value)[] Members = [("High", 3), ("Low", 1), ("Middle", 2)];

	private static IEnumerable<(string Function, string A, string B, bool Expected)> Cases()
	{
		foreach ((string a, int x) in Members)
		{
			foreach ((string b, int y) in Members)
			{
				yield return ("less", a, b, x < y);
				yield return ("at_most", a, b, x <= y);
				yield return ("greater", a, b, x > y);
				yield return ("at_least", a, b, x >= y);
			}
		}
	}

	/// <summary>
	/// Tests that generated Python orders two enumeration values by their numbers.
	/// </summary>
	[TestMethod]
	public void Python_OrdersEnumValuesByTheirNumbers()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new PythonGenerator().Generate(File()));
		module.AppendLine();
		foreach ((string function, string a, string b, bool expected) in Cases())
		{
			string call = $"{function}(Level.{a}, Level.{b})";
			module.AppendLine($"assert {call} is {(expected ? "True" : "False")}, \"{call}\"");
		}

		Run(python, "check.py", module.ToString());
	}

	/// <summary>
	/// Tests that generated Rust compiles an ordering of two enumeration values and orders them by
	/// their numbers.
	/// </summary>
	[TestMethod]
	public void Rust_OrdersEnumValuesByTheirNumbers()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		string generated = new RustGenerator().Generate(File());
		StringBuilder source = new();
		source.AppendLine("#![allow(unused_parens, dead_code)]");
		source.AppendLine(generated);
		source.AppendLine("fn main() {");
		foreach ((string function, string a, string b, bool expected) in Cases())
		{
			string call = $"{function}(Level::{a}, Level::{b})";
			source.AppendLine($"    assert!({call} == {(expected ? "true" : "false")}, \"{call}\");");
		}

		source.AppendLine("}");

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, "levels.rs"), source.ToString());
			(int compiled, string compileOutput) = ToolchainHarness.Run("rustc", "--edition 2021 -o levels levels.rs", directory);
			Assert.AreEqual(0, compiled, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{compileOutput}");

			(int exitCode, string output) = ToolchainHarness.Run(Path.Combine(directory, "levels"), string.Empty, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
	}

	private static void Run(string runtime, string fileName, string source) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, fileName), source);
			(int exitCode, string output) = ToolchainHarness.Run(runtime, fileName, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
}
