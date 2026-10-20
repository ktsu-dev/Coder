// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that an entry point declared at file scope, in a namespace or in a type is written where
/// each language's toolchain looks for it, rather than where the document happened to put it (#128).
/// </summary>
[TestClass]
public class EntryPointPlacementTests
{
	/// <summary>
	/// The exit code the programs below return, which is what proves the body ran.
	/// </summary>
	private const int Expected = 3;

	/// <summary>
	/// The project the C# program is built as.
	/// </summary>
	private const string Project = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <OutputType>Exe</OutputType>
		    <TargetFramework>net10.0</TargetFramework>
		    <Nullable>disable</Nullable>
		    <ImplicitUsings>disable</ImplicitUsings>
		  </PropertyGroup>
		</Project>
		""";

	/// <summary>
	/// Tests that C# puts an entry point outside a type into a static class, since C# declares a method
	/// nowhere else.
	/// </summary>
	/// <param name="placement">Where the document declared the entry point.</param>
	[TestMethod]
	[DataRow("file")]
	[DataRow("namespace")]
	public void CSharp_WrapsAnEntryPointOutsideATypeInAStaticClass(string placement)
	{
		string code = new CSharpGenerator().Generate(Program(placement, withHelper: false));

		int holder = code.IndexOf("static class", StringComparison.Ordinal);
		int main = code.IndexOf("static int Main", StringComparison.Ordinal);
		Assert.IsTrue(holder >= 0 && holder < main, code);
	}

	/// <summary>
	/// Tests that every language other than C# writes an entry point declared in a type at file scope,
	/// after the type, rather than inside it or not at all.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="signature">How the entry point starts, at the start of a line.</param>
	[TestMethod]
	[DataRow("C", "int main(")]
	[DataRow("C++", "int main(")]
	[DataRow("Rust", "fn main(")]
	[DataRow("Go", "func main(")]
	[DataRow("Python", "def main(")]
	[DataRow("JavaScript", "function main(")]
	public void AnEntryPointInATypeIsWrittenAtFileScope(string language, string signature)
	{
		string code = GeneratorFor(language).Generate(Program("type", withHelper: false));

		int type = code.IndexOf("Program", StringComparison.Ordinal);
		int main = code.IndexOf($"\n{signature}", StringComparison.Ordinal);
		Assert.IsTrue(type >= 0 && main > type, code);
	}

	/// <summary>
	/// Tests that C++ and Rust write an entry point declared in a namespace at file scope, where it is
	/// found, and keep the names declared beside it in reach of its body.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="signature">How the entry point starts, at the start of a line.</param>
	/// <param name="import">What brings the namespace's names into the body.</param>
	[TestMethod]
	[DataRow("C++", "int main(", "using namespace App;")]
	[DataRow("Rust", "fn run(", "use self::App::*;")]
	public void AnEntryPointInANamespaceIsWrittenAtFileScope(string language, string signature, string import)
	{
		string code = GeneratorFor(language).Generate(Program("namespace", withHelper: true));

		Assert.IsTrue(code.Contains($"\n{signature}", StringComparison.Ordinal), code);
		StringAssert.Contains(code, import, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a program whose entry point is declared in a namespace, beside the function it calls,
	/// compiles, links and runs in every language with a compiler.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	[TestMethod]
	[DataRow("C")]
	[DataRow("C++")]
	[DataRow("Rust")]
	[DataRow("Go")]
	[DataRow("C#")]
	public void AnEntryPointInANamespace_CompilesLinksAndRuns(string language) =>
		CompileAndRun(language, Program("namespace", withHelper: true));

	/// <summary>
	/// Tests that a program whose entry point is declared in a type compiles, links and runs in every
	/// language with a compiler.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	[TestMethod]
	[DataRow("C")]
	[DataRow("C++")]
	[DataRow("Rust")]
	[DataRow("Go")]
	[DataRow("C#")]
	public void AnEntryPointInAType_CompilesLinksAndRuns(string language) =>
		CompileAndRun(language, Program("type", withHelper: false));

	/// <summary>
	/// Builds a program that exits with <see cref="Expected"/>.
	/// </summary>
	/// <param name="placement">Where the entry point is declared: <c>file</c>, <c>namespace</c> or <c>type</c>.</param>
	/// <param name="withHelper">Whether the value comes from a function declared beside the entry point.</param>
	/// <returns>The file.</returns>
	private static SourceFile Program(string placement, bool withHelper)
	{
		EntryPoint main = new() { ReturnsExitCode = true };
		Expression value = withHelper ? new CallExpression("helper") : new LiteralExpression<int>(Expected);
		main.Body.Add(new ReturnStatement(value));

		List<AstNode> members = [];
		if (withHelper)
		{
			FunctionDeclaration helper = new("helper") { ReturnType = "int", Visibility = Visibility.Public, IsStatic = true };
			helper.Body.Add(new ReturnStatement(new LiteralExpression<int>(Expected)));
			members.Add(helper);
		}

		members.Add(main);

		SourceFile file = new("program");
		switch (placement)
		{
			case "namespace":
			{
				NamespaceDeclaration container = new("App");
				members.ForEach(container.Members.Add);
				file.Members.Add(container);
				break;
			}

			case "type":
			{
				ClassDeclaration container = new("Program");
				members.ForEach(container.Members.Add);
				file.Members.Add(container);
				break;
			}

			default:
				members.ForEach(file.Members.Add);
				break;
		}

		return file;
	}

	private static void CompileAndRun(string language, SourceFile file)
	{
		(string[] commands, string versionArgument, string fileName, string build, string run, string runArguments) = ToolchainFor(language);
		string? compiler = ToolchainHarness.FindOnPath(versionArgument, commands);
		if (compiler is null)
		{
			Assert.Inconclusive($"No {language} toolchain on the path, so nothing was compiled.");
			return;
		}

		string code = GeneratorFor(language).Generate(file);
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, fileName), code);
			if (language == "Go")
			{
				File.WriteAllText(Path.Combine(directory, "go.mod"), "module program\n\ngo 1.21\n");
			}
			else if (language == "C#")
			{
				File.WriteAllText(Path.Combine(directory, "program.csproj"), Project);
			}

			(int exitCode, string output) = ToolchainHarness.Run(compiler, build, directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");

			string program = run.Length == 0 ? Path.Combine(directory, "program") : run;
			(int programExitCode, string programOutput) = ToolchainHarness.Run(program, runArguments, directory);
			Assert.AreEqual(Expected, programExitCode, $"The entry point did not run:{Environment.NewLine}{code}{Environment.NewLine}{programOutput}");
		});
	}

	private static (string[] Commands, string VersionArgument, string FileName, string Build, string Run, string RunArguments) ToolchainFor(string language) => language switch
	{
		"C" => (["cc", "gcc", "clang"], "--version", "program.c", "-std=c11 -Wall -Wextra -pedantic -Werror program.c -o program", string.Empty, string.Empty),
		"C++" => (["c++", "g++", "clang++"], "--version", "program.cpp", "-std=c++20 -Wall -Wextra -pedantic -Werror program.cpp -o program", string.Empty, string.Empty),
		"Rust" => (["rustc"], "--version", "program.rs", "--edition 2021 -o program program.rs", string.Empty, string.Empty),
		"Go" => (["go"], "version", "program.go", "build -o program .", string.Empty, string.Empty),
		"C#" => (["dotnet"], "--version", "Program.cs", "build -nologo -o out", "dotnet", Path.Combine("out", "program.dll")),
		_ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
	};

	private static ILanguageGenerator GeneratorFor(string language) => language switch
	{
		"C" => new CGenerator(),
		"C++" => new CppGenerator(),
		"Rust" => new RustGenerator(),
		"Go" => new GoGenerator(),
		"Python" => new PythonGenerator(),
		"JavaScript" => new JavaScriptGenerator(),
		"C#" => new CSharpGenerator(),
		_ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
	};
}
