// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.App;

using System.ComponentModel;
using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// A TUI application for the Coder library using Spectre.Console.
/// </summary>
public static class Program
{
	/// <summary>
	/// Entry point for the application.
	/// </summary>
	public static int Main(string[] args)
	{
		// Test basic console output first
		Console.WriteLine("=== STARTING CODER APPLICATION ===");
		Console.WriteLine($"Arguments: {string.Join(", ", args)}");

#pragma warning disable CA1031 // Do not catch general exception types
		try
		{
			// Test Spectre.Console first
			Console.WriteLine("Testing Spectre.Console...");
			TestProgram.TestSpectreConsole();
			Console.WriteLine("Spectre.Console test completed.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in Spectre.Console test: {ex.Message}");
		}
#pragma warning restore CA1031 // Do not catch general exception types

		var app = new CommandApp<CoderCommand>();
		app.Configure(config =>
		{
			config.SetApplicationName("coder");
			config.SetApplicationVersion("1.0.0");
			config.AddCommand<InteractiveCommand>("interactive")
				.WithDescription("Launch interactive TUI mode")
				.WithAlias("tui");

			config.AddCommand<GenerateCommand>("generate")
				.WithDescription("Generate code from function definitions");

			config.AddCommand<DemoCommand>("demo")
				.WithDescription("Run a demo showing various features");
		});

		return app.Run(args);
	}
}

/// <summary>
/// Default command that launches interactive mode.
/// </summary>
[Description("Launch the Coder TUI application")]
public sealed class CoderCommand : Command<CoderCommand.Settings>
{
	/// <summary>
	/// Settings for the coder command.
	/// </summary>
	public sealed class Settings : CommandSettings
	{
	}

	/// <summary>
	/// Executes the coder command.
	/// </summary>
	/// <param name="context">The command context.</param>
	/// <param name="settings">The command settings.</param>
	/// <returns>The exit code.</returns>
	public override int Execute(CommandContext context, Settings settings) =>
		new InteractiveCommand().Execute(context, new InteractiveCommand.Settings());
}

/// <summary>
/// Interactive TUI command.
/// </summary>
[Description("Launch interactive TUI mode")]
public sealed class InteractiveCommand : Command<InteractiveCommand.Settings>
{
	/// <summary>
	/// Settings for the interactive command.
	/// </summary>
	public sealed class Settings : CommandSettings
	{
	}

	private static readonly List<FunctionDeclaration> functions = [];

	/// <summary>
	/// Executes the interactive command.
	/// </summary>
	/// <param name="context">The command context.</param>
	/// <param name="settings">The command settings.</param>
	/// <returns>The exit code.</returns>
	public override int Execute(CommandContext context, Settings settings)
	{
		ShowWelcome();

		while (true)
		{
			var choice = ShowMainMenu();

			if (!HandleMenuChoice(choice))
			{
				break;
			}
		}

		AnsiConsole.MarkupLine("[bold green]Thank you for using Coder![/]");
		return 0;
	}

	private static void ShowWelcome()
	{
		var panel = new Panel(new FigletText("CODER").Color(Color.Blue))
			.Border(BoxBorder.Double)
			.BorderStyle(Style.Parse("blue"))
			.Header("[bold yellow]Code Generation Library[/]")
			.Padding(1, 1);

		AnsiConsole.Write(panel);
		AnsiConsole.WriteLine();
	}

