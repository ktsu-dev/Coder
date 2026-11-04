// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a variable declared directly in a namespace or a file is written as the declaration
/// that scope holds, rather than as a local (issue #232).
/// </summary>
/// <remarks>
/// C#'s <c>var</c> is only for a local (CS0825), Rust's <c>let</c> is a statement and Go's
/// <c>:=</c> is one too, so all three were refused outside a function body. A local in a function
/// of the same scope is still a local.
/// </remarks>
[TestClass]
public class ModuleScopeVariableTests
{
	/// <summary>
	/// Builds a file holding a namespace, each with an inferred and a typed variable, and a function
	/// that declares an inferred local of its own.
	/// </summary>
	/// <returns>The file.</returns>
	internal static SourceFile Scoped()
	{
		NamespaceDeclaration geometry = new("geometry");
		geometry.Members.Add(new VariableDeclaration("count", initialValue: Literal.Number(0)));
		geometry.Members.Add(new VariableDeclaration("ratio", "int", Literal.Number(2)));

		FunctionDeclaration measure = new("measure") { ReturnType = "int" };
		measure.Body.Add(new VariableDeclaration("local", initialValue: Literal.Number(3)));
		measure.Body.Add(new ReturnStatement(new VariableReference("local")));
		geometry.Members.Add(measure);

		SourceFile file = new("scoped");
		file.Members.Add(new VariableDeclaration("label", initialValue: Literal.Text("a")));
		file.Members.Add(new VariableDeclaration("limit", initialValue: Literal.Number(4)) { IsConstant = true });
		file.Members.Add(geometry);
		return file;
	}

	/// <summary>
	/// Tests that C# says the type an inferred held variable's initialiser establishes.
	/// </summary>
	[TestMethod]
	public void CSharp_InferredHeldVariable_SaysItsType()
	{
		string code = new CSharpGenerator().Generate(Scoped());

		Assert.Contains("public static int count = 0;", code, StringComparison.Ordinal, code);
		Assert.Contains("public static int ratio = 2;", code, StringComparison.Ordinal, code);
		Assert.Contains("public static string label = \"a\";", code, StringComparison.Ordinal, code);
		Assert.Contains("public const int limit = 4;", code, StringComparison.Ordinal, code);
		Assert.Contains("var local = 3;", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("static var", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that C# writes a held variable with nothing to infer from as an object, and says so.
	/// </summary>
	[TestMethod]
	public void CSharp_HeldVariableWithNoType_IsAnObjectWithANote()
	{
		NamespaceDeclaration scope = new("Scope");
		scope.Members.Add(new VariableDeclaration("unknown", initialValue: new VariableReference("elsewhere")));

		string code = new CSharpGenerator().Generate(scope);

		Assert.Contains("public static object unknown = elsewhere;", code, StringComparison.Ordinal, code);
		Assert.Contains("// unknown: C# infers no type for a field", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that Rust writes a module's variables as statics and constants, and a function's as
	/// <c>let</c>.
	/// </summary>
	[TestMethod]
	public void Rust_ModuleVariables_AreItems()
	{
		string code = new RustGenerator().Generate(Scoped());

		Assert.Contains("static count: i32 = 0;", code, StringComparison.Ordinal, code);
		Assert.Contains("static ratio: i32 = 2;", code, StringComparison.Ordinal, code);
		Assert.Contains("static label: &'static str = \"a\";", code, StringComparison.Ordinal, code);
		Assert.Contains("const limit: i32 = 4;", code, StringComparison.Ordinal, code);
		Assert.Contains("let mut local = 3;", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("let mut count", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that Go writes a package's inferred variable with <c>var</c>, and a function's with
	/// <c>:=</c>.
	/// </summary>
	[TestMethod]
	public void Go_PackageVariables_AreDeclaredWithVar()
	{
		string code = new GoGenerator().Generate(Scoped());

		Assert.Contains("var count = 0", code, StringComparison.Ordinal, code);
		Assert.Contains("var label = \"a\"", code, StringComparison.Ordinal, code);
		Assert.Contains("local := 3", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("count :=", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("label :=", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that the C# for the file builds.
	/// </summary>
	[TestMethod]
	public void CSharp_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No .NET SDK on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Scoped.csproj"), """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <TargetFramework>net10.0</TargetFramework>
				    <OutputType>Library</OutputType>
				  </PropertyGroup>
				</Project>
				""");
			File.WriteAllText(Path.Combine(directory, "Scoped.cs"), new CSharpGenerator().Generate(Scoped()));

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that the Rust for the file compiles.
	/// </summary>
	[TestMethod]
	public void Rust_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new RustGenerator().Generate(Scoped());
			File.WriteAllText(Path.Combine(directory, "scoped.rs"), $"#![allow(dead_code, non_upper_case_globals)]{Environment.NewLine}{source}");

			(int exitCode, string output) = ToolchainHarness.Run("rustc", "--crate-type lib --edition 2021 -o scoped.rlib scoped.rs", directory);
			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that the Go for the file compiles.
	/// </summary>
	[TestMethod]
	public void Go_Compiles()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new GoGenerator().Generate(Scoped());
			File.WriteAllText(Path.Combine(directory, "go.mod"), $"module scoped{Environment.NewLine}{Environment.NewLine}go 1.21{Environment.NewLine}");
			File.WriteAllText(Path.Combine(directory, "scoped.go"), source);

			(int exitCode, string output) = ToolchainHarness.Run("go", "vet .", directory);
			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}
}
