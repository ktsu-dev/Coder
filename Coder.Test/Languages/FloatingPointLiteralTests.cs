// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a floating-point literal is written so that each language reads it as one.
/// </summary>
/// <remarks>
/// A whole number written without its fraction is an integer in every target, so <c>1.0 / 2.0</c>
/// written as <c>1 / 2</c> is integer division — zero — in C, C++ and Go, and a type error in Rust.
/// </remarks>
[TestClass]
public class FloatingPointLiteralTests
{
	/// <summary>
	/// The generators that spell a double through the shared literal formatting.
	/// </summary>
	private static readonly ILanguageGenerator[] Generators =
	[
		new CGenerator(),
		new CppGenerator(),
		new GoGenerator(),
		new RustGenerator(),
		new PythonGenerator(),
		new JavaScriptGenerator(),
	];

	/// <summary>
	/// Tests that dividing two whole-number doubles is written as a floating-point division.
	/// </summary>
	[TestMethod]
	public void AWholeNumberDoubleKeepsItsFraction()
	{
		BinaryExpression half = new(
			new LiteralExpression<double>(1.0),
			BinaryOperator.Divide,
			new LiteralExpression<double>(2.0));

		foreach (ILanguageGenerator generator in Generators)
		{
			string code = generator.Generate(half);

			Assert.Contains("1.0 / 2.0", code, StringComparison.Ordinal, $"{generator.GetType().Name} wrote {code}");
		}
	}

	/// <summary>
	/// Tests that a value that already reads as a float is written as it round-trips, unchanged.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="expected">What each generator should write.</param>
	[TestMethod]
	[DataRow(0.5, "0.5")]
	[DataRow(-3.25, "-3.25")]
	[DataRow(1e300, "1E+300")]
	[DataRow(0.1, "0.1")]
	public void AFractionalOrExponentDoubleIsWrittenAsItRoundTrips(double value, string expected)
	{
		foreach (ILanguageGenerator generator in Generators)
		{
			Assert.AreEqual(expected, generator.Generate(new LiteralExpression<double>(value)).Trim(), generator.GetType().Name);
		}
	}
}
