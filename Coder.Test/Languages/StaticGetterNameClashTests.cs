// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the getter a static property is separated into keeps clear of a static field that
/// would be spelled the same, in the targets that hold the two in one scope.
/// </summary>
/// <remarks>
/// A static field <c>limit</c> with a property <c>Limit</c> over it is the ordinary C# shape. The
/// getter's invented name was <c>limit</c> (<c>GaugeLimit</c> in Go), the field's name in every
/// target that writes a type's statics beside its functions: C wrote a second <c>Gauge_limit</c> at
/// file scope, Go a second <c>GaugeLimit</c> in the package, Rust a second <c>limit</c> in the
/// <c>impl</c> (E0592), and Python's <c>def limit()</c> replaced the class attribute (issue #444).
/// </remarks>
[TestClass]
public class StaticGetterNameClashTests
{
	private static readonly string[] CCompilers = ["cc", "gcc", "clang"];

	private const string AllowedRustLints = "#![allow(unused_parens, dead_code, unused_variables, unused_mut, non_upper_case_globals)]";

	/// <summary>
	/// Tests that C names a colliding getter <c>get_</c>, and that the header compiles as strict C
	/// with the field and the getter both reachable.
	/// </summary>
	[TestMethod]
	public void C_RenamesAGetterBesideAStaticFieldAndCompiles()
	{
		string source = new CGenerator().Generate(Gauge());
		StringAssert.Contains(source, "int Gauge_get_limit(void)", StringComparison.Ordinal);
		StringAssert.Contains(source, "int Gauge_get_total(void)", StringComparison.Ordinal);

		string? compiler = ToolchainHarness.FindOnPath("--version", CCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		const string driver = """
			#include <stdio.h>
			#include "gauge.h"

			int main(void)
			{
				printf("%d %d %d %d\n", Gauge_limit, Gauge_get_limit(), Gauge_total, Gauge_get_total());
				return 0;
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "gauge.h"), source);
			File.WriteAllText(Path.Combine(directory, "driver.c"), driver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c11 -Wall -Wextra -pedantic-errors driver.c -o gauge",
				directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "gauge"), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("10 10 0 0", printed.Trim(), source);
		});
	}

	/// <summary>
	/// Tests that Go names a colliding static getter <c>GaugeGetLimit</c>, and that the package
	/// builds, passes vet and runs.
	/// </summary>
	[TestMethod]
	public void Go_RenamesAStaticGetterBesideAPackageLevelFieldAndRuns()
	{
		SourceFile file = Gauge();
		file.Name = "main";
		string source = new GoGenerator().Generate(file);
		StringAssert.Contains(source, "func GaugeGetLimit() int", StringComparison.Ordinal);
		StringAssert.Contains(source, "func GaugeGetTotal() int", StringComparison.Ordinal);

		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		const string module = """
			module gauge

			go 1.21

			""";

		const string driver = """
			package main

			import "fmt"

			func main() {
				fmt.Println(GaugeLimit, GaugeGetLimit(), GaugeTotal, GaugeGetTotal())
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "go.mod"), module);
			File.WriteAllText(Path.Combine(directory, "driver.go"), driver);
			File.WriteAllText(Path.Combine(directory, "gauge.go"), source);

			(int vetted, string vetOutput) = ToolchainHarness.Run("go", "vet .", directory);
			Assert.AreEqual(0, vetted, $"go vet rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{vetOutput}");

			(int exitCode, string output) = ToolchainHarness.Run("go", "run .", directory);
			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{output}");
			Assert.AreEqual("10 10 0 0", output.Trim(), source);
		});
	}

	/// <summary>
	/// Tests that Rust names a getter beside an associated constant <c>get_limit</c>, and that the
	/// crate compiles and runs.
	/// </summary>
	[TestMethod]
	public void Rust_RenamesAGetterBesideAnAssociatedConstantAndRuns()
	{
		string source = new RustGenerator().Generate(Gauge());
		StringAssert.Contains(source, "pub fn get_limit() -> i32", StringComparison.Ordinal);

		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		string driver = """
			fn main() {
			    println!("{} {}", Gauge::limit, Gauge::get_limit());
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "gauge.rs"),
				$"{AllowedRustLints}{Environment.NewLine}{source}{Environment.NewLine}{driver}");

			(int exitCode, string output) = ToolchainHarness.Run("rustc", "--edition 2021 -o gauge gauge.rs", directory);
			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "gauge"), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("10 10", printed.Trim(), source);
		});
	}

	/// <summary>
	/// Tests that Python names a static getter beside a class attribute <c>get_limit</c>, so the
	/// attribute still holds its value after import.
	/// </summary>
	[TestMethod]
	public void Python_KeepsAClassAttributeBesideAStaticGetter()
	{
		string source = new PythonGenerator().Generate(Gauge());
		StringAssert.Contains(source, "def get_limit() -> int:", StringComparison.Ordinal);
		StringAssert.Contains(source, "def get_total() -> int:", StringComparison.Ordinal);

		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		const string driver = """
			from gauge import Gauge

			assert Gauge.limit == 10, Gauge.limit
			assert Gauge.get_limit() == 10
			assert Gauge.total == 0, Gauge.total
			assert Gauge.get_total() == 0
			print("ok")

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "gauge.py"), source);
			File.WriteAllText(Path.Combine(directory, "driver.py"), driver);

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(0, exitCode, $"Python rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a static getter with nothing of its name beside it keeps the name it always had.
	/// </summary>
	[TestMethod]
	public void AGetterWithNoClash_KeepsItsName()
	{
		ClassDeclaration gauge = new("Gauge");
		gauge.Members.Add(Getter("Limit", 10));
		SourceFile file = new("gauge");
		file.Members.Add(gauge);

		StringAssert.Contains(new CGenerator().Generate(file), "int Gauge_limit(void)", StringComparison.Ordinal);
		StringAssert.Contains(new GoGenerator().Generate(file), "func GaugeLimit() int", StringComparison.Ordinal);
		StringAssert.Contains(new RustGenerator().Generate(file), "pub fn limit() -> i32", StringComparison.Ordinal);
		StringAssert.Contains(new PythonGenerator().Generate(file), "def limit() -> int:", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a Go method keeps its name beside a package-level constant, which lives in the
	/// package rather than on the receiver.
	/// </summary>
	[TestMethod]
	public void Go_AnInstanceGetterBesideAConstant_KeepsItsName()
	{
		ClassDeclaration gauge = new("Gauge");
		gauge.Members.Add(new FieldDeclaration("limit", "int") { IsStatic = true, IsConstant = true, InitialValue = new LiteralExpression<int>(10) });
		PropertyDeclaration instance = Getter("Limit", 10);
		instance.IsStatic = false;
		gauge.Members.Add(instance);
		SourceFile file = new("gauge");
		file.Members.Add(gauge);

		string source = new GoGenerator().Generate(file);

		Assert.DoesNotContain("GetLimit", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// The issue's reproduction: a constant <c>limit</c> and a mutable <c>total</c>, each with a
	/// static property over it.
	/// </summary>
	/// <returns>The file holding the type.</returns>
	private static SourceFile Gauge()
	{
		ClassDeclaration gauge = new("Gauge");
		gauge.Members.Add(new FieldDeclaration("limit", "int") { IsStatic = true, IsConstant = true, InitialValue = new LiteralExpression<int>(10) });
		gauge.Members.Add(Getter("Limit", 10));
		gauge.Members.Add(new FieldDeclaration("total", "int") { IsStatic = true, InitialValue = new LiteralExpression<int>(0) });
		gauge.Members.Add(Getter("Total", 0));

		SourceFile file = new("gauge");
		file.Members.Add(gauge);
		return file;
	}

	/// <summary>
	/// A public static read-only property answering a constant.
	/// </summary>
	/// <param name="name">The property's name.</param>
	/// <param name="value">What its getter returns.</param>
	/// <returns>The property.</returns>
	private static PropertyDeclaration Getter(string name, int value)
	{
		PropertyDeclaration property = new(name, new TypeReference("int"))
		{
			Visibility = Visibility.Public,
			IsStatic = true,
		};
		property.GetterBody.Add(new ReturnStatement(new LiteralExpression<int>(value)));
		return property;
	}
}
