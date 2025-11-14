// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;

[TestClass]
public class YamlSerializationTests
{
	[TestMethod]
	public void Serialize_FunctionDeclaration_GeneratesCorrectYaml()
	{
		// Arrange
		FunctionDeclaration funcDecl = new("MyFunction")
		{
			ReturnType = "int",
			Parameters =
			[
				new Parameter("param1", "int"),
				new Parameter("param2", "string") { IsOptional = true, DefaultValue = "\"default\"" }
			]
		};
		funcDecl.Body.Add(new ReturnStatement(42));

		YamlSerializer serializer = new();

		// Act
		string yaml = serializer.Serialize(funcDecl);

		// Assert
		Assert.IsNotNull(yaml);
		StringAssert.Contains(yaml, "functionDeclaration:");
		StringAssert.Contains(yaml, "name: MyFunction");
		StringAssert.Contains(yaml, "returnType: int");
		StringAssert.Contains(yaml, "parameters:");
		StringAssert.Contains(yaml, "name: param1");
		StringAssert.Contains(yaml, "type: int");
		StringAssert.Contains(yaml, "isOptional: true");
		StringAssert.Contains(yaml, "defaultValue: '\"default\"'");
		StringAssert.Contains(yaml, "body:");
		StringAssert.Contains(yaml, "returnStatement:");
	}

	[TestMethod]
	public void SerializeAndDeserialize_FunctionDeclaration_RoundTripsCorrectly()
	{
		// Arrange
		FunctionDeclaration originalFuncDecl = new("MyFunction")
		{
			ReturnType = "int",
			Parameters =
			[
				new Parameter("param1", "int"),
				new Parameter("param2", "string") { IsOptional = true, DefaultValue = "\"default\"" }
			]
		};
		originalFuncDecl.Body.Add(new ReturnStatement(42));

		YamlSerializer serializer = new();
		YamlDeserializer deserializer = new();

		// Act
		string yaml = serializer.Serialize(originalFuncDecl);
		AstNode? deserializedNode = deserializer.Deserialize(yaml);

		// Assert
		Assert.IsNotNull(deserializedNode);
		Assert.IsInstanceOfType<FunctionDeclaration>(deserializedNode);

		FunctionDeclaration? deserializedFunc = (FunctionDeclaration)deserializedNode;
		Assert.AreEqual(originalFuncDecl.Name, deserializedFunc.Name);
		Assert.AreEqual(originalFuncDecl.ReturnType, deserializedFunc.ReturnType);
		Assert.AreEqual(originalFuncDecl.Parameters.Count, deserializedFunc.Parameters.Count);
		Assert.AreEqual(originalFuncDecl.Parameters[0].Name, deserializedFunc.Parameters[0].Name);
		Assert.AreEqual(originalFuncDecl.Parameters[0].Type, deserializedFunc.Parameters[0].Type);
		Assert.AreEqual(originalFuncDecl.Parameters[1].IsOptional, deserializedFunc.Parameters[1].IsOptional);
		Assert.AreEqual(originalFuncDecl.Parameters[1].DefaultValue, deserializedFunc.Parameters[1].DefaultValue);
		Assert.AreEqual(originalFuncDecl.Body.Count, deserializedFunc.Body.Count);

		Assert.IsInstanceOfType<ReturnStatement>(deserializedFunc.Body[0]);
		ReturnStatement returnStmt = (ReturnStatement)deserializedFunc.Body[0];
		Assert.IsNotNull(returnStmt.Expression);
		Assert.IsInstanceOfType<AstLeafNode<int>>(returnStmt.Expression);
		Assert.AreEqual(42, ((AstLeafNode<int>)returnStmt.Expression).Value);
	}

	[TestMethod]
	public void Serialize_ReturnStatement_GeneratesCorrectYaml()
	{
		// Arrange
		ReturnStatement returnStmt = new(42);
		YamlSerializer serializer = new();

		// Act
		string yaml = serializer.Serialize(returnStmt);

		// Assert
		Assert.IsNotNull(yaml);
		StringAssert.Contains(yaml, "returnStatement:");
		StringAssert.Contains(yaml, "expression:");
		StringAssert.Contains(yaml, "leaf<Int32>: 42");
	}

	[TestMethod]
	public void Serialize_LeafNode_GeneratesCorrectYaml()
	{
		// Arrange
		AstLeafNode<string> leafNode = new("test value");
		YamlSerializer serializer = new();

		// Act
		string yaml = serializer.Serialize(leafNode);

		// Assert
		Assert.IsNotNull(yaml);
		StringAssert.Contains(yaml, "leaf<String>: test value");
	}

	[TestMethod]
	public void DeserializeInvalidYaml_ThrowsException()
	{
		// Arrange
		YamlDeserializer deserializer = new();
		string invalidYaml = "this is not valid yaml @#$%";

		// Act & Assert
		Assert.ThrowsException<YamlDotNet.Core.YamlException>(() => deserializer.Deserialize(invalidYaml));
	}
}
