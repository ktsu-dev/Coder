// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles and runs what the Rust, C and Go generators write for a constructor that leaves a field
/// with a declared starting value unset. None of the three has field initialisers, so the
/// constructor is the only place the declared value can be applied; every other target applies it.
/// </summary>
/// <remarks>
/// The tests are inconclusive rather than failing where no compiler is on the path, which is the
/// honest result: nothing was checked.
/// </remarks>
[TestClass]
public class ConstructorFieldDefaultTests
{
	private static readonly string[] CCompilers = ["cc", "gcc", "clang"];

	/// <summary>
	/// The Rust driver. <c>Mixed</c> has a field with no declared value, which its constructor
	/// leaves to <c>Default</c>; the driver supplies that, as a user of the type would.
	/// </summary>
	private const string RustDriver = """

		impl Default for Mixed {
		    fn default() -> Self {
		        Mixed { count: 0, step: 7 }
		    }
		}

		fn main() {
		    let counter = Counter::new(3);
		    if counter.count != 5 || counter.step != 3 { std::process::exit(1); }
		    let settings = Settings::new();
		    if settings.count != 5 || settings.step != 1 { std::process::exit(2); }
		    let mixed = Mixed::new();
		    if mixed.count != 5 || mixed.step != 7 { std::process::exit(3); }
		}

		""";

	/// <summary>
	/// The C driver: each constructor's members read back what was declared or passed.
	/// </summary>
	private const string CDriver = """
		#include "defaults.h"

		int main(void)
		{
			Counter counter = Counter_create(3);
			if (counter.count != 5 || counter.step != 3) { return 1; }
			Settings settings = Settings_create();
			if (settings.count != 5 || settings.step != 1) { return 2; }
			Mixed mixed = Mixed_create();
			if (mixed.count != 5 || mixed.step != 0) { return 3; }
			return 0;
		}

		""";

	/// <summary>
	/// Tests that a Rust constructor builds a field it does not set from its declared value, with or
	/// without initialisers of its own, and leaves only the undeclared rest to <c>Default</c>.
	/// </summary>
	[TestMethod]
	public void Rust_ConstructorAppliesDeclaredFieldValues()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "defaults.exe" : "defaults";
			string source = new RustGenerator().Generate(Types()) + RustDriver;
			File.WriteAllText(Path.Combine(directory, "defaults.rs"), source);

