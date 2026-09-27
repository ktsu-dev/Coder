// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that infinite and NaN double literals survive a YAML round trip.
///
/// YamlDotNet writes them with the core schema's spellings (<c>.inf</c>, <c>-.inf</c>, <c>.nan</c>),
/// which <see cref="double.TryParse(string?, System.Globalization.NumberStyles, IFormatProvider?, out double)"/>
/// cannot read. The deserializer used to drop the literal, so a declaration initialised to infinity
/// came back with no initialiser (ktsu-dev/Coder#78).
/// </summary>
[TestClass]
public class NonFiniteDoubleSerializationTests
{
	[TestMethod]
	[DataRow(double.PositiveInfinity)]
	[DataRow(double.NegativeInfinity)]
	[DataRow(double.NaN)]
	public void RoundTrip_NonFiniteLiteral_KeepsValue(double value)
	{
		string yaml = new YamlSerializer().Serialize(Literal.DecimalValue(value));

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		LiteralExpression<double> literal = Assert.IsInstanceOfType<LiteralExpression<double>>(deserialized);
		Assert.AreEqual(value, literal.Value);
	}

	[TestMethod]
	public void RoundTrip_VariableInitialisedToInfinity_KeepsInitialiser()
	{
		VariableDeclaration declaration = new("inf", "double", Literal.DecimalValue(double.PositiveInfinity));
		string yaml = new YamlSerializer().Serialize(declaration);

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		VariableDeclaration roundTripped = Assert.IsInstanceOfType<VariableDeclaration>(deserialized);
		LiteralExpression<double> initialiser = Assert.IsInstanceOfType<LiteralExpression<double>>(roundTripped.InitialValue);
		Assert.AreEqual(double.PositiveInfinity, initialiser.Value);
	}

	[TestMethod]
	[DataRow(".inf", double.PositiveInfinity)]
	[DataRow(".Inf", double.PositiveInfinity)]
	[DataRow(".INF", double.PositiveInfinity)]
	[DataRow("+.inf", double.PositiveInfinity)]
	[DataRow("-.inf", double.NegativeInfinity)]
	[DataRow("-.INF", double.NegativeInfinity)]
	[DataRow(".nan", double.NaN)]
	[DataRow(".NaN", double.NaN)]
	[DataRow(".NAN", double.NaN)]
	public void Deserialize_CoreSchemaNonFiniteSpelling_ReadsValue(string spelling, double expected)
	{
		string yaml = new YamlSerializer().Serialize(Literal.DecimalValue(1.5)).Replace("1.5", spelling, StringComparison.Ordinal);

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		LiteralExpression<double> literal = Assert.IsInstanceOfType<LiteralExpression<double>>(deserialized);
		Assert.AreEqual(expected, literal.Value);
	}
}
