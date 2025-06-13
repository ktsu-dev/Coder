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
	public void LiteralExpression_CreatesCorrectly()
	{
		// Arrange & Act
		LiteralExpression<string> stringLiteral = Literal.String("hello");
		LiteralExpression<int> intLiteral = Literal.Int(42);
		LiteralExpression<bool> boolLiteral = Literal.Bool(true);
		LiteralExpression<double> doubleLiteral = Literal.Double(3.14);

		// Assert
		Assert.AreEqual("hello", stringLiteral.Value);
		Assert.AreEqual(42, intLiteral.Value);
		Assert.IsTrue(boolLiteral.Value);
		Assert.AreEqual(3.14, doubleLiteral.Value);

		Assert.AreEqual("String", stringLiteral.ExpectedType);
		Assert.AreEqual("Int32", intLiteral.ExpectedType);
		Assert.AreEqual("Boolean", boolLiteral.ExpectedType);
		Assert.AreEqual("Double", doubleLiteral.ExpectedType);
	}

	/// <summary>
	/// Tests that binary expressions can be created and structured correctly.
	/// </summary>
	[TestMethod]
	public void BinaryExpression_CreatesCorrectly()
	{
		// Arrange
		LiteralExpression<int> left = Literal.Int(5);
		LiteralExpression<int> right = Literal.Int(3);

		// Act
		BinaryExpression addExpression = new(left, BinaryOperator.Add, right);
		BinaryExpression compareExpression = new(left, BinaryOperator.GreaterThan, right);

		// Assert
		Assert.AreEqual(BinaryOperator.Add, addExpression.Operator);
		Assert.AreSame(left, addExpression.Left);
		Assert.AreSame(right, addExpression.Right);

		Assert.AreEqual(BinaryOperator.GreaterThan, compareExpression.Operator);
	}

	/// <summary>
	/// Tests that variable declarations work correctly.
	/// </summary>
	[TestMethod]
	public void VariableDeclaration_CreatesCorrectly()
	{
		// Arrange & Act
		VariableDeclaration simpleVar = new("x", "int");
		VariableDeclaration inferredVar = new("result", null, Literal.Int(42));
		VariableDeclaration constantVar = new("PI", "double", Literal.Double(3.14159))
		{
			IsConstant = true
		};

		// Assert
		Assert.AreEqual("x", simpleVar.Name);
		Assert.AreEqual("int", simpleVar.Type);
		Assert.IsNull(simpleVar.InitialValue);
		Assert.IsFalse(simpleVar.IsTypeInferred);

		Assert.AreEqual("result", inferredVar.Name);
		Assert.IsNull(inferredVar.Type);
		Assert.IsNotNull(inferredVar.InitialValue);
		Assert.IsTrue(inferredVar.IsTypeInferred);

		Assert.AreEqual("PI", constantVar.Name);
		Assert.IsTrue(constantVar.IsConstant);
	}

	/// <summary>
	/// Tests that assignment statements work correctly.
	/// </summary>
	[TestMethod]
	public void AssignmentStatement_CreatesCorrectly()
	{
		// Arrange
		LiteralExpression<string> varName = Literal.String("x");
		LiteralExpression<int> value = Literal.Int(10);

		// Act
		AssignmentStatement assignment = new(varName, value);
		AssignmentStatement addAssignment = new(varName, value, AssignmentOperator.AddAssign);

		// Assert
		Assert.AreSame(varName, assignment.Target);
		Assert.AreSame(value, assignment.Value);
		Assert.AreEqual(AssignmentOperator.Assign, assignment.Operator);

		Assert.AreEqual(AssignmentOperator.AddAssign, addAssignment.Operator);
	}

	/// <summary>
	/// Tests that expressions can be cloned correctly.
	/// </summary>
	[TestMethod]
	public void Expressions_CloneCorrectly()
	{
		// Arrange
		LiteralExpression<int> original = Literal.Int(42);
		BinaryExpression binaryOriginal = new(Literal.Int(5), BinaryOperator.Add, Literal.Int(3));

		// Act
		LiteralExpression<int> cloned = (LiteralExpression<int>)original.Clone();
		BinaryExpression binaryCloned = (BinaryExpression)binaryOriginal.Clone();

		// Assert
		Assert.AreNotSame(original, cloned);
		Assert.AreEqual(original.Value, cloned.Value);

		Assert.AreNotSame(binaryOriginal, binaryCloned);
		Assert.AreEqual(binaryOriginal.Operator, binaryCloned.Operator);
		Assert.AreNotSame(binaryOriginal.Left, binaryCloned.Left);
		Assert.AreNotSame(binaryOriginal.Right, binaryCloned.Right);
	}

	/// <summary>
	/// Tests that complex nested expressions work.
	/// </summary>
	[TestMethod]
	public void ComplexExpression_WorksCorrectly()
	{
		// Arrange - Create expression: (a + b) * (c - d)
		LiteralExpression<string> a = Literal.String("a");
		LiteralExpression<string> b = Literal.String("b");
		LiteralExpression<string> c = Literal.String("c");
		LiteralExpression<string> d = Literal.String("d");

		BinaryExpression add = new(a, BinaryOperator.Add, b);
		BinaryExpression subtract = new(c, BinaryOperator.Subtract, d);

		// Act
		BinaryExpression multiply = new(add, BinaryOperator.Multiply, subtract);

		// Assert
		Assert.AreEqual(BinaryOperator.Multiply, multiply.Operator);
		Assert.IsInstanceOfType(multiply.Left, typeof(BinaryExpression));
		Assert.IsInstanceOfType(multiply.Right, typeof(BinaryExpression));

		BinaryExpression leftSide = (BinaryExpression)multiply.Left;
		BinaryExpression rightSide = (BinaryExpression)multiply.Right;

		Assert.AreEqual(BinaryOperator.Add, leftSide.Operator);
		Assert.AreEqual(BinaryOperator.Subtract, rightSide.Operator);
	}

	/// <summary>
	/// Tests that node type names are correct for expressions.
	/// </summary>
	[TestMethod]
	public void Expression_NodeTypeNames_AreCorrect()
	{
		// Arrange & Act
		LiteralExpression<string> stringLit = Literal.String("test");
		LiteralExpression<int> intLit = Literal.Int(42);
		BinaryExpression binExpr = new(intLit, BinaryOperator.Add, intLit);
		VariableDeclaration varDecl = new("x", "int");
		AssignmentStatement assignment = new(stringLit, intLit);

		// Assert
		Assert.AreEqual("Literal<String>", stringLit.GetNodeTypeName());
		Assert.AreEqual("Literal<Int32>", intLit.GetNodeTypeName());
		Assert.AreEqual("BinaryExpression", binExpr.GetNodeTypeName());
		Assert.AreEqual("VariableDeclaration", varDecl.GetNodeTypeName());
		Assert.AreEqual("AssignmentStatement", assignment.GetNodeTypeName());
	}

	/// <summary>
	/// Tests error handling for null parameters.
	/// </summary>
	[TestMethod]
	public void Expression_ErrorHandling_ThrowsForNullParameters()
	{
		// Arrange
		LiteralExpression<int> validExpr = Literal.Int(42);

		// Act & Assert
		Assert.ThrowsException<ArgumentNullException>(() => new BinaryExpression(null!, BinaryOperator.Add, validExpr));
		Assert.ThrowsException<ArgumentNullException>(() => new BinaryExpression(validExpr, BinaryOperator.Add, null!));
		Assert.ThrowsException<ArgumentNullException>(() => new AssignmentStatement(null!, validExpr));
		Assert.ThrowsException<ArgumentNullException>(() => new AssignmentStatement(validExpr, null!));
		Assert.ThrowsException<ArgumentException>(() => new VariableDeclaration(""));
		Assert.ThrowsException<ArgumentException>(() => new VariableDeclaration(null!));
	}
}