			(int built, string output) = ToolchainHarness.Run("rustc", $"--edition 2021 -o {program} defaults.rs", directory);
			Assert.AreEqual(0, built, $"rustc rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a field did not start at its declared value (driver check {ran}):{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a C constructor builds a member it does not set from its declared value, with or
	/// without initialisers of its own, so the struct's <c>defaults to</c> note holds.
	/// </summary>
	[TestMethod]
	public void C_ConstructorAppliesDeclaredFieldValues()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "defaults.exe" : "defaults";
			string header = new CGenerator().Generate(Types());
			File.WriteAllText(Path.Combine(directory, "defaults.h"), header);
			File.WriteAllText(Path.Combine(directory, "driver.c"), CDriver);

			(int built, string output) = ToolchainHarness.Run(
				compiler,
				$"-std=c11 -Wall -Wextra -pedantic -o {program} driver.c",
				directory);
			Assert.AreEqual(0, built, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{header}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a member did not start at its declared value (driver check {ran}):{Environment.NewLine}{header}");
		});
	}

	/// <summary>
	/// Tests that a Rust constructor fills a field declared as a variable from its starting value,
	/// as it does one declared as a field.
	/// </summary>
	/// <remarks>
	/// Only <see cref="FieldDeclaration"/> starting values were gathered, so a
	/// <see cref="VariableDeclaration"/> member the constructor did not set was missing from the
	/// struct literal and rustc refused it with E0063 (issue #262).
	/// </remarks>
	[TestMethod]
	public void Rust_ConstructorAppliesDeclaredVariableValues()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		const string driver = """

			fn main() {
			    let point = Point::new(3);
			    if point.x != 3 { std::process::exit(1); }
			    if point.y != 7 { std::process::exit(2); }
			    if point.z != 9 { std::process::exit(3); }
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "point.exe" : "point";
			string source = new RustGenerator().Generate(PointFile()) + driver;
			File.WriteAllText(Path.Combine(directory, "point.rs"), source);

			(int built, string output) = ToolchainHarness.Run("rustc", $"--edition 2021 -o {program} point.rs", directory);
			Assert.AreEqual(0, built, $"rustc rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a field did not start at its declared value (driver check {ran}):{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a C constructor fills a member declared as a variable from its starting value.
	/// </summary>
	/// <remarks>
	/// The member used to start at zero, because only <see cref="FieldDeclaration"/> starting values
	/// were gathered (issue #262).
	/// </remarks>
	[TestMethod]
	public void C_ConstructorAppliesDeclaredVariableValues()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		const string driver = """
			#include "point.h"

			int main(void)
			{
				Point point = Point_create(3);
				if (point.x != 3) { return 1; }
				if (point.y != 7) { return 2; }
				if (point.z != 9) { return 3; }
				return 0;
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "point.exe" : "point";
			string header = new CGenerator().Generate(PointFile());
			File.WriteAllText(Path.Combine(directory, "point.h"), header);
			File.WriteAllText(Path.Combine(directory, "driver.c"), driver);

			(int built, string output) = ToolchainHarness.Run(
				compiler,
				$"-std=c11 -Wall -Wextra -pedantic -o {program} driver.c",
				directory);
			Assert.AreEqual(0, built, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{header}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a member did not start at its declared value (driver check {ran}):{Environment.NewLine}{header}");
		});
	}

	/// <summary>
	/// Tests that a Go constructor fills a field declared as a variable from its starting value.
	/// </summary>
	/// <remarks>
	/// The field used to start at zero, because only <see cref="FieldDeclaration"/> starting values
	/// were gathered (issue #262).
	/// </remarks>
	[TestMethod]
	public void Go_ConstructorAppliesDeclaredVariableValues()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		const string driver = """
			package main

			import "fmt"

			func main() {
				point := NewPoint(3)
				fmt.Println(point.x, point.y, point.z)
			}

			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new GoGenerator().Generate(PointFile());
			File.WriteAllText(Path.Combine(directory, "go.mod"), "module point\n\ngo 1.21\n");
			File.WriteAllText(Path.Combine(directory, "driver.go"), driver);
			File.WriteAllText(Path.Combine(directory, "point.go"), source);

			(int exitCode, string output) = ToolchainHarness.Run("go", "run .", directory);
			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
			Assert.AreEqual("3 7 9", output.Trim(), source);
		});
	}

	/// <summary>
	/// Builds a type mixing the two ways of declaring a member with a starting value, whose
	/// constructor sets only the member that has none.
	/// </summary>
	/// <returns>The file holding it.</returns>
	private static SourceFile PointFile()
	{
		SourceFile file = new("main");
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int"));
		point.Members.Add(new VariableDeclaration("y", "int", Literal.Number(7)));
		point.Members.Add(new FieldDeclaration("z", new TypeReference("int")) { Visibility = Visibility.Public, InitialValue = Literal.Number(9) });
		FunctionDeclaration constructor = new("Point") { Kind = FunctionKind.Constructor, Visibility = Visibility.Public };
		constructor.Parameters.Add(new Parameter("x", "int"));
		constructor.Initialisers.Add(new MemberInitialiser("x", new VariableReference("x")));
		point.Members.Add(constructor);
		file.Members.Add(point);
		return file;
	}

	/// <summary>
	/// Builds three types whose constructors leave a declared field unset: one alongside an
	/// initialiser, one with no initialisers and every field declared, and one with no initialisers
	/// and a field that declares nothing.
	/// </summary>
	/// <returns>The file holding them.</returns>
	private static SourceFile Types()
	{
		SourceFile file = new("defaults");

		ClassDeclaration counter = new("Counter") { Kind = TypeDeclarationKind.Struct };
		counter.Members.Add(new FieldDeclaration("count", new TypeReference("int")) { Visibility = Visibility.Public, InitialValue = Literal.Number(5) });
		counter.Members.Add(new FieldDeclaration("step", new TypeReference("int")) { Visibility = Visibility.Public });
		FunctionDeclaration counterNew = new("Counter") { Kind = FunctionKind.Constructor, Visibility = Visibility.Public };
		counterNew.Parameters.Add(new Parameter("s", "int"));
		counterNew.Initialisers.Add(new MemberInitialiser("step", new VariableReference("s")));
		counter.Members.Add(counterNew);
		file.Members.Add(counter);

		ClassDeclaration settings = new("Settings") { Kind = TypeDeclarationKind.Struct };
		settings.Members.Add(new FieldDeclaration("count", new TypeReference("int")) { Visibility = Visibility.Public, InitialValue = Literal.Number(5) });
		settings.Members.Add(new FieldDeclaration("step", new TypeReference("int")) { Visibility = Visibility.Public, InitialValue = Literal.Number(1) });
		settings.Members.Add(new FunctionDeclaration("Settings") { Kind = FunctionKind.Constructor, Visibility = Visibility.Public });
		file.Members.Add(settings);

		ClassDeclaration mixed = new("Mixed") { Kind = TypeDeclarationKind.Struct };
		mixed.Members.Add(new FieldDeclaration("count", new TypeReference("int")) { Visibility = Visibility.Public, InitialValue = Literal.Number(5) });
		mixed.Members.Add(new FieldDeclaration("step", new TypeReference("int")) { Visibility = Visibility.Public });
		mixed.Members.Add(new FunctionDeclaration("Mixed") { Kind = FunctionKind.Constructor, Visibility = Visibility.Public });
		file.Members.Add(mixed);

		return file;
	}
}
