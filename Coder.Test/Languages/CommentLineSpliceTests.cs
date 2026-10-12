// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System;
using System.IO;
using System.Linq;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a comment line ending in a backslash does not splice the next line of code into the
/// comment in C and C++.
/// </summary>
/// <remarks>
/// The preprocessor joins a line ending in <c>\</c> to the one after it before it strips comments,
/// so <c>/// Reads files under C:\data\</c> would swallow the declaration written beneath it.
/// </remarks>
[TestClass]
public class CommentLineSpliceTests
{
	/// <summary>
	/// The documentation lines that would splice: a bare backslash, one followed by whitespace, which
	/// gcc still splices, and the trigraph that is a backslash wherever trigraphs are on. The plain
	/// backslash comes last, so the line it would splice is the function's signature rather than
	/// another comment.
	/// </summary>
	private static readonly string[] SplicingLines =
	[
		"Ends in a trigraph ??/",
		"Ends in a backslash and a space \\ ",
		@"Reads files under C:\data\",
	];

	/// <summary>
	/// Tests that no comment line C or C++ writes ends in something that splices.
	/// </summary>
	/// <param name="language">The generator's language identifier.</param>
	[TestMethod]
	[DataRow("c")]
	[DataRow("cpp")]
	public void CFamilyCommentLinesDoNotEndInASplice(string language)
	{
		ILanguageGenerator generator = Generators().Single(g => g.LanguageId == language);

		string code = generator.Generate(Exemplar());

		foreach (string line in code.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
		{
			string trimmed = line.TrimEnd();
			Assert.IsFalse(
				trimmed.EndsWith('\\') || trimmed.EndsWith("??/", StringComparison.Ordinal),
				$"{language} wrote a line that splices the next one: {line}{Environment.NewLine}{code}");
		}

		Assert.Contains(@"/// Reads files under C:\data\.", code, StringComparison.Ordinal, $"{language} wrote {code}");
	}

	/// <summary>
	/// Tests that the languages without line splices write the comment as it was given.
	/// </summary>
	/// <param name="language">The generator's language identifier.</param>
	[TestMethod]
	[DataRow("csharp")]
	[DataRow("rust")]
	[DataRow("go")]
	public void OtherLanguagesWriteTheCommentAsGiven(string language)
	{
		ILanguageGenerator generator = Generators().Single(g => g.LanguageId == language);

		string code = generator.Generate(Exemplar());

		Assert.Contains(@"Reads files under C:\data\" + Environment.NewLine, code.ReplaceLineEndings(), StringComparison.Ordinal, $"{language} wrote {code}");
	}

	/// <summary>
	/// Tests that the generated C keeps the function declared after the comment.
	/// </summary>
	[TestMethod]
	public void GeneratedCCompiles() =>
		AssertCompiles(new CGenerator(), "describe.h", "driver.c", ["cc", "gcc", "clang"], "-std=c11 -Wall -Wextra -pedantic -Werror=trigraphs driver.c -o driver");

	/// <summary>
	/// Tests that the generated C++ keeps the function declared after the comment.
	/// </summary>
	[TestMethod]
	public void GeneratedCppCompiles() =>
		AssertCompiles(new CppGenerator(), "describe.hpp", "driver.cpp", ["c++", "g++", "clang++"], "-std=c++20 -Wall -Wextra -pedantic driver.cpp -o driver");

	private static void AssertCompiles(ILanguageGenerator generator, string header, string driver, string[] compilers, string arguments)
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", compilers);
		if (compiler is null)
		{
			Assert.Inconclusive($"No {generator.DisplayName} compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, header), generator.Generate(Exemplar()));
			File.WriteAllText(Path.Combine(directory, driver), $$"""
				#include "{{header}}"

				int main(void)
				{
					return describe() == 7 ? 0 : 1;
				}

				""");

			(int exitCode, string output) = ToolchainHarness.Run(compiler, arguments, directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{output}");

			(int runExit, string runOutput) = ToolchainHarness.Run(Path.Combine(directory, "driver"), string.Empty, directory);
			Assert.AreEqual(0, runExit, $"describe() did not return 7.{Environment.NewLine}{runOutput}");
		});
	}

	/// <summary>
	/// Builds a header holding one function whose documentation lines each end in a splice.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile Exemplar()
	{
		SourceFile file = new("describe") { IsHeader = true };

		FunctionDeclaration describe = new("describe") { ReturnType = "int" };
		foreach (string line in SplicingLines)
		{
			describe.Documentation.Add(line);
		}

		describe.Body.Add(new ReturnStatement(new LiteralExpression<int>(7)));
		file.Members.Add(describe);
		return file;
	}

	private static ILanguageGenerator[] Generators() =>
	[
		new CSharpGenerator(),
		new CGenerator(),
		new CppGenerator(),
		new GoGenerator(),
		new RustGenerator(),
	];
}
