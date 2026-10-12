// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a double literal that is NaN or an infinity is written as a name the target language
/// knows, and that a file holding one imports what that name needs.
/// </summary>
/// <remarks>
/// Round-trip formatting gives <c>NaN</c>, <c>Infinity</c> and <c>-Infinity</c>, which is JavaScript
/// and nothing else: every other target read them as undefined names, and C# as the tokens
/// <c>NaNd</c> and <c>Infinityd</c>.
/// </remarks>
[TestClass]
public class NonFiniteDoubleLiteralTests
{
	/// <summary>
	/// Tests that each generator spells NaN, positive infinity and negative infinity in its own
	/// language.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="expected">What the generator should write.</param>
	[TestMethod]
	[DataRow("C#", double.NaN, "double.NaN")]
	[DataRow("C#", double.PositiveInfinity, "double.PositiveInfinity")]
	[DataRow("C#", double.NegativeInfinity, "double.NegativeInfinity")]
	[DataRow("C++", double.NaN, "std::numeric_limits<double>::quiet_NaN()")]
	[DataRow("C++", double.PositiveInfinity, "std::numeric_limits<double>::infinity()")]
	[DataRow("C++", double.NegativeInfinity, "-std::numeric_limits<double>::infinity()")]
	[DataRow("C", double.NaN, "NAN")]
	[DataRow("C", double.PositiveInfinity, "INFINITY")]
	[DataRow("C", double.NegativeInfinity, "-INFINITY")]
	[DataRow("Go", double.NaN, "math.NaN()")]
	[DataRow("Go", double.PositiveInfinity, "math.Inf(1)")]
	[DataRow("Go", double.NegativeInfinity, "math.Inf(-1)")]
	[DataRow("Rust", double.NaN, "f64::NAN")]
	[DataRow("Rust", double.PositiveInfinity, "f64::INFINITY")]
	[DataRow("Rust", double.NegativeInfinity, "f64::NEG_INFINITY")]
	[DataRow("Python", double.NaN, "float(\"nan\")")]
	[DataRow("Python", double.PositiveInfinity, "float(\"inf\")")]
	[DataRow("Python", double.NegativeInfinity, "float(\"-inf\")")]
	[DataRow("JavaScript", double.NaN, "NaN")]
	[DataRow("JavaScript", double.PositiveInfinity, "Infinity")]
	[DataRow("JavaScript", double.NegativeInfinity, "-Infinity")]
	public void ANonFiniteDoubleIsSpelledInTheTargetLanguage(string language, double value, string expected) =>
		Assert.AreEqual(expected, GeneratorFor(language).Generate(new LiteralExpression<double>(value)).Trim());

	/// <summary>
	/// Tests that each generator spells a NaN or infinite float in its own language, at single
	/// precision where the language has a name for it.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="expected">What the generator should write.</param>
	[TestMethod]
	[DataRow("C#", float.NaN, "float.NaN")]
	[DataRow("C#", float.PositiveInfinity, "float.PositiveInfinity")]
	[DataRow("C#", float.NegativeInfinity, "float.NegativeInfinity")]
	[DataRow("C++", float.NaN, "std::numeric_limits<float>::quiet_NaN()")]
	[DataRow("C++", float.PositiveInfinity, "std::numeric_limits<float>::infinity()")]
	[DataRow("C++", float.NegativeInfinity, "-std::numeric_limits<float>::infinity()")]
	[DataRow("C", float.NaN, "NAN")]
	[DataRow("C", float.PositiveInfinity, "INFINITY")]
	[DataRow("C", float.NegativeInfinity, "-INFINITY")]
	[DataRow("Go", float.NaN, "float32(math.NaN())")]
	[DataRow("Go", float.PositiveInfinity, "float32(math.Inf(1))")]
	[DataRow("Go", float.NegativeInfinity, "float32(math.Inf(-1))")]
	[DataRow("Rust", float.NaN, "f32::NAN")]
	[DataRow("Rust", float.PositiveInfinity, "f32::INFINITY")]
	[DataRow("Rust", float.NegativeInfinity, "f32::NEG_INFINITY")]
	[DataRow("Python", float.NaN, "float(\"nan\")")]
	[DataRow("Python", float.PositiveInfinity, "float(\"inf\")")]
	[DataRow("Python", float.NegativeInfinity, "float(\"-inf\")")]
	[DataRow("JavaScript", float.NaN, "NaN")]
	[DataRow("JavaScript", float.PositiveInfinity, "Infinity")]
	[DataRow("JavaScript", float.NegativeInfinity, "-Infinity")]
	public void ANonFiniteFloatIsSpelledInTheTargetLanguage(string language, float value, string expected) =>
		Assert.AreEqual(expected, GeneratorFor(language).Generate(new LiteralExpression<float>(value)).Trim());

