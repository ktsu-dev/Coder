// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.CLI;

using ktsu.Coder.Core;
using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Demonstrates the extended features of the Coder library including expressions,
/// variable declarations, assignments, and multiple language generators.
/// </summary>
public static class ExtendedDemo
{
	/// <summary>
	/// Runs a comprehensive demo showing all the features.
	/// </summary>
	public static void RunDemo()
	{
		Console.WriteLine("=== Extended Coder Demo - Advanced Features ===");
		Console.WriteLine();

		try
		{
			// Demo 1: Dependency Injection Setup
			DemoDependencyInjection();

			// Demo 2: Complex Function with Expressions
			DemoComplexFunction();

			// Demo 3: Variable Declarations and Assignments
			DemoVariablesAndAssignments();

			// Demo 4: Multiple Language Generation
			DemoMultipleLanguages();

			Console.WriteLine("✓ All extended demos completed successfully!");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Demo failed: {ex.Message}");
		}
	}

	private static void DemoDependencyInjection()
	{
		Console.WriteLine("Demo 1: Dependency Injection Setup");
		Console.WriteLine("==================================");

		// Set up DI container
		ServiceCollection services = new();
		services.AddCoder();
		ServiceProvider provider = services.BuildServiceProvider();

		// Get all language generators
		IEnumerable<ILanguageGenerator> generators = provider.GetServices<ILanguageGenerator>();

		Console.WriteLine($"Registered language generators: {generators.Count()}");
		foreach (ILanguageGenerator generator in generators)
		{
			Console.WriteLine($"  - {generator.DisplayName} ({generator.LanguageId}) -> .{generator.FileExtension}");
		}

		// Get serialization services
		YamlSerializer? serializer = provider.GetService<YamlSerializer>();
		YamlDeserializer? deserializer = provider.GetService<YamlDeserializer>();

		Console.WriteLine($"Serializer available: {serializer != null}");
		Console.WriteLine($"Deserializer available: {deserializer != null}");
		Console.WriteLine();
	}

	private static void DemoComplexFunction()
	{
		Console.WriteLine("Demo 2: Complex Function with Expressions");
		Console.WriteLine("=========================================");

		// Create a function that calculates compound interest
		FunctionDeclaration function = new("CalculateCompoundInterest")
		{
			ReturnType = "double"
		};

		// Add parameters
		function.Parameters.Add(new Parameter("principal", "double"));
		function.Parameters.Add(new Parameter("rate", "double"));
		function.Parameters.Add(new Parameter("time", "int"));
		function.Parameters.Add(new Parameter("compound", "int") { IsOptional = true, DefaultValue = "1" });

		// Create complex expression: principal * (1 + rate/compound)^(compound*time)
		// For demo, we'll create a simpler version: principal * (1 + rate * time)
		BinaryExpression rateTimeProduct = new(
			new VariableReference("rate"),
			BinaryOperator.Multiply,
			new VariableReference("time")
		);

		BinaryExpression onePlusRateTime = new(
			Literal.Number(1),
			BinaryOperator.Add,
			rateTimeProduct
		);

		BinaryExpression result = new(
			new VariableReference("principal"),
			BinaryOperator.Multiply,
			onePlusRateTime
		);

		function.Body.Add(new ReturnStatement(result));

		// Generate in both languages
		PythonGenerator pythonGen = new();
		CSharpGenerator csharpGen = new();

		Console.WriteLine("Python version:");
		Console.WriteLine(pythonGen.Generate(function));

		Console.WriteLine("C# version:");
		Console.WriteLine(csharpGen.Generate(function));
		Console.WriteLine();
	}

	private static void DemoVariablesAndAssignments()
	{
		Console.WriteLine("Demo 3: Variable Declarations and Assignments");
		Console.WriteLine("=============================================");

		// Create a function with variable declarations and assignments
		FunctionDeclaration function = new("ProcessData")
		{
			ReturnType = "void"
		};

		function.Parameters.Add(new Parameter("input", "string"));

		// Variable declarations
		VariableDeclaration lengthVar = new("length", "int",
			new BinaryExpression(new VariableReference("input.Length"), BinaryOperator.Add, Literal.Number(0)));

		VariableDeclaration resultVar = new("result", null, Literal.Text("Processing: "))
		{
			IsTypeInferred = true
		};

		VariableDeclaration counterVar = new("counter", "int", Literal.Number(0));

		// Assignment statements
		AssignmentStatement resultAssignment = new(
			new VariableReference("result"),
			new BinaryExpression(new VariableReference("result"), BinaryOperator.Add, new VariableReference("input"))
		);

		AssignmentStatement counterIncrement = new(
			new VariableReference("counter"),
			new VariableReference("counter"),
			AssignmentOperator.AddAssign
		);

		// Add to function body
		function.Body.Add(lengthVar);
		function.Body.Add(resultVar);
		function.Body.Add(counterVar);
		function.Body.Add(resultAssignment);
		function.Body.Add(counterIncrement);

		// Show YAML serialization
		YamlSerializer serializer = new();
		string yaml = serializer.Serialize(function);

		Console.WriteLine("YAML representation:");
		Console.WriteLine(yaml);

		// Generate code
		CSharpGenerator csharpGen = new();
		Console.WriteLine("Generated C# code:");
		Console.WriteLine(csharpGen.Generate(function));
		Console.WriteLine();
	}

	private static void DemoMultipleLanguages()
	{
		Console.WriteLine("Demo 4: Multiple Language Generation");
		Console.WriteLine("===================================");

		// Create a simple mathematical function
		FunctionDeclaration mathFunction = new("CalculateSum")
		{
			ReturnType = "int"
		};

		mathFunction.Parameters.Add(new Parameter("a", "int"));
		mathFunction.Parameters.Add(new Parameter("b", "int"));

		BinaryExpression sum = new(
			new AstLeafNode<string>("a"),
			BinaryOperator.Add,
			new AstLeafNode<string>("b")
		);

		mathFunction.Body.Add(new ReturnStatement(sum));

		// Generate in all available languages
		PythonGenerator pythonGen = new();
		CSharpGenerator csharpGen = new();

		Console.WriteLine("Same function in different languages:");
		Console.WriteLine();

		Console.WriteLine("Python:");
		Console.WriteLine(pythonGen.Generate(mathFunction));

		Console.WriteLine("C#:");
		Console.WriteLine(csharpGen.Generate(mathFunction));

		// Show feature comparison
		Console.WriteLine("Language Feature Comparison:");
		Console.WriteLine($"Python - Can generate: {pythonGen.CanGenerate(mathFunction)}");
		Console.WriteLine($"C# - Can generate: {csharpGen.CanGenerate(mathFunction)}");
		Console.WriteLine();
	}
}
