// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the helper Go and Rust write an exit-code entry point's body into does not take a name
/// the user's own code declares (#299).
/// </summary>
/// <remarks>
/// Both targets put the body in a <c>run</c> and have <c>main</c> exit with what it answers. A file
/// that already declares <c>run</c> would then declare it twice.
/// </remarks>
[TestClass]
public class ExitCodeHelperNameTests
{
	/// <summary>
	/// Tests that the helper keeps the name <c>run</c> when nothing else has it.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="helper">How the helper's declaration should start.</param>
	[TestMethod]
	[DataRow("Go", "func run() int")]
	[DataRow("Rust", "fn run() -> i32")]
	public void TheHelperIsRunWhenNothingElseIs(string language, string helper)
	{
		SourceFile file = new("program");
		EntryPoint entryPoint = new() { ReturnsExitCode = true };
		entryPoint.Body.Add(new ReturnStatement(new LiteralExpression<int>(3)));
		file.Members.Add(entryPoint);

		string code = GeneratorFor(language).Generate(file);

		StringAssert.Contains(code, helper, StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the helper takes the fallback name, numbered past any the user also declares.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="fallback">The fallback stem.</param>
	/// <param name="helper">How the helper's declaration should start.</param>
	/// <param name="exit">How <c>main</c> should hand on its answer.</param>
	[TestMethod]
	[DataRow("Go", "runMain", "func runMain2() int", "os.Exit(runMain2())")]
	[DataRow("Rust", "run_main", "fn run_main2() -> i32", "std::process::exit(run_main2());")]
	public void TheHelperSkipsEveryNameTheUserDeclares(string language, string fallback, string helper, string exit)
	{
		SourceFile file = FileWithUserRun();
		file.Members.Insert(1, ReturnsThree(fallback));

		string code = GeneratorFor(language).Generate(file);

		StringAssert.Contains(code, helper, StringComparison.Ordinal);
		StringAssert.Contains(code, exit, StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a program with its own <c>run</c> and an exit-code entry point that calls it builds
	/// in Go and Rust and exits with what <c>run</c> answers.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	[TestMethod]
	[DataRow("Go")]
	[DataRow("Rust")]
	public void AUserRunBesideAnExitCodeEntryPoint_BuildsAndRuns(string language)
	{
		(string[] commands, string askVersion, string fileName, string arguments) = ToolchainFor(language);

		string? compiler = ToolchainHarness.FindOnPath(askVersion, commands);
		if (compiler is null)
		{
			Assert.Inconclusive($"No {language} toolchain on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			if (language == "Go")
			{
				File.WriteAllText(Path.Combine(directory, "go.mod"), "module program\n\ngo 1.21\n");
			}

			string code = GeneratorFor(language).Generate(FileWithUserRun());
			File.WriteAllText(Path.Combine(directory, fileName), code);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, arguments, directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");

			(int programExitCode, string programOutput) = ToolchainHarness.Run(Path.Combine(directory, "program"), string.Empty, directory);
			Assert.AreEqual(3, programExitCode, $"The program did not exit with run's answer:{Environment.NewLine}{code}{Environment.NewLine}{programOutput}");
		});
	}

	/// <summary>
	/// Builds the issue's program: a user <c>run</c> answering 3, and an exit-code entry point
	/// returning what it answers.
	/// </summary>
	private static SourceFile FileWithUserRun()
	{
		SourceFile file = new("program");
		file.Members.Add(ReturnsThree("run"));

		EntryPoint entryPoint = new() { ReturnsExitCode = true };
		entryPoint.Body.Add(new ReturnStatement(new CallExpression("run")));
		file.Members.Add(entryPoint);
		return file;
	}

	private static FunctionDeclaration ReturnsThree(string name)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference("int") };
		function.Body.Add(new ReturnStatement(new LiteralExpression<int>(3)));
		return function;
	}

	private static (string[] Commands, string AskVersion, string FileName, string Arguments) ToolchainFor(string language) => language == "Go"
		? (["go"], "version", "program.go", "build -o program .")
		: (["rustc"], "--version", "program.rs", "program.rs -o program");

	private static ILanguageGenerator GeneratorFor(string language) => language switch
	{
		"Go" => new GoGenerator(),
		"Rust" => new RustGenerator(),
		_ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
	};
}
