// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;

[TestClass]
public sealed partial class CommentWritingTests
{
	[TestMethod]
	public void CallerTextInCommentsCannotBecomeSource()
	{
		SourceFile file = new("injection");
		file.HeaderComment.Add("header\nINJECTED = 1");
		ClassDeclaration declaration = new("Example");
		declaration.Documentation.Add("documentation\r\nINJECTED = 1");
		file.Members.Add(declaration);

		ILanguageGenerator[] generators =
		[
			new CSharpGenerator(),
			new CppGenerator(),
			new CGenerator(),
			new GoGenerator(),
			new JavaScriptGenerator(),
			new PythonGenerator(),
			new RustGenerator(),
		];

		foreach (ILanguageGenerator generator in generators)
		{
			string[] lines = generator.Generate(file).Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

			Assert.IsFalse(
				lines.Any(line => line.TrimStart().StartsWith("INJECTED = 1", StringComparison.Ordinal)),
				$"{generator.DisplayName} allowed comment text to become source.");
		}
	}

	[TestMethod]
	public void LanguageGeneratorsDoNotBuildCommentPrefixesAroundCallerText()
	{
		string directory = FindRepositoryRoot();
		foreach (string path in Directory.GetFiles(Path.Combine(directory, "Coder", "Languages"), "*.cs"))
		{
			string source = File.ReadAllText(path);
			Assert.IsFalse(UnsafePrefix().IsMatch(source), $"{Path.GetFileName(path)} builds a comment prefix by hand.");
		}
	}

	[GeneratedRegex(@"\{(?:CommentPrefix|DocumentationPrefix)\}\s+\{", RegexOptions.CultureInvariant)]
	private static partial Regex UnsafePrefix();

	private static string FindRepositoryRoot()
	{
		for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "Coder", "Languages")))
			{
				return directory.FullName;
			}
		}

		throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
	}
}
