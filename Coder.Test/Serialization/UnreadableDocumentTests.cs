// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Serialization;

using System.IO;
using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that a document the deserializer cannot fully read is refused rather than loaded as a
/// partial tree, which the editor's next save would write over the user's file.
/// </summary>
[TestClass]
public class UnreadableDocumentTests
{
	[TestMethod]
	public void Deserialize_UnknownStatementInABody_IsRefusedWithItsPath()
	{
		const string yaml = """
			functionDeclaration:
			  name: clamp
			  returnType: int
			  body:
			  - retrunStatement:
			      expression:
			        variableReference:
			          name: a
			""";

		InvalidDataException ex = Assert.ThrowsExactly<InvalidDataException>(() => new YamlDeserializer().Deserialize(yaml));

		Assert.AreEqual("functionDeclaration: unknown node 'retrunStatement'", ex.Message);
	}

	[TestMethod]
	public void Deserialize_UnknownNodeNestedInAnExpression_NamesEveryEnclosingNode()
	{
		const string yaml = """
			functionDeclaration:
			  name: f
			  body:
			  - returnStatement:
			      expression:
			        binaryExpression:
			          left:
			            ifExpression:
			              name: a
			          operator: Add
			          right:
			            variableReference:
			              name: b
			""";

		InvalidDataException ex = Assert.ThrowsExactly<InvalidDataException>(() => new YamlDeserializer().Deserialize(yaml));

		Assert.AreEqual("functionDeclaration/returnStatement/binaryExpression: unknown node 'ifExpression'", ex.Message);
	}

	[TestMethod]
	public void Deserialize_UnknownRootNode_IsRefused()
	{
		InvalidDataException ex = Assert.ThrowsExactly<InvalidDataException>(() => new YamlDeserializer().Deserialize("ifStatement:\n  condition: x\n"));

		Assert.AreEqual("unknown node 'ifStatement'", ex.Message);
	}

	[TestMethod]
	[DataRow("Minus")]
	[DataRow("999")]
	[DataRow("-1")]
	[DataRow("1")]
	public void Deserialize_OperatorThatIsNotAMemberName_IsRefused(string value)
	{
		string yaml = $"""
			binaryExpression:
			  left:
			    variableReference:
			      name: a
			  operator: {value}
			  right:
			    variableReference:
			      name: b
			""";

		InvalidDataException ex = Assert.ThrowsExactly<InvalidDataException>(() => new YamlDeserializer().Deserialize(yaml));

		Assert.AreEqual($"binaryExpression: operator '{value}' is not a BinaryOperator", ex.Message);
	}

	[TestMethod]
	public void Deserialize_OperatorInAnotherCase_IsRead()
	{
		const string yaml = """
			binaryExpression:
			  left:
			    variableReference:
			      name: a
			  operator: subtract
			  right:
			    variableReference:
			      name: b
			""";

		BinaryExpression restored = Assert.IsInstanceOfType<BinaryExpression>(new YamlDeserializer().Deserialize(yaml));

		Assert.AreEqual(BinaryOperator.Subtract, restored.Operator);
	}

	[TestMethod]
	public void Deserialize_UnaryAndAssignmentOperatorsThatAreNotMembers_AreRefused()
	{
		const string unary = """
			unaryExpression:
			  operator: Minus
			  operand:
			    variableReference:
			      name: a
			""";
		const string assignment = """
			assignmentStatement:
			  target:
			    variableReference:
			      name: a
			  operator: 42
			  value:
			    variableReference:
			      name: b
			""";

		Assert.ThrowsExactly<InvalidDataException>(() => new YamlDeserializer().Deserialize(unary));
		Assert.ThrowsExactly<InvalidDataException>(() => new YamlDeserializer().Deserialize(assignment));
	}

	[TestMethod]
	public void Deserialize_UnknownNodeUnderAChildrenMapping_IsRefused()
	{
		const string yaml = """
			functionDeclaration:
			  name: f
			  children:
			    note:
			      variabelReference:
			        name: attached
			""";

		InvalidDataException ex = Assert.ThrowsExactly<InvalidDataException>(() => new YamlDeserializer().Deserialize(yaml));

		Assert.AreEqual("functionDeclaration: unknown node 'variabelReference'", ex.Message);
	}
}
