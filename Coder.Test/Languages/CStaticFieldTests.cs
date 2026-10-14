// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a static field of a type is written beside the C struct rather than inside it.
/// </summary>
/// <remarks>
/// C has no static data member. Written inside the struct, a static field became one copy per
/// instance, lost its initial value and changed the struct's size, and a static constant table became
/// a flexible array member that does not compile anywhere but last (issue #167).
/// </remarks>
[TestClass]
public class CStaticFieldTests
{
	private static readonly string[] Compilers = ["cc", "gcc", "clang"];

	private const string Driver = """
		#include <stdio.h>
		#include "table.h"

		int main(void)
		{
			Table one = { 4 };
			Table_instances += 1;
			printf("%d %d %d %d %d\n", Table_primes[0], Table_primes[1], Table_instances, one.count, (int)(sizeof(Table) == sizeof(int)));
			return 0;
		}

		""";

	/// <summary>
	/// Tests that the static fields leave the struct body and are declared at file scope as
	/// <c>Type_member</c>, and that the instance field stays a member.
	/// </summary>
	[TestMethod]
	public void StaticFields_AreDeclaredBesideTheStruct()
	{
		string code = new CGenerator().Generate(Table());

		StringAssert.Contains(code, "static const int Table_primes[] = { 2, 3 };", StringComparison.Ordinal);
		StringAssert.Contains(code, "static int Table_instances = 0;", StringComparison.Ordinal);

		string body = code[code.IndexOf("typedef struct Table", StringComparison.Ordinal)..code.IndexOf("} Table;", StringComparison.Ordinal)];
		StringAssert.Contains(body, "int count;", StringComparison.Ordinal);
		Assert.DoesNotContain("primes", body, StringComparison.Ordinal, code);
		Assert.DoesNotContain("instances", body, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that an instance constant keeps its member and note, since only a static field leaves.
	/// </summary>
	[TestMethod]
	public void AnInstanceConstant_StaysAMember()
	{
		ClassDeclaration type = new("Limits");
		type.Members.Add(new FieldDeclaration("max", new TypeReference("int")) { IsConstant = true, InitialValue = new LiteralExpression<int>(9) });

		string code = new CGenerator().Generate(type);

		StringAssert.Contains(code, "int max;  // constant, 9", StringComparison.Ordinal);
		Assert.DoesNotContain("Limits_max", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a type holding a static constant table before an instance field compiles as strict
	/// C, and that the statics keep their values and stay out of the struct's size.
	/// </summary>
	[TestMethod]
	public void StaticFields_CompileAndKeepTheirValues()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", Compilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CGenerator().Generate(Table());
			File.WriteAllText(Path.Combine(directory, "table.h"), source);
			File.WriteAllText(Path.Combine(directory, "driver.c"), Driver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c11 -Wall -Wextra -pedantic-errors -Wno-unused-variable driver.c -o table",
				directory);
			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");

			(int ran, string printed) = ToolchainHarness.Run(Path.Combine(directory, "table"), string.Empty, directory);
			Assert.AreEqual(0, ran, printed);
			Assert.AreEqual("2 3 1 4 1", printed.Trim(), source);
		});
	}

	/// <summary>
	/// The issue's reproduction: a static constant table, a static counter and an instance field.
	/// </summary>
	private static SourceFile Table()
	{
		ClassDeclaration table = new("Table");

		ConstructionExpression primes = new();
		primes.Arguments.Add(new LiteralExpression<int>(2));
		primes.Arguments.Add(new LiteralExpression<int>(3));
		table.Members.Add(new FieldDeclaration("primes", new TypeReference("int") { IsArray = true })
		{
			IsStatic = true,
			IsConstant = true,
			InitialValue = primes,
		});
		table.Members.Add(new FieldDeclaration("instances", new TypeReference("int")) { IsStatic = true, InitialValue = new LiteralExpression<int>(0) });
		table.Members.Add(new FieldDeclaration("count", new TypeReference("int")));

		SourceFile file = new("table") { IsHeader = true };
		file.Members.Add(table);
		return file;
	}
}