	private static string ShowMainMenu()
	{
		return AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("[bold cyan]What would you like to do?[/]")
				.PageSize(10)
				.AddChoices([
					"🏗️  Create Function",
					"📁 Load Functions",
					"🐍 Generate Python Code",
					"📄 View YAML Output",
					"🎮 Run Demo",
					"📊 View Statistics",
					"⚙️  Settings",
					"❌ Exit"
				]));
	}

	private static bool HandleMenuChoice(string choice)
	{
		return choice switch
		{
			"🏗️  Create Function" => HandleCreateFunction(),
			"📁 Load Functions" => HandleLoadFunctions(),
			"🐍 Generate Python Code" => HandleGenerateCode(),
			"📄 View YAML Output" => HandleViewYaml(),
			"🎮 Run Demo" => HandleRunDemo(),
			"📊 View Statistics" => HandleViewStats(),
			"⚙️  Settings" => HandleSettings(),
			"❌ Exit" => false,
			_ => true
		};
	}

	private static bool HandleCreateFunction()
	{
		AnsiConsole.MarkupLine("[bold yellow]Create New Function[/]");
		AnsiConsole.WriteLine();

		var functionName = AnsiConsole.Ask<string>("Enter function [green]name[/]:");
		var returnType = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title("Select [green]return type[/]:")
				.AddChoices("str", "int", "float", "bool", "list", "dict", "void"));

		var function = new FunctionDeclaration(functionName)
		{
			ReturnType = returnType
		};

		// Add parameters
		if (AnsiConsole.Confirm("Add parameters?"))
		{
			AddParameters(function);
		}

		// Add body statements
		if (AnsiConsole.Confirm("Add return statement?"))
		{
			AddReturnStatement(function, returnType);
		}

		functions.Add(function);

		var panel = new Panel($"[green]Function '{functionName}' created successfully![/]")
			.Border(BoxBorder.Rounded)
			.BorderStyle(Style.Parse("green"));

		AnsiConsole.Write(panel);
		AnsiConsole.WriteLine();

		return true;
	}

	private static void AddParameters(FunctionDeclaration function)
	{
		while (true)
		{
			var paramName = AnsiConsole.Ask<string>("Parameter [green]name[/] (or press Enter to finish):");
			if (string.IsNullOrWhiteSpace(paramName))
			{
				break;
			}

			var paramType = AnsiConsole.Prompt(
				new SelectionPrompt<string>()
					.Title($"Type for parameter '[green]{paramName}[/]':")
					.AddChoices("str", "int", "float", "bool", "list", "dict"));

			var isOptional = AnsiConsole.Confirm("Is this parameter optional?");

			var parameter = new Parameter(paramName, paramType)
			{
				IsOptional = isOptional
			};

			if (isOptional)
			{
				var defaultValue = AnsiConsole.Ask<string>("Default value:");
				parameter.DefaultValue = defaultValue;
			}

			function.Parameters.Add(parameter);

			if (!AnsiConsole.Confirm("Add another parameter?"))
			{
				break;
			}
		}
	}

	private static void AddReturnStatement(FunctionDeclaration function, string returnType)
	{
		if (returnType == "void")
		{
			return;
		}

		var returnValue = returnType switch
		{
			"str" => AnsiConsole.Ask<string>("Return value:"),
			"int" => AnsiConsole.Ask<int>("Return value:").ToString(),
			"float" => AnsiConsole.Ask<float>("Return value:").ToString(),
			"bool" => AnsiConsole.Confirm("Return value:").ToString().ToLower(),
			_ => "None"
		};

		function.Body.Add(new ReturnStatement(new AstLeafNode<string>(returnValue)));
	}

	private static bool HandleLoadFunctions()
	{
		functions.Clear();
		functions.AddRange(CreateSampleFunctions());

		AnsiConsole.MarkupLine("[bold green]Sample functions loaded![/]");
		AnsiConsole.WriteLine();

		ShowFunctionList();
		return true;
	}

	private static bool HandleGenerateCode()
	{
		if (functions.Count == 0)
		{
			AnsiConsole.MarkupLine("[bold red]No functions available. Create or load functions first.[/]");
			return true;
		}

		var selectedFunction = SelectFunction("Select function to generate code for:");
		if (selectedFunction == null)
		{
			return true;
		}

		var generator = new PythonGenerator();
		var code = generator.Generate(selectedFunction);

		var panel = new Panel(new Text(code))
			.Header($"[bold yellow]Generated Python Code for '{selectedFunction.Name}'[/]")
			.Border(BoxBorder.Double)
			.BorderStyle(Style.Parse("yellow"));

		AnsiConsole.Write(panel);
		AnsiConsole.WriteLine();

		return true;
	}

	private static bool HandleViewYaml()
	{
		if (functions.Count == 0)
		{
			AnsiConsole.MarkupLine("[bold red]No functions available. Create or load functions first.[/]");
			return true;
		}

		var selectedFunction = SelectFunction("Select function to view YAML for:");
		if (selectedFunction == null)
		{
			return true;
		}

		var serializer = new YamlSerializer();
		var yaml = serializer.Serialize(selectedFunction);

		var panel = new Panel(new Text(yaml))
			.Header($"[bold cyan]YAML Representation of '{selectedFunction.Name}'[/]")
			.Border(BoxBorder.Double)
			.BorderStyle(Style.Parse("cyan"));

		AnsiConsole.Write(panel);
		AnsiConsole.WriteLine();

		return true;
	}

	private static bool HandleRunDemo()
	{
		AnsiConsole.MarkupLine("[bold magenta]Running Demo...[/]");
		AnsiConsole.WriteLine();

		var demoCommand = new DemoCommand();
		demoCommand.Execute(null!, new DemoCommand.Settings());

		return true;
	}

	private static bool HandleViewStats()
	{
		var table = new Table()
			.Border(TableBorder.Rounded)
			.BorderStyle(Style.Parse("blue"))
			.AddColumn("[bold]Statistic[/]")
			.AddColumn("[bold]Value[/]");

		table.AddRow("Total Functions", functions.Count.ToString());
		table.AddRow("Functions with Parameters", functions.Count(f => f.Parameters.Count != 0).ToString());
		table.AddRow("Functions with Return Values", functions.Count(f => f.ReturnType != "void").ToString());

		if (functions.Count != 0)
		{
			table.AddRow("Average Parameters", functions.Average(f => f.Parameters.Count).ToString("F1"));
		}

		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();

		return true;
	}

	private static bool HandleSettings()
	{
		AnsiConsole.MarkupLine("[bold yellow]Settings[/]");
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("🚧 Settings panel coming soon!");
		AnsiConsole.WriteLine();
		return true;
	}

	private static void ShowFunctionList()
	{
		if (functions.Count == 0)
		{
			AnsiConsole.MarkupLine("[dim]No functions available[/]");
			return;
		}

		var table = new Table()
			.Border(TableBorder.Rounded)
			.BorderStyle(Style.Parse("green"))
			.AddColumn("[bold]Name[/]")
			.AddColumn("[bold]Return Type[/]")
			.AddColumn("[bold]Parameters[/]");

		foreach (var function in functions)
		{
			var paramStr = function.Parameters.Count > 0
				? string.Join(", ", function.Parameters.Select(p => $"{p.Name}: {p.Type}"))
				: "[dim]None[/]";

			table.AddRow(function.Name ?? "Unknown", function.ReturnType ?? "void", paramStr);
		}

		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();
	}

	private static FunctionDeclaration? SelectFunction(string prompt)
	{
		if (functions.Count == 0)
		{
			return null;
		}

		var choices = functions.Select(f => $"{f.Name} ({f.ReturnType})").ToArray();
		var selected = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title(prompt)
				.AddChoices(choices));

		var index = Array.IndexOf(choices, selected);
		return functions[index];
	}

	private static List<FunctionDeclaration> CreateSampleFunctions()
	{
		var sampleFunctions = new List<FunctionDeclaration>();

		// Simple function
		var hello = new FunctionDeclaration("say_hello")
		{
			ReturnType = "str"
		};
		hello.Parameters.Add(new Parameter("name", "str"));
		hello.Body.Add(new ReturnStatement(new AstLeafNode<string>("f\"Hello, {name}!\"")));
		sampleFunctions.Add(hello);

		// Function with multiple parameters
		var calculate = new FunctionDeclaration("calculate_sum")
		{
			ReturnType = "float"
		};
		calculate.Parameters.Add(new Parameter("a", "float"));
		calculate.Parameters.Add(new Parameter("b", "float"));
		calculate.Parameters.Add(new Parameter("multiplier", "float") { IsOptional = true, DefaultValue = "1.0" });
		calculate.Body.Add(new ReturnStatement(new AstLeafNode<string>("(a + b) * multiplier")));
		sampleFunctions.Add(calculate);

		// Boolean function
		var validator = new FunctionDeclaration("is_valid_email")
		{
			ReturnType = "bool"
		};
		validator.Parameters.Add(new Parameter("email", "str"));
		validator.Body.Add(new ReturnStatement(new AstLeafNode<string>("\"@\" in email and \".\" in email")));
		sampleFunctions.Add(validator);

		return sampleFunctions;
	}
}

