// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles what <see cref="CSharpGenerator"/> writes, with <c>dotnet build</c>.
/// </summary>
/// <remarks>
/// Where C# can declare a member is a rule the text does not show: a field or function written
/// straight into a namespace reads like any other and fails with CS0116. A test that pinned the
/// spelling passed on exactly that output, which is why it was not noticed.
/// <para>
/// A driver in the same project uses every declaration, so a declaration that compiles on its own
/// but cannot be reached from outside fails here too. The test is inconclusive rather than failing
/// where no <c>dotnet</c> is on the path, which is the honest result: nothing was checked.
/// </para>
/// </remarks>
[TestClass]
public class CSharpGeneratedSourceCompilesTests
{
	/// <summary>
	/// The project the generated file is compiled as: a library, with warnings left as warnings,
	/// because what is under test is whether C# accepts the file rather than what it thinks of it.
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
	/// The consumer of the generated declarations, written the way a person would write one.
	/// </summary>
	private const string Driver = """
		public static class Driver
		{
			public static int Use()
			{
				global::Geometry.Point point = new global::Geometry.Point();
				global::Geometry.Geometry.count = point is null ? 0 : 1;
				return global::Geometry.Geometry.twice(global::Geometry.Geometry.Origins)
					+ global::Geometry.Geometry.count
					+ Globals.half(4)
					+ (new Tool() is null ? 0 : 1);
			}
		}
		""";

	/// <summary>
	/// Tests that a namespace and a file holding fields, functions and a variable beside their types
	/// compile, and that a second file can reach all of them.
	/// </summary>
	[TestMethod]
	public void ScopeMembers_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = new("Generated");
		file.Members.Add(CSharpScopeMemberTests.Geometry());

		FunctionDeclaration half = new("half") { ReturnType = "int" };
		half.Parameters.Add(new Parameter("n", "int"));
		half.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("n"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(half);
		file.Members.Add(new ClassDeclaration("Tool"));

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), Driver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), new CSharpGenerator().Generate(file));

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}");
		});
	}
}
