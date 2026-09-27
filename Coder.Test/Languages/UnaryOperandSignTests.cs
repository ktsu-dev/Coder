// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a unary operator is not run into the sign of a negative operand.
/// </summary>
/// <remarks>
/// Negating <c>-1</c> written as <c>(--1)</c> is a decrement of a literal in C, C++, C#, JavaScript
/// and Go, which fails to compile.
/// </remarks>
[TestClass]
public class UnaryOperandSignTests
{
	/// <summary>
	/// Every generator, since each one negates through the shared unary emitter.
	/// </summary>
	private static readonly ILanguageGenerator[] Generators =
	[
		new CSharpGenerator(),
		new CGenerator(),
		new CppGenerator(),
		new GoGenerator(),
		new RustGenerator(),
		new PythonGenerator(),
		new JavaScriptGenerator(),
	];

	/// <summary>
	/// Tests that negating a negative integer literal parenthesises the literal.
	/// </summary>
	[TestMethod]
	public void NegatingANegativeIntegerDoesNotWriteADecrement()
	{
		UnaryExpression negated = new(UnaryOperator.Negate, Literal.Number(-1));

		foreach (ILanguageGenerator generator in Generators)
		{
			Assert.AreEqual("(-(-1))", generator.Generate(negated).Trim(), generator.GetType().Name);
		}
	}

	/// <summary>
	/// Tests that negating a negative double literal parenthesises the literal.
	/// </summary>
	[TestMethod]
	public void NegatingANegativeDoubleDoesNotWriteADecrement()
	{
		UnaryExpression negated = new(UnaryOperator.Negate, Literal.DecimalValue(-1.5));

		foreach (ILanguageGenerator generator in Generators)
		{
			string code = generator.Generate(negated);

			Assert.DoesNotContain("--", code, StringComparison.Ordinal, $"{generator.GetType().Name} wrote {code}");
			Assert.StartsWith("(-(-1.5", code.Trim(), StringComparison.Ordinal, $"{generator.GetType().Name} wrote {code}");
		}
	}

	/// <summary>
	/// Tests that an operand that cannot fuse with the operator is still written bare.
	/// </summary>
	/// <param name="op">The operator under test.</param>
	/// <param name="value">The literal operand.</param>
	/// <param name="expected">The expected generated source.</param>
	[TestMethod]
	[DataRow(UnaryOperator.Negate, 1, "(-1)")]
	[DataRow(UnaryOperator.Plus, -1, "(+-1)")]
	public void AnOperandThatCannotFuseIsWrittenBare(UnaryOperator op, int value, string expected)
	{
		UnaryExpression unary = new(op, Literal.Number(value));

		foreach (ILanguageGenerator generator in Generators)
		{
			Assert.AreEqual(expected, generator.Generate(unary).Trim(), generator.GetType().Name);
		}
	}
}
