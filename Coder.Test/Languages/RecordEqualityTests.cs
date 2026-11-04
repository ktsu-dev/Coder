// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a record declaring its own equality operator compiles in C# and Rust (#301).
/// </summary>
/// <remarks>
/// Both targets give a record equality of their own: C# synthesises <c>==</c> and <c>!=</c>, and
/// Rust's record derives <c>PartialEq</c>. A declared operator then collides with the one the
/// language already wrote, which the text does not show, so each case is compiled.
/// </remarks>
[TestClass]
public class RecordEqualityTests
{
	/// <summary>
	/// The project the generated file is compiled as.
	/// </summary>
	private const string Project = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net10.0</TargetFramework>
		    <Nullable>disable</Nullable>
		    <ImplicitUsings>disable</ImplicitUsings>
		  </PropertyGroup>
		</Project>
		""";

	/// <summary>
	/// Uses both comparisons on the record, the way a caller would.
	/// </summary>
	private const string Driver = """
		public static class Driver
		{
			public static bool Use()
			{
				Point a = new Point();
				Point b = new Point();
				return a == b && !(a != b);
			}
		}
		""";

	/// <summary>
	/// Uses both comparisons on the record, the way a caller would.
	/// </summary>
	private const string RustDriver = """

		pub fn use_point(a: &Point, b: &Point) -> bool {
			a == b && !(a != b)
		}
		""";

	/// <summary>
	/// Tests that a C# record declaring <c>==</c> compiles.
	/// </summary>
	[TestMethod]
	public void CSharp_RecordDeclaringEquality_Compiles() => CompileCSharp(PointDeclaring("=="));

	/// <summary>
	/// Tests that a C# record declaring only <c>!=</c> compiles.
	/// </summary>
	[TestMethod]
	public void CSharp_RecordDeclaringOnlyInequality_Compiles() => CompileCSharp(PointDeclaring("!="));

	/// <summary>
	/// Tests that the C# record says what happened to the declared operator rather than dropping it
	/// in silence.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesTheDeclaredOperatorDown()
	{
		string generated = new CSharpGenerator().Generate(PointDeclaring("=="));

		StringAssert.Contains(generated, "synthesised by the record", StringComparison.Ordinal);
		Assert.IsFalse(generated.Contains("operator ==(", StringComparison.Ordinal), generated);
		Assert.IsFalse(generated.Contains("operator !=(", StringComparison.Ordinal), generated);
	}

	/// <summary>
	/// Tests that a C# record still declares an equality operator against another type, which the
	/// record does not synthesise, along with its partner.
	/// </summary>
	[TestMethod]
	public void CSharp_KeepsEqualityAgainstAnotherType()
	{
		ClassDeclaration point = PointDeclaring("==");
		((FunctionDeclaration)point.Members[^1]).Parameters[0].Type = "int";

		string generated = new CSharpGenerator().Generate(point);

		StringAssert.Contains(generated, "operator ==(Point", StringComparison.Ordinal);
		StringAssert.Contains(generated, "operator !=(Point", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a Rust record declaring <c>==</c> compiles, and that its own <c>PartialEq</c> is
	/// the only one.
	/// </summary>
	[TestMethod]
	public void Rust_RecordDeclaringEquality_Compiles()
	{
		string generated = CompileRust(PointDeclaring("=="));

		StringAssert.Contains(generated, "#[derive(Clone, Debug)]", StringComparison.Ordinal);
		StringAssert.Contains(generated, "impl PartialEq for Point", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a Rust record declaring only <c>!=</c> compiles and keeps its derived equality,
	/// which is what supplies <c>!=</c> in Rust.
	/// </summary>
	[TestMethod]
	public void Rust_RecordDeclaringOnlyInequality_Compiles()
	{
		string generated = CompileRust(PointDeclaring("!="));

		StringAssert.Contains(generated, "#[derive(Clone, Debug, PartialEq)]", StringComparison.Ordinal);
	}

	/// <summary>
	/// Builds a record with one field and one comparison operator against itself.
	/// </summary>
	/// <param name="op">The operator it declares.</param>
	/// <returns>The record.</returns>
	private static ClassDeclaration PointDeclaring(string op)
	{
		ClassDeclaration point = new("Point") { IsRecord = true };
		point.Members.Add(new VariableDeclaration("x", "int"));

		FunctionDeclaration comparison = new(op) { Kind = FunctionKind.Operator, ReturnType = "bool" };
		comparison.Parameters.Add(new Parameter("rhs", "Point"));
		comparison.Body.Add(new ReturnStatement(new LiteralExpression<bool>(op == "==")));
		point.Members.Add(comparison);

		return point;
	}

	private static void CompileCSharp(ClassDeclaration point)
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = new("Generated");
		file.Members.Add(point);
		string source = new CSharpGenerator().Generate(file);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), Driver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	private static string CompileRust(ClassDeclaration point)
	{
		SourceFile file = new("point");
		file.Members.Add(point);
		string source = new RustGenerator().Generate(file);

		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return source;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "point.rs"),
				$"#![allow(unused_parens, dead_code, unused_variables)]{Environment.NewLine}{source}{RustDriver}");

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o point.rlib point.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});

		return source;
	}
}
