// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Runs an enumeration whose member values name other members in each target that renames or scopes
/// its members, and checks the values come out as the reference meant (#258).
/// </summary>
/// <remarks>
/// <c>ReadWrite = Read | Write</c> is a value the AST passes through as text, and C, Go, Rust and
/// JavaScript each declare <c>Read</c> under another name or not as a value at all, so the text has to
/// follow the rename. <c>Next</c> has no value of its own, so it checks the member after a reference
/// still counts on from what the reference came to. Each test is inconclusive where its toolchain is
/// not on the path.
/// </remarks>
[TestClass]
public class EnumSiblingReferenceTests
{
	/// <summary>
	/// Tests that C names the members a value refers to by their prefixed names.
	/// </summary>
	[TestMethod]
	public void C_ValuesNamingMembers_CompileAndRun()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "cc", "gcc", "clang");
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		const string driver = """
			#include <stdio.h>
			#include "perm.h"

			int main(void)
			{
				printf("%d %d\n", (int)Perm_ReadWrite, (int)Perm_Next);
				return 0;
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CGenerator().Generate(PermFile("perm"));
			File.WriteAllText(Path.Combine(directory, "perm.h"), source);
			File.WriteAllText(Path.Combine(directory, "driver.c"), driver);

			string program = OperatingSystem.IsWindows() ? "perm.exe" : "perm";
			(int built, string output) = ToolchainHarness.Run(compiler, $"-std=c11 -Wall -Wextra -pedantic driver.c -o {program}", directory);
			Assert.AreEqual(0, built, $"{compiler} rejected the generated header:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("3 4", printed.Trim(), source);
		});
	}

	/// <summary>
	/// Tests that Go names the members a value refers to by their prefixed constants.
	/// </summary>
	[TestMethod]
	public void Go_ValuesNamingMembers_CompileAndRun()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		const string module = """
			module perm

			go 1.21

			""";

		const string driver = """
			package main

			import "fmt"

			func main() {
				fmt.Println(int(PermReadWrite), int(PermNext))
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new GoGenerator().Generate(PermFile("main"));
			File.WriteAllText(Path.Combine(directory, "go.mod"), module);
			File.WriteAllText(Path.Combine(directory, "driver.go"), driver);
			File.WriteAllText(Path.Combine(directory, "perm.go"), source);

			(int exitCode, string output) = ToolchainHarness.Run("go", "run .", directory);
			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
			Assert.AreEqual("3 4", output.Trim(), source);
		});
	}

	/// <summary>
	/// Tests that Rust names the variants a discriminant refers to through the enumeration, cast to
	/// the discriminant's type, both with a fixed representation and without one.
	/// </summary>
	/// <param name="underlying">The enumeration's underlying type, or null for none.</param>
	/// <param name="cast">The type Rust reads the discriminant as.</param>
	[TestMethod]
	[DataRow("int", "i32")]
	[DataRow(null, "isize")]
	public void Rust_ValuesNamingMembers_CompileAndRun(string? underlying, string cast)
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		string driver = $$"""

			fn main() {
				println!("{} {}", Perm::ReadWrite as {{cast}}, Perm::Next as {{cast}});
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new RustGenerator().Generate(PermFile("perm", underlying));
			string allowed = "#![allow(dead_code)]";
			File.WriteAllText(Path.Combine(directory, "perm.rs"), $"{allowed}{Environment.NewLine}{source}{driver}");

			string program = OperatingSystem.IsWindows() ? "perm.exe" : "perm";
			(int built, string output) = ToolchainHarness.Run("rustc", $"--edition 2021 -o {program} perm.rs", directory);
			Assert.AreEqual(0, built, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("3 4", printed.Trim(), source);
		});
	}

	/// <summary>
	/// Tests that JavaScript, whose object literal cannot read its own properties while it is built,
	/// is handed the values the members refer to.
	/// </summary>
	[TestMethod]
	public void JavaScript_ValuesNamingMembers_LoadAndRun()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No Node.js runtime on the path, so nothing was run.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new JavaScriptGenerator().Generate(PermFile("perm"));
			string path = Path.Combine(directory, "perm.mjs");
			File.WriteAllText(path, $"{source}console.log(Perm.ReadWrite, Perm.Next);\n");

			(int exitCode, string output) = ToolchainHarness.Run(node, path, directory);
			Assert.AreEqual(0, exitCode, $"Node.js could not load the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
			Assert.AreEqual("3 4", output.Trim(), source);
		});
	}

	/// <summary>
	/// Tests that a reference already qualified by whoever wrote it, or a word that only contains a
	/// member's name, is left as it is.
	/// </summary>
	[TestMethod]
	public void QualifiedReferencesAndLongerWords_AreLeftAlone()
	{
		EnumDeclaration perm = new("Perm");
		perm.Members.Add(new EnumMember("Read") { Value = "1" });
		perm.Members.Add(new EnumMember("Write") { Value = "Other.Read | ReadMask | Read" });

		string code = new CGenerator().Generate(perm);

		Assert.Contains("Perm_Write = Other.Read | ReadMask | Perm_Read,", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Builds the issue's file: <c>Perm</c> with <c>Read = 1</c>, <c>Write = 2</c>,
	/// <c>ReadWrite = Read | Write</c> and a <c>Next</c> that follows it.
	/// </summary>
	/// <param name="name">The file's name, which Go takes as its package.</param>
	/// <param name="underlying">The enumeration's underlying type, or null for none.</param>
	/// <returns>The file to generate.</returns>
	private static SourceFile PermFile(string name, string? underlying = null)
	{
		EnumDeclaration perm = new("Perm");
		if (underlying is not null)
		{
			perm.UnderlyingType = underlying;
		}

		perm.Members.Add(new EnumMember("Read") { Value = "1" });
		perm.Members.Add(new EnumMember("Write") { Value = "2" });
		perm.Members.Add(new EnumMember("ReadWrite") { Value = "Read | Write" });
		perm.Members.Add(new EnumMember("Next"));

		SourceFile file = new(name);
		file.Members.Add(perm);
		return file;
	}
}