/// <summary>
/// Generate command for batch code generation.
/// </summary>
[Description("Generate code from function definitions")]
public sealed class GenerateCommand : Command<GenerateCommand.Settings>
{
	/// <summary>
	/// Settings for the generate command.
	/// </summary>
	public sealed class Settings : CommandSettings
	{
		/// <summary>
		/// Gets or sets the target language for code generation.
		/// </summary>
		[CommandOption("-l|--language")]
		[Description("Target language for code generation")]
		[DefaultValue("python")]
		public string Language { get; set; } = "python";

		/// <summary>
		/// Gets or sets the output file path.
		/// </summary>
		[CommandOption("-o|--output")]
		[Description("Output file path")]
		public string? OutputPath { get; set; }
	}

	/// <summary>
	/// Executes the generate command.
	/// </summary>
	/// <param name="context">The command context.</param>
	/// <param name="settings">The command settings.</param>
	/// <returns>The exit code.</returns>
	public override int Execute(CommandContext context, Settings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		AnsiConsole.MarkupLine($"[bold cyan]Generating {settings.Language} code...[/]");

		// This is a placeholder - in a real implementation you'd read function definitions
		// from files or other sources
		var functions = CreateSampleFunctions();
		var generator = new PythonGenerator();

		foreach (var function in functions)
		{
			var code = generator.Generate(function);
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[bold yellow]// Function: {function.Name}[/]");
			AnsiConsole.WriteLine(code);
		}

		return 0;
	}

