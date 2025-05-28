// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.App;

using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;

/// <summary>
/// A sample application demonstrating the Coder library functionality.
/// </summary>
public static class CoderApp
{
	/// <summary>
	/// Entry point for the application.
	/// </summary>
	public static void Main(string[] args)
	{
		Console.WriteLine("=== Coder Application - Code Generation Demo ===");
		Console.WriteLine();

		try
		{
			// Create a more complex example
			var complexFunction = CreateComplexFunction();

			// Show the AST structure
			Console.WriteLine("Complex Function AST:");
			Console.WriteLine("====================");
			ShowFunctionDetails(complexFunction);
			Console.WriteLine();

			// Serialize to YAML
			var serializer = new YamlSerializer();
			var yamlContent = serializer.Serialize(complexFunction);
			Console.WriteLine("YAML Representation:");
			Console.WriteLine("===================");
			Console.WriteLine(yamlContent);
			Console.WriteLine();

			// Generate Python code
			var pythonGenerator = new PythonGenerator();
			var pythonCode = pythonGenerator.Generate(complexFunction);
			Console.WriteLine("Generated Python Code:");
			Console.WriteLine("=====================");
			Console.WriteLine(pythonCode);
			Console.WriteLine();

			// Demonstrate multiple functions
			Console.WriteLine("Multiple Functions Example:");
			Console.WriteLine("==========================");
			var functions = CreateMultipleFunctions();

			foreach (var func in functions)
			{
				Console.WriteLine($"Function: {func.Name}");
				var code = pythonGenerator.Generate(func);
				Console.WriteLine(code);
				Console.WriteLine();
			}

			Console.WriteLine("✓ Application demo completed successfully!");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
			Environment.ExitCode = 1;
		}
	}

	private static FunctionDeclaration CreateComplexFunction()
	{
		var function = new FunctionDeclaration("process_data")
		{
			ReturnType = "bool"
		};

		// Add parameters with different types
		function.Parameters.Add(new Parameter("data", "list"));
		function.Parameters.Add(new Parameter("threshold", "float") { IsOptional = true, DefaultValue = "0.5" });
		function.Parameters.Add(new Parameter("debug", "bool") { IsOptional = true, DefaultValue = "False" });

		// Add multiple statements to the body
		function.Body.Add(new ReturnStatement(new AstLeafNode<bool>(true)));

		return function;
	}

	private static List<FunctionDeclaration> CreateMultipleFunctions()
	{
		var functions = new List<FunctionDeclaration>();

		// Simple getter function
		var getter = new FunctionDeclaration("get_value")
		{
			ReturnType = "int"
		};
		getter.Body.Add(new ReturnStatement(42));
		functions.Add(getter);

		// Function with string return
		var greeter = new FunctionDeclaration("greet")
		{
			ReturnType = "str"
		};
		greeter.Parameters.Add(new Parameter("name", "str"));
		greeter.Body.Add(new ReturnStatement(new AstLeafNode<string>("Hello, " + "name")));
		functions.Add(greeter);

		// Boolean function
		var checker = new FunctionDeclaration("is_valid")
		{
			ReturnType = "bool"
		};
		checker.Parameters.Add(new Parameter("value", "int"));
		checker.Body.Add(new ReturnStatement(new AstLeafNode<bool>(true)));
		functions.Add(checker);

		return functions;
	}

	private static void ShowFunctionDetails(FunctionDeclaration function)
	{
		Console.WriteLine($"Name: {function.Name}");
		Console.WriteLine($"Return Type: {function.ReturnType}");
		Console.WriteLine($"Parameters ({function.Parameters.Count}):");

		foreach (var param in function.Parameters)
		{
			var optionalInfo = param.IsOptional ? $" = {param.DefaultValue}" : "";
			Console.WriteLine($"  - {param.Name}: {param.Type}{optionalInfo}");
		}

		Console.WriteLine($"Body Statements: {function.Body.Count}");
		Console.WriteLine($"Children: {function.Children.Count}");
	}
}
