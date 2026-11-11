// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that negating a <c>uint</c> or <c>ulong</c> wraps the way the C family does in every target (#373).
/// </summary>
/// <remarks>
/// <c>-a</c> on an unsigned operand failed to compile in Rust (E0600) and C# (CS0266 for <c>uint</c>,
/// CS0023 for <c>ulong</c>), and gave a negative number in Python and JavaScript. The generated code is
/// run rather than read, since the wrong answer is a number like the right one.
/// </remarks>
[TestClass]
public class UnsignedNegationTests
{
	private const string Expected32 = "4294967291";

	private const string Expected64 = "18446744073709551611";

	private static SourceFile File()
	{
		SourceFile file = new("main");
		file.Members.Add(Negating("neg", "uint"));
		file.Members.Add(Negating("neg64", "ulong"));
		file.Members.Add(Negating("neg_int", "int"));
		return file;
	}

	private static FunctionDeclaration Negating(string name, string type)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference(type) };
		function.Parameters.Add(new Parameter("a", type));
		function.Body.Add(new ReturnStatement(new UnaryExpression(UnaryOperator.Negate, new VariableReference("a"))));
		return function;
	}

	private static void RunAndCompare(string command, string file, string program, string expected, string source)
	{
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, file), program);
			(int exitCode, string output) = ToolchainHarness.Run(command, file, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
			Assert.AreEqual(expected, output.Trim().ReplaceLineEndings("\n"), source);
		});
	}

	/// <summary>
	/// Tests that the Python output masks a negated unsigned value back to its width.
	/// </summary>
	[TestMethod]
	public void Python_NegatedUnsigned_Wraps()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		string source = new PythonGenerator().Generate(File());
		StringBuilder module = new(source);
		module.AppendLine();
		module.AppendLine("print(neg(5))");
		module.AppendLine("print(neg64(5))");
		module.AppendLine("print(neg_int(5))");
		RunAndCompare(python, "check.py", module.ToString(), $"{Expected32}\n{Expected64}\n-5", source);
	}

	/// <summary>
	/// Tests that the JavaScript output reads a negated unsigned value back unsigned.
	/// </summary>
	[TestMethod]
	public void JavaScript_NegatedUnsigned_Wraps()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		string source = new JavaScriptGenerator().Generate(File());
		StringBuilder module = new(source);
		module.AppendLine();
		module.AppendLine("console.log(neg(5));");

		// A ulong past 2^53 is rounded by JavaScript's number conversion, which #256 accepted, so the
		// 64-bit answer is compared with the exact one rounded the same way.
		module.AppendLine("console.log(neg64(5) === Number(18446744073709551611n));");
		module.AppendLine("console.log(neg_int(5));");
		RunAndCompare(node, "check.js", module.ToString(), $"{Expected32}\ntrue\n-5", source);
	}

	/// <summary>
	/// Tests that the Rust output compiles and wraps, Rust having no unary minus on an unsigned integer.
	/// </summary>
	[TestMethod]
	public void Rust_NegatedUnsigned_CompilesAndWraps()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		string source = new RustGenerator().Generate(File());
		Assert.Contains("(a.wrapping_neg())", source, StringComparison.Ordinal, source);
		string program = $$"""
			#![allow(unused_parens, dead_code)]
			{{source}}
			fn main() {
			    println!("{}", neg(5));
			    println!("{}", neg64(5));
			    println!("{}", neg_int(5));
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, "neg.rs"), program);
			(int exitCode, string output) = ToolchainHarness.Run("rustc", "--edition 2021 -o neg neg.rs", directory);
			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
			(exitCode, output) = ToolchainHarness.Run(Path.Combine(directory, "neg"), string.Empty, directory);
			Assert.AreEqual(0, exitCode, output);
			Assert.AreEqual($"{Expected32}\n{Expected64}\n-5", output.Trim().ReplaceLineEndings("\n"), source);
		});
	}

	/// <summary>
	/// Tests that the C# output compiles and wraps, C# promoting <c>-uint</c> to <c>long</c> and having no <c>-ulong</c>.
	/// </summary>
	[TestMethod]
	public void CSharp_NegatedUnsigned_CompilesAndWraps()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		string source = new CSharpGenerator().Generate(File());
		Assert.Contains("unchecked(0u - a)", source, StringComparison.Ordinal, source);
		Assert.Contains("unchecked(0UL - a)", source, StringComparison.Ordinal, source);
		Assert.Contains("(-a)", source, StringComparison.Ordinal, source);

		const string project = """
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <OutputType>Exe</OutputType>
			    <TargetFramework>net10.0</TargetFramework>
			    <Nullable>disable</Nullable>
			    <ImplicitUsings>disable</ImplicitUsings>
			  </PropertyGroup>
			</Project>
			""";
		const string program = """
			public static class Program
			{
				public static void Main()
				{
					System.Console.WriteLine(Globals.neg(5u));
					System.Console.WriteLine(Globals.neg64(5UL));
					System.Console.WriteLine(Globals.neg_int(5));
				}
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, "Generated.csproj"), project);
			System.IO.File.WriteAllText(Path.Combine(directory, "Program.cs"), program);
			System.IO.File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);
			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "run -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
			Assert.EndsWith($"{Expected32}\n{Expected64}\n-5", output.Trim().ReplaceLineEndings("\n"), source);
		});
	}
}
