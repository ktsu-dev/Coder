// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that <c>virtual</c> and <c>abstract</c> are written only where C++ and C# accept them (#467).
/// </summary>
/// <remarks>
/// C++ refuses <c>virtual</c> on a static member, a constructor and a function outside a class, and
/// C# refuses it on a constructor and a finalizer (CS0106). A virtual destructor is the ordinary C++
/// base-class shape, so an AST written C++-first carries one, and C++ keeps it.
/// </remarks>
[TestClass]
public class VirtualModifierPlacementTests
{
	/// <summary>
	/// The C++ compilers to look for, in the order a C++ project would.
	/// </summary>
	private static readonly string[] CppCompilers = ["c++", "g++", "clang++"];

	/// <summary>
	/// The project the generated C# is built in.
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
	/// The consumer of the generated C#, which calls everything that is still declared.
	/// </summary>
	private const string CSharpDriver = """
		public static class Driver
		{
			public static int Use() => new Shape().area() + Shape.make() + Globals.helper();
		}
		""";

	/// <summary>
	/// The consumer of the generated C++ header, which calls everything that is still defined.
	/// </summary>
	private const string CppDriver = """
		#include "shape.h"

		int main()
		{
			Shape s;
			return (s.area() + Shape::make() + helper()) * 0;
		}

		""";

	/// <summary>
	/// Builds the file the issue reproduces with: a class holding a virtual and an abstract static
	/// function, a virtual constructor, a virtual destructor and an ordinary virtual method, beside a
	/// virtual free function.
	/// </summary>
	/// <returns>The file.</returns>
	private static SourceFile ShapeFile()
	{
		ClassDeclaration shape = new("Shape");
		shape.Members.Add(new FunctionDeclaration("Shape") { Kind = FunctionKind.Constructor, IsVirtual = true });
		shape.Members.Add(new FunctionDeclaration("Shape") { Kind = FunctionKind.Destructor, IsVirtual = true });
		shape.Members.Add(new FunctionDeclaration("make")
		{
			ReturnType = "int",
			IsStatic = true,
			IsVirtual = true,
			Body = { new ReturnStatement(new LiteralExpression<int>(1)) },
		});
		shape.Members.Add(new FunctionDeclaration("build") { ReturnType = "int", IsStatic = true, IsAbstract = true });
		shape.Members.Add(new FunctionDeclaration("area")
		{
			ReturnType = "int",
			IsVirtual = true,
			Body = { new ReturnStatement(new LiteralExpression<int>(2)) },
		});

		SourceFile file = new("shape");
		file.Members.Add(shape);
		file.Members.Add(new FunctionDeclaration("helper")
		{
			ReturnType = "int",
			IsVirtual = true,
			Body = { new ReturnStatement(new LiteralExpression<int>(3)) },
		});
		return file;
	}

	/// <summary>
	/// Tests that C++ writes <c>virtual</c> on the destructor and the instance method only, and says
	/// what it dropped from the rest.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesVirtualOnlyOnNonStaticMembersThatAreNotConstructors()
	{
		string header = new CppGenerator().Generate(ShapeFile());

		Assert.Contains("virtual ~Shape()", header, StringComparison.Ordinal, header);
		Assert.Contains("virtual int area()", header, StringComparison.Ordinal, header);
		Assert.DoesNotContain("static virtual", header, StringComparison.Ordinal, header);
		Assert.DoesNotContain("virtual Shape()", header, StringComparison.Ordinal, header);
		Assert.DoesNotContain("virtual int helper()", header, StringComparison.Ordinal, header);
		Assert.Contains("static int build();", header, StringComparison.Ordinal, header);
		Assert.DoesNotContain("= 0", header, StringComparison.Ordinal, header);
		Assert.Contains("// virtual: make is static, which C++ cannot declare virtual", header, StringComparison.Ordinal, header);
		Assert.Contains("// abstract: build is static, which C++ cannot declare virtual", header, StringComparison.Ordinal, header);
		Assert.Contains("// virtual: Shape is a constructor, which C++ cannot declare virtual", header, StringComparison.Ordinal, header);
		Assert.Contains("// virtual: helper is not a member of a class, which C++ cannot declare virtual", header, StringComparison.Ordinal, header);
	}

	/// <summary>
	/// Tests that C# writes <c>virtual</c> on neither the constructor nor the finalizer, and says so.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesVirtualOnNeitherConstructorNorFinalizer()
	{
		string source = new CSharpGenerator().Generate(ShapeFile());

		Assert.DoesNotContain("virtual Shape(", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("virtual ~Shape(", source, StringComparison.Ordinal, source);
		Assert.Contains("public virtual int area()", source, StringComparison.Ordinal, source);
		Assert.Contains("// virtual: Shape is a constructor, which C# never overrides", source, StringComparison.Ordinal, source);
		Assert.Contains("// virtual: ~Shape is a finalizer", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that an abstract constructor or destructor is written down rather than declared in C#,
	/// since without the modifier it has no body.
	/// </summary>
	[TestMethod]
	public void CSharp_AbstractConstructorAndDestructor_AreWrittenDown()
	{
		ClassDeclaration shape = new("Shape");
		shape.Members.Add(new FunctionDeclaration("Shape") { Kind = FunctionKind.Constructor, IsAbstract = true });
		shape.Members.Add(new FunctionDeclaration("Shape") { Kind = FunctionKind.Destructor, IsAbstract = true });

		string source = new CSharpGenerator().Generate(shape);

		Assert.DoesNotContain("Shape();", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("abstract", source.Replace("an abstract", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal, source);
		Assert.Contains("// Shape is an abstract constructor", source, StringComparison.Ordinal, source);
		Assert.Contains("// ~Shape is an abstract destructor", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that the C++ the issue's file generates compiles.
	/// </summary>
	[TestMethod]
	public void Cpp_Compiles()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CppCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		string header = new CppGenerator().Generate(ShapeFile());

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "shape.h"), header);
			File.WriteAllText(Path.Combine(directory, "driver.cpp"), CppDriver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c++20 -Wall -Wextra -pedantic -Werror -c driver.cpp -o driver.o",
				directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{header}");
		});
	}

	/// <summary>
	/// Tests that the C# the issue's file generates compiles.
	/// </summary>
	[TestMethod]
	public void CSharp_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		string source = new CSharpGenerator().Generate(ShapeFile());

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), CSharpDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}
}
