// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles a <c>char</c> and a <c>decimal</c> field, parameter and return value in each target that
/// has no type of either name, with that target's real toolchain (#474).
/// </summary>
/// <remarks>
/// Go has no <c>char</c>, and none of Go, Rust, C and C++ has a <c>decimal</c>, so a name passed
/// through verbatim fails to compile there. Python and JavaScript already approximate a decimal with
/// their binary floating type, and these targets take the same approximation. Each test is
/// inconclusive rather than failing where its toolchain is not on the path, because then nothing was
/// checked.
/// </remarks>
[TestClass]
public class CharAndDecimalTypeCompileTests
{
	private static readonly string[] CCompilers = ["cc", "gcc", "clang"];

	private static readonly string[] CppCompilers = ["c++", "g++", "clang++"];

	/// <summary>
	/// The Go module the generated package is built in.
	/// </summary>
	private const string GoModule = """
		module exemplar

		go 1.21

		""";

	/// <summary>
	/// A type holding a <c>decimal</c> and a <c>char</c> field, and functions taking and returning each.
	/// </summary>
	/// <param name="name">The name of the source file.</param>
	/// <returns>The source file.</returns>
	private static SourceFile Exemplar(string name)
	{
		SourceFile file = new(name);

		ClassDeclaration account = new("Account") { Kind = TypeDeclarationKind.Struct };
		account.Members.Add(new FieldDeclaration("balance", new TypeReference("decimal")));
		account.Members.Add(new FieldDeclaration("grade", new TypeReference("char")));
		file.Members.Add(account);

		FunctionDeclaration initial = new("initial") { ReturnType = new TypeReference("char") };
		initial.Parameters.Add(new Parameter("amount", "decimal"));
		initial.Parameters.Add(new Parameter("fallback", "char"));
		initial.Body.Add(new ReturnStatement(new VariableReference("fallback")));
		file.Members.Add(initial);

		FunctionDeclaration same = new("same") { ReturnType = new TypeReference("decimal") };
		same.Parameters.Add(new Parameter("amount", "decimal"));
		same.Body.Add(new ReturnStatement(new VariableReference("amount")));
		file.Members.Add(same);

		return file;
	}

	/// <summary>
	/// Tests that Go spells <c>char</c> as <c>rune</c> and <c>decimal</c> as <c>float64</c>, and that the
	/// result builds.
	/// </summary>
	[TestMethod]
	public void Go_CharAndDecimal_Compile()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		string source = new GoGenerator().Generate(Exemplar("account"));
		StringAssert.Contains(source, "rune");
		StringAssert.Contains(source, "float64");

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "go.mod"), GoModule);
			File.WriteAllText(Path.Combine(directory, "account.go"), source);

			(int exitCode, string output) = ToolchainHarness.Run("go", "build ./...", directory);
			Assert.AreEqual(0, exitCode, $"Go rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that Rust spells <c>decimal</c> as <c>f64</c>, and that the result compiles.
	/// </summary>
	[TestMethod]
	public void Rust_CharAndDecimal_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		string source = new RustGenerator().Generate(Exemplar("account"));
		StringAssert.Contains(source, "f64");

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "account.rs"),
				$"#![allow(dead_code, unused_variables)]{Environment.NewLine}{source}");

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o account.rlib account.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that C++ spells <c>decimal</c> as <c>double</c>, and that a translation unit using the
	/// generated header compiles.
	/// </summary>
	[TestMethod]
	public void Cpp_CharAndDecimal_Compile()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CppCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		string source = new CppGenerator().Generate(Exemplar("account"));
		Assert.IsFalse(source.Contains("decimal", StringComparison.Ordinal), source);

		const string driver = """
			#include "account.h"

			int main()
			{
				Account account{};
				return (initial(same(account.balance), account.grade) == 'a') ? 1 : 0;
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "account.h"), source);
			File.WriteAllText(Path.Combine(directory, "driver.cpp"), driver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c++20 -Wall -Wextra -pedantic -c driver.cpp -o driver.o",
				directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated header:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that C spells <c>decimal</c> as <c>double</c>, and that a translation unit using the
	/// generated header compiles as strict C.
	/// </summary>
	[TestMethod]
	public void C_CharAndDecimal_Compile()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		string source = new CGenerator().Generate(Exemplar("account"));
		Assert.IsFalse(source.Contains("decimal", StringComparison.Ordinal), source);

		const string driver = """
			#include "account.h"

			int main(void)
			{
				Account account = { 0 };
				return (initial(same(account.balance), account.grade) == 'a') ? 1 : 0;
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "account.h"), source);
			File.WriteAllText(Path.Combine(directory, "driver.c"), driver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c11 -Wall -Wextra -pedantic -c driver.c -o driver.o",
				directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated header:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}
}
