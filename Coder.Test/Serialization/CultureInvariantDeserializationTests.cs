// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Serialization;

using System.Globalization;
using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that YAML documents round-trip identically regardless of the current culture.
///
/// The serializer emits scalars through YamlDotNet, which writes numbers invariantly
/// (dot-decimal). The deserializer must therefore read them back invariantly too; parsing
/// against the current culture turns "3.14" into 314 on a culture that groups with "."
/// (de-DE, fr-FR, es-ES, ...), silently corrupting the loaded AST.
/// </summary>
[TestClass]
[DoNotParallelize]
public class CultureInvariantDeserializationTests
{
	/// <summary>
	/// A culture whose decimal separator is "," and whose group separator is ".", like de-DE,
	/// fr-FR, es-ES and the rest of continental Europe. That pairing is what makes a dot-decimal
	/// literal parse to a wrong value rather than fail outright: "3.14" reads as three thousand
	/// one hundred and four tenths of nothing — 314.
	///
	/// It is built from the invariant culture rather than named, because the test project runs in
	/// globalization-invariant mode where <c>new CultureInfo("de-DE")</c> throws. Building it this
	/// way also keeps the test hermetic: it asserts against a fixed number format rather than
	/// whatever ICU data the host happens to carry.
	/// </summary>
	private static CultureInfo DotGroupingCulture()
	{
		CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		culture.NumberFormat.NumberDecimalSeparator = ",";
		culture.NumberFormat.NumberGroupSeparator = ".";
		culture.NumberFormat.NegativeSign = "−";
		return culture;
	}

	[TestMethod]
	public void DeserializeFractionLiteral_UnderDotGroupingCulture_RoundTripsValue()
	{
		LiteralExpression<double> literal = Literal.DecimalValue(3.14);

		LiteralExpression<double> roundTripped = RoundTripUnder<LiteralExpression<double>>(DotGroupingCulture(), literal);

		Assert.AreEqual(3.14, roundTripped.Value);
	}

	[TestMethod]
	public void DeserializeNegativeFractionLiteral_UnderDotGroupingCulture_RoundTripsValue()
	{
		LiteralExpression<double> literal = Literal.DecimalValue(-0.125);

		LiteralExpression<double> roundTripped = RoundTripUnder<LiteralExpression<double>>(DotGroupingCulture(), literal);

		Assert.AreEqual(-0.125, roundTripped.Value);
	}

	[TestMethod]
	public void DeserializeIntegerLiteral_UnderDotGroupingCulture_RoundTripsValue()
	{
		LiteralExpression<int> literal = Literal.Number(-42);

		LiteralExpression<int> roundTripped = RoundTripUnder<LiteralExpression<int>>(DotGroupingCulture(), literal);

		Assert.AreEqual(-42, roundTripped.Value);
	}

	[TestMethod]
	public void DeserializeBooleanLiteral_UnderDotGroupingCulture_RoundTripsValue()
	{
		LiteralExpression<bool> literal = Literal.Bool(true);

		LiteralExpression<bool> roundTripped = RoundTripUnder<LiteralExpression<bool>>(DotGroupingCulture(), literal);

		Assert.IsTrue(roundTripped.Value);
	}

	[TestMethod]
	public void SerializeFractionLiteral_UnderDotGroupingCulture_WritesDotDecimal()
	{
		// The deserializer reads invariantly, so the serializer must write invariantly too;
		// this pins the half of the contract the fix depends on.
		string yaml = UnderCulture(DotGroupingCulture(), () => new YamlSerializer().Serialize(Literal.DecimalValue(3.14)));

		StringAssert.Contains(yaml, "3.14");
	}

	[TestMethod]
	public void DeserializeFunctionBody_UnderDotGroupingCulture_PreservesReturnedFraction()
	{
		FunctionDeclaration function = new("Rate") { ReturnType = "double" };
		function.Body.Add(new ReturnStatement(Literal.DecimalValue(0.5)));

		FunctionDeclaration roundTripped = RoundTripUnder<FunctionDeclaration>(DotGroupingCulture(), function);

		ReturnStatement returned = (ReturnStatement)roundTripped.Body[0];
		LiteralExpression<double> value = (LiteralExpression<double>)returned.Expression!;
		Assert.AreEqual(0.5, value.Value);
	}

	/// <summary>
	/// Serializes and deserializes <paramref name="node"/> with <paramref name="culture"/> installed
	/// as the current culture for both halves of the trip.
	/// </summary>
	private static T RoundTripUnder<T>(CultureInfo culture, AstNode node) where T : AstNode
	{
		AstNode? deserialized = UnderCulture(culture, () =>
		{
			string yaml = new YamlSerializer().Serialize(node);
			return new YamlDeserializer().Deserialize(yaml);
		});

		Assert.IsNotNull(deserialized);
		Assert.IsInstanceOfType<T>(deserialized);
		return (T)deserialized;
	}

	/// <summary>
	/// Runs <paramref name="action"/> on a dedicated thread with <paramref name="culture"/> as the
	/// current culture, so the ambient culture of the test host is never mutated.
	/// </summary>
	private static TResult UnderCulture<TResult>(CultureInfo culture, Func<TResult> action)
	{
		// LongRunning gets a dedicated thread rather than a pool one, so the culture is discarded
		// with the thread instead of outliving the test on a thread someone else will be handed.
		// The task carries any failure back to this thread, so the round trip needs no catch of its own.
		return Task.Factory.StartNew(
			() =>
			{
				CultureInfo.CurrentCulture = culture;
				CultureInfo.CurrentUICulture = culture;
				return action();
			},
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default).GetAwaiter().GetResult();
	}
}
