// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.App;

using Spectre.Console;

/// <summary>
/// Simple test program to verify Spectre.Console functionality.
/// </summary>
public static class TestProgram
{
	/// <summary>
	/// Test method to verify Spectre.Console is working.
	/// </summary>
	public static void TestSpectreConsole()
	{
#pragma warning disable CA1031 // Do not catch general exception types
		try
		{
			AnsiConsole.WriteLine("Testing Spectre.Console...");
			AnsiConsole.MarkupLine("[bold green]This is a test![/]");

			var panel = new Panel("Hello World!")
				.Border(BoxBorder.Double)
				.BorderStyle(Style.Parse("blue"));

			AnsiConsole.Write(panel);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
			throw;
		}
#pragma warning restore CA1031 // Do not catch general exception types
	}
}
