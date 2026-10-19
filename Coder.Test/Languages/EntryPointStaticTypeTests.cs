// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that an entry point's locals have static types in C, C++ and Go, as a function's do.
/// </summary>
/// <remarks>
/// C, C++ and Go wrote an entry point's body with no type scope open, so every local in <c>main</c>
/// was unknown: C wrote an inferred local as <c>void* b = a;</c>, and a <c>%</c> of two doubles stayed
/// <c>%</c>, which all three reject (ktsu-dev/Coder#277).
/// </remarks>
[TestClass]
public class EntryPointStaticTypeTests
{
	/// <summary>
	/// Tests that C types a local inferred from another local by that local's type.
	/// </summary>
	[TestMethod]
	public void C_InferredLocalTakesItsInitialiserType()
	{
		string code = new CGenerator().Generate(InferredLocals());

		StringAssert.Contains(code, "double b = a;", StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that the C entry point holding an inferred local compiles.
	/// </summary>
	[TestMethod]
	public void C_InferredLocalCompiles()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "cc", "gcc", "clang");
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		string code = new CGenerator().Generate(InferredLocals());
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "main.c"), code);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, "-std=c11 -pedantic -c main.c -o main.o", directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that each of the three targets writes a remainder of two double locals in an entry point
	/// as its library call.
	/// </summary>
	/// <param name="language">The generator's display name.</param>
	/// <param name="expected">What <c>x % y</c> should be written as.</param>
	[TestMethod]
	[DataRow("C", "fmod(x, y)")]
	[DataRow("C++", "std::fmod(x, y)")]
	[DataRow("Go", "math.Mod(x, y)")]
	public void AFloatingRemainderOfLocalsIsACall(string language, string expected)
	{
		string code = GeneratorFor(language).Generate(RemainderOfLocals(returnsExitCode: false));

		StringAssert.Contains(code, expected, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that Go's <c>run</c>, which holds the body of an entry point that returns an exit code,
	/// knows its locals' types too.
	/// </summary>
	[TestMethod]
	public void Go_ExitCodeEntryPointTypesItsLocals()
	{
		string code = new GoGenerator().Generate(RemainderOfLocals(returnsExitCode: true));

		StringAssert.Contains(code, "math.Mod(x, y)", StringComparison.Ordinal, code);
	}

	private static ILanguageGenerator GeneratorFor(string language) => language switch
	{
		"C++" => new CppGenerator(),
		"C" => new CGenerator(),
		"Go" => new GoGenerator(),
		_ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
	};

	/// <summary>
	/// Builds an entry point holding <c>a = 1.5</c> and <c>b = a</c>, both inferred.
	/// </summary>
	private static SourceFile InferredLocals()
	{
		EntryPoint entryPoint = new();
		entryPoint.Body.Add(new VariableDeclaration("a", null, Literal.DecimalValue(1.5)) { IsTypeInferred = true });
		entryPoint.Body.Add(new VariableDeclaration("b", null, new VariableReference("a")) { IsTypeInferred = true });

		SourceFile file = new("main");
		file.Members.Add(entryPoint);
		return file;
	}

	/// <summary>
	/// Builds an entry point holding two double locals and the remainder of one by the other.
	/// </summary>
	private static SourceFile RemainderOfLocals(bool returnsExitCode)
	{
		EntryPoint entryPoint = new() { ReturnsExitCode = returnsExitCode };
		entryPoint.Body.Add(new VariableDeclaration("x", "double", Literal.DecimalValue(7.5)));
		entryPoint.Body.Add(new VariableDeclaration("y", "double", Literal.DecimalValue(2.0)));
		entryPoint.Body.Add(new VariableDeclaration(
			"z",
			"double",
			new BinaryExpression(new VariableReference("x"), BinaryOperator.Modulo, new VariableReference("y"))));

		SourceFile file = new("main");
		file.Members.Add(entryPoint);
		return file;
	}
}
