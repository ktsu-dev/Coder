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
		Assert.IsTrue(yaml.Contains("binaryExpression"), "Serialized YAML should contain 'binaryExpression'");
		Assert.IsTrue(yaml.Contains("operator: Divide"), "Serialized YAML should contain 'operator: Divide'");
		Assert.IsTrue(yaml.Contains("expectedType: double"), "Serialized YAML should contain 'expectedType: double'");

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
		Assert.IsTrue(pythonCode.Contains('a'), "Python code should contain variable 'a'");
		Assert.IsTrue(pythonCode.Contains('b'), "Python code should contain variable 'b'");
		Assert.IsTrue(pythonCode.Contains('c'), "Python code should contain variable 'c'");
		Assert.IsTrue(pythonCode.Contains('d'), "Python code should contain variable 'd'");

		// Test C# code generation
		CSharpGenerator csharpGen = new();
		string csharpCode = csharpGen.Generate(deserializedExpr);

		Console.WriteLine($"Generated C# code: '{csharpCode}'");
		Assert.IsNotNull(csharpCode);
		Assert.IsTrue(csharpCode.Contains('a'), "C# code should contain variable 'a'");
		Assert.IsTrue(csharpCode.Contains('b'), "C# code should contain variable 'b'");
		Assert.IsTrue(csharpCode.Contains('c'), "C# code should contain variable 'c'");
		Assert.IsTrue(csharpCode.Contains('d'), "C# code should contain variable 'd'");
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
		Assert.IsTrue(yaml.Contains("functionDeclaration"), "Serialized YAML should contain 'functionDeclaration'");
		Assert.IsTrue(yaml.Contains("name: Calculate"), "Serialized YAML should contain 'name: Calculate'");
		Assert.IsTrue(yaml.Contains("returnType: double"), "Serialized YAML should contain 'returnType: double'");

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
		Assert.IsTrue(pythonCode.Contains("def Calculate"), "Python code should contain 'def Calculate'");
		Assert.IsTrue(pythonCode.Contains('x'), "Python code should contain parameter 'x'");
		Assert.IsTrue(pythonCode.Contains('y'), "Python code should contain parameter 'y'");
		Assert.IsTrue(pythonCode.Contains("return"), "Python code should contain 'return' statement");

		// Test C# code generation
		CSharpGenerator csharpGen = new();
		string csharpCode = csharpGen.Generate(deserialized);

		Console.WriteLine($"Generated C# function: '{csharpCode}'");
		Assert.IsNotNull(csharpCode);
		Assert.IsTrue(csharpCode.Contains("Calculate"), "C# code should contain method name 'Calculate'");
		Assert.IsTrue(csharpCode.Contains('x'), "C# code should contain parameter 'x'");
		Assert.IsTrue(csharpCode.Contains('y'), "C# code should contain parameter 'y'");
		Assert.IsTrue(csharpCode.Contains("return"), "C# code should contain 'return' statement");
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

		Assert.IsTrue(pythonVarCode.Contains("result = (a + b)"), "Python variable declaration should contain 'result = (a + b)'");
		Assert.IsTrue(pythonAssignCode.Contains("result *= 2"), "Python assignment should contain 'result *= 2'");

		// Test C# generation
		CSharpGenerator csharpGen = new();
		string csharpVarCode = csharpGen.Generate(varDecl);
		string csharpAssignCode = csharpGen.Generate(assignment);

		Assert.IsTrue(csharpVarCode.Contains("var result = (a + b)"), "C# variable declaration should contain 'var result = (a + b)'");
		Assert.IsTrue(csharpAssignCode.Contains("result *= 2"), "C# assignment should contain 'result *= 2'");
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

		Assert.IsTrue(pythonCode.Contains("(x > 0) and (y < 100)"), "Python code should contain '(x > 0) and (y < 100)'");

		// Test C# generation
		CSharpGenerator csharpGen = new();
		string csharpCode = csharpGen.Generate(condition);

		Assert.IsTrue(csharpCode.Contains("(x > 0) && (y < 100)"), "C# code should contain '(x > 0) && (y < 100)'");
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