	private static List<FunctionDeclaration> CreateSampleFunctions()
	{
		// Same as in InteractiveCommand for consistency
		var functions = new List<FunctionDeclaration>();

		var hello = new FunctionDeclaration("say_hello")
		{
			ReturnType = "str"
		};
		hello.Parameters.Add(new Parameter("name", "str"));
		hello.Body.Add(new ReturnStatement(new AstLeafNode<string>("f\"Hello, {name}!\"")));
		functions.Add(hello);

		return functions;
	}
}

/// <summary>
/// Demo command to showcase features.
/// </summary>
[Description("Run a demo showing various features")]
public sealed class DemoCommand : Command<DemoCommand.Settings>
{
	/// <summary>
	/// Settings for the demo command.
	/// </summary>
	public sealed class Settings : CommandSettings
	{
	}

	/// <summary>
	/// Executes the demo command.
	/// </summary>
	/// <param name="context">The command context.</param>
	/// <param name="settings">The command settings.</param>
	/// <returns>The exit code.</returns>
	public override int Execute(CommandContext context, Settings settings)
	{
		AnsiConsole.Status()
			.Start("Running demo...", RunDemoWithProgress);

		return 0;
	}

	private static void RunDemoWithProgress(StatusContext ctx)
	{
		ctx.Status("Creating sample function...");
		Thread.Sleep(1000);

		var function = new FunctionDeclaration("demo_function")
		{
			ReturnType = "str"
		};
		function.Parameters.Add(new Parameter("value", "int"));
		function.Body.Add(new ReturnStatement(new AstLeafNode<string>("f\"Value is: {value}\"")));

		ctx.Status("Generating Python code...");
		Thread.Sleep(1000);

		var generator = new PythonGenerator();
		var pythonCode = generator.Generate(function);

		ctx.Status("Serializing to YAML...");
		Thread.Sleep(1000);

		var serializer = new YamlSerializer();
		var yamlOutput = serializer.Serialize(function);

		ctx.Status("Preparing output...");
		Thread.Sleep(500);

		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold green]✓ Demo completed![/]");
		AnsiConsole.WriteLine();

		var table = new Table()
			.Border(TableBorder.Double)
			.BorderStyle(Style.Parse("green"))
			.AddColumn("[bold]Generated Python Code[/]")
			.AddColumn("[bold]YAML Representation[/]");

		table.AddRow(pythonCode ?? "Error generating code", yamlOutput ?? "Error serializing YAML");
		AnsiConsole.Write(table);
	}
}
