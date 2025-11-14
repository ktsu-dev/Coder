// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests for the complete expression system integration including serialization and code generation.
/// </summary>
[TestClass]
public class ExpressionIntegrationTests
{
	/// <summary>
	/// Tests that complex mathematical expressions can be created, serialized, deserialized, and generated correctly.
	/// </summary>
	[TestMethod]
	public void ComplexMathExpression_ShouldWorkEndToEnd()
	{
		// Create a mathematical expression: (a + b) * (c - d) / 2.0
		BinaryExpression aPlusB = new(
			new VariableReference("a"),
			BinaryOperator.Add,
			new VariableReference("b")
		);

		BinaryExpression cMinusD = new(
			new VariableReference("c"),
			BinaryOperator.Subtract,
			new VariableReference("d")
		);

		BinaryExpression multiply = new(
			aPlusB,
			BinaryOperator.Multiply,
			cMinusD
		);

		BinaryExpression divide = new(
			multiply,
			BinaryOperator.Divide,
			Literal.DecimalValue(2.0)
		)
		{
			ExpectedType = "double"
		};

		// Test serialization
		YamlSerializer serializer = new();
		string yaml = serializer.Serialize(divide);

		Assert.IsNotNull(yaml);
		Assert.IsTrue(yaml.Contains("binaryExpression"));
		Assert.IsTrue(yaml.Contains("operator: Divide"));
		Assert.IsTrue(yaml.Contains("expectedType: double"));

		// Test deserialization
		YamlDeserializer deserializer = new();
		AstNode? deserialized = deserializer.Deserialize(yaml);

		Assert.IsNotNull(deserialized);
		Assert.IsInstanceOfType<BinaryExpression>(deserialized);

		BinaryExpression deserializedExpr = (BinaryExpression)deserialized;
		Assert.AreEqual(BinaryOperator.Divide, deserializedExpr.Operator);
		Assert.AreEqual("double", deserializedExpr.ExpectedType);

		// Test Python code generation
		PythonGenerator pythonGen = new();
		string pythonCode = pythonGen.Generate(deserializedExpr);

		Console.WriteLine($"Generated Python code: '{pythonCode}'");
		Assert.IsNotNull(pythonCode);
		// Expected: (((a + b) * (c - d)) / 2.0)
		Assert.IsTrue(pythonCode.Contains('a'));
		Assert.IsTrue(pythonCode.Contains('b'));
		Assert.IsTrue(pythonCode.Contains('c'));
		Assert.IsTrue(pythonCode.Contains('d'));

		// Test C# code generation
		CSharpGenerator csharpGen = new();
		string csharpCode = csharpGen.Generate(deserializedExpr);

		Console.WriteLine($"Generated C# code: '{csharpCode}'");
		Assert.IsNotNull(csharpCode);
		Assert.IsTrue(csharpCode.Contains('a'));
		Assert.IsTrue(csharpCode.Contains('b'));
		Assert.IsTrue(csharpCode.Contains('c'));
		Assert.IsTrue(csharpCode.Contains('d'));
	}

	/// <summary>
	/// Tests that a function with expression-based return statement works correctly.
	/// </summary>
	[TestMethod]
	public void FunctionWithExpressionReturn_ShouldWorkEndToEnd()
	{
		// Create function: double Calculate(double x, double y) { return x * y + 10; }
		BinaryExpression multiply = new(
			new VariableReference("x"),
			BinaryOperator.Multiply,
			new VariableReference("y")
		);

		BinaryExpression add = new(
			multiply,
			BinaryOperator.Add,
			Literal.Number(10)
		);

		FunctionDeclaration function = new("Calculate")
		{
			ReturnType = "double"
		};
		function.Parameters.Add(new Parameter("x", "double"));
		function.Parameters.Add(new Parameter("y", "double"));
		function.Body.Add(new ReturnStatement(add));

		// Test serialization
		YamlSerializer serializer = new();
		string yaml = serializer.Serialize(function);

		Assert.IsNotNull(yaml);
		Assert.IsTrue(yaml.Contains("functionDeclaration"));
		Assert.IsTrue(yaml.Contains("name: Calculate"));
		Assert.IsTrue(yaml.Contains("returnType: double"));

		// Test deserialization
		YamlDeserializer deserializer = new();
		AstNode? deserialized = deserializer.Deserialize(yaml);

		Assert.IsNotNull(deserialized);
		Assert.IsInstanceOfType<FunctionDeclaration>(deserialized);

		// Test Python code generation
		PythonGenerator pythonGen = new();
		string pythonCode = pythonGen.Generate(deserialized);

		Console.WriteLine($"Generated Python function: '{pythonCode}'");
		Assert.IsNotNull(pythonCode);
		Assert.IsTrue(pythonCode.Contains("def Calculate"));
		Assert.IsTrue(pythonCode.Contains('x'));
		Assert.IsTrue(pythonCode.Contains('y'));
		Assert.IsTrue(pythonCode.Contains("return"));

		// Test C# code generation
		CSharpGenerator csharpGen = new();
		string csharpCode = csharpGen.Generate(deserialized);

		Console.WriteLine($"Generated C# function: '{csharpCode}'");
		Assert.IsNotNull(csharpCode);
		Assert.IsTrue(csharpCode.Contains("Calculate"));
		Assert.IsTrue(csharpCode.Contains('x'));
		Assert.IsTrue(csharpCode.Contains('y'));
		Assert.IsTrue(csharpCode.Contains("return"));
	}

