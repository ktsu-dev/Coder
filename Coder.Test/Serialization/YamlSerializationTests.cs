// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Serialization;
using System.Collections.Generic;

[TestClass]
public class YamlSerializationTests
{
    [TestMethod]
    public void Serialize_FunctionDeclaration_GeneratesCorrectYaml()
    {
        // Arrange
        var funcDecl = new FunctionDeclaration("MyFunction")
        {
            ReturnType = "int",
            Parameters = new List<Parameter>
            {
                new Parameter("param1", "int"),
                new Parameter("param2", "string") { IsOptional = true, DefaultValue = "\"default\"" }
            }
        };
        funcDecl.Body.Add(new ReturnStatement(42));
        
        var serializer = new YamlSerializer();
        
        // Act
        var yaml = serializer.Serialize(funcDecl);
        
        // Assert
        Assert.IsNotNull(yaml);
        StringAssert.Contains(yaml, "functionDeclaration:");
        StringAssert.Contains(yaml, "name: MyFunction");
        StringAssert.Contains(yaml, "returnType: int");
        StringAssert.Contains(yaml, "parameters:");
        StringAssert.Contains(yaml, "name: param1");
        StringAssert.Contains(yaml, "type: int");
        StringAssert.Contains(yaml, "isOptional: true");
        StringAssert.Contains(yaml, "defaultValue: \"\\\"default\\\"\"");
        StringAssert.Contains(yaml, "body:");
        StringAssert.Contains(yaml, "returnStatement:");
    }
    
    [TestMethod]
    public void SerializeAndDeserialize_FunctionDeclaration_RoundTripsCorrectly()
    {
        // Arrange
        var originalFuncDecl = new FunctionDeclaration("MyFunction")
        {
            ReturnType = "int",
            Parameters = new List<Parameter>
            {
                new Parameter("param1", "int"),
                new Parameter("param2", "string") { IsOptional = true, DefaultValue = "\"default\"" }
            }
        };
        originalFuncDecl.Body.Add(new ReturnStatement(42));
        
        var serializer = new YamlSerializer();
        var deserializer = new YamlDeserializer();
        
        // Act
        var yaml = serializer.Serialize(originalFuncDecl);
        var deserializedNode = deserializer.Deserialize(yaml);
        
        // Assert
        Assert.IsNotNull(deserializedNode);
        Assert.IsInstanceOfType(deserializedNode, typeof(FunctionDeclaration));
        
        var deserializedFunc = (FunctionDeclaration)deserializedNode;
        Assert.AreEqual(originalFuncDecl.Name, deserializedFunc.Name);
        Assert.AreEqual(originalFuncDecl.ReturnType, deserializedFunc.ReturnType);
        Assert.AreEqual(originalFuncDecl.Parameters.Count, deserializedFunc.Parameters.Count);
        Assert.AreEqual(originalFuncDecl.Parameters[0].Name, deserializedFunc.Parameters[0].Name);
        Assert.AreEqual(originalFuncDecl.Parameters[0].Type, deserializedFunc.Parameters[0].Type);
        Assert.AreEqual(originalFuncDecl.Parameters[1].IsOptional, deserializedFunc.Parameters[1].IsOptional);
        Assert.AreEqual(originalFuncDecl.Parameters[1].DefaultValue, deserializedFunc.Parameters[1].DefaultValue);
        Assert.AreEqual(originalFuncDecl.Body.Count, deserializedFunc.Body.Count);
        
        Assert.IsInstanceOfType(deserializedFunc.Body[0], typeof(ReturnStatement));
        var returnStmt = (ReturnStatement)deserializedFunc.Body[0];
        Assert.IsNotNull(returnStmt.Expression);
        Assert.IsInstanceOfType(returnStmt.Expression, typeof(AstLeafNode<int>));
        Assert.AreEqual(42, ((AstLeafNode<int>)returnStmt.Expression).Value);
    }
    
    [TestMethod]
    public void Serialize_ReturnStatement_GeneratesCorrectYaml()
    {
        // Arrange
        var returnStmt = new ReturnStatement(42);
        var serializer = new YamlSerializer();
        
        // Act
        var yaml = serializer.Serialize(returnStmt);
        
        // Assert
        Assert.IsNotNull(yaml);
        StringAssert.Contains(yaml, "returnStatement:");
        StringAssert.Contains(yaml, "expression:");
        StringAssert.Contains(yaml, "Leaf<Int32>: 42");
    }
    
    [TestMethod]
    public void Serialize_LeafNode_GeneratesCorrectYaml()
    {
        // Arrange
        var leafNode = new AstLeafNode<string>("test value");
        var serializer = new YamlSerializer();
        
        // Act
        var yaml = serializer.Serialize(leafNode);
        
        // Assert
        Assert.IsNotNull(yaml);
        StringAssert.Contains(yaml, "Leaf<String>: test value");
    }
    
    [TestMethod]
    public void DeserializeInvalidYaml_ThrowsException()
    {
        // Arrange
        var deserializer = new YamlDeserializer();
        var invalidYaml = "this is not valid yaml @#$%";
        
        // Act & Assert
        Assert.ThrowsException<YamlDotNet.Core.YamlException>(() => deserializer.Deserialize(invalidYaml));
    }
} 