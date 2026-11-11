// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a C++ string literal holding a NUL keeps every character once it is a <c>std::string</c>.
/// </summary>
/// <remarks>
/// A bare <c>"a\000b"</c> reaches <c>std::string</c> through its <c>const char*</c> constructor, which
/// stops at the first NUL, so the generated program held <c>a</c> where every other target held three
/// characters (issue #482). The generated code is compiled and run, because the length is what a
/// consumer gets and the text alone cannot show it.
/// </remarks>
[TestClass]
public class CppNulStringLiteralTests
{
	private const string Driver = """
		#include <cstdio>
		#include "text.h"

		int main()
		{
			std::string a = text();
			std::string b = plain();
			std::printf("%zu %d %d %d %zu\n", a.size(), a[0], a[1], a[2], b.size());
			return 0;
		}

		""";

	private static readonly string[] CppCompilers = ["c++", "g++", "clang++"];

	/// <summary>
	/// Tests that a literal with a NUL is written with its byte count, and one without stays a plain
	/// literal.
	/// </summary>
	[TestMethod]
	public void Cpp_ALiteralWithANul_SaysItsLength()
	{
		string code = new CppGenerator().Generate(File());

		StringAssert.Contains(code, @"std::string(""a\000b"", 3)", StringComparison.Ordinal);
		StringAssert.Contains(code, @"return ""ab"";", StringComparison.Ordinal);
		StringAssert.Contains(code, "#include <string>", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the C++ output compiles and its <c>std::string</c> keeps the NUL and the character
	/// after it.
	/// </summary>
	[TestMethod]
	public void Cpp_ALiteralWithANul_KeepsItsLengthAndContents()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CppCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CppGenerator().Generate(File());
			System.IO.File.WriteAllText(Path.Combine(directory, "text.h"), source);
			System.IO.File.WriteAllText(Path.Combine(directory, "driver.cpp"), Driver);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, "-std=c++20 -Wall -Wextra -pedantic -Werror driver.cpp -o text", directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "text"), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("3 97 0 98 2", printed.Trim(), source);
		});
	}

	/// <summary>
	/// <c>string text() { return "a\0b"; }</c>, the issue's reproduction, beside a literal with no NUL.
	/// </summary>
	private static SourceFile File()
	{
		FunctionDeclaration text = new("text") { ReturnType = new TypeReference("string") };
		text.Body.Add(new ReturnStatement(Literal.Text("a\0b")));

		FunctionDeclaration plain = new("plain") { ReturnType = new TypeReference("string") };
		plain.Body.Add(new ReturnStatement(Literal.Text("ab")));

		SourceFile file = new("text");
		file.Members.Add(text);
		file.Members.Add(plain);
		return file;
	}
}
