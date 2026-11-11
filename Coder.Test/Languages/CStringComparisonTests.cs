// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that C compares two strings by their text, with <c>strcmp</c>, rather than by address.
/// </summary>
/// <remarks>
/// The AST's <c>==</c> on strings means the same text, and every other target compares it that way.
/// C's <c>==</c> on two <c>const char*</c> compiles, usually without a warning, and compares the
/// addresses, so the same AST gave a different answer in C (#239).
/// </remarks>
[TestClass]
public class CStringComparisonTests
{
	/// <summary>
	/// The compilers to look for, in the order a C project would.
	/// </summary>
	private static readonly string[] Compilers = ["cc", "gcc", "clang"];

	/// <summary>
	/// The consumer of the generated header. Each argument is built in a buffer, so no comparison can
	/// pass by two equal literals happening to share an address.
	/// </summary>
	private const string Driver = """
		#include <stdio.h>
		#include <string.h>
		#include "compare.h"

		int main(void)
		{
			char admin[16];
			char apple[16];
			char banana[16];
			strcpy(admin, "admin");
			strcpy(apple, "apple");
			strcpy(banana, "banana");

			int failures = 0;
			if (!is_admin(admin)) { puts("is_admin(\"admin\") was false"); failures++; }
			if (is_admin(apple)) { puts("is_admin(\"apple\") was true"); failures++; }
			if (!same(apple, "apple")) { puts("same(apple, apple) was false"); failures++; }
			if (!differ(apple, banana)) { puts("differ(apple, banana) was false"); failures++; }
			if (!before(apple, banana)) { puts("before(apple, banana) was false"); failures++; }
			if (before(banana, apple)) { puts("before(banana, apple) was true"); failures++; }
			if (!at_most(apple, "apple")) { puts("at_most(apple, apple) was false"); failures++; }
			if (!after(banana, apple)) { puts("after(banana, apple) was false"); failures++; }
			if (!at_least(banana, "banana")) { puts("at_least(banana, banana) was false"); failures++; }
			return failures;
		}

		""";

