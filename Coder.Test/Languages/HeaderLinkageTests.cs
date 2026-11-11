// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a generated C or C++ header can be included by more than one translation unit of a
/// program.
/// </summary>
/// <remarks>
/// <c>#pragma once</c> keeps a header to once per translation unit and no further, so a free
/// function, a function a method is lowered to, or a mutable variable defined in it with external
/// linkage is defined once in each unit, and the program fails to link with "multiple definition"
/// (issue #423). C++ now writes them <c>inline</c>; C writes a function <c>static inline</c> and a
/// variable <c>static</c>, one copy per unit, since C has no inline variable.
/// </remarks>
[TestClass]
public class HeaderLinkageTests
{
	private static readonly string[] CCompilers = ["cc", "gcc", "clang"];

	private static readonly string[] CppCompilers = ["c++", "g++", "clang++"];

	/// <summary>
	/// Tests that C writes a header's functions <c>static inline</c> and its mutable variables
	/// <c>static</c>.
	/// </summary>
	[TestMethod]
	public void C_HeaderDefinitions_HaveInternalLinkage()
	{
		string header = new CGenerator().Generate(Header());

		StringAssert.Contains(header, "static int limit = 5;", StringComparison.Ordinal);
		StringAssert.Contains(header, "static inline int twice(int x)", StringComparison.Ordinal);
		StringAssert.Contains(header, "static inline int P_get(P* self)", StringComparison.Ordinal);
		StringAssert.Contains(header, "static inline int P_make(void)", StringComparison.Ordinal);
		StringAssert.Contains(header, "static int scale = 8;", StringComparison.Ordinal);
		StringAssert.Contains(header, "static inline int half(int x)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that C++ writes a header's free functions and namespace-scope variables <c>inline</c>,
	/// and leaves a class's members as they were.
	/// </summary>
	[TestMethod]
	public void Cpp_HeaderDefinitions_AreInline()
	{
		string header = new CppGenerator().Generate(Header());

		StringAssert.Contains(header, "inline int limit = 5;", StringComparison.Ordinal);
		StringAssert.Contains(header, "inline int twice(int x)", StringComparison.Ordinal);
		StringAssert.Contains(header, "inline int scale = 8;", StringComparison.Ordinal);
		StringAssert.Contains(header, "inline int half(int x)", StringComparison.Ordinal);
		StringAssert.Contains(header, "    int get()", StringComparison.Ordinal);
		StringAssert.Contains(header, "    static int make()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a file that is not a header is written as it always was.
	/// </summary>
	[TestMethod]
	public void ASourceFileThatIsNotAHeader_IsUnchanged()
	{
		SourceFile file = Header();
		file.IsHeader = false;

		string c = new CGenerator().Generate(file);
		string cpp = new CppGenerator().Generate(file);

		Assert.DoesNotContain("inline", c, StringComparison.Ordinal, c);
		Assert.DoesNotContain("static int limit", c, StringComparison.Ordinal, c);
		Assert.DoesNotContain("static int scale", c, StringComparison.Ordinal, c);
		StringAssert.Contains(c, "int twice(int x)", StringComparison.Ordinal);
		StringAssert.Contains(cpp, "int limit = 5;", StringComparison.Ordinal);
		Assert.DoesNotContain("inline int", cpp, StringComparison.Ordinal, cpp);
	}

	/// <summary>
	/// Tests that a C header included from two translation units compiles, links and runs.
	/// </summary>
	[TestMethod]
	public void C_HeaderIncludedTwice_Links()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		const string first = """
			#include <stdio.h>
			#include "lib.h"

			int other(void);

			int main(void)
			{
				P p = { 1 };
				printf("%d %d %d %d %d %d\n", limit, twice(3), P_get(&p), P_make(), half(scale), other());
				return 0;
			}

			""";

		const string second = """
			#include "lib.h"

			int other(void)
			{
				P p = { 2 };
				return twice(limit) + P_get(&p) + P_make() + half(scale) + P_count;
			}

			""";

		LinkTwoUnits(compiler, new CGenerator().Generate(Header()), "lib.h", ("a.c", first), ("b.c", second), "-std=c11 -Wall -Wextra -pedantic-errors");
	}

	/// <summary>
	/// Tests that a C++ header included from two translation units compiles, links and runs.
	/// </summary>
	[TestMethod]
	public void Cpp_HeaderIncludedTwice_Links()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", CppCompilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C++ compiler on the path, so nothing was compiled.");
			return;
		}

		const string first = """
			#include <cstdio>
			#include "lib.h"

			int other();

			int main()
			{
				P p{};
				std::printf("%d %d %d %d %d %d\n", limit, twice(3), p.get(), P::make(), geo::half(geo::scale), other());
				return 0;
			}

			""";

		const string second = """
			#include "lib.h"

			int other()
			{
				P p{};
				return twice(limit) + p.get() + P::make() + geo::half(geo::scale) + P::count;
			}

			""";

		LinkTwoUnits(compiler, new CppGenerator().Generate(Header()), "lib.h", ("a.cpp", first), ("b.cpp", second), "-std=c++17 -Wall -Wextra -pedantic-errors");
	}