	/// <summary>
	/// Tests that variable declarations and assignments with expressions work correctly.
	/// </summary>
	[TestMethod]
	public void VariableDeclarationAndAssignment_ShouldWorkEndToEnd()
	{
		// Create: var result = a + b; result *= 2;
		BinaryExpression aPlusB = new(
			new VariableReference("a"),
			BinaryOperator.Add,
			new VariableReference("b")
		);

		VariableDeclaration varDecl = new("result", null, aPlusB)
		{
			IsTypeInferred = true
		};

		AssignmentStatement assignment = new(
			new VariableReference("result"),
			Literal.Number(2),
			AssignmentOperator.MultiplyAssign
		);

		// Test Python generation for variable declaration
		PythonGenerator pythonGen = new();
		string pythonVarCode = pythonGen.Generate(varDecl);
		string pythonAssignCode = pythonGen.Generate(assignment);

		Assert.IsTrue(pythonVarCode.Contains("result = (a + b)"));
		Assert.IsTrue(pythonAssignCode.Contains("result *= 2"));

		// Test C# generation
		CSharpGenerator csharpGen = new();
		string csharpVarCode = csharpGen.Generate(varDecl);
		string csharpAssignCode = csharpGen.Generate(assignment);

		Assert.IsTrue(csharpVarCode.Contains("var result = (a + b)"));
		Assert.IsTrue(csharpAssignCode.Contains("result *= 2"));
	}

	/// <summary>
	/// Tests that conditional expressions with logical operators work correctly.
	/// </summary>
	[TestMethod]
	public void ConditionalExpression_ShouldWorkEndToEnd()
	{
		// Create: (x > 0) && (y < 100)
		BinaryExpression xGreaterThanZero = new(
			new VariableReference("x"),
			BinaryOperator.GreaterThan,
			Literal.Number(0)
		);

		BinaryExpression yLessThan100 = new(
			new VariableReference("y"),
			BinaryOperator.LessThan,
			Literal.Number(100)
		);

		BinaryExpression condition = new(
			xGreaterThanZero,
			BinaryOperator.LogicalAnd,
			yLessThan100
		);

		// Test Python generation
		PythonGenerator pythonGen = new();
		string pythonCode = pythonGen.Generate(condition);

		Assert.IsTrue(pythonCode.Contains("(x > 0) and (y < 100)"));

		// Test C# generation
		CSharpGenerator csharpGen = new();
		string csharpCode = csharpGen.Generate(condition);

		Assert.IsTrue(csharpCode.Contains("(x > 0) && (y < 100)"));
	}

	/// <summary>
	/// Tests that all literal expression types work correctly.
	/// </summary>
	[TestMethod]
	public void LiteralExpressions_ShouldWorkForAllTypes()
	{
		// Test different literal types
		LiteralExpression<string> stringLit = Literal.Text("hello world");
		LiteralExpression<int> intLit = Literal.Number(42);
		LiteralExpression<bool> boolLit = Literal.Bool(true);
		LiteralExpression<double> doubleLit = Literal.DecimalValue(3.14159);

		PythonGenerator pythonGen = new();
		CSharpGenerator csharpGen = new();

		// Test Python generation
		Assert.AreEqual("\"hello world\"", pythonGen.Generate(stringLit));
		Assert.AreEqual("42", pythonGen.Generate(intLit));
		Assert.AreEqual("True", pythonGen.Generate(boolLit));
		Assert.AreEqual("3.14159", pythonGen.Generate(doubleLit));

		// Test C# generation
		Assert.AreEqual("\"hello world\"", csharpGen.Generate(stringLit));
		Assert.AreEqual("42", csharpGen.Generate(intLit));
		Assert.AreEqual("true", csharpGen.Generate(boolLit));
		Assert.AreEqual("3.14159d", csharpGen.Generate(doubleLit));
	}
}
