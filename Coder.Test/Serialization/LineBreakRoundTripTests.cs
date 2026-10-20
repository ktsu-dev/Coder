// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that a string holding a carriage return or a Unicode line break survives a YAML round trip.
/// </summary>
/// <remarks>
/// A string with a line break in it used to be written as a block scalar, and YAML reads every line
/// break inside a block scalar back as <c>\n</c>, so <c>"a\r\nb"</c> reloaded as <c>"a\nb"</c>
/// (ktsu-dev/Coder#284).
/// </remarks>
[TestClass]
public class LineBreakRoundTripTests
{
	private static IEnumerable<object[]> Texts =>
	[
		["a\r\nb"],
		["a\rb"],
		["line\r\n"],
		["a\u0085b"],
		["a\u2028b"],
		["a\u2029b"],
		["\r"],
		["a\nb"],
	];

	[TestMethod]
	[DynamicData(nameof(Texts))]
	public void RoundTrip_StringLiteral_KeepsLineBreaks(string text)
	{
		string yaml = new YamlSerializer().Serialize(Literal.Text(text));

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		LiteralExpression<string> literal = Assert.IsInstanceOfType<LiteralExpression<string>>(deserialized);
		Assert.AreEqual(text, literal.Value);
	}

	[TestMethod]
	[DynamicData(nameof(Texts))]
	public void RoundTrip_VariableReference_KeepsLineBreaks(string text)
	{
		// A reference name can't be all whitespace, so give the lone "\r" something to sit beside.
		string name = "x" + text;
		string yaml = new YamlSerializer().Serialize(new VariableReference(name));

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		VariableReference reference = Assert.IsInstanceOfType<VariableReference>(deserialized);
		Assert.AreEqual(name, reference.Name);
	}

	[TestMethod]
	[DynamicData(nameof(Texts))]
	public void RoundTrip_DocumentationLine_KeepsLineBreaks(string text)
	{
		FunctionDeclaration function = new("area");
		function.Documentation.Add(text);
		string yaml = new YamlSerializer().Serialize(function);

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		FunctionDeclaration reloaded = Assert.IsInstanceOfType<FunctionDeclaration>(deserialized);
		Assert.AreEqual(1, reloaded.Documentation.Count);
		Assert.AreEqual(text, reloaded.Documentation[0]);
	}
}
