// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a Rust local whose type is inferred from a <c>long</c> or <c>float</c> keeps that width.
/// </summary>
/// <remarks>
/// Rust infers an unconstrained integer literal as <c>i32</c> and a float literal as <c>f64</c>. A
/// <c>long</c> past <c>i32</c>'s range then fails the deny-by-default <c>overflowing_literals</c>
/// lint, and a <c>float</c> silently becomes a <c>double</c>.
/// </remarks>
[TestClass]
public class RustInferredLiteralWidthTests
{
	/// <summary>
	/// Tests that an inferred local initialised with a <c>long</c> or <c>float</c> is annotated with
	/// its width, and one initialised with an <c>int</c> or <c>double</c> is still left to inference.
	/// </summary>
	[TestMethod]
	public void AnInferredLongOrFloatLocal_IsAnnotatedWithItsWidth()
	{
		string code = new RustGenerator().Generate(Widths());

		StringAssert.Contains(code, "let mut big: i64 = 5000000000;", StringComparison.Ordinal);
		StringAssert.Contains(code, "let mut narrow: f32 = ", StringComparison.Ordinal);
		StringAssert.Contains(code, "let mut small = 5;", StringComparison.Ordinal);
		StringAssert.Contains(code, "let mut wide = ", StringComparison.Ordinal);
		Assert.DoesNotContain("let mut wide:", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that an unconstrained inferred local holding a <c>long</c> past <c>i32</c>'s range
	/// compiles.
	/// </summary>
	[TestMethod]
	public void AnUnconstrainedLongLocal_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			// Nothing reads the locals, which is what leaves their types unconstrained.
			string source = new RustGenerator().Generate(Widths());
			string code = $"#![allow(unused_variables, unused_mut, dead_code)]{Environment.NewLine}{source}";
			File.WriteAllText(Path.Combine(directory, "widths.rs"), code);

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o widths.rlib widths.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");
		});
	}

	private static SourceFile Widths()
	{
		FunctionDeclaration function = new("widths");
		function.Body.Add(new VariableDeclaration("big", null, new LiteralExpression<long>(5_000_000_000)));
		function.Body.Add(new VariableDeclaration("narrow", null, new LiteralExpression<float>(0.1f)));
		function.Body.Add(new VariableDeclaration("small", null, new LiteralExpression<int>(5)));
		function.Body.Add(new VariableDeclaration("wide", null, new LiteralExpression<double>(0.1)));

		SourceFile file = new("widths");
		file.Members.Add(function);
		return file;
	}
}
