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

	/// <summary>
	/// The line separators C# or JavaScript end a line at besides CR and LF.
	/// </summary>
	private static readonly char[] UnicodeLineBreaks = ['\u0085', '\u2028', '\u2029'];

	/// <summary>
	/// The project the generated C# is compiled as: a library, with warnings left as warnings.
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
	/// Tests that U+0085, U+2028 and U+2029 inside documentation start a new comment line in every
	/// language, rather than leaving the text after them on a line the compiler reads as code.
	/// </summary>
	[TestMethod]
	public void UnicodeLineBreaksInCommentsStartANewCommentLine()
	{
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

		foreach (char separator in UnicodeLineBreaks)
		{
			foreach (ILanguageGenerator generator in generators)
			{
				string source = generator.Generate(Widget(separator));
				string[] lines = source.Split(["\r\n", "\n", "\r", .. UnicodeLineBreaks.Select(c => c.ToString())], StringSplitOptions.None);

				Assert.IsFalse(
					lines.Any(line => line.TrimStart().StartsWith("Copied from", StringComparison.Ordinal)),
					$"{generator.DisplayName} left the text after U+{(int)separator:X4} outside the comment:{Environment.NewLine}{source}");
				Assert.IsFalse(source.Contains(separator, StringComparison.Ordinal), $"{generator.DisplayName} wrote U+{(int)separator:X4} into the output.");
			}
		}
	}

	/// <summary>
	/// Tests that the generated JavaScript parses when a documentation line holds U+2028 or U+2029.
	/// </summary>
	[TestMethod]
	public void JavaScript_UnicodeLineBreaksInDocumentationParse()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was checked.");
			return;
		}

		foreach (char separator in UnicodeLineBreaks)
		{
			string source = new JavaScriptGenerator().Generate(Widget(separator));
			ToolchainHarness.InTemporaryDirectory(directory =>
			{
				File.WriteAllText(Path.Combine(directory, "widget.js"), source);
				(int exitCode, string output) = ToolchainHarness.Run(node, "--check widget.js", directory);
				Assert.AreEqual(0, exitCode, $"JavaScript rejected the source with U+{(int)separator:X4}:{Environment.NewLine}{output}{Environment.NewLine}{source}");
			});
		}
	}

	/// <summary>
	/// Tests that the generated C# compiles when a documentation line holds U+0085, U+2028 or U+2029.
	/// </summary>
	[TestMethod]
	public void CSharp_UnicodeLineBreaksInDocumentationCompile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Widgets.csproj"), Project);
			foreach (char separator in UnicodeLineBreaks)
			{
				SourceFile file = Widget(separator);
				NamespaceDeclaration scope = new($"Widgets{(int)separator:X4}");
				foreach (AstNode member in file.Members)
				{
					scope.Members.Add(member);
				}

				file.Members.Clear();
				file.Members.Add(scope);
				File.WriteAllText(Path.Combine(directory, $"Widget{(int)separator:X4}.cs"), new CSharpGenerator().Generate(file));
			}

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Builds a class <c>Widget</c> whose public method <c>Size</c> is documented with a line that
	/// holds <paramref name="separator"/>.
	/// </summary>
	/// <param name="separator">The line separator to put inside the documentation.</param>
	/// <returns>The file to generate.</returns>
	private static SourceFile Widget(char separator)
	{
		FunctionDeclaration size = new("Size") { ReturnType = "int", Visibility = Visibility.Public };
		size.Documentation.Add($"Returns the size.{separator}Copied from the spec, see section 3.");
		size.Body.Add(new ReturnStatement(new LiteralExpression<int>(3)));
		ClassDeclaration widget = new("Widget") { Visibility = Visibility.Public };
		widget.Members.Add(size);
		SourceFile file = new("widget");
		file.Members.Add(widget);
		return file;
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
