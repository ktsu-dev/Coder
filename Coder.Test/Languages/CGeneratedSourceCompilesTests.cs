// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles what <see cref="CGenerator"/> writes, with a real C compiler.
/// </summary>
/// <remarks>
/// Every other test here pins a spelling, and a spelling can be pinned and still be wrong: C's rules
/// about what may initialise an object with static storage duration, what an empty parameter list
/// declares, and what a file-scope <c>const</c> links as are not visible in the text at all. The only
/// thing that knows them is a compiler.
/// <para>
/// The header is included twice on purpose. That is what a header is for, and it is what makes
/// <c>#pragma once</c> and the <c>static const</c> spelling of a constant load-bearing rather than
/// stylistic.
/// </para>
/// <para>
/// The test is inconclusive rather than failing where no compiler is on the path, which is the
/// honest result: nothing was checked. A hosted Linux or macOS runner has one; a Windows one usually
/// does not, and the rest of the suite covers what the text says there.
/// </para>
/// </remarks>
[TestClass]
public class CGeneratedSourceCompilesTests
{
	/// <summary>
	/// The compilers to look for, in the order a C project would.
	/// </summary>
	private static readonly string[] Compilers = ["cc", "gcc", "clang"];

	/// <summary>
	/// The consumer of the generated header, written the way a person would write one.
	/// </summary>
	/// <remarks>
	/// It uses every declaration the header makes, so a declaration that compiles on its own but
	/// cannot be used — a constant that is not a constant expression, a function pointer whose
	/// signature does not match what implements it — fails here rather than passing quietly.
	/// </remarks>
	private const string Driver = """
		#include "exemplar.h"
		#include "exemplar.h"

		static double circle_area(const void* self)
		{
			(void)self;
			return 1.0;
		}

		static void circle_draw(void* self, double scale)
		{
			(void)self;
			(void)scale;
		}

		int main(void)
		{
			Point built = Point_create(1, 2);
			Point zeroed = Point_zero();
			Origin first = ORIGINS[0];
			Shape shape = { .draw = circle_draw, .area = circle_area };
			Colour colour = Colour_Green;

			shape.draw(&built, 2.0);

			return (built.x + zeroed.y + first.y + (int)shape.area(&first) + (int)colour) * 0;
		}

		""";

	/// <summary>
	/// Tests that a header holding every kind of declaration the generator writes compiles, and that
	/// a translation unit including it twice compiles too.
	/// </summary>
	[TestMethod]
	public void GeneratedHeader_Compiles()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", Compilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "exemplar.h"),
				new CGenerator().Generate(Exemplar()));
			File.WriteAllText(Path.Combine(directory, "driver.c"), Driver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c11 -Wall -Wextra -pedantic -c driver.c -o driver.o",
				directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated header:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// The consumer of the call header, which only has to reach the one function in it.
	/// </summary>
	private const string CallDriver = """
		#include "calls.h"

		int main(void)
		{
			return Counter_span();
		}

		""";

	/// <summary>
	/// Tests that a call whose receiver C moves into the first argument reaches the member function
	/// the same generator emitted for it.
	/// </summary>
	/// <remarks>
	/// This is the one thing about <see cref="CallExpression"/> that a pinned spelling cannot settle.
	/// C lowers a member function to a free function taking a pointer to the instance, so the call
	/// site has to take the receiver's address for the two to meet — and whether they meet is a
	/// question about types, which is not visible in the text. A compiler is the only thing that
	/// knows the answer.
	/// </remarks>
	[TestMethod]
	public void ACallWithAReceiver_ReachesTheFunctionItLowersTo()
	{
		string? compiler = ToolchainHarness.FindOnPath("--version", Compilers);
		if (compiler is null)
		{
			Assert.Inconclusive("No C compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(
				Path.Combine(directory, "calls.h"),
				new CGenerator().Generate(CallExemplar()));
			File.WriteAllText(Path.Combine(directory, "driver.c"), CallDriver);

			(int exitCode, string output) = ToolchainHarness.Run(
				compiler,
				"-std=c11 -Wall -Wextra -pedantic -c driver.c -o driver.o",
				directory);

			Assert.AreEqual(0, exitCode, $"{compiler} rejected the generated calls:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Builds a header whose one static member calls a member function on a local instance, both as
	/// a statement and for its value.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile CallExemplar()
	{
		SourceFile file = new("calls") { IsHeader = true };
		file.HeaderComment.Add("Generated by Coder. Do not edit.");

		ClassDeclaration counter = new("Counter") { Kind = TypeDeclarationKind.Struct };
		counter.Documentation.Add("Something with a member function to call.");
		counter.Members.Add(new VariableDeclaration("count", "int"));

		// The member the call has to reach. C writes it as Counter_value(const Counter* self).
		FunctionDeclaration value = new("value") { ReturnType = "int", IsReadOnly = true };
		value.Body.Add(new ReturnStatement(0));
		counter.Members.Add(value);

		ConstructionExpression zero = new(new TypeReference("Counter"));
		zero.Arguments.Add(new MemberInitialiser("count") { Value = new LiteralExpression<int>(0) });

		FunctionDeclaration span = new("span") { ReturnType = "int", IsStatic = true };
		span.Body.Add(new VariableDeclaration("here", "Counter", zero));

		// Once for its effect and once for its value, which are the two shapes the node exists for.
		span.Body.Add(new ExpressionStatement(
			new CallExpression(new VariableReference("here"), "Counter_value")));
		span.Body.Add(new ReturnStatement(
			new CallExpression(new VariableReference("here"), "Counter_value")));
		counter.Members.Add(span);

		file.Members.Add(counter);
		return file;
	}

	/// <summary>
	/// Builds a header holding one of everything the generator has a spelling for.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile Exemplar()
	{
		SourceFile file = new("exemplar") { IsHeader = true };
		file.HeaderComment.Add("Generated by Coder. Do not edit.");
		file.Imports.Add("<stdbool.h>");

		file.Members.Add(CompiledExemplar.Colour());
		file.Members.Add(CompiledExemplar.Point());
		file.Members.Add(CompiledExemplar.Shape());
		file.Members.Add(CompiledExemplar.OriginAlias());
		file.Members.Add(CompiledExemplar.OriginTable());
		file.Members.Add(new CompileTimeAssertion
		{
			Condition = "sizeof(Point) == 2 * sizeof(int)",
			Message = "Point must stay two ints",
		});

		return file;
	}
}
