// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles and runs what the Rust, C++, Python, C# and Go generators write for names that are keywords of
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
	/// Tests that a Python enumeration member whose value names a keyword-escaped sibling, explicitly
	/// or through the <c>(origin) + n</c> a following member derives from it, names the escaped
	/// member rather than the keyword, so the module imports and the values resolve.
	/// </summary>
	[TestMethod]
	public void Python_EnumValueNamingAnEscapedSiblingRunsAndResolves()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		EnumDeclaration access = new("Access");
		access.Members.Add(new EnumMember("None") { Value = "0" });
		access.Members.Add(new EnumMember("Read") { Value = "1" });
		access.Members.Add(new EnumMember("Default") { Value = "None" });
		access.Members.Add(new EnumMember("Next"));
		SourceFile file = new("access");
		file.Members.Add(access);

		string source = new PythonGenerator().Generate(file);
		Assert.Contains("Default = None_", source, StringComparison.Ordinal, source);
		Assert.Contains("Next = (None_) + 1", source, StringComparison.Ordinal, source);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string driver = """


				if Access.Default != Access.None_:
				    raise SystemExit(1)
				if Access.Next != 1:
				    raise SystemExit(2)

				""";
			File.WriteAllText(Path.Combine(directory, "access.py"), source + driver);

			(int ran, string output) = ToolchainHarness.Run(python, "access.py", directory);
			Assert.AreEqual(0, ran, $"the generated module failed, or a sibling-named value read back wrongly (driver check {ran}):{Environment.NewLine}{output}{Environment.NewLine}{source}");
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
	/// The project the generated C# is built as: a program, so the driver can check what it reads back.
	/// </summary>
	private const string CSharpProject = """
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
	/// The C# driver. <c>@</c> is how a C# caller names a keyword, so it reads the escaped names back
	/// exactly as a user of the generated type would.
	/// </summary>
	private const string CSharpDriver = """
		public static class Program
		{
			public static int Main()
			{
				Holder holder = Holder.make();
				if (holder.@class != 9 || holder.@event != 4) { return 1; }
				if (holder.@default != 1) { return 2; }
				if (holder.total(5, 6) != 20) { return 3; }
				if ((int)Flag.@null != 0 || (int)Flag.All != 1) { return 4; }
				if (Globals.@return(7) != 7) { return 5; }
				if (Globals.widest() != int.MaxValue) { return 6; }
				return 0;
			}
		}
		""";

	/// <summary>
	/// Tests that C# writes a keyword-named field, property, parameter, local, enumeration member,
	/// function and designated member as a verbatim identifier, at their declarations and where they
	/// are used, and keeps a built-in type's keyword leading a member access as written.
	/// </summary>
	[TestMethod]
	public void CSharp_KeywordNamesCompileAndResolve()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(CSharpTypes());
			File.WriteAllText(Path.Combine(directory, "Holder.csproj"), CSharpProject);
			File.WriteAllText(Path.Combine(directory, "Program.cs"), CSharpDriver);
			File.WriteAllText(Path.Combine(directory, "Holder.cs"), source);

			(int ran, string output) = ToolchainHarness.Run("dotnet", "run -nologo", directory);
			Assert.AreEqual(0, ran, $"C# rejected the generated source, or a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a C# reference keeps the receiver and the literal words as written while escaping
	/// a keyword that names a member.
	/// </summary>
	[TestMethod]
	public void CSharp_ReferenceKeepsTheReceiverAndLiteralsAndEscapesTheMember()
	{
		FunctionDeclaration read = new("read") { ReturnType = "int" };
		read.Body.Add(new VariableDeclaration("unused", "object", new VariableReference("null")));
		read.Body.Add(new VariableDeclaration("empty", "string", new VariableReference("string.Empty")));
		read.Body.Add(new ReturnStatement(new VariableReference("this.base")));
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new VariableDeclaration("base", "int"));
		holder.Members.Add(read);
		SourceFile file = new("holder");
		file.Members.Add(holder);

		string source = new CSharpGenerator().Generate(file);

		Assert.Contains("int @base", source, StringComparison.Ordinal, source);
		Assert.Contains("return this.@base;", source, StringComparison.Ordinal, source);
		Assert.Contains("= null;", source, StringComparison.Ordinal, source);
		Assert.Contains("= string.Empty;", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("@this", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("@null", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("@string", source, StringComparison.Ordinal, source);
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
	/// Tests that Rust calls a keyword-named free function and method by the raw identifiers they
	/// were declared with: with no receiver, through <c>self</c>, and through a raw pointer, which is
	/// written <c>unsafe { (*p).callee() }</c>.
	/// </summary>
	[TestMethod]
	public void Rust_KeywordNamedCalleesCompileAndResolve()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "calls.exe" : "calls";
			SourceFile file = KeywordCalls("match", "loop", "self");
			FunctionDeclaration through = new("through") { ReturnType = "int" };
			through.Parameters.Add(new Parameter("other", "const Holder*"));
			through.Body.Add(new ReturnStatement(new CallExpression(new VariableReference("other"), "loop")));
			file.Members.Add(through);

			string generated = new RustGenerator().Generate(file);
			Assert.Contains("unsafe { (*other).r#loop() }", generated, StringComparison.Ordinal, generated);

			string source = generated + """

				fn main() {
				    let mut holder = Holder::make();
				    if relay() != 5 { std::process::exit(1); }
				    if holder.twice() != 3 { std::process::exit(2); }
				    if through(&holder) != 2 { std::process::exit(3); }
				}

				""";
			File.WriteAllText(Path.Combine(directory, "calls.rs"), source);

			(int built, string output) = ToolchainHarness.Run("rustc", $"--edition 2021 -o {program} calls.rs", directory);
			Assert.AreEqual(0, built, $"rustc rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a keyword-named callee resolved wrongly (driver check {ran}):{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that C++ calls a keyword-named free function and method by the suffixed names they were
	/// declared with, and that a reserved callee nothing declares, <c>sizeof</c>, stays as written.
	/// </summary>
	[TestMethod]
	public void Cpp_KeywordNamedCalleesCompileAndResolve()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CppCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "calls.exe" : "calls";
			SourceFile file = KeywordCalls("delete", "default", null);
			FunctionDeclaration width = new("width") { ReturnType = "int" };
			width.Parameters.Add(new Parameter("value", "int"));
			CallExpression size = new("sizeof");
			size.Arguments.Add(new VariableReference("value"));
			width.Body.Add(new ReturnStatement(size));
			file.Members.Add(width);

			string header = new CppGenerator().Generate(file);
			File.WriteAllText(Path.Combine(directory, "calls.hpp"), header);
			File.WriteAllText(Path.Combine(directory, "driver.cpp"), """
				#include "calls.hpp"

				int main()
				{
					Holder holder = Holder::make();
					if (relay() != 5) { return 1; }
					if (holder.twice() != 3) { return 2; }
					if (width(0) != static_cast<int>(sizeof(int))) { return 3; }
					return 0;
				}

				""");

			(int built, string output) = ToolchainHarness.Run(
				compiler,
				$"-std=c++20 -Wall -Wextra -pedantic -o {program} driver.cpp",
				directory);
			Assert.AreEqual(0, built, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{header}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a keyword-named callee resolved wrongly (driver check {ran}):{Environment.NewLine}{header}");
		});
	}

	/// <summary>
	/// Tests that Python calls a keyword-named free function and method by the suffixed names they
	/// were declared with.
	/// </summary>
	[TestMethod]
	public void Python_KeywordNamedCalleesRunAndResolve()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new PythonGenerator().Generate(KeywordCalls("lambda", "pass", "self")) + """


				holder = Holder()
				if relay() != 5:
				    raise SystemExit(1)
				if holder.twice() != 3:
				    raise SystemExit(2)

				""";
			File.WriteAllText(Path.Combine(directory, "calls.py"), source);

			(int ran, string output) = ToolchainHarness.Run(python, "calls.py", directory);
			Assert.AreEqual(0, ran, $"the generated module failed, or a keyword-named callee resolved wrongly (driver check {ran}):{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// The module the generated Go is compiled as. Nothing is required, so nothing is fetched.
	/// </summary>
	private const string GoModule = """
		module holder

		go 1.21

		""";

	/// <summary>
	/// The Go driver. Go has no escape for a keyword, so an escaped name has had an underscore
	/// appended, and that is the name a caller uses.
	/// </summary>
	private const string GoDriver = """
		package main

		import "os"

		func main() {
			holder := HolderMake()
			if holder.type_ != 3 || holder.range_ != 4 {
				os.Exit(1)
			}
			if holder.total(5, 6) != 14 {
				os.Exit(2)
			}
			if relay() != 5 {
				os.Exit(3)
			}
			if holder.twice() != 3 {
				os.Exit(4)
			}
			if map_ != 7 {
				os.Exit(5)
			}
		}

		""";

	/// <summary>
	/// Tests that Go appends an underscore to a keyword-named field, parameter, local, method,
	/// function, package-level variable and designated member, at their declarations and where they
	/// are used, and that the result is what <c>gofmt</c> writes.
	/// </summary>
	[TestMethod]
	public void Go_KeywordNamesCompileAndResolve()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new GoGenerator().Generate(GoTypes());
			File.WriteAllText(Path.Combine(directory, "go.mod"), GoModule);
			File.WriteAllText(Path.Combine(directory, "driver.go"), GoDriver);
			File.WriteAllText(Path.Combine(directory, "holder.go"), source);

			(int ran, string output) = ToolchainHarness.Run("go", "run .", directory);
			Assert.AreEqual(0, ran, $"go rejected the generated source, or a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{output}{Environment.NewLine}{source}");

			(int formatted, string differs) = ToolchainHarness.Run("gofmt", "-l holder.go", directory);
			Assert.AreEqual(0, formatted, $"gofmt did not run:{Environment.NewLine}{differs}");
			Assert.AreEqual(string.Empty, differs.Trim(), $"gofmt would rewrite the generated source:{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a Go reference keeps the receiver and the predeclared identifiers as written while
	/// escaping a keyword that names a member. <c>nil</c>, <c>true</c> and <c>len</c> are predeclared
	/// rather than reserved, so they are names like any other and are not escaped.
	/// </summary>
	[TestMethod]
	public void Go_ReferenceKeepsTheReceiverAndPredeclaredNamesAndEscapesTheMember()
	{
		FunctionDeclaration read = new("read") { ReturnType = "int" };
		read.Body.Add(new VariableDeclaration("unused", "bool", new VariableReference("true")));
		read.Body.Add(new ReturnStatement(new VariableReference("self.chan")));
		ClassDeclaration holder = new("Holder") { Kind = TypeDeclarationKind.Struct };
		holder.Members.Add(new VariableDeclaration("chan", "int"));
		holder.Members.Add(read);
		SourceFile file = new("holder");
		file.Members.Add(holder);

		string source = new GoGenerator().Generate(file);

		Assert.Contains("chan_ int", source, StringComparison.Ordinal, source);
		Assert.Contains("return self.chan_", source, StringComparison.Ordinal, source);
		Assert.Contains("= true", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("self_", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("true_", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Builds a struct, functions and a package-level variable whose names are Go keywords.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile GoTypes()
	{
		SourceFile file = KeywordCalls("func", "switch", "self");
		file.Name = "main";

		ClassDeclaration holder = file.Members.OfType<ClassDeclaration>().Single();
		holder.Members.Insert(0, new VariableDeclaration("type", "int"));
		holder.Members.Insert(1, new VariableDeclaration("range", "int"));

		ConstructionExpression built = holder.Members.OfType<FunctionDeclaration>().Single(function => function.Name == "make")
			.Body.OfType<ReturnStatement>().Single().Expression as ConstructionExpression
			?? throw new InvalidOperationException("make no longer returns a construction.");
		built.Arguments.Add(new MemberInitialiser("type", new LiteralExpression<int>(3)));
		built.Arguments.Add(new MemberInitialiser("range", new LiteralExpression<int>(4)));

		FunctionDeclaration total = new("total") { ReturnType = "int" };
		total.Parameters.Add(new Parameter("chan", "int"));
		total.Parameters.Add(new Parameter("select", "int"));
		total.Body.Add(new VariableDeclaration(
			"go",
			"int",
			new BinaryExpression(new VariableReference("chan"), BinaryOperator.Add, new VariableReference("select"))));
		total.Body.Add(new VariableDeclaration(
			"defer",
			"int",
			new BinaryExpression(new VariableReference("go"), BinaryOperator.Add, new VariableReference("self.type"))));
		total.Body.Add(new ReturnStatement(new VariableReference("defer")));
		holder.Members.Add(total);

		file.Members.Add(new FieldDeclaration("map", new TypeReference("int")) { InitialValue = new LiteralExpression<int>(7) });
		return file;
	}

	/// <summary>
	/// Builds a free function and a method named after keywords, and the generated code that calls
	/// each of them: <c>relay</c> calls the free function, and <c>twice</c> calls the method on its
	/// own instance.
	/// </summary>
	/// <param name="function">The free function's name.</param>
	/// <param name="method">The method's name.</param>
	/// <param name="receiver">How the target's method body names its own instance, or null to call the method with no receiver.</param>
	/// <returns>The file to generate.</returns>
	private static SourceFile KeywordCalls(string function, string method, string? receiver)
	{
		ClassDeclaration holder = new("Holder") { Kind = TypeDeclarationKind.Struct };
		holder.Members.Add(new VariableDeclaration("seed", "int"));

		ConstructionExpression built = new(new TypeReference("Holder"));
		built.Arguments.Add(new MemberInitialiser("seed", new LiteralExpression<int>(0)));
		FunctionDeclaration make = new("make") { ReturnType = "Holder", IsStatic = true };
		make.Body.Add(new ReturnStatement(built));
		holder.Members.Add(make);

		FunctionDeclaration keywordMethod = new(method) { ReturnType = "int", IsReadOnly = true };
		keywordMethod.Body.Add(new ReturnStatement(new LiteralExpression<int>(2)));
		holder.Members.Add(keywordMethod);

		CallExpression callMethod = receiver is null ? new(method) : new(new VariableReference(receiver), method);
		FunctionDeclaration twice = new("twice") { ReturnType = "int" };
		twice.Body.Add(new ReturnStatement(new BinaryExpression(callMethod, BinaryOperator.Add, new LiteralExpression<int>(1))));
		holder.Members.Add(twice);

		FunctionDeclaration keywordFunction = new(function) { ReturnType = "int" };
		keywordFunction.Parameters.Add(new Parameter("value", "int"));
		keywordFunction.Body.Add(new ReturnStatement(new VariableReference("value")));

		CallExpression callFunction = new(function);
		callFunction.Arguments.Add(new LiteralExpression<int>(5));
		FunctionDeclaration relay = new("relay") { ReturnType = "int" };
		relay.Body.Add(new ReturnStatement(callFunction));

		SourceFile file = new("calls");
		file.Members.Add(holder);
		file.Members.Add(keywordFunction);
		file.Members.Add(relay);
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
	/// Builds a class, an enumeration and functions whose names are C# keywords.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile CSharpTypes()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new VariableDeclaration("class", "int", new LiteralExpression<int>(3)) { Visibility = Visibility.Public });
		holder.Members.Add(new VariableDeclaration("event", "int", new LiteralExpression<int>(4)) { Visibility = Visibility.Public });
		holder.Members.Add(new PropertyDeclaration("default", "int")
		{
			GetterBody = { new ReturnStatement(new LiteralExpression<int>(1)) },
		});

		ConstructionExpression built = new(new TypeReference("Holder"));
		built.Arguments.Add(new MemberInitialiser("class", new LiteralExpression<int>(9)));
		FunctionDeclaration make = new("make") { ReturnType = "Holder", IsStatic = true };
		make.Body.Add(new ReturnStatement(built));
		holder.Members.Add(make);

		FunctionDeclaration total = new("total") { ReturnType = "int" };
		total.Parameters.Add(new Parameter("in", "int"));
		total.Parameters.Add(new Parameter("out", "int"));
		total.Body.Add(new VariableDeclaration(
			"lock",
			"int",
			new BinaryExpression(new VariableReference("in"), BinaryOperator.Add, new VariableReference("out"))));
		total.Body.Add(new VariableDeclaration(
			"fixed",
			"int",
			new BinaryExpression(new VariableReference("lock"), BinaryOperator.Add, new VariableReference("this.class"))));
		total.Body.Add(new ReturnStatement(new VariableReference("fixed")));
		holder.Members.Add(total);

		EnumDeclaration flag = new("Flag");
		flag.Members.Add(new EnumMember("null"));
		flag.Members.Add(new EnumMember("All"));

		FunctionDeclaration identity = new("return") { ReturnType = "int" };
		identity.Parameters.Add(new Parameter("params", "int"));
		identity.Body.Add(new ReturnStatement(new VariableReference("params")));

		FunctionDeclaration widest = new("widest") { ReturnType = "int" };
		widest.Body.Add(new ReturnStatement(new VariableReference("int.MaxValue")));

		SourceFile file = new("holder");
		file.Members.Add(holder);
		file.Members.Add(flag);
		file.Members.Add(identity);
		file.Members.Add(widest);
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

	/// <summary>
	/// Tests that C appends an underscore to a keyword-named field, parameter, local and function,
	/// at their declarations and where they are used.
	/// </summary>
	[TestMethod]
	public void C_KeywordNamesCompileAndResolve()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", "cc", "gcc", "clang");
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		ClassDeclaration holder = new("Holder") { Kind = TypeDeclarationKind.Struct };
		holder.Members.Add(new VariableDeclaration("auto", "int"));
		holder.Members.Add(new VariableDeclaration("register", "int"));

		ConstructionExpression built = new(new TypeReference("Holder"));
		built.Arguments.Add(new MemberInitialiser("auto", new LiteralExpression<int>(3)));
		built.Arguments.Add(new MemberInitialiser("register", new LiteralExpression<int>(4)));
		FunctionDeclaration make = new("make") { ReturnType = "Holder" };
		make.Body.Add(new ReturnStatement(built));

		FunctionDeclaration sum = new("for") { ReturnType = "int" };
		sum.Parameters.Add(new Parameter("do", "int"));
		sum.Parameters.Add(new Parameter("case", "int"));
		sum.Body.Add(new VariableDeclaration(
			"goto",
			"int",
			new BinaryExpression(new VariableReference("do"), BinaryOperator.Add, new VariableReference("case"))));
		sum.Body.Add(new VariableDeclaration("unused", "bool", new VariableReference("true")));
		sum.Body.Add(new ReturnStatement(new VariableReference("goto")));

		CallExpression callSum = new("for");
		callSum.Arguments.Add(new LiteralExpression<int>(5));
		callSum.Arguments.Add(new LiteralExpression<int>(6));
		FunctionDeclaration relay = new("relay") { ReturnType = "int" };
		relay.Body.Add(new ReturnStatement(callSum));

		SourceFile file = new("keywords");
		file.Members.Add(holder);
		file.Members.Add(make);
		file.Members.Add(sum);
		file.Members.Add(relay);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "keywords.exe" : "keywords";
			string header = new CGenerator().Generate(file);
			Assert.DoesNotContain("true_", header, StringComparison.Ordinal, header);
			File.WriteAllText(Path.Combine(directory, "keywords.h"), header);
			File.WriteAllText(Path.Combine(directory, "driver.c"), """
				#include <stdbool.h>
				#include "keywords.h"

				int main(void)
				{
					Holder holder = make();
					if (holder.auto_ != 3 || holder.register_ != 4) { return 1; }
					if (for_(5, 6) != 11) { return 2; }
					if (relay() != 11) { return 3; }
					return 0;
				}

				""");

			(int built, string output) = ToolchainHarness.Run(
				compiler,
				$"-std=c11 -Wall -Wextra -Wno-unused-variable -Wno-unused-but-set-variable -pedantic-errors -o {program} driver.c",
				directory);
			Assert.AreEqual(0, built, $"{compiler} rejected the generated header:{Environment.NewLine}{output}{Environment.NewLine}{header}");

			(int ran, _) = ToolchainHarness.Run(Path.Combine(directory, program), string.Empty, directory);
			Assert.AreEqual(0, ran, $"a keyword-named member read back wrongly (driver check {ran}):{Environment.NewLine}{header}");
		});
	}
}
