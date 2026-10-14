// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that an alias or a class declared as a member of a class comes out where the target accepts
/// one, in C# and JavaScript.
/// </summary>
/// <remarks>
/// Neither language takes either declaration in a class body as written at namespace scope: C# has a
/// using alias only at file or namespace scope (CS1513, CS1529, CS1022), and a JavaScript class body
/// holds only methods and fields, so <c>const Id = …</c> and <c>class Inner { }</c> are both syntax
/// errors there.
/// </remarks>
[TestClass]
public class TypeMemberDeclarationTests
{
	/// <summary>
	/// A class whose field is typed by an alias the class declares.
	/// </summary>
	private static ClassDeclaration Holder()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new UsingAlias("Id", "int"));
		holder.Members.Add(new FieldDeclaration("id", "Id") { Visibility = Visibility.Public });
		return holder;
	}

	/// <summary>
	/// A class holding another, which holds an alias of its own.
	/// </summary>
	private static ClassDeclaration Outer()
	{
		ClassDeclaration inner = new("Inner");
		inner.Members.Add(new UsingAlias("Count", "long"));
		inner.Members.Add(new FieldDeclaration("count", "Count") { Visibility = Visibility.Public });

		ClassDeclaration outer = new("Outer");
		outer.Members.Add(inner);
		outer.Members.Add(new FieldDeclaration("size", "int") { Visibility = Visibility.Public });
		return outer;
	}

	/// <summary>
	/// The project the generated C# is compiled as.
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
	/// Tests that an alias a C# type declares is written ahead of the type rather than inside it.
	/// </summary>
	[TestMethod]
	public void CSharp_MemberAlias_IsWrittenAheadOfItsType()
	{
		string code = new CSharpGenerator().Generate(Holder());

		int alias = code.IndexOf("using Id = int;", StringComparison.Ordinal);
		int type = code.IndexOf("class Holder", StringComparison.Ordinal);

		Assert.IsGreaterThanOrEqualTo(0, alias, code);
		Assert.IsLessThan(type, alias, code);
		Assert.AreEqual(1, CountOf(code, "using Id"), code);
	}

	/// <summary>
	/// Tests that every alias a namespace holds, its own and those of the types in it, is written
	/// ahead of everything else in it, which is the only place C# takes one.
	/// </summary>
	[TestMethod]
	public void CSharp_NamespaceAliases_PrecedeItsTypes()
	{
		NamespaceDeclaration shapes = new("Shapes");
		shapes.Members.Add(Outer());
		shapes.Members.Add(new UsingAlias("Name", "string"));

		string code = new CSharpGenerator().Generate(shapes);

		int type = code.IndexOf("class Outer", StringComparison.Ordinal);
		Assert.IsLessThan(type, code.IndexOf("using Count = long;", StringComparison.Ordinal), code);
		Assert.IsLessThan(type, code.IndexOf("using Name = string;", StringComparison.Ordinal), code);
		Assert.AreEqual(1, CountOf(code, "using Count"), code);
	}

	/// <summary>
	/// Tests that C# accepts a file holding a class-member alias, a nested class with its own alias,
	/// and a namespace alias declared after a type.
	/// </summary>
	[TestMethod]
	public void CSharp_MemberAliasesAndNestedClasses_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		NamespaceDeclaration shapes = new("Shapes");
		shapes.Members.Add(Holder());
		shapes.Members.Add(Outer());
		shapes.Members.Add(new UsingAlias("Name", "string"));

		SourceFile file = new("Generated");
		file.Members.Add(shapes);

		const string driver = """
			public static class Driver
			{
				public static long Use() =>
					new global::Shapes.Holder().id + new global::Shapes.Outer.Inner().count + new global::Shapes.Outer().size;
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), driver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), new CSharpGenerator().Generate(file));

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that JavaScript writes a member alias and a nested class as static members, the shape a
	/// nested enumeration already takes.
	/// </summary>
	[TestMethod]
	public void JavaScript_MemberAliasAndNestedClass_AreStaticMembers()
	{
		JavaScriptGenerator generator = new();

		StringAssert.Contains(generator.Generate(Holder()), "static Id = int;", StringComparison.Ordinal);
		StringAssert.Contains(generator.Generate(Outer()), "static Inner = class Inner {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that Node.js parses a class holding an alias and one holding a nested class.
	/// </summary>
	[TestMethod]
	public void JavaScript_MemberAliasAndNestedClass_Parse()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No Node.js runtime on the path, so nothing was parsed.");
			return;
		}

		SourceFile file = new("members");
		file.Members.Add(Holder());
		file.Members.Add(Outer());
		string generated = new JavaScriptGenerator().Generate(file);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string path = Path.Combine(directory, "members.mjs");
			File.WriteAllText(path, generated);
			(int exitCode, string output) = ToolchainHarness.Run(node, $"--check {path}", directory);
			Assert.AreEqual(0, exitCode, $"Node.js rejected the generated source:{Environment.NewLine}{generated}{Environment.NewLine}{output}");
		});
	}

	private static int CountOf(string text, string value)
	{
		int count = 0;
		for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}
}
