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

	[TestMethod]
	public void RoundTrip_FunctionDeclaration_KeyedChildNamedLikeAPropertyLeavesThePropertyIntact()
	{
		FunctionDeclaration function = new("compute") { ReturnType = "int" };
		function.Body.Add(new ReturnStatement(1));
		function.SetChild("Name", new VariableReference("x"));
		function.SetChild("Body", new VariableReference("y"));

		FunctionDeclaration restored = Assert.IsInstanceOfType<FunctionDeclaration>(RoundTrip(function));

		Assert.AreEqual("compute", restored.Name);
		Assert.HasCount(1, restored.Body);
		Assert.AreEqual("x", Assert.IsInstanceOfType<VariableReference>(restored.GetChild("Name")).Name);
		Assert.AreEqual("y", Assert.IsInstanceOfType<VariableReference>(restored.GetChild("Body")).Name);
	}

	[TestMethod]
	public void RoundTrip_ClassDeclaration_KeyedChildNamedLikeAPropertyLeavesThePropertyIntact()
	{
		ClassDeclaration declaration = new("Point");
		declaration.Members.Add(new FieldDeclaration("x", new TypeReference("int")));
		declaration.SetChild("Name", new VariableReference("x"));
		declaration.SetChild("Members", new VariableReference("y"));

		ClassDeclaration restored = Assert.IsInstanceOfType<ClassDeclaration>(RoundTrip(declaration));

		Assert.AreEqual("Point", restored.Name);
		Assert.HasCount(1, restored.Members);
		Assert.AreEqual("x", Assert.IsInstanceOfType<VariableReference>(restored.GetChild("Name")).Name);
		Assert.AreEqual("y", Assert.IsInstanceOfType<VariableReference>(restored.GetChild("Members")).Name);
	}

	[TestMethod]
	[DynamicData(nameof(CompositeDeclarations))]
	public void RoundTrip_Declaration_KeepsTheCaseOfAKeyedChildsKey(AstCompositeNode declaration)
	{
		Assert.IsNotNull(declaration);
		declaration.SetChild("myChild", new VariableReference("attached"));

		AstCompositeNode restored = Assert.IsInstanceOfType<AstCompositeNode>(RoundTrip(declaration));

		Assert.AreEqual("attached", Assert.IsInstanceOfType<VariableReference>(restored.GetChild("myChild")).Name);
	}

	[TestMethod]
	public void RoundTrip_ReturnStatement_KeepsAKeyedChildBesideItsExpression()
	{
		ReturnStatement statement = new(1);
		statement.SetChild("extra", new VariableReference("attached"));

		ReturnStatement restored = Assert.IsInstanceOfType<ReturnStatement>(RoundTrip(statement));

		Assert.AreEqual(1, Assert.IsInstanceOfType<AstLeafNode<int>>(restored.Expression).Value);
		Assert.AreEqual("attached", Assert.IsInstanceOfType<VariableReference>(restored.GetChild("extra")).Name);
		Assert.HasCount(2, restored.Children);
	}

	[TestMethod]
	public void Deserialize_KeyedChildInTheOldLayout_IsStillRead()
	{
		const string yaml = """
			functionDeclaration:
			  name: compute
			  returnType: int
			  note:
			    variableReference:
			      name: attached
			""";

		FunctionDeclaration restored = Assert.IsInstanceOfType<FunctionDeclaration>(new YamlDeserializer().Deserialize(yaml));

		Assert.AreEqual("compute", restored.Name);
		Assert.AreEqual("attached", Assert.IsInstanceOfType<VariableReference>(restored.GetChild("note")).Name);
	}

	private static AstNode RoundTrip(AstNode node)
	{
		string yaml = new YamlSerializer().Serialize(node);
		AstNode? restored = new YamlDeserializer().Deserialize(yaml);
		Assert.IsNotNull(restored, yaml);
		return restored;
	}
}
