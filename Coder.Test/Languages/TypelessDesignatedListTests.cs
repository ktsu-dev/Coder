// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a typeless list naming its members, which initialises a declaration that has said its
/// type, compiles in C# and Rust (#473).
/// </summary>
[TestClass]
public class TypelessDesignatedListTests
{
	private const string CSharpProject = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net10.0</TargetFramework>
		    <Nullable>disable</Nullable>
		    <ImplicitUsings>disable</ImplicitUsings>
		  </PropertyGroup>
		</Project>
		""";

	/// <summary>
	/// Builds a struct <c>Point { x, y }</c>, a struct <c>Frame</c> whose <c>origin</c> field starts
	/// at a typeless list naming Point's members, and a function <c>sum</c> whose local is
	/// initialised the same way (#473).
	/// </summary>
	/// <returns>The source file.</returns>
	private static SourceFile DesignatedLists()
	{
		static ConstructionExpression Designated(int x, int y)
		{
			ConstructionExpression list = new();
			list.Arguments.Add(new MemberInitialiser("x", Literal.Number(x)));
			list.Arguments.Add(new MemberInitialiser("y", Literal.Number(y)));
			return list;
		}

		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct, Visibility = Visibility.Public };
		point.Members.Add(new FieldDeclaration("x", "int") { Visibility = Visibility.Public });
		point.Members.Add(new FieldDeclaration("y", "int") { Visibility = Visibility.Public });

		ClassDeclaration frame = new("Frame") { Kind = TypeDeclarationKind.Struct, Visibility = Visibility.Public };
		frame.Members.Add(new FieldDeclaration("origin", "Point") { Visibility = Visibility.Public, InitialValue = Designated(3, 4) });
		frame.Members.Add(new FunctionDeclaration("Frame") { Kind = FunctionKind.Constructor, Visibility = Visibility.Public });

		FunctionDeclaration sum = new("sum") { ReturnType = "int", Visibility = Visibility.Public, IsStatic = true };
		sum.Body.Add(new VariableDeclaration("p", "Point", Designated(1, 2)));
		sum.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("p.x"), BinaryOperator.Add, new VariableReference("p.y"))));
		frame.Members.Add(sum);

		SourceFile file = new("designated");
		file.Members.Add(point);
		file.Members.Add(frame);
		return file;
	}

	/// <summary>
	/// Tests that a typeless list naming its members, initialising a local and a field, compiles as
	/// a target-typed <c>new() { … }</c> rather than bare braces, which C# takes only for an array
	/// (CS0622), and that the values arrive (#473).
	/// </summary>
	[TestMethod]
	public void TypelessDesignatedLists_Compile()
	{
		string source = new CSharpGenerator().Generate(DesignatedLists());
		Assert.Contains("Point p = new() { x = 1, y = 2 };", source, StringComparison.Ordinal, source);
		Assert.Contains("Point origin = new() { x = 3, y = 4 };", source, StringComparison.Ordinal, source);

		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		const string DesignatedDriver = """
			public static class DesignatedDriver
			{
				public static int Use() => Frame.sum() + new Frame().origin.x + new Frame().origin.y;
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), CSharpProject);
			File.WriteAllText(Path.Combine(directory, "DesignatedDriver.cs"), DesignatedDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a typeless list naming its members, initialising a local and a field, is written
	/// as a struct literal of the receiving type rather than <c>[x: 1, y: 2]</c>, which is not Rust,
	/// and that the values arrive when run (#473).
	/// </summary>
	[TestMethod]
	public void TypelessDesignatedLists_CompileAndRun()
	{
		string source = new RustGenerator().Generate(DesignatedLists());
		Assert.Contains("Point { x: 1, y: 2 }", source, StringComparison.Ordinal, source);
		Assert.Contains("Point { x: 3, y: 4 }", source, StringComparison.Ordinal, source);

		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		const string Driver = """
			fn main() {
			    let f = Frame::new();
			    if Frame::sum() + f.origin.x + f.origin.y != 10 {
			        std::process::exit(1);
			    }
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "designated.rs"),
				$"#![allow(unused_parens, dead_code, unused_mut)]{Environment.NewLine}{source}{Environment.NewLine}{Driver}");

			(int exitCode, string output) = ToolchainHarness.Run("rustc", "--edition 2021 -o designated designated.rs", directory);
			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "designated"), string.Empty, directory);
			Assert.AreEqual(0, ran, $"The program computed the wrong total:{Environment.NewLine}{printed}");
		});
	}
}
