// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.ConsoleApp;

using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;

/// <summary>
/// A command-line interface for the Coder library demonstrating AST generation and code output.
/// </summary>
public static class CoderCLI
{
	/// <summary>
	/// Entry point for the CLI application.
	/// </summary>
	public static void Main(string[] args)
	{
		ArgumentNullException.ThrowIfNull(args);

		Console.WriteLine("=== Coder CLI - Code Generation Tool ===");
		Console.WriteLine();

		try
		{
			if (args.Length > 0 && args[0] == "--help")
			{
				ShowHelp();
				return;
			}

			if (args.Length > 0 && args[0] == "--extended")
			{
				ExtendedDemo.RunDemo();
				return;
			}

			// Create a sample function AST
			FunctionDeclaration sampleFunction = CreateSampleFunction();

			// Show the AST structure
			Console.WriteLine("Generated AST Structure:");
			Console.WriteLine("========================");
			ShowAstDetails(sampleFunction);
			Console.WriteLine();

			// Serialize to YAML
			YamlSerializer serializer = new();
			string yamlContent = serializer.Serialize(sampleFunction);
			Console.WriteLine("YAML Serialization:");
			Console.WriteLine("==================");
			Console.WriteLine(yamlContent);
			Console.WriteLine();

			// Generate Python code
			PythonGenerator pythonGenerator = new();
			string pythonCode = pythonGenerator.Generate(sampleFunction);
			Console.WriteLine("Generated Python Code:");
			Console.WriteLine("=====================");
			Console.WriteLine(pythonCode);
			Console.WriteLine();

			// Demonstrate round-trip serialization
			YamlDeserializer deserializer = new();
			AstNode? deserializedAst = deserializer.Deserialize(yamlContent);
			if (deserializedAst == null)
			{
				Console.WriteLine("❌ Failed to deserialize YAML content");
				Environment.ExitCode = 1;
				return;
			}

			string regeneratedCode = pythonGenerator.Generate(deserializedAst);

			Console.WriteLine("Round-trip Test (YAML -> AST -> Python):");
			Console.WriteLine("========================================");
			Console.WriteLine(regeneratedCode);
			Console.WriteLine();

			Console.WriteLine("✓ All operations completed successfully!");
		}
		catch (ArgumentException ex)
		{
			Console.WriteLine($"❌ Argument Error: {ex.Message}");
			Environment.ExitCode = 1;
		}
		catch (InvalidOperationException ex)
		{
			Console.WriteLine($"❌ Operation Error: {ex.Message}");
			Environment.ExitCode = 1;
		}
#pragma warning disable CA1031 // Do not catch general exception types
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Unexpected Error: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
			Environment.ExitCode = 1;
		}
#pragma warning restore CA1031 // Do not catch general exception types
	}

	private static FunctionDeclaration CreateSampleFunction()
	{
		FunctionDeclaration function = new("calculate_sum")
		{
			ReturnType = "int"
		};

		// Add parameters
		function.Parameters.Add(new Parameter("a", "int"));
		function.Parameters.Add(new Parameter("b", "int"));
		function.Parameters.Add(new Parameter("include_debug", "bool") { IsOptional = true, DefaultValue = "False" });

		// Add function body
		function.Body.Add(new ReturnStatement(new AstLeafNode<string>("a + b")));

		return function;
	}

	private static void ShowAstDetails(FunctionDeclaration function)
	{
		Console.WriteLine($"Function: {function.Name}");
		Console.WriteLine($"Return Type: {function.ReturnType}");
		Console.WriteLine($"Parameters: {function.Parameters.Count}");

		foreach (Parameter param in function.Parameters)
		{
			string optionalInfo = param.IsOptional ? $" (optional, default: {param.DefaultValue})" : "";
			Console.WriteLine($"  - {param.Name}: {param.Type}{optionalInfo}");
		}

		Console.WriteLine($"Body Statements: {function.Body.Count}");
	}

	private static void ShowHelp()
	{
		Console.WriteLine("Coder CLI - Code Generation Tool");
		Console.WriteLine("Usage: Coder.CLI [options]");
		Console.WriteLine();
		Console.WriteLine("Options:");
		Console.WriteLine("  --help       Show this help message");
		Console.WriteLine("  --extended   Run extended demo with advanced features");
		Console.WriteLine();
		Console.WriteLine("This tool demonstrates the Coder library's capabilities:");
		Console.WriteLine("- Creating AST (Abstract Syntax Tree) structures");
		Console.WriteLine("- Serializing AST to YAML format");
		Console.WriteLine("- Generating code in supported languages (Python, C#)");
		Console.WriteLine("- Round-trip serialization/deserialization");
		Console.WriteLine("- Expression system (binary operators, literals)");
		Console.WriteLine("- Variable declarations and assignments");
		Console.WriteLine("- Dependency injection configuration");
	}
}
