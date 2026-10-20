// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that an operator declared outside any type compiles in C# (#261).
/// </summary>
/// <remarks>
/// C# declares an operator only inside a type one of its operands is, so a free operator over a
/// type declared beside it is moved into that type, and one with no such type is written down
/// rather than emitted as source C# refuses. Whether the moved operator compiles is not visible in
/// the text, so each case is compiled.
/// </remarks>
[TestClass]
public class FreeOperatorTests
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
	/// Adds two points the way a caller would.
	/// </summary>
	private const string Driver = """
		public static class Driver
		{
			public static Point Use()
			{
				Point a = new Point();
				Point b = new Point();
				Point c = a + b;
				return c;
			}
		}
		""";

	/// <summary>
	/// Adds two points the way a caller outside the namespace would.
	/// </summary>
	private const string NamespacedDriver = """
		using Geometry;

		public static class Driver
		{
			public static Point Use()
			{
				Point a = new Point();
				Point b = new Point();
				Point c = a + b;
				return c;
			}
		}
		""";

	/// <summary>
	/// Tests that a file holding a struct and a free <c>operator +</c> over it compiles, and that
	/// <c>a + b</c> works.
	/// </summary>
	[TestMethod]
	public void CSharp_FreeOperatorInAFile_Compiles()
	{
		SourceFile file = new("Generated");
		file.Members.Add(PointStruct());
		file.Members.Add(PlusOver("Point"));

		Compile(file, Driver);
	}

	/// <summary>
	/// Tests that a namespace holding a struct and a free <c>operator +</c> over it compiles.
	/// </summary>
	[TestMethod]
	public void CSharp_FreeOperatorInANamespace_Compiles()
	{
		NamespaceDeclaration geometry = new("Geometry");
		geometry.Members.Add(PointStruct());
		geometry.Members.Add(PlusOver("Point"));

		SourceFile file = new("Generated");
		file.Members.Add(geometry);

		Compile(file, NamespacedDriver);
	}

	/// <summary>
	/// Tests that the moved operator keeps both of its declared operands rather than gaining the
	/// implicit one a member operator takes.
	/// </summary>
	[TestMethod]
	public void CSharp_MovedOperatorKeepsItsOperands()
	{
		SourceFile file = new("Generated");
		file.Members.Add(PointStruct());
		file.Members.Add(PlusOver("Point"));

		string generated = new CSharpGenerator().Generate(file);

		StringAssert.Contains(generated, "operator +(Point lhs, Point rhs)", StringComparison.Ordinal);
		Assert.IsFalse(generated.Contains("self", StringComparison.Ordinal), generated);
	}

	/// <summary>
	/// Tests that a free operator over no type declared beside it is written down rather than
	/// emitted as an operator outside a type.
	/// </summary>
	[TestMethod]
	public void CSharp_FreeOperatorWithNoTypeToJoin_IsWrittenDown()
	{
		SourceFile file = new("Generated");
		file.Members.Add(PlusOver("int"));

		string generated = new CSharpGenerator().Generate(file);

		StringAssert.Contains(generated, "declared outside a type", StringComparison.Ordinal);
		Assert.IsFalse(generated.Contains("operator +(", StringComparison.Ordinal), generated);
	}

	/// <summary>
	/// Builds a struct with one field.
	/// </summary>
	/// <returns>The struct.</returns>
	private static ClassDeclaration PointStruct()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int"));
		return point;
	}

	/// <summary>
	/// Builds a free <c>operator +</c> taking two operands of a type and returning its left one.
	/// </summary>
	/// <param name="type">The type of both operands and of the result.</param>
	/// <returns>The operator.</returns>
	private static FunctionDeclaration PlusOver(string type)
	{
		FunctionDeclaration plus = new("+") { Kind = FunctionKind.Operator, ReturnType = type };
		plus.Parameters.Add(new Parameter("lhs", type));
		plus.Parameters.Add(new Parameter("rhs", type));
		plus.Body.Add(new ReturnStatement(new VariableReference("lhs")));
		return plus;
	}

	private static void Compile(SourceFile file, string driver)
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		string source = new CSharpGenerator().Generate(file);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), driver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}
}