	/// <summary>
	/// Tests that a file whose only non-finite literal is a float still imports what its spelling
	/// needs.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="import">The line the file should carry.</param>
	[TestMethod]
	[DataRow("C", "#include <math.h>")]
	[DataRow("C++", "#include <limits>")]
	[DataRow("Go", "import \"math\"")]
	public void AFileHoldingANonFiniteFloatImportsItsSpelling(string language, string import)
	{
		string code = GeneratorFor(language).Generate(FileReturningFloats("limits", float.NaN));

		Assert.AreEqual(1, Occurrences(code, import), code);
	}

	/// <summary>
	/// Tests that a file holding a non-finite double includes or imports what its spelling needs.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="import">The line the file should carry.</param>
	[TestMethod]
	[DataRow("C", "#include <math.h>")]
	[DataRow("C++", "#include <limits>")]
	[DataRow("Go", "import \"math\"")]
	public void AFileHoldingANonFiniteDoubleImportsItsSpelling(string language, string import)
	{
		string code = GeneratorFor(language).Generate(FileReturning("limits", double.PositiveInfinity));

		Assert.AreEqual(1, Occurrences(code, import), code);
	}

	/// <summary>
	/// Tests that a file already importing what the spelling needs does not import it twice.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="fileImport">The import as the file carries it.</param>
	/// <param name="import">The line the file should carry once.</param>
	[TestMethod]
	[DataRow("C", "<math.h>", "#include <math.h>")]
	[DataRow("C++", "<limits>", "#include <limits>")]
	[DataRow("Go", "math", "import \"math\"")]
	public void AnImportTheFileAlreadyHasIsWrittenOnce(string language, string fileImport, string import)
	{
		SourceFile file = FileReturning("limits", double.NaN);
		file.Imports.Add(fileImport);

		string code = GeneratorFor(language).Generate(file);

		Assert.AreEqual(1, Occurrences(code, import), code);
	}

	/// <summary>
	/// Tests that a file holding only finite doubles gets no import it did not ask for.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="header">Text that would appear only if the import were added.</param>
	[TestMethod]
	[DataRow("C", "math.h")]
	[DataRow("C++", "<limits>")]
	[DataRow("Go", "\"math\"")]
	public void AFileOfFiniteDoublesImportsNothing(string language, string header)
	{
		string code = GeneratorFor(language).Generate(FileReturning("limits", 0.5));

		Assert.DoesNotContain(header, code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a C file returning NaN and both infinities compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedC_Compiles()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "cc", "gcc", "clang");
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		CompileIn(new CGenerator(), "limits.c", compiler, "-std=c11 -Wall -Wextra -pedantic -c limits.c -o limits.o");
	}