	/// <summary>
	/// The issue's reproduction, widened to every kind of definition a header can hold: a mutable
	/// variable and a function at file scope, a struct with an instance method, a static method and a
	/// static field, and a namespace holding a variable and a function.
	/// </summary>
	/// <returns>The header.</returns>
	private static SourceFile Header()
	{
		SourceFile file = new("lib") { IsHeader = true };
		file.Members.Add(new FieldDeclaration("limit", "int") { InitialValue = new LiteralExpression<int>(5) });
		file.Members.Add(Function("twice", BinaryOperator.Multiply));

		ClassDeclaration point = new("P") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new FieldDeclaration("v", "int") { Visibility = Visibility.Public });
		point.Members.Add(new FieldDeclaration("count", "int") { IsStatic = true, Visibility = Visibility.Public, InitialValue = new LiteralExpression<int>(0) });
		FunctionDeclaration get = new("get") { ReturnType = "int", Visibility = Visibility.Public };
		get.Body.Add(new ReturnStatement(new LiteralExpression<int>(3)));
		point.Members.Add(get);
		FunctionDeclaration make = new("make") { ReturnType = "int", Visibility = Visibility.Public, IsStatic = true };
		make.Body.Add(new ReturnStatement(new LiteralExpression<int>(4)));
		point.Members.Add(make);
		file.Members.Add(point);

		NamespaceDeclaration geo = new("geo");
		geo.Members.Add(new FieldDeclaration("scale", "int") { InitialValue = new LiteralExpression<int>(8) });
		geo.Members.Add(Function("half", BinaryOperator.Divide));
		file.Members.Add(geo);
		return file;
	}

	/// <summary>
	/// A function of one <c>int</c> that applies an operator to it and two.
	/// </summary>
	/// <param name="name">The function's name.</param>
	/// <param name="op">The operator.</param>
	/// <returns>The function.</returns>
	private static FunctionDeclaration Function(string name, BinaryOperator op)
	{
		FunctionDeclaration function = new(name) { ReturnType = "int" };
		function.Parameters.Add(new Parameter("x", "int"));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), op, new LiteralExpression<int>(2))));
		return function;
	}

	/// <summary>
	/// Compiles two translation units that both include a header, links them, and runs the result.
	/// </summary>
	/// <param name="compiler">The compiler driver.</param>
	/// <param name="header">The generated header.</param>
	/// <param name="headerName">The name the units include it by.</param>
	/// <param name="first">The unit holding <c>main</c>.</param>
	/// <param name="second">The other unit.</param>
	/// <param name="flags">The dialect and warning flags.</param>
	private static void LinkTwoUnits(string compiler, string header, string headerName, (string Name, string Source) first, (string Name, string Source) second, string flags)
	{
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, headerName), header);
			File.WriteAllText(Path.Combine(directory, first.Name), first.Source);
			File.WriteAllText(Path.Combine(directory, second.Name), second.Source);

			(int exitCode, string output) = ToolchainHarness.Run(compiler, $"{flags} {first.Name} {second.Name} -o linked", directory);
			Assert.AreEqual(0, exitCode, $"{compiler} could not build the two units:{Environment.NewLine}{header}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "linked"), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("5 6 3 4 4 21", printed.Trim(), header);
		});
	}
}
