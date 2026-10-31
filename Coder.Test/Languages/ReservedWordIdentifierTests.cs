// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles and runs what the Rust, C++ and Python generators write for names that are keywords of
/// the target: a property called <c>Type</c> or <c>Default</c>, which are ordinary C# names, fields,
/// parameters and locals called <c>where</c>, <c>virtual</c>, <c>match</c>, <c>delete</c>,
/// <c>loop</c> and <c>new</c>, and, for Python, an enumeration member called <c>None</c>. Each name has to come out escaped where it is declared and spelled the
/// same way wherever it is used, or the output either fails to parse or fails to resolve.
/// </summary>
/// <remarks>
/// The tests are inconclusive rather than failing where no compiler is on the path, which is the
/// honest result: nothing was checked.
/// </remarks>
[TestClass]
public class ReservedWordIdentifierTests
{
	private static readonly string[] CppCompilers = ["c++", "g++", "clang++"];

	/// <summary>
	/// The Rust driver. <c>r#</c> is how a Rust caller names a keyword, so it reads the escaped names
	/// back exactly as a user of the generated type would.
	/// </summary>
	private const string RustDriver = """

		fn main() {
		    let mut holder = Holder::make();
		    if holder.r#where != 3 || holder.r#virtual != 4 { std::process::exit(1); }
		    if holder.r#type() != 1 { std::process::exit(2); }
		    if holder.default() != 2 { std::process::exit(3); }
		    if holder.total(5, 6) != 14 { std::process::exit(4); }
		}

		""";

	/// <summary>
	/// The C++ driver. C++ has no escape for a keyword, so an escaped name has had an underscore
	/// appended, and that is the name a caller uses.
	/// </summary>
	private const string CppDriver = """
		#include "holder.hpp"

		int main()
		{
			Holder holder = Holder::make();
			if (holder.where != 3 || holder.virtual_ != 4) { return 1; }
			if (holder.type() != 1) { return 2; }
			if (holder.default_() != 2) { return 3; }
			if (holder.total(5, 6) != 15) { return 4; }
			return 0;
		}

		""";

	/// <summary>
	/// Tests that Rust names a keyword-named field, getter, parameter and local as raw identifiers,
	/// at their declarations and where they are used.
	/// </summary>
	[TestMethod]
	public void Rust_KeywordNamesCompileAndResolve()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "holder.exe" : "holder";
			string source = new RustGenerator().Generate(Types("self.where")) + RustDriver;
			File.WriteAllText(Path.Combine(directory, "holder.rs"), source);

