// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a remainder of floating-point operands is written as a call in the targets whose
/// <c>%</c> takes only integers, and that the file includes or imports what the call needs.
/// </summary>
/// <remarks>
/// C, C++ and Go reject <c>a % b</c> and <c>x %= y</c> on a <c>double</c> or a <c>float</c>. Python
/// accepts them but floors, so <c>-7.5 % 2.0</c> is <c>0.5</c> rather than <c>-1.5</c>. C#, Rust and
/// JavaScript accept them and truncate.
/// </remarks>
[TestClass]
public class FloatingRemainderTests
{
	/// <summary>
	/// Tests that each of the three targets spells a floating remainder and a floating <c>%=</c> as
	/// its library call.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="doubleRemainder">What <c>a % b</c> on doubles should be written as.</param>
	/// <param name="floatRemainder">What <c>a % b</c> on floats should be written as.</param>
	/// <param name="compoundRemainder">What <c>x %= b</c> on a double local should be written as.</param>
	[TestMethod]
	[DataRow("C", "return fmod(a, b);", "return fmodf(a, b);", "x = fmod(x, b);")]
	[DataRow("C++", "return std::fmod(a, b);", "return std::fmod(a, b);", "x = std::fmod(x, b);")]
	[DataRow("Go", "return math.Mod(a, b)", "return float32(math.Mod(float64(a), float64(b)))", "x = math.Mod(x, b)")]
	[DataRow("Python", "return math.fmod(a, b)", "return math.fmod(a, b)", "x = math.fmod(x, b)")]
	public void AFloatingRemainderIsACall(string language, string doubleRemainder, string floatRemainder, string compoundRemainder)
	{
		string code = GeneratorFor(language).Generate(FileOfRemainders());

		StringAssert.Contains(code, doubleRemainder, StringComparison.Ordinal);
		StringAssert.Contains(code, floatRemainder, StringComparison.Ordinal);
		StringAssert.Contains(code, compoundRemainder, StringComparison.Ordinal);
		Assert.DoesNotContain("%=", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that an integer remainder and an integer <c>%=</c> are still <c>%</c>.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="remainder">What <c>a % b</c> on ints should be written as.</param>
	/// <param name="compoundRemainder">What <c>x %= b</c> on an int local should be written as.</param>
	[TestMethod]
	[DataRow("C", "return (a % b);", "x %= b;")]
	[DataRow("C++", "return (a % b);", "x %= b;")]
	[DataRow("Go", "return (a % b)", "x %= b")]
	public void AnIntegerRemainderIsUnchanged(string language, string remainder, string compoundRemainder)
	{
		string code = GeneratorFor(language).Generate(FileOfIntegerRemainders());

		StringAssert.Contains(code, remainder, StringComparison.Ordinal);
		StringAssert.Contains(code, compoundRemainder, StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a file taking a floating remainder includes or imports the call's declaration once,
	/// and that one taking only integer remainders does not.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="import">The line the file should carry.</param>
	[TestMethod]
	[DataRow("C", "#include <math.h>")]
	[DataRow("C++", "#include <cmath>")]
	[DataRow("Go", "\"math\"")]
	[DataRow("Python", "import math")]
	public void OnlyAFloatingRemainderImportsItsCall(string language, string import)
	{
		string floating = GeneratorFor(language).Generate(FileOfRemainders());
		string integer = GeneratorFor(language).Generate(FileOfIntegerRemainders());

		Assert.AreEqual(1, floating.Split(import).Length - 1, floating);
		Assert.DoesNotContain(import, integer, StringComparison.Ordinal, integer);
	}

	/// <summary>
	/// Tests that C, C++ and Go output for floating and integer remainders compiles without warnings.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	[TestMethod]
	[DataRow("C")]
	[DataRow("C++")]
	[DataRow("Go")]
	public void RemaindersCompile(string language)
	{
		(string[] commands, string askVersion, string fileName, string arguments) = ToolchainFor(language);

		string? compiler = ToolchainHarness.FindOnPath(askVersion, commands);
		if (compiler is null)
		{
			Assert.Inconclusive($"No {language} toolchain on the path, so nothing was compiled.");
			return;
		}

		SourceFile[] files = [FileOfRemainders(), FileOfIntegerRemainders()];
		foreach (SourceFile file in files)
		{
			ToolchainHarness.InTemporaryDirectory(directory =>
			{
				if (language == "Go")
				{
					File.WriteAllText(Path.Combine(directory, "go.mod"), "module remainders\n\ngo 1.21\n");
				}

				string code = GeneratorFor(language).Generate(file);
				File.WriteAllText(Path.Combine(directory, fileName), code);

				(int exitCode, string output) = ToolchainHarness.Run(compiler, arguments, directory);

				Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");
			});
		}
	}

	/// <summary>
	/// Tests that Python's floating remainder, and its floating <c>%=</c>, take the dividend's sign
	/// the way the C family's <c>fmod</c> does.
	/// </summary>
	[TestMethod]
	public void Python_FloatingRemainderMatchesTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		string source = new PythonGenerator().Generate(FileOfRemainders())
			+ Environment.NewLine
			+ "assert wrap(-7.5, 2.0) == -1.5, wrap(-7.5, 2.0)" + Environment.NewLine
			+ "assert wrap(7.5, -2.0) == 1.5, wrap(7.5, -2.0)" + Environment.NewLine
			+ "assert wrapf(-7.5, 2.0) == -1.5, wrapf(-7.5, 2.0)" + Environment.NewLine
			+ "assert accumulate(-7.5, 2.0) == -1.5, accumulate(-7.5, 2.0)" + Environment.NewLine;

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "check.py"), source);
			(int exitCode, string output) = ToolchainHarness.Run(python, "check.py", directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
	}

	private static (string[] Commands, string AskVersion, string FileName, string Arguments) ToolchainFor(string language) => language switch
	{
		"C" => (["cc", "gcc", "clang"], "--version", "remainders.c", "-std=c11 -Wall -Wextra -pedantic -Werror -c remainders.c -o remainders.o"),
		"C++" => (["c++", "g++", "clang++"], "--version", "remainders.cpp", "-std=c++20 -Wall -Wextra -pedantic -Werror -c remainders.cpp -o remainders.o"),
		_ => (["go"], "version", "remainders.go", "vet ./..."),
	};

	private static ILanguageGenerator GeneratorFor(string language) => language switch
	{
		"C++" => new CppGenerator(),
		"C" => new CGenerator(),
		"Go" => new GoGenerator(),
		"Python" => new PythonGenerator(),
		_ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
	};

	/// <summary>
	/// Builds a file whose functions take the remainder of doubles, of floats, and of a double local
	/// through <c>%=</c>.
	/// </summary>
	private static SourceFile FileOfRemainders()
	{
		SourceFile file = new("remainders");
		file.Members.Add(Remainder("wrap", "double"));
		file.Members.Add(Remainder("wrapf", "float"));
		file.Members.Add(CompoundRemainder("accumulate", "double"));
		return file;
	}

	/// <summary>
	/// Builds the same functions over ints.
	/// </summary>
	private static SourceFile FileOfIntegerRemainders()
	{
		SourceFile file = new("remainders");
		file.Members.Add(Remainder("wrap", "int"));
		file.Members.Add(CompoundRemainder("accumulate", "int"));
		return file;
	}

	private static FunctionDeclaration Remainder(string name, string type)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference(type) };
		function.Parameters.Add(new Parameter("a", type));
		function.Parameters.Add(new Parameter("b", type));
		function.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("a"), BinaryOperator.Modulo, new VariableReference("b"))));
		return function;
	}

	private static FunctionDeclaration CompoundRemainder(string name, string type)
	{
		FunctionDeclaration function = new(name) { ReturnType = new TypeReference(type) };
		function.Parameters.Add(new Parameter("a", type));
		function.Parameters.Add(new Parameter("b", type));
		function.Body.Add(new VariableDeclaration("x", type, new VariableReference("a")));
		function.Body.Add(new AssignmentStatement(new VariableReference("x"), new VariableReference("b"), AssignmentOperator.ModuloAssign));
		function.Body.Add(new ReturnStatement(new VariableReference("x")));
		return function;
	}
}
