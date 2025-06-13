// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Core.Ast;

/// <summary>
/// Tests for the expression system AST nodes.
/// </summary>
[TestClass]
public class ExpressionTests
{
	/// <summary>
	/// Tests that literal expressions can be created and used correctly.
	/// </summary>
	[TestMethod]
	public void LiteralExpression_ShouldCreateCorrectTypes()
	{
		// Test string literal
		LiteralExpression<string> stringLiteral = Literal.Text("hello");
		LiteralExpression<int> intLiteral = Literal.Number(42);
		LiteralExpression<bool> boolLiteral = Literal.Bool(true);
		LiteralExpression<double> doubleLiteral = Literal.DecimalValue(3.14);

		Assert.AreEqual("hello", stringLiteral.Value);
		Assert.AreEqual(42, intLiteral.Value);
		Assert.AreEqual(true, boolLiteral.Value);
		Assert.AreEqual(3.14, doubleLiteral.Value, 0.001);
	}

	/// <summary>
	/// Tests that binary expressions can be created and structured correctly.
	/// </summary>
	[TestMethod]
	public void BinaryExpression_ShouldCreateCorrectStructure()
	{
		// Test simple arithmetic: 5 + 3
		LiteralExpression<int> left = Literal.Number(5);
		LiteralExpression<int> right = Literal.Number(3);
		BinaryExpression binary = new(left, BinaryOperator.Add, right);

		Assert.AreEqual(left, binary.Left);
		Assert.AreEqual(BinaryOperator.Add, binary.Operator);
		Assert.AreEqual(right, binary.Right);
	}

	/// <summary>
	/// Tests that variable declarations work correctly.
	/// </summary>
	[TestMethod]
	public void VariableDeclaration_ShouldSupportTypeInference()
	{
		// Test type inference
		VariableDeclaration inferredVar = new("result", null, Literal.Number(42));
		VariableDeclaration constantVar = new("PI", "double", Literal.DecimalValue(3.14159))
		{
			IsConstant = true
		};

		Assert.AreEqual("result", inferredVar.Name);
		Assert.IsNull(inferredVar.Type);
		Assert.IsTrue(inferredVar.IsTypeInferred);

		Assert.AreEqual("PI", constantVar.Name);
		Assert.AreEqual("double", constantVar.Type);
		Assert.IsTrue(constantVar.IsConstant);
	}

	/// <summary>
	/// Tests that assignment statements work correctly.
	/// </summary>
	[TestMethod]
	public void AssignmentStatement_ShouldSupportDifferentOperators()
	{
		// Test assignment operators
		VariableReference varName = new("x");
		LiteralExpression<int> value = Literal.Number(10);

		AssignmentStatement assignment = new(varName, value);
		AssignmentStatement addAssign = new(varName, value, AssignmentOperator.AddAssign);

		Assert.AreEqual(varName, assignment.Target);
		Assert.AreEqual(value, assignment.Value);
		Assert.AreEqual(AssignmentOperator.Assign, assignment.Operator);

		Assert.AreEqual(AssignmentOperator.AddAssign, addAssign.Operator);
	}

	/// <summary>
	/// Tests that expressions can be cloned correctly.
	/// </summary>
	[TestMethod]
	public void Expression_ShouldSupportCloning()
	{
		// Test cloning expressions
		LiteralExpression<int> original = Literal.Number(42);
		BinaryExpression binaryOriginal = new(Literal.Number(5), BinaryOperator.Add, Literal.Number(3));

		LiteralExpression<int> cloned = (LiteralExpression<int>)original.Clone();
		BinaryExpression binaryCloned = (BinaryExpression)binaryOriginal.Clone();

		Assert.AreNotSame(original, cloned);
		Assert.AreEqual(original.Value, cloned.Value);

		Assert.AreNotSame(binaryOriginal, binaryCloned);
		Assert.AreEqual(binaryOriginal.Operator, binaryCloned.Operator);
	}

	/// <summary>
	/// Tests that complex nested expressions work.
	/// </summary>
	[TestMethod]
	public void Expression_ShouldSupportNestedExpressions()
	{
		// Test nested expressions: (a + b) * (c + d)
		LiteralExpression<string> a = Literal.Text("a");
		LiteralExpression<string> b = Literal.Text("b");
		LiteralExpression<string> c = Literal.Text("c");
		LiteralExpression<string> d = Literal.Text("d");

		BinaryExpression aPlusB = new(a, BinaryOperator.Add, b);
		BinaryExpression cPlusD = new(c, BinaryOperator.Add, d);
		BinaryExpression result = new(aPlusB, BinaryOperator.Multiply, cPlusD);

		Assert.AreEqual(aPlusB, result.Left);
		Assert.AreEqual(cPlusD, result.Right);
		Assert.AreEqual(BinaryOperator.Multiply, result.Operator);
	}

	/// <summary>
	/// Tests that node type names are correct for expressions.
	/// </summary>
	[TestMethod]
	public void Expression_ShouldHaveCorrectNodeTypeNames()
	{
		LiteralExpression<int> validExpr = Literal.Number(42);
		BinaryExpression binaryExpr = new(validExpr, BinaryOperator.Add, validExpr);
		VariableReference varRef = new("test");

		Assert.AreEqual("Literal<Int32>", validExpr.GetNodeTypeName());
		Assert.AreEqual("BinaryExpression", binaryExpr.GetNodeTypeName());
		Assert.AreEqual("VariableReference", varRef.GetNodeTypeName());
	}

	/// <summary>
	/// Tests error handling for null parameters.
	/// </summary>
	[TestMethod]
	public void Expression_ErrorHandling_ThrowsForNullParameters()
	{
		// Arrange
		LiteralExpression<int> validExpr = Literal.Number(42);

		// Act & Assert
		Assert.ThrowsException<ArgumentNullException>(() => new BinaryExpression(null!, BinaryOperator.Add, validExpr));
		Assert.ThrowsException<ArgumentNullException>(() => new BinaryExpression(validExpr, BinaryOperator.Add, null!));
		Assert.ThrowsException<ArgumentNullException>(() => new AssignmentStatement(null!, validExpr));
		Assert.ThrowsException<ArgumentNullException>(() => new AssignmentStatement(validExpr, null!));
		Assert.ThrowsException<ArgumentException>(() => new VariableDeclaration(""));
		Assert.ThrowsException<ArgumentException>(() => new VariableDeclaration(null!));
	}

	/// <summary>
	/// Tests that expressions can be converted to string correctly.
	/// </summary>
	[TestMethod]
	public void Expression_ShouldSupportToString()
	{
		// Test ToString methods
		LiteralExpression<string> stringLit = Literal.Text("test");
		LiteralExpression<int> intLit = Literal.Number(42);
		VariableReference varRef = new("myVar");

		Assert.AreEqual("test", stringLit.ToString());
		Assert.AreEqual("42", intLit.ToString());
		Assert.AreEqual("myVar", varRef.ToString());
	}
}