	/// <summary>
	/// Tests the issue's own example: a string parameter compared with a string literal.
	/// </summary>
	[TestMethod]
	public void EqualStrings_CompareWithStrcmp()
	{
		string code = new CGenerator().Generate(IsAdmin());

		StringAssert.Contains(code, "return (strcmp(name, \"admin\") == 0);", StringComparison.Ordinal, code);
		Assert.DoesNotContain("name == \"admin\"", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that each comparison operator on two strings compares <c>strcmp</c>'s result with zero.
	/// </summary>
	[TestMethod]
	public void EveryComparison_ComparesStrcmpWithZero()
	{
		string code = new CGenerator().Generate(Exemplar());

		StringAssert.Contains(code, "return (strcmp(a, b) == 0);", StringComparison.Ordinal, code);
		StringAssert.Contains(code, "return (strcmp(a, b) != 0);", StringComparison.Ordinal, code);
		StringAssert.Contains(code, "return (strcmp(a, b) < 0);", StringComparison.Ordinal, code);
		StringAssert.Contains(code, "return (strcmp(a, b) <= 0);", StringComparison.Ordinal, code);
		StringAssert.Contains(code, "return (strcmp(a, b) > 0);", StringComparison.Ordinal, code);
		StringAssert.Contains(code, "return (strcmp(a, b) >= 0);", StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a file comparing strings includes the header that declares <c>strcmp</c>, and that
	/// one that does not leaves it out.
	/// </summary>
	[TestMethod]
	public void StringHeader_IsIncludedOnlyWhereStringsAreCompared()
	{
		StringAssert.Contains(new CGenerator().Generate(Exemplar()), "#include <string.h>", StringComparison.Ordinal);

		SourceFile numbers = new("numbers") { IsHeader = true };
		numbers.Imports.Add("<stdbool.h>");
		numbers.Members.Add(Compare("same_number", "int", BinaryOperator.Equal));
		string code = new CGenerator().Generate(numbers);

		Assert.DoesNotContain("<string.h>", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("strcmp", code, StringComparison.Ordinal, code);
		StringAssert.Contains(code, "return (a == b);", StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a string compared with an operand of unknown type, such as <c>NULL</c>, is still a
	/// pointer comparison: <c>strcmp</c> on a null pointer is undefined.
	/// </summary>
	[TestMethod]
	public void StringComparedWithAnUntypedOperand_StaysAPointerComparison()
	{
		FunctionDeclaration isNull = new("is_null") { ReturnType = "bool", IsStatic = true };
		isNull.Parameters.Add(new Parameter("name", "string"));
		isNull.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("name"), BinaryOperator.Equal, new VariableReference("NULL"))));

		string code = new CGenerator().Generate(isNull);

		StringAssert.Contains(code, "return (name == NULL);", StringComparison.Ordinal, code);
		Assert.DoesNotContain("strcmp", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that the comparisons compile and give the answer every other target gives, for strings
	/// that are equal in text but held at different addresses.
	/// </summary>
	[TestMethod]
	public void StringComparisons_CompileAndCompareText()
	{
		string header = new CGenerator().Generate(Exemplar());

		string? compiler = ToolchainHarness.FindOnPath("--version", Compilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "compare.h"), header);
			File.WriteAllText(Path.Combine(directory, "driver.c"), Driver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c11 -Wall -Wextra -Wno-unused-function -pedantic -Werror driver.c -o driver",
				directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated comparisons:{Environment.NewLine}{output}{Environment.NewLine}{header}");

			(exitCode, output) = ToolchainHarness.Run(Path.Combine(directory, "driver"), string.Empty, directory);

			Assert.AreEqual(0, exitCode, $"A string comparison gave the wrong answer:{Environment.NewLine}{output}{Environment.NewLine}{header}");
		});
	}

	/// <summary>
	/// Builds the issue's example: <c>bool is_admin(string name) { return name == "admin"; }</c>.
	/// </summary>
	/// <returns>The function.</returns>
	private static FunctionDeclaration IsAdmin()
	{
		FunctionDeclaration isAdmin = new("is_admin") { ReturnType = "bool", IsStatic = true };
		isAdmin.Parameters.Add(new Parameter("name", "string"));
		isAdmin.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("name"), BinaryOperator.Equal, Literal.Text("admin"))));
		return isAdmin;
	}

	/// <summary>
	/// Builds a function comparing its two parameters with one operator.
	/// </summary>
	/// <param name="name">The function's name.</param>
	/// <param name="type">The parameters' type.</param>
	/// <param name="op">The comparison.</param>
	/// <returns>The function.</returns>
	private static FunctionDeclaration Compare(string name, string type, BinaryOperator op)
	{
		FunctionDeclaration function = new(name) { ReturnType = "bool", IsStatic = true };
		function.Parameters.Add(new Parameter("a", type));
		function.Parameters.Add(new Parameter("b", type));
		function.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("a"), op, new VariableReference("b"))));
		return function;
	}

	/// <summary>
	/// Builds a header holding the issue's example and one function per comparison operator.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile Exemplar()
	{
		SourceFile file = new("compare") { IsHeader = true };
		file.Imports.Add("<stdbool.h>");
		file.Members.Add(IsAdmin());
		file.Members.Add(Compare("same", "string", BinaryOperator.Equal));
		file.Members.Add(Compare("differ", "string", BinaryOperator.NotEqual));
		file.Members.Add(Compare("before", "string", BinaryOperator.LessThan));
		file.Members.Add(Compare("at_most", "string", BinaryOperator.LessThanOrEqual));
		file.Members.Add(Compare("after", "string", BinaryOperator.GreaterThan));
		file.Members.Add(Compare("at_least", "string", BinaryOperator.GreaterThanOrEqual));
		return file;
	}
}
