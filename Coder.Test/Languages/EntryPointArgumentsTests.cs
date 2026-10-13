// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a C or C++ entry point that accepts arguments gives its body <c>args</c>, holding the
/// arguments the user typed, which is the binding every other target makes (#86).
/// </summary>
[TestClass]
public class EntryPointArgumentsTests
{
	/// <summary>
	/// Tests that C binds <c>args</c> past the program's path, with a count beside it.
	/// </summary>
	[TestMethod]
	public void C_BindsArgsPastTheProgramPath()
	{
		string code = new CGenerator().Generate(new EntryPoint { AcceptsArguments = true });

		StringAssert.Contains(code, "char** args = argv + 1;", StringComparison.Ordinal);
		StringAssert.Contains(code, "int args_count = argc - 1;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that C++ binds <c>args</c> as a vector of strings past the program's path, and includes
	/// what that needs.
	/// </summary>
	[TestMethod]
	public void Cpp_BindsArgsPastTheProgramPath()
	{
		SourceFile file = new("program");
		file.Members.Add(new EntryPoint { AcceptsArguments = true });

		string code = new CppGenerator().Generate(file);

		StringAssert.Contains(code, "std::vector<std::string> args(argv + 1, argv + argc);", StringComparison.Ordinal);
		StringAssert.Contains(code, "#include <vector>", StringComparison.Ordinal);
		StringAssert.Contains(code, "#include <string>", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an entry point that takes no arguments binds nothing and includes nothing for it.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	[TestMethod]
	[DataRow("C")]
	[DataRow("C++")]
	public void AnEntryPointWithoutArgumentsBindsNothing(string language)
	{
		SourceFile file = new("program");
		file.Members.Add(new EntryPoint());

		string code = GeneratorFor(language).Generate(file);

		Assert.DoesNotContain("args", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("#include", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a body reading <c>args[0]</c> compiles, links and runs in C and C++, and that it reads
	/// the first argument the user typed: the program exits with that argument's first digit.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	[TestMethod]
	[DataRow("C")]
	[DataRow("C++")]
	public void ABodyReadingTheFirstArgument_CompilesAndRuns(string language)
	{
		(string[] commands, string fileName, string arguments) = ToolchainFor(language);

		string? compiler = ToolchainHarness.FindOnPath("--version", commands);
		if (compiler is null)
		{
			Assert.Inconclusive($"No {language} compiler on the path, so nothing was compiled.");
			return;
		}

		EntryPoint entryPoint = new() { AcceptsArguments = true, ReturnsExitCode = true };
		entryPoint.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("args[0][0]"), BinaryOperator.Subtract, new VariableReference("'0'"))));

		SourceFile file = new("program");
		file.Members.Add(entryPoint);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string code = GeneratorFor(language).Generate(file);
			File.WriteAllText(Path.Combine(directory, fileName), code);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, arguments, directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");

			(int programExitCode, string programOutput) = ToolchainHarness.Run(Path.Combine(directory, "program"), "7 3", directory);
			Assert.AreEqual(7, programExitCode, $"args[0] was not the first argument:{Environment.NewLine}{code}{Environment.NewLine}{programOutput}");
		});
	}

	private static (string[] Commands, string FileName, string Arguments) ToolchainFor(string language) => language == "C"
		? (["cc", "gcc", "clang"], "program.c", "-std=c11 -Wall -Wextra -pedantic -Werror program.c -o program")
		: (["c++", "g++", "clang++"], "program.cpp", "-std=c++20 -Wall -Wextra -pedantic -Werror program.cpp -o program");

	private static ILanguageGenerator GeneratorFor(string language) => language switch
	{
		"C" => new CGenerator(),
		"C++" => new CppGenerator(),
		_ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
	};
}
