// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Core.Ast;

[TestClass]
public class AstNodeTests
{
	[TestMethod]
	public void FunctionDeclaration_HasCorrectNodeTypeName()
	{
		// Arrange
		var functionDeclaration = new FunctionDeclaration("TestFunction");

		// Act
		var nodeType = functionDeclaration.GetNodeTypeName();

		// Assert
		Assert.AreEqual("FunctionDeclaration", nodeType);
	}

	[TestMethod]
	public void Parameter_HasCorrectNodeTypeName()
	{
		// Arrange
		var parameter = new Parameter("param1", "int");

		// Act
		var nodeType = parameter.GetNodeTypeName();

		// Assert
		Assert.AreEqual("Parameter", nodeType);
	}

	[TestMethod]
	public void ReturnStatement_HasCorrectNodeTypeName()
	{
		// Arrange
		var returnStatement = new ReturnStatement(42);

		// Act
		var nodeType = returnStatement.GetNodeTypeName();

		// Assert
		Assert.AreEqual("ReturnStatement", nodeType);
	}

	[TestMethod]
	public void LeafNode_HasCorrectNodeTypeName()
	{
		// Arrange
		var leafNode = new AstLeafNode<int>(42);

		// Act
		var nodeType = leafNode.GetNodeTypeName();

		// Assert
		Assert.AreEqual("Leaf<Int32>", nodeType);
	}

	[TestMethod]
	public void FunctionDeclaration_Clone_CreatesDeepCopy()
	{
		// Arrange
		var originalFunc = new FunctionDeclaration("TestFunction")
		{
			ReturnType = "int",
			Parameters =
			[
				new Parameter("param1", "int"),
				new Parameter("param2", "string")
			]
		};
		originalFunc.Body.Add(new ReturnStatement(42));

		// Act
		var clonedFunc = (FunctionDeclaration)originalFunc.Clone();

		// Assert - Check properties match but are not reference equal
		Assert.AreEqual(originalFunc.Name, clonedFunc.Name);
		Assert.AreEqual(originalFunc.ReturnType, clonedFunc.ReturnType);
		Assert.AreEqual(originalFunc.Parameters.Count, clonedFunc.Parameters.Count);
		Assert.AreEqual(originalFunc.Body.Count, clonedFunc.Body.Count);

		// Verify they're different objects (deep copy)
		Assert.AreNotSame(originalFunc.Parameters, clonedFunc.Parameters);
		Assert.AreNotSame(originalFunc.Body, clonedFunc.Body);

		// Check that parameters were cloned correctly
		for (var i = 0; i < originalFunc.Parameters.Count; i++)
		{
			Assert.AreNotSame(originalFunc.Parameters[i], clonedFunc.Parameters[i]);
			Assert.AreEqual(originalFunc.Parameters[i].Name, clonedFunc.Parameters[i].Name);
			Assert.AreEqual(originalFunc.Parameters[i].Type, clonedFunc.Parameters[i].Type);
		}

		// Check that body elements were cloned correctly
		var originalReturn = (ReturnStatement)originalFunc.Body[0];
		var clonedReturn = (ReturnStatement)clonedFunc.Body[0];
		Assert.AreNotSame(originalReturn, clonedReturn);
	}
}
