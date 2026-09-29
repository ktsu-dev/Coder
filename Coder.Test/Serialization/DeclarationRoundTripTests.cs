// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that a declaration keeps its metadata and its keyed children through a YAML round trip.
/// </summary>
/// <remarks>
/// Both used to be written only for nodes that were not declarations, so a tool that attached
/// metadata to a function or a class lost it the first time the document was saved
/// (ktsu-dev/Coder#87).
/// </remarks>
[TestClass]
public class DeclarationRoundTripTests
{
	private const string MetadataKey = "origin";
	private const string MetadataValue = "schema";

	public static IEnumerable<object[]> Declarations =>
	[
		[new SourceFile("file")],
		[new NamespaceDeclaration("space")],
		[new EnumDeclaration("Colour")],
		[new EnumMember("Red")],
		[new PropertyDeclaration("Size", new TypeReference("int"))],
		[new FieldDeclaration("count", new TypeReference("int"))],
		[new ClassDeclaration("Point")],
		[new FunctionDeclaration("area")],
		[new EntryPoint()],
		[new Parameter("value", "int")],
	];

	[TestMethod]
	[DynamicData(nameof(Declarations))]
	public void RoundTrip_Declaration_KeepsMetadata(AstNode declaration)
	{
		Assert.IsNotNull(declaration);
		declaration.Metadata[MetadataKey] = MetadataValue;

		AstNode restored = RoundTrip(declaration);

		Assert.AreEqual(declaration.GetType(), restored.GetType());
		Assert.IsTrue(restored.Metadata.TryGetValue(MetadataKey, out object? value), "the metadata was dropped");
		Assert.AreEqual(MetadataValue, value?.ToString());
	}

	[TestMethod]
	public void RoundTrip_ParameterOfAFunction_KeepsMetadata()
	{
		Parameter parameter = new("value", "int");
		parameter.Metadata[MetadataKey] = MetadataValue;
		FunctionDeclaration function = new("area");
		function.Parameters.Add(parameter);

		FunctionDeclaration restored = Assert.IsInstanceOfType<FunctionDeclaration>(RoundTrip(function));

		Assert.AreEqual(1, restored.Parameters.Count);
		Assert.AreEqual(MetadataValue, restored.Parameters[0].Metadata[MetadataKey]?.ToString());
	}

	public static IEnumerable<object[]> CompositeDeclarations =>
	[
		[new ClassDeclaration("Point")],
		[new FunctionDeclaration("area")],
		[new EntryPoint()],
	];

	[TestMethod]
	[DynamicData(nameof(CompositeDeclarations))]
	public void RoundTrip_Declaration_KeepsKeyedChild(AstCompositeNode declaration)
	{
		Assert.IsNotNull(declaration);
		declaration.SetChild("note", new VariableReference("attached"));

		AstCompositeNode restored = Assert.IsInstanceOfType<AstCompositeNode>(RoundTrip(declaration));

		Assert.IsTrue(restored.Children.TryGetValue("note", out AstNode? child), "the keyed child was dropped");
		Assert.AreEqual("attached", Assert.IsInstanceOfType<VariableReference>(child).Name);
	}

	private static AstNode RoundTrip(AstNode node)
	{
		string yaml = new YamlSerializer().Serialize(node);
		AstNode? restored = new YamlDeserializer().Deserialize(yaml);
		Assert.IsNotNull(restored, yaml);
		return restored;
	}
}