			(int built, string output) = ToolchainHarness.Run("rustc", $"--edition 2021 -o {program} holder.rs", directory);
			Assert.AreEqual(0, built, $"rustc rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that C++ appends an underscore to a keyword-named field, getter, parameter and local,
	/// at their declarations and where they are used.
	/// </summary>
	[TestMethod]
	public void Cpp_KeywordNamesCompileAndResolve()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CppCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "holder.exe" : "holder";
			string header = new CppGenerator().Generate(Types("virtual"));
			File.WriteAllText(Path.Combine(directory, "holder.hpp"), header);
			File.WriteAllText(Path.Combine(directory, "driver.cpp"), CppDriver);

			(int built, string output) = ToolchainHarness.Run(
				compiler,
				$"-std=c++20 -Wall -Wextra -pedantic -o {program} driver.cpp",
				directory);
			Assert.AreEqual(0, built, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{header}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{header}");
		});
	}

	/// <summary>
	/// The Python driver. Python has no escape for a keyword either, so an escaped name has had an
	/// underscore appended, and that is the name a caller uses.
	/// </summary>
	private const string PythonDriver = """


		holder = Holder()
		if holder.from_ != 3 or holder.lambda_ != 4:
		    raise SystemExit(1)
		if holder.class_ != 1:
		    raise SystemExit(2)
		if holder.total(5, 6) != 14:
		    raise SystemExit(3)
		if Flag.None_ != 0 or Flag.All != 1:
		    raise SystemExit(4)
		if def_(7) != 7:
		    raise SystemExit(5)

		""";

	/// <summary>
	/// Tests that Python appends an underscore to a keyword-named field, property, parameter, local,
	/// enumeration member and function, at their declarations and where they are used.
	/// </summary>
	[TestMethod]
	public void Python_KeywordNamesRunAndResolve()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new PythonGenerator().Generate(PythonTypes()) + PythonDriver;
			File.WriteAllText(Path.Combine(directory, "holder.py"), source);

			(int ran, string output) = ToolchainHarness.Run(python, "holder.py", directory);
			Assert.AreEqual(0, ran, $"the generated module failed, or a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a Python reference keeps the literal words as written while escaping a keyword
	/// that names a member.
	/// </summary>
	[TestMethod]
	public void Python_ReferenceKeepsTheLiteralWordsAndEscapesTheMember()
	{
		FunctionDeclaration read = new("read") { ReturnType = "int" };
		read.Body.Add(new VariableDeclaration("unused", "int", new VariableReference("None")));
		read.Body.Add(new ReturnStatement(new VariableReference("self.from")));
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new VariableDeclaration("from", "int"));
		holder.Members.Add(read);
		SourceFile file = new("holder");
		file.Members.Add(holder);

		string source = new PythonGenerator().Generate(file);

		Assert.Contains("unused = None", source, StringComparison.Ordinal, source);
		Assert.Contains("return self.from_", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("None_", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that a reference keeps a receiver as written and escapes only the plain names after it.
	/// </summary>
	[TestMethod]
	public void Rust_ReferenceKeepsTheReceiverAndEscapesTheMember()
	{
		FunctionDeclaration read = new("read") { ReturnType = "int" };
		read.Body.Add(new ReturnStatement(new VariableReference("self.type")));
		ClassDeclaration holder = new("Holder") { Kind = TypeDeclarationKind.Struct };
		holder.Members.Add(new VariableDeclaration("type", "int"));
		holder.Members.Add(read);
		SourceFile file = new("holder");
		file.Members.Add(holder);

		string source = new RustGenerator().Generate(file);

		Assert.Contains("pub r#type: i32,", source, StringComparison.Ordinal, source);
		Assert.Contains("self.r#type", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that the path keywords, which Rust refuses as raw identifiers, are given an underscore
	/// instead when they name a declaration.
	/// </summary>
	[TestMethod]
	public void Rust_PathKeywordNamesAreSuffixed()
	{
		FunctionDeclaration function = new("super") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("crate", "int"));
		function.Body.Add(new ReturnStatement(new VariableReference("crate")));
		SourceFile file = new("paths");
		file.Members.Add(function);

		string source = new RustGenerator().Generate(file);

		Assert.Contains("fn super_(crate_: i32)", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("r#super", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that Rust declares a keyword-named enumeration member as a raw identifier, and that a
	/// <c>Kind::type</c> path and a sibling's value naming it are spelled the same way. <c>public</c>
	/// and <c>private</c> are not Rust keywords, so they stay as written.
	/// </summary>
	[TestMethod]
	public void Rust_KeywordNamedEnumMembersCompileAndResolve()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "kinds.exe" : "kinds";
			string source = new RustGenerator().Generate(KeywordEnums()) + RustEnumDriver;
			File.WriteAllText(Path.Combine(directory, "kinds.rs"), source);

			(int built, string output) = ToolchainHarness.Run("rustc", $"--edition 2021 -o {program} kinds.rs", directory);
			Assert.AreEqual(0, built, $"rustc rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that C++ appends an underscore to a keyword-named enumeration member, and that a
	/// <c>Access::public</c> path and a sibling's value naming one are spelled the same way.
	/// <c>type</c> is not a C++ keyword, so it stays as written.
	/// </summary>
	[TestMethod]
	public void Cpp_KeywordNamedEnumMembersCompileAndResolve()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CppCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "kinds.exe" : "kinds";
			string header = new CppGenerator().Generate(KeywordEnums());
			File.WriteAllText(Path.Combine(directory, "kinds.hpp"), header);
			File.WriteAllText(Path.Combine(directory, "driver.cpp"), CppEnumDriver);

			(int built, string output) = ToolchainHarness.Run(
				compiler,
				$"-std=c++20 -Wall -Wextra -pedantic -o {program} driver.cpp",
				directory);
			Assert.AreEqual(0, built, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{header}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{header}");
		});
	}

	/// <summary>
	/// The Rust driver for <see cref="KeywordEnums"/>.
	/// </summary>
	private const string RustEnumDriver = """

		fn main() {
		    if pick() != Kind::r#type || Kind::Other as isize != 2 { std::process::exit(1); }
		    if open() != Access::public || Access::Hidden as i32 != 2 { std::process::exit(2); }
		}

		""";

	/// <summary>
	/// The C++ driver for <see cref="KeywordEnums"/>.
	/// </summary>
	private const string CppEnumDriver = """
		#include "kinds.hpp"

		int main()
		{
			if (pick() != Kind::type || static_cast<int>(Kind::Other) != 2) { return 1; }
			if (open() != Access::public_ || static_cast<int>(Access::Hidden) != 2) { return 2; }
			return 0;
		}

		""";

	/// <summary>
	/// Builds two enumerations whose members are C++ and Rust keywords, one member whose value names a
	/// keyword-named sibling, and two functions that return a keyword-named member by its path.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile KeywordEnums()
	{
		EnumDeclaration access = new("Access") { UnderlyingType = new TypeReference("int") };
		access.Members.Add(new EnumMember("public") { Value = "0" });
		access.Members.Add(new EnumMember("private") { Value = "1" });
		access.Members.Add(new EnumMember("Hidden") { Value = "private + 1" });

		EnumDeclaration kind = new("Kind");
		kind.Members.Add(new EnumMember("None"));
		kind.Members.Add(new EnumMember("type") { Value = "1" });
		kind.Members.Add(new EnumMember("Other") { Value = "type + 1" });

		FunctionDeclaration pick = new("pick") { ReturnType = "Kind" };
		pick.Body.Add(new ReturnStatement(new VariableReference("Kind::type")));

		FunctionDeclaration open = new("open") { ReturnType = "Access" };
		open.Body.Add(new ReturnStatement(new VariableReference("Access::public")));

		SourceFile file = new("kinds");
		file.Members.Add(access);
		file.Members.Add(kind);
		file.Members.Add(pick);
		file.Members.Add(open);
		return file;
	}

	/// <summary>
	/// Builds a class, an enumeration and a function whose names are Python keywords.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile PythonTypes()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new VariableDeclaration("from", "int", new LiteralExpression<int>(3)));
		holder.Members.Add(new VariableDeclaration("lambda", "int", new LiteralExpression<int>(4)));
		holder.Members.Add(new PropertyDeclaration("class", "int")
		{
			GetterBody = { new ReturnStatement(new LiteralExpression<int>(1)) },
		});

		FunctionDeclaration total = new("total") { ReturnType = "int" };
		total.Parameters.Add(new Parameter("in", "int"));
		total.Parameters.Add(new Parameter("is", "int"));
		total.Body.Add(new VariableDeclaration(
			"pass",
			"int",
			new BinaryExpression(new VariableReference("in"), BinaryOperator.Add, new VariableReference("is"))));
		total.Body.Add(new VariableDeclaration(
			"del",
			"int",
			new BinaryExpression(new VariableReference("pass"), BinaryOperator.Add, new VariableReference("self.from"))));
		total.Body.Add(new ReturnStatement(new VariableReference("del")));
		holder.Members.Add(total);

		EnumDeclaration flag = new("Flag");
		flag.Members.Add(new EnumMember("None"));
		flag.Members.Add(new EnumMember("All"));

		FunctionDeclaration identity = new("def") { ReturnType = "int" };
		identity.Parameters.Add(new Parameter("with", "int"));
		identity.Body.Add(new ReturnStatement(new VariableReference("with")));

		SourceFile file = new("holder");
		file.Members.Add(holder);
		file.Members.Add(flag);
		file.Members.Add(identity);
		return file;
	}

	/// <summary>
	/// Builds a struct whose members are all named after a keyword of one target or the other.
	/// </summary>
	/// <param name="fieldReference">How the target's method body refers to one of the struct's own fields.</param>
	/// <returns>The file to generate.</returns>
	private static SourceFile Types(string fieldReference)
	{
		ClassDeclaration holder = new("Holder") { Kind = TypeDeclarationKind.Struct };
		holder.Members.Add(new VariableDeclaration("where", "int"));
		holder.Members.Add(new VariableDeclaration("virtual", "int"));
		holder.Members.Add(new PropertyDeclaration("Type", "int")
		{
			GetterBody = { new ReturnStatement(new LiteralExpression<int>(1)) },
		});
		holder.Members.Add(new PropertyDeclaration("Default", "int")
		{
			GetterBody = { new ReturnStatement(new LiteralExpression<int>(2)) },
		});

		ConstructionExpression built = new(new TypeReference("Holder"));
		built.Arguments.Add(new MemberInitialiser("where", new LiteralExpression<int>(3)));
		built.Arguments.Add(new MemberInitialiser("virtual", new LiteralExpression<int>(4)));
		FunctionDeclaration make = new("make") { ReturnType = "Holder", IsStatic = true };
		make.Body.Add(new ReturnStatement(built));
		holder.Members.Add(make);

		FunctionDeclaration total = new("total") { ReturnType = "int" };
		total.Parameters.Add(new Parameter("match", "int"));
		total.Parameters.Add(new Parameter("delete", "int"));
		total.Body.Add(new VariableDeclaration(
			"loop",
			"int",
			new BinaryExpression(new VariableReference("match"), BinaryOperator.Add, new VariableReference("delete"))));
		total.Body.Add(new VariableDeclaration(
			"new",
			"int",
			new BinaryExpression(new VariableReference("loop"), BinaryOperator.Add, new VariableReference(fieldReference))));
		total.Body.Add(new ReturnStatement(new VariableReference("new")));
		holder.Members.Add(total);

		SourceFile file = new("holder");
		file.Members.Add(holder);
		return file;
	}
}