	/// <summary>
	/// Tests that a C++ file returning NaN and both infinities compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedCpp_Compiles()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "c++", "g++", "clang++");
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		CompileIn(new CppGenerator(), "limits.cpp", compiler, "-std=c++20 -Wall -Wextra -pedantic -c limits.cpp -o limits.o");
	}

	/// <summary>
	/// Tests that a Go file returning NaN and both infinities compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedGo_Compiles()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		CompileIn(
			new GoGenerator(),
			"limits.go",
			"go",
			"build ./...",
			directory => File.WriteAllText(Path.Combine(directory, "go.mod"), "module limits\n\ngo 1.21\n"));
	}

	/// <summary>
	/// Tests that a constant field or local holding NaN or an infinity is a Go <c>var</c>, because
	/// <c>math.NaN()</c> and <c>math.Inf</c> are calls and a Go <c>const</c> cannot hold one.
	/// </summary>
	[TestMethod]
	public void GoConstantNonFiniteValue_IsAVar()
	{
		string code = new GoGenerator().Generate(FileWithNonFiniteConstants());

		StringAssert.Contains(code, "var Unset float64 = math.NaN()", StringComparison.Ordinal);
		StringAssert.Contains(code, "var ceiling float64 = math.Inf(1)", StringComparison.Ordinal);
		Assert.DoesNotContain("const Unset", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("const ceiling", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a Go file declaring a constant NaN field and a constant infinite local compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedGoConstants_Compile()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "go.mod"), "module limits\n\ngo 1.21\n");

			string code = new GoGenerator().Generate(FileWithNonFiniteConstants());
			File.WriteAllText(Path.Combine(directory, "limits.go"), code);

			(int exitCode, string output) = ToolchainHarness.Run("go", "build ./...", directory);

			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a Rust file returning NaN and both infinities compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedRust_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		CompileIn(new RustGenerator(), "limits.rs", "rustc", "--crate-type lib --edition 2021 -o limits.rlib limits.rs");
	}

	/// <summary>
	/// Tests that a C file returning a NaN and both infinite floats compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedCFloats_Compile()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "cc", "gcc", "clang");
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		CompileIn(new CGenerator(), FloatFile(), "limits.c", compiler, "-std=c11 -Wall -Wextra -pedantic -Werror -c limits.c -o limits.o");
	}

	/// <summary>
	/// Tests that a C++ file returning a NaN and both infinite floats compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedCppFloats_Compile()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "c++", "g++", "clang++");
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		CompileIn(new CppGenerator(), FloatFile(), "limits.cpp", compiler, "-std=c++20 -Wall -Wextra -pedantic -Werror -c limits.cpp -o limits.o");
	}

	/// <summary>
	/// Tests that a Go file returning a NaN and both infinite floats, and declaring an inferred
	/// local holding one, compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedGoFloats_Compile()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		CompileIn(
			new GoGenerator(),
			FloatFile(),
			"limits.go",
			"go",
			"build ./...",
			directory => File.WriteAllText(Path.Combine(directory, "go.mod"), "module limits\n\ngo 1.21\n"));
	}

	/// <summary>
	/// Tests that a Rust file returning a NaN and both infinite floats compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedRustFloats_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		CompileIn(new RustGenerator(), FloatFile(), "limits.rs", "rustc", "--crate-type lib --edition 2021 -o limits.rlib limits.rs");
	}

	private static ILanguageGenerator GeneratorFor(string language) => language switch
	{
		"C#" => new CSharpGenerator(),
		"C++" => new CppGenerator(),
		"C" => new CGenerator(),
		"Go" => new GoGenerator(),
		"Rust" => new RustGenerator(),
		"Python" => new PythonGenerator(),
		"JavaScript" => new JavaScriptGenerator(),
		_ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
	};

	/// <summary>
	/// Builds a file with one function returning each of the given values.
	/// </summary>
	private static SourceFile FileReturning(string name, params double[] values)
	{
		SourceFile file = new(name);

		for (int index = 0; index < values.Length; index++)
		{
			FunctionDeclaration function = new($"value{index}") { ReturnType = new TypeReference("double") };
			function.Body.Add(new ReturnStatement(new LiteralExpression<double>(values[index])));
			file.Members.Add(function);
		}

		return file;
	}

	/// <summary>
	/// Builds a file with one function returning each of the given floats.
	/// </summary>
	private static SourceFile FileReturningFloats(string name, params float[] values)
	{
		SourceFile file = new(name);

		for (int index = 0; index < values.Length; index++)
		{
			FunctionDeclaration function = new($"value{index}") { ReturnType = new TypeReference("float") };
			function.Body.Add(new ReturnStatement(new LiteralExpression<float>(values[index])));
			file.Members.Add(function);
		}

		return file;
	}

	/// <summary>
	/// Builds a file returning a NaN and both infinite floats, and a function that holds one in an
	/// inferred local, whose type is the one the spelling gives it.
	/// </summary>
	private static SourceFile FloatFile()
	{
		SourceFile file = FileReturningFloats("limits", float.NaN, float.PositiveInfinity, float.NegativeInfinity);

		FunctionDeclaration inferred = new("inferred") { ReturnType = new TypeReference("float") };
		inferred.Body.Add(new VariableDeclaration("ceiling", "float", new LiteralExpression<float>(float.PositiveInfinity)) { IsTypeInferred = true });
		inferred.Body.Add(new ReturnStatement(new VariableReference("ceiling")));
		file.Members.Add(inferred);

		return file;
	}

	/// <summary>
	/// Builds a file with a constant NaN field and a function holding a constant infinite local.
	/// </summary>
	private static SourceFile FileWithNonFiniteConstants()
	{
		SourceFile file = new("limits");
		file.Members.Add(new FieldDeclaration("Unset", new TypeReference("double"))
		{
			IsConstant = true,
			IsStatic = true,
			InitialValue = new LiteralExpression<double>(double.NaN),
		});

		FunctionDeclaration function = new("ceiling") { ReturnType = new TypeReference("double") };
		function.Body.Add(new VariableDeclaration("ceiling", "double", new LiteralExpression<double>(double.PositiveInfinity)) { IsConstant = true });
		function.Body.Add(new ReturnStatement(new VariableReference("ceiling")));
		file.Members.Add(function);

		return file;
	}

	private static int Occurrences(string text, string value) =>
		text.Split(value, StringSplitOptions.None).Length - 1;

	private static void CompileIn(
		ILanguageGenerator generator,
		string fileName,
		string compiler,
		string arguments,
		Action<string>? prepare = null) =>
		CompileIn(
			generator,
			FileReturning("limits", double.NaN, double.PositiveInfinity, double.NegativeInfinity),
			fileName,
			compiler,
			arguments,
			prepare);

	private static void CompileIn(
		ILanguageGenerator generator,
		SourceFile file,
		string fileName,
		string compiler,
		string arguments,
		Action<string>? prepare = null) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			prepare?.Invoke(directory);

			string code = generator.Generate(file);
			File.WriteAllText(Path.Combine(directory, fileName), code);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, arguments, directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");
		});
}
