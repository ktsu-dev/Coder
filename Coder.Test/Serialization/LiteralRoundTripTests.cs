// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that every literal the <see cref="Literal"/> helpers can build survives a YAML round trip.
/// </summary>
/// <remarks>
/// A string literal whose text reads as another YAML scalar used to be written bare, so
/// <c>Literal.Text("null")</c> came back as <c>''</c>. Float and long literals had no case in the
/// serializer at all, so they were written as <c>{}</c> and read back as nothing (ktsu-dev/Coder#83).
/// </remarks>
[TestClass]
public class LiteralRoundTripTests
{
	[TestMethod]
	[DataRow("null")]
	[DataRow("Null")]
	[DataRow("~")]
	[DataRow("true")]
	[DataRow("False")]
	[DataRow("123")]
	[DataRow("-4.5")]
	[DataRow("1e3")]
	[DataRow(".inf")]
	[DataRow(".nan")]
	[DataRow("")]
	[DataRow("plain text")]
	public void RoundTrip_StringLiteral_KeepsText(string text)
	{
		string yaml = new YamlSerializer().Serialize(Literal.Text(text));

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		LiteralExpression<string> literal = Assert.IsInstanceOfType<LiteralExpression<string>>(deserialized);
		Assert.AreEqual(text, literal.Value);
	}

	[TestMethod]
	[DataRow(1.5f)]
	[DataRow(-0.1f)]
	[DataRow(3.0f)]
	[DataRow(float.MaxValue)]
	[DataRow(float.PositiveInfinity)]
	[DataRow(float.NegativeInfinity)]
	[DataRow(float.NaN)]
	public void RoundTrip_FloatLiteral_KeepsValue(float value)
	{
		string yaml = new YamlSerializer().Serialize(Literal.SingleValue(value));

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		LiteralExpression<float> literal = Assert.IsInstanceOfType<LiteralExpression<float>>(deserialized);
		Assert.AreEqual(value, literal.Value);
	}

	[TestMethod]
	[DataRow(5000000000L)]
	[DataRow(-5000000000L)]
	[DataRow(long.MaxValue)]
	[DataRow(long.MinValue)]
	public void RoundTrip_LongLiteral_KeepsValue(long value)
	{
		string yaml = new YamlSerializer().Serialize(Literal.BigNumberValue(value));

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		LiteralExpression<long> literal = Assert.IsInstanceOfType<LiteralExpression<long>>(deserialized);
		Assert.AreEqual(value, literal.Value);
	}

	[TestMethod]
	public void RoundTrip_VariableInitialisedToLong_KeepsInitialiser()
	{
		VariableDeclaration declaration = new("big", "long", Literal.BigNumberValue(5000000000L));
		string yaml = new YamlSerializer().Serialize(declaration);

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		VariableDeclaration roundTripped = Assert.IsInstanceOfType<VariableDeclaration>(deserialized);
		LiteralExpression<long> initialiser = Assert.IsInstanceOfType<LiteralExpression<long>>(roundTripped.InitialValue);
		Assert.AreEqual(5000000000L, initialiser.Value);
	}

	/// <summary>
	/// Every generator writes a float and a long literal rather than throwing
	/// <see cref="NotSupportedException"/>.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_WritesFloatAndLongLiterals()
	{
		ILanguageGenerator[] generators =
		[
			new CSharpGenerator(),
			new CGenerator(),
			new CppGenerator(),
			new GoGenerator(),
			new RustGenerator(),
			new PythonGenerator(),
			new JavaScriptGenerator(),
		];

		foreach (ILanguageGenerator generator in generators)
		{
			string name = generator.GetType().Name;
			Assert.Contains("1.5", generator.Generate(Literal.SingleValue(1.5f)), StringComparison.Ordinal, name);
			Assert.Contains("5000000000", generator.Generate(Literal.BigNumberValue(5000000000L)), StringComparison.Ordinal, name);
		}
	}

	[TestMethod]
	public void CSharpGenerator_SuffixesFloatAndLongLiterals()
	{
		CSharpGenerator generator = new();

		Assert.AreEqual("1.5f", generator.Generate(Literal.SingleValue(1.5f)).Trim());
		Assert.AreEqual("5000000000L", generator.Generate(Literal.BigNumberValue(5000000000L)).Trim());
	}

	[TestMethod]
	public void SharedGenerators_KeepAWholeNumberFloatFloatingPoint()
	{
		Assert.AreEqual("3.0", new CGenerator().Generate(Literal.SingleValue(3.0f)).Trim());
		Assert.AreEqual("0.1", new PythonGenerator().Generate(Literal.SingleValue(0.1f)).Trim());
	}

	[TestMethod]
	public void NegatingANegativeLong_DoesNotFuseTheSigns()
	{
		UnaryExpression negation = new(UnaryOperator.Negate, Literal.BigNumberValue(-5000000000L));

		Assert.DoesNotContain("--", new CGenerator().Generate(negation), StringComparison.Ordinal);
	}
}
