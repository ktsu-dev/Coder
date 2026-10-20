// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the two targets with no implicit numeric conversion widen a narrower number where it
/// meets a wider one, and that the targets which convert by themselves are written as they were
/// (issue #238).
/// </summary>
[TestClass]
public class NumericWideningTests
{
	/// <summary>
	/// Builds the declarations every widening test compiles: mixed operands, an integer literal
	/// beside a floating value, an integer initialising and assigned to a floating variable, an
	/// integer returned as a wider integer, and an integer passed where a wider one is declared.
	/// </summary>
	/// <returns>The file.</returns>
	internal static SourceFile Widening()
	{
		SourceFile file = new("widening");

		FunctionDeclaration mixed = new("mixed") { ReturnType = new TypeReference("long") };
		mixed.Parameters.Add(new Parameter("a", "long"));
		mixed.Parameters.Add(new Parameter("b", "int"));
		mixed.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));
		file.Members.Add(mixed);

		FunctionDeclaration scaled = new("scaled") { ReturnType = new TypeReference("double") };
		scaled.Parameters.Add(new Parameter("x", "double"));
		scaled.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), BinaryOperator.Multiply, Literal.Number(2))));
		file.Members.Add(scaled);

		FunctionDeclaration half = new("half") { ReturnType = new TypeReference("double") };
		half.Parameters.Add(new Parameter("b", "int"));
		half.Body.Add(new VariableDeclaration("h", "double", new VariableReference("b")));
		half.Body.Add(new AssignmentStatement(new VariableReference("h"), new VariableReference("b")));
		half.Body.Add(new ReturnStatement(new VariableReference("h")));
		file.Members.Add(half);

		FunctionDeclaration widened = new("widened") { ReturnType = new TypeReference("long") };
		widened.Parameters.Add(new Parameter("b", "int"));
		widened.Body.Add(new ReturnStatement(new VariableReference("b")));
		file.Members.Add(widened);

		FunctionDeclaration called = new("called") { ReturnType = new TypeReference("long") };
		called.Parameters.Add(new Parameter("b", "int"));
		CallExpression call = new("mixed");
		call.Arguments.Add(new VariableReference("b"));
		call.Arguments.Add(new VariableReference("b"));
		called.Body.Add(new ReturnStatement(call));
		file.Members.Add(called);

		return file;
	}

	/// <summary>Tests where Rust converts.</summary>
	[TestMethod]
	public void Rust_WidensTheNarrowerSide()
	{
		string source = new RustGenerator().Generate(Widening());

		Assert.Contains("(a + (b as i64))", source, StringComparison.Ordinal);
		Assert.Contains("(x * 2.0)", source, StringComparison.Ordinal);
		Assert.Contains("let mut h: f64 = (b as f64);", source, StringComparison.Ordinal);
		Assert.Contains("h = (b as f64);", source, StringComparison.Ordinal);
		Assert.Contains("return (b as i64);", source, StringComparison.Ordinal);
		Assert.Contains("mixed((b as i64), b)", source, StringComparison.Ordinal);
	}

	/// <summary>Tests where Go converts.</summary>
	[TestMethod]
	public void Go_WidensTheNarrowerSide()
	{
		string source = new GoGenerator().Generate(Widening());

		Assert.Contains("(a + int64(b))", source, StringComparison.Ordinal);
		Assert.Contains("(x * 2)", source, StringComparison.Ordinal);
		Assert.Contains("float64(b)", source, StringComparison.Ordinal);
		Assert.Contains("h = float64(b)", source, StringComparison.Ordinal);
		Assert.Contains("return int64(b)", source, StringComparison.Ordinal);
		Assert.Contains("mixed(int64(b), b)", source, StringComparison.Ordinal);
	}

	/// <summary>Tests that a language converting by itself is written as before.</summary>
	[TestMethod]
	public void CSharp_IsUnchanged()
	{
		string source = new CSharpGenerator().Generate(Widening());

		Assert.Contains("(a + b)", source, StringComparison.Ordinal);
		Assert.Contains("(x * 2)", source, StringComparison.Ordinal);
		Assert.Contains("return b;", source, StringComparison.Ordinal);
		Assert.Contains("mixed(b, b)", source, StringComparison.Ordinal);
	}
}
