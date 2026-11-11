// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a local marked inferred keeps its declared type when its initialiser is a typeless
/// list, which has no type of its own to infer (#447).
/// </summary>
/// <remarks>
/// The list takes its type from the declaration, so dropping the declared type for <c>var</c>,
/// <c>auto</c>, <c>:=</c> or a bare <c>let</c> leaves nothing to say what it is: C# refuses
/// <c>var xs = [1, 2];</c> (CS9176), Go refuses <c>xs := {1, 2}</c>, and C++ and Rust infer an
/// <c>std::initializer_list</c> and an array, which fail as soon as the list is used as one. Each
/// test returns the local from a function declared to return the list, which is where Rust's array
/// stops compiling.
/// </remarks>
[TestClass]
public class InferredListLocalTests
{
	private const string CSharpProject = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net10.0</TargetFramework>
		    <OutputType>Library</OutputType>
		    <Nullable>enable</Nullable>
		    <ImplicitUsings>disable</ImplicitUsings>
		  </PropertyGroup>
		</Project>
		""";

	private const string GoModule = """
		module exemplar

		go 1.21

		""";

	private const string CppDriver = """
		#include <vector>
		#include "lists.h"

		int main()
		{
			std::vector<int> xs = Build();
			xs.push_back(3);
			return static_cast<int>(xs.size()) == 3 ? 0 : 1;
		}
		""";

	/// <summary>
	/// Builds <c>list&lt;int&gt; Build() { list&lt;int&gt; xs = {1, 2} /* inferred */; return xs; }</c>.
	/// </summary>
	/// <returns>The source file.</returns>
	/// <param name="import">The import the file declares, which only C# needs for <c>List&lt;T&gt;</c>.</param>
	private static SourceFile InferredList(string? import = null)
	{
		ConstructionExpression list = new();
		list.Arguments.Add(Literal.Number(1));
		list.Arguments.Add(Literal.Number(2));

		FunctionDeclaration make = new("Build") { ReturnType = TypeReference.Parse("list<int>"), Visibility = Visibility.Public };
		make.Body.Add(new VariableDeclaration("xs", "list<int>", list) { IsTypeInferred = true });
		make.Body.Add(new ReturnStatement(new VariableReference("xs")));

		SourceFile file = new("lists");
		if (import is not null)
		{
			file.Imports.Add(import);
		}

		file.Members.Add(make);
		return file;
	}

	/// <summary>
	/// Tests that C# says the list's type rather than <c>var</c>, and compiles.
	/// </summary>
	[TestMethod]
	public void CSharp_KeepsTheDeclaredTypeAndCompiles()
	{
		string source = new CSharpGenerator().Generate(InferredList("System.Collections.Generic"));
		Assert.DoesNotContain("var xs", source, StringComparison.Ordinal, source);
		Assert.Contains("List<int> xs = [1, 2];", source, StringComparison.Ordinal, source);

		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), CSharpProject);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a C# local with neither a type nor a typed initialiser says why it is an object,
	/// rather than writing a <c>var</c> C# cannot infer.
	/// </summary>
	[TestMethod]
	public void CSharp_UntypedListLocal_SaysItIsInexpressible()
	{
		ConstructionExpression list = new();
		list.Arguments.Add(Literal.Number(1));

		FunctionDeclaration make = new("make") { ReturnType = "void" };
		make.Body.Add(new VariableDeclaration("xs", initialValue: list) { IsTypeInferred = true });

		SourceFile file = new("lists");
		file.Members.Add(make);

		string source = new CSharpGenerator().Generate(file);
		Assert.DoesNotContain("var xs", source, StringComparison.Ordinal, source);
		Assert.Contains("xs: C# infers no type for a list without one", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that Go says the slice's type rather than <c>:=</c>, and compiles.
	/// </summary>
	[TestMethod]
	public void Go_KeepsTheDeclaredTypeAndCompiles()
	{
		string source = new GoGenerator().Generate(InferredList());
		Assert.DoesNotContain("xs :=", source, StringComparison.Ordinal, source);
		Assert.Contains("var xs []int", source, StringComparison.Ordinal, source);

		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "go.mod"), GoModule);
			File.WriteAllText(Path.Combine(directory, "lists.go"), source);

			(int exitCode, string output) = ToolchainHarness.Run("go", "build ./...", directory);
			Assert.AreEqual(0, exitCode, $"Go rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a Go local with neither a type nor a typed initialiser says why, rather than
	/// writing a <c>:=</c> Go cannot infer.
	/// </summary>
	[TestMethod]
	public void Go_UntypedListLocal_SaysItIsInexpressible()
	{
		ConstructionExpression list = new();
		list.Arguments.Add(Literal.Number(1));

		FunctionDeclaration make = new("build") { ReturnType = "void" };
		make.Body.Add(new VariableDeclaration("xs", initialValue: list) { IsTypeInferred = true });

		SourceFile file = new("lists");
		file.Members.Add(make);

		string source = new GoGenerator().Generate(file);
		Assert.DoesNotContain("xs :=", source, StringComparison.Ordinal, source);
		Assert.Contains("xs: Go infers no type for a list without one", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that C++ says <c>std::vector</c> rather than <c>auto</c>, whose
	/// <c>std::initializer_list</c> has no <c>push_back</c>, and that the result runs.
	/// </summary>
	[TestMethod]
	public void Cpp_KeepsTheDeclaredTypeAndRuns()
	{
		string source = new CppGenerator().Generate(InferredList());
		Assert.DoesNotContain("auto xs", source, StringComparison.Ordinal, source);
		Assert.Contains("std::vector<int> xs", source, StringComparison.Ordinal, source);

		string? compiler = ToolchainHarness.FindOnPath("--version", "g++", "clang++");
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "lists.h"), source);
			File.WriteAllText(Path.Combine(directory, "driver.cpp"), CppDriver);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, "-std=c++20 -Wall -Wextra -pedantic driver.cpp -o driver", directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{source}");

			(int ran, string said) = ToolchainHarness.Run(Path.Combine(directory, "driver"), string.Empty, directory);
			Assert.AreEqual(0, ran, $"The list did not hold three elements after a push_back:{Environment.NewLine}{said}");
		});
	}

	/// <summary>
	/// Tests that Rust annotates the <c>Vec</c> rather than leaving the list to be inferred as an
	/// array.
	/// </summary>
	/// <remarks>
	/// This pins the declaration only. Rust still spells the typeless list itself as an array literal
	/// even under a <c>Vec</c> annotation, so the output does not compile until #409 is fixed; with
	/// the annotation the error is at least reported where the list is declared.
	/// </remarks>
	[TestMethod]
	public void Rust_KeepsTheDeclaredType()
	{
		string source = new RustGenerator().Generate(InferredList());
		Assert.Contains("let mut xs: Vec<i32> =", source, StringComparison.Ordinal, source);
	}
}
