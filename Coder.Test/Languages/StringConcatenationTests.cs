// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that <see cref="BinaryOperator.Add"/> on strings is written as something each target
/// accepts, in the two targets where <c>+</c> is not concatenation.
/// </summary>
/// <remarks>
/// Rust implements <c>+</c> only as <c>String + &amp;str</c>, so a literal on the left or a
/// <c>String</c> on the right does not compile; C's strings are pointers, and <c>+</c> on two of them
/// is a constraint violation. Both used to come out as a bare <c>+</c> with nothing saying why.
/// </remarks>
[TestClass]
public class StringConcatenationTests
{
	/// <summary>
	/// Tests that Rust writes a join as one <c>format!</c> call, with one placeholder per operand.
	/// </summary>
	[TestMethod]
	public void Rust_WritesAJoinAsFormat()
	{
		string code = new RustGenerator().Generate(Joins());

		StringAssert.Contains(code, "return format!(\"{}{}\", \"Hello, \", name);");
		StringAssert.Contains(code, "return format!(\"{}{}\", name, \"!\");");
		StringAssert.Contains(code, "return format!(\"{}{}\", a, b);");
		StringAssert.Contains(code, "return format!(\"{}{}{}\", a, \" \", b);");
	}

	/// <summary>
	/// Tests that Rust leaves an addition of numbers as <c>+</c>.
	/// </summary>
	[TestMethod]
	public void Rust_LeavesNumericAdditionAlone()
	{
		FunctionDeclaration sum = new("sum") { ReturnType = "int" };
		sum.Parameters.Add(new Parameter("a", "int"));
		sum.Parameters.Add(new Parameter("b", "int"));
		sum.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));

		string code = new RustGenerator().Generate(sum);

		StringAssert.Contains(code, "(a + b)");
		Assert.IsFalse(code.Contains("format!", StringComparison.Ordinal), code);
	}

	/// <summary>
	/// Tests that every mix of literal and <c>String</c> operands compiles in Rust: literal and
	/// <c>String</c>, <c>String</c> and literal, two <c>String</c>s, and a chain of three.
	/// </summary>
	[TestMethod]
	public void Rust_JoinsCompile()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new RustGenerator().Generate(Joins());
			string allowed = "#![allow(unused_parens, dead_code, unused_variables, unused_mut)]";

			File.WriteAllText(
				Path.Combine(directory, "joins.rs"),
				$"{allowed}{Environment.NewLine}{source}");

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o joins.rlib joins.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that C writes a note in front of a join, saying why the compiler rejects it.
	/// </summary>
	[TestMethod]
	public void C_NotesThatAJoinHasNoExpression()
	{
		string code = new CGenerator().Generate(Greet());

		StringAssert.Contains(
			code,
			"return /* C has no string concatenation: join these into a buffer with snprintf or strcat */ (\"Hello, \" + name);");
	}

	/// <summary>
	/// Tests that C writes no note in front of an addition of numbers.
	/// </summary>
	[TestMethod]
	public void C_LeavesNumericAdditionAlone()
	{
		FunctionDeclaration sum = new("sum") { ReturnType = "int" };
		sum.Parameters.Add(new Parameter("a", "int"));
		sum.Parameters.Add(new Parameter("b", "int"));
		sum.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));

		string code = new CGenerator().Generate(sum);

		StringAssert.Contains(code, "return (a + b);");
		Assert.IsFalse(code.Contains("/*", StringComparison.Ordinal), code);
	}

	private static FunctionDeclaration Greet()
	{
		FunctionDeclaration greet = new("greet") { ReturnType = "string", IsStatic = true };
		greet.Parameters.Add(new Parameter("name", "string"));
		greet.Body.Add(new ReturnStatement(new BinaryExpression(
			Literal.Text("Hello, "), BinaryOperator.Add, new VariableReference("name"))));
		return greet;
	}

	private static SourceFile Joins()
	{
		FunctionDeclaration exclaim = new("exclaim") { ReturnType = "string" };
		exclaim.Parameters.Add(new Parameter("name", "string"));
		exclaim.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("name"), BinaryOperator.Add, Literal.Text("!"))));

		FunctionDeclaration join = new("join") { ReturnType = "string" };
		join.Parameters.Add(new Parameter("a", "string"));
		join.Parameters.Add(new Parameter("b", "string"));
		join.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));

		FunctionDeclaration spaced = new("spaced") { ReturnType = "string" };
		spaced.Parameters.Add(new Parameter("a", "string"));
		spaced.Parameters.Add(new Parameter("b", "string"));
		spaced.Body.Add(new ReturnStatement(new BinaryExpression(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, Literal.Text(" ")),
			BinaryOperator.Add,
			new VariableReference("b"))));

		SourceFile file = new("joins");
		file.Members.Add(Greet());
		file.Members.Add(exclaim);
		file.Members.Add(join);
		file.Members.Add(spaced);
		return file;
	}
}
