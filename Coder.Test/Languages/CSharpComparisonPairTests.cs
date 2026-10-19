// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a type declaring one side of a C# comparison pair gets the other side written for it.
/// </summary>
/// <remarks>
/// C# requires <c>==</c> with <c>!=</c>, <c>&lt;</c> with <c>&gt;</c> and <c>&lt;=</c> with
/// <c>&gt;=</c> (CS0216). C++20 and Go accept one side alone, so one side is a natural AST to write.
/// </remarks>
[TestClass]
public class CSharpComparisonPairTests
{
	/// <summary>
	/// The project the generated file is compiled as, with warnings left as warnings.
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
	/// Tests that the missing partner is written with a body that answers from the declared side.
	/// </summary>
	/// <param name="declared">The operator the type declares.</param>
	/// <param name="partner">The signature of the partner C# needs.</param>
	/// <param name="body">What the partner returns.</param>
	[TestMethod]
	[DataRow("==", "public static bool operator !=(Point self, Point rhs)", "return (!(self == rhs));")]
	[DataRow("!=", "public static bool operator ==(Point self, Point rhs)", "return (!(self != rhs));")]
	[DataRow("<", "public static bool operator >(Point self, Point rhs)", "return (rhs < self);")]
	[DataRow(">", "public static bool operator <(Point self, Point rhs)", "return (rhs > self);")]
	[DataRow("<=", "public static bool operator >=(Point self, Point rhs)", "return (rhs <= self);")]
	[DataRow(">=", "public static bool operator <=(Point self, Point rhs)", "return (rhs >= self);")]
	public void OneSideGetsItsPartner(string declared, string partner, string body)
	{
		ClassDeclaration point = PointWith(Comparison(declared, "Point"));

		string code = new CSharpGenerator().Generate(point);

		StringAssert.Contains(code, partner, StringComparison.Ordinal);
		StringAssert.Contains(code, body, StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a type declaring both sides gets neither written twice.
	/// </summary>
	[TestMethod]
	public void BothSidesAreLeftAsDeclared()
	{
		ClassDeclaration point = PointWith(Comparison("==", "Point"), Comparison("!=", "Point"));

		string code = new CSharpGenerator().Generate(point);

		Assert.AreEqual(1, code.Split("operator ==(").Length - 1, code);
		Assert.AreEqual(1, code.Split("operator !=(").Length - 1, code);
	}

	/// <summary>
	/// Tests that an ordering against another type is not given a partner that swaps its operands,
	/// since the swapped call names an operator nobody declared.
	/// </summary>
	[TestMethod]
	public void AnOrderingAgainstAnotherTypeIsNotSwapped()
	{
		ClassDeclaration point = PointWith(Comparison("<", "int"));

		string code = new CSharpGenerator().Generate(point);

		Assert.DoesNotContain("operator >(", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a type declaring only <c>==</c>, or only <c>&lt;</c>, compiles and can be compared
	/// both ways.
	/// </summary>
	/// <param name="declared">The operator the type declares.</param>
	/// <param name="use">An expression using both sides of the pair.</param>
	[TestMethod]
	[DataRow("==", "(a == b) || (a != b)")]
	[DataRow("<", "(a < b) || (a > b)")]
	public void OneSideCompiles(string declared, string use)
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = new("Generated");
		file.Members.Add(PointWith(Comparison(declared, "Point")));
		string source = new CSharpGenerator().Generate(file);
		string driver = $$"""
			public static class Driver
			{
				public static bool Use(Point a, Point b) => {{use}};
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), driver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	private static ClassDeclaration PointWith(params FunctionDeclaration[] operators)
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int"));
		foreach (FunctionDeclaration op in operators)
		{
			point.Members.Add(op);
		}

		return point;
	}

	private static FunctionDeclaration Comparison(string symbol, string operandType)
	{
		FunctionDeclaration comparison = new(symbol) { Kind = FunctionKind.Operator, ReturnType = "bool" };
		comparison.Parameters.Add(new Parameter("rhs", operandType));
		comparison.Body.Add(new ReturnStatement(new LiteralExpression<bool>(true)));
		return comparison;
	}
}
