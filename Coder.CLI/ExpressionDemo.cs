// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.CLI;

using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Demonstrates the complete expression system working with visual editor in mind.
/// </summary>
public static class ExpressionDemo
{
	/// <summary>
	/// Runs the comprehensive expression system demonstration.
	/// </summary>
	public static void Run()
	{
		Console.WriteLine("🚀 Expression System Demo - Visual Editor Ready");
		Console.WriteLine("=" + new string('=', 50));

		// Setup DI container
		ServiceCollection services = new();
		services.AddCoder();
		using ServiceProvider provider = services.BuildServiceProvider();

		YamlSerializer yamlSerializer = provider.GetRequiredService<YamlSerializer>();
		YamlDeserializer yamlDeserializer = provider.GetRequiredService<YamlDeserializer>();
		PythonGenerator pythonGenerator = provider.GetRequiredService<PythonGenerator>();
		CSharpGenerator csharpGenerator = provider.GetRequiredService<CSharpGenerator>();

		// Create a mathematical expression: result = (a + b) * (c - d) / 2.0
		BinaryExpression demo = CreateMathExpression();
		Console.WriteLine("\n📊 Mathematical Expression Demo");
		Console.WriteLine($"Expression: {demo}");

		// Create a conditional logic expression
		BinaryExpression conditional = CreateConditionalExpression();
		Console.WriteLine("\n🤔 Conditional Logic Demo");
		Console.WriteLine($"Condition: {conditional}");

		// Create variable declarations with expressions
		List<VariableDeclaration> variables = CreateVariableDeclarations();
		Console.WriteLine("\n📋 Variable Declarations with Expressions");
		foreach (VariableDeclaration variable in variables)
		{
			Console.WriteLine($"  {variable}");
		}

		// Create a function with complex expressions
		FunctionDeclaration function = CreateComplexFunction();
		Console.WriteLine("\n⚙️ Complex Function with Expressions");
		Console.WriteLine($"Function: {function.Name}({string.Join(", ", function.Parameters.Select(p => $"{p.Type} {p.Name}"))})");

		// Test serialization (critical for visual editor state saving)
		Console.WriteLine("\n💾 YAML Serialization Test (Visual Editor State)");
		try
		{
			string serialized = yamlSerializer.Serialize(function);
			Console.WriteLine("✅ Serialization successful - Visual editor can save/load this structure");
			Console.WriteLine($"First few lines:\n{string.Join('\n', serialized.Split('\n').Take(10))}...");

			// Test deserialization
			FunctionDeclaration? deserialized = yamlDeserializer.Deserialize(serialized) as FunctionDeclaration;
			Console.WriteLine("✅ Deserialization successful - Visual editor can restore this structure");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Serialization error: {ex.Message}");
		}

		// Test code generation
		Console.WriteLine("\n🔄 Multi-Language Code Generation");
		try
		{
			Console.WriteLine("Python output:");
			Console.WriteLine(pythonGenerator.Generate(function));

			Console.WriteLine("\nC# output:");
			Console.WriteLine(csharpGenerator.Generate(function));
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Code generation error: {ex.Message}");
		}

		Console.WriteLine("\n🎯 Visual Editor Benefits:");
		Console.WriteLine("• Each Expression is a discrete node - perfect for visual connections");
		Console.WriteLine("• Type information enables intelligent connection validation");
		Console.WriteLine("• YAML serialization preserves exact graph structure");
		Console.WriteLine("• Clone() methods support visual copy/paste operations");
		Console.WriteLine("• Extensible design allows custom expression types");
	}

	private static BinaryExpression CreateMathExpression()
	{
		// (a + b) * (c - d) / 2.0
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
		return divide;
	}

	private static BinaryExpression CreateConditionalExpression()
	{
		// (age >= 18) && (hasLicense == true)
		BinaryExpression ageCheck = new(
			new VariableReference("age"),
			BinaryOperator.GreaterThanOrEqual,
			Literal.Number(18)
		);

		BinaryExpression licenseCheck = new(
			new VariableReference("hasLicense"),
			BinaryOperator.Equal,
			Literal.Bool(true)
		);

		BinaryExpression combined = new(
			ageCheck,
			BinaryOperator.LogicalAnd,
			licenseCheck
		)
		{
			ExpectedType = "bool"
		};
		return combined;
	}

	private static List<VariableDeclaration> CreateVariableDeclarations()
	{
		return
		[
			new("result", "double", CreateMathExpression()),
			new("canDrive", "bool", CreateConditionalExpression()),
			new("message", null, Literal.Text("Hello, World!")) { IsTypeInferred = true },
			new("counter", "int", Literal.Number(0)) { IsConstant = true }
		];
	}

	private static FunctionDeclaration CreateComplexFunction()
	{
		FunctionDeclaration function = new("calculateScore")
		{
			ReturnType = "double"
		};
		function.Parameters.Add(new Parameter("baseScore", "double"));
		function.Parameters.Add(new Parameter("multiplier", "double") { IsOptional = true, DefaultValue = "1.0" });
		function.Parameters.Add(new Parameter("bonus", "double") { IsOptional = true, DefaultValue = "0.0" });

		// Complex calculation: (baseScore * multiplier) + bonus
		BinaryExpression multiplication = new(
			new VariableReference("baseScore"),
			BinaryOperator.Multiply,
			new VariableReference("multiplier")
		);

		BinaryExpression finalResult = new(
			multiplication,
			BinaryOperator.Add,
			new VariableReference("bonus")
		);

		function.Body.Add(new ReturnStatement(finalResult));
		return function;
	}
}
