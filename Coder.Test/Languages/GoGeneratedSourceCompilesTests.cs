// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles what <see cref="GoGenerator"/> writes, with a real Go toolchain, and checks that it is
/// already what <c>gofmt</c> would write.
/// </summary>
/// <remarks>
/// Go refuses things the other two only grumble about, and each of them is invisible in the text: an
/// import nothing uses and a local nothing reads are errors rather than warnings, a method declared
/// twice under one name is an error, and a <c>const</c> holding anything the compiler cannot
/// evaluate is an error. A test that pinned the spelling would pass on all four.
/// <para>
/// The formatting check is the one no other generator here can have, and it is worth more than it
/// looks. Go has a single formatter that everybody runs, so "what <c>gofmt</c> would write" is not a
/// style this generator picked but the only spelling the file has — and a generated file that fails
/// it produces a diff the first time anybody opens it. Asserting it also pins the two rules
/// <c>gofmt</c> has that nothing else here does: the tab, and the columns of a struct's fields and a
/// constant block lining up.
/// </para>
/// <para>
/// The driver is a second file in the same package, written the way a person would write one, and it
/// uses every declaration the generated file makes — so a declaration that compiles on its own but
/// cannot be used fails here rather than passing quietly. It is what proves the two mappings that
/// are only claims otherwise: that <c>Circle</c> satisfies <c>Shape</c> without saying so, which is
/// the whole of what a Go interface is, and that an embedded <c>Point</c> answers <c>Sum</c>, which
/// is the whole of what Go has in place of inheritance.
/// </para>
/// <para>
/// The test is inconclusive rather than failing where no toolchain is on the path, which is the
/// honest result: nothing was checked.
/// </para>
/// </remarks>
[TestClass]
public class GoGeneratedSourceCompilesTests
{
	/// <summary>
	/// The module the generated file is compiled as.
	/// </summary>
	/// <remarks>
	/// Go compiles a module rather than a file, and a directory without one of these is not a module
	/// however much Go source is in it. Nothing is required, so nothing is fetched.
	/// </remarks>
	private const string Module = """
		module exemplar

		go 1.21

		""";

	/// <summary>
	/// The consumer of the generated package, written the way a person would write one.
	/// </summary>
	private const string Driver = """
		package exemplar

		// Circle satisfies Shape by having the methods, and says so nowhere — which is the one thing
		// Go's interfaces do that no other target here does at all.
		func (self *Circle) draw(scale float64) { _ = scale }

		func (self Circle) area() float64 { return self.radius }

		func use() int {
			built := NewPoint(1, 2)
			zeroed := PointZero()
			first := ORIGINS[0]
			var shape Shape = &Circle{}
			var named Origin = built

			built.Shift(1)
			shape.draw(2.0)

			circle := Circle{Point: built, radius: 1.0}
			circle.Close()

			// circle.Sum is Point's, reached through the embedded field rather than inherited.
			return built.Sum() + zeroed.y + first.x + named.x + circle.Sum() + built.Pick() +
				built.Add(zeroed).x + built.Negate().x +
				int(built.ToFloat64()) + int(shape.area()) + int(ColourGreen) + measure("a", []int{1})
		}

		""";

	/// <summary>
	/// Tests that a package holding every kind of declaration the generator writes compiles, that a
	/// second file in the package can use all of it, and that the generated file is already
	/// formatted.
	/// </summary>
	[TestMethod]
	public void GeneratedSource_CompilesAndIsFormatted()
	{
		if (ToolchainHarness.FindOnPath("version", "go") is null)
		{
			Assert.Inconclusive("No Go toolchain on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "go.mod"), Module);
			File.WriteAllText(Path.Combine(directory, "driver.go"), Driver);
			File.WriteAllText(
				Path.Combine(directory, "exemplar.go"),
				new GoGenerator().Generate(Exemplar()));

			(int exitCode, string output) = ToolchainHarness.Run("go", "build ./...", directory);
			Assert.AreEqual(0, exitCode, $"go rejected the generated source:{Environment.NewLine}{output}");

			// gofmt lists the files it would change, so the evidence of agreement is that it named
			// none. Only the generated file is offered: how the driver is written is nobody's claim.
			(int formatted, string differs) = ToolchainHarness.Run("gofmt", "-l exemplar.go", directory);
			Assert.AreEqual(0, formatted, $"gofmt did not run:{Environment.NewLine}{differs}");
			Assert.AreEqual(
				string.Empty,
				differs.Trim(),
				"gofmt would rewrite the generated source, so it is not what a Go file looks like");
		});
	}

	/// <summary>
	/// Builds a file holding one of everything the generator has a spelling for.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile Exemplar()
	{
		SourceFile file = new("exemplar");
		file.HeaderComment.Add("Generated by Coder. Do not edit.");
		file.Imports.Add("unsafe");

		ClassDeclaration point = CompiledExemplar.Point();
		point.Members.Add(CompiledExemplar.Sum("Sum"));
		point.Members.Add(CompiledExemplar.Shift("Shift"));
		point.Members.Add(CompiledExemplar.Plus());
		point.Members.Add(CompiledExemplar.Negate());
		point.Members.Add(CompiledExemplar.ToDouble("float64(self.x)"));
		point.Members.Add(CompiledExemplar.Pick("Pick", "Shift"));

		ClassDeclaration circle = new("Circle") { BaseType = "Point" };
		circle.Documentation.Add("A shape with one radius.");
		circle.Members.Add(new VariableDeclaration("radius", "double"));
		circle.Members.Add(Released());

		file.Members.Add(CompiledExemplar.Colour());
		file.Members.Add(point);
		file.Members.Add(CompiledExemplar.Shape());
		file.Members.Add(circle);
		file.Members.Add(DescribesPoint());
		file.Members.Add(CompiledExemplar.OriginAlias());
		file.Members.Add(CompiledExemplar.OriginTable());
		file.Members.Add(CompiledExemplar.Measure());
		file.Members.Add(new CompileTimeAssertion
		{
			Condition = "unsafe.Sizeof(Point{}) == 2*unsafe.Sizeof(0)",
			Message = "Point must stay two ints",
		});

		return file;
	}

	/// <summary>
	/// Builds the destructor, which becomes the <c>Close</c> the caller has to call.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static FunctionDeclaration Released()
	{
		FunctionDeclaration close = new("Circle") { Kind = FunctionKind.Destructor };
		close.Body.Add(new VariableDeclaration("going", "bool", new LiteralExpression<bool>(true))
		{
			IsTypeInferred = true,
		});
		close.Body.Add(new AssignmentStatement(
			new VariableReference("_"), new VariableReference("going"), AssignmentOperator.Assign));
		return close;
	}

	/// <summary>
	/// Builds the declaration that is for a type rather than of one, which Go cannot write.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration DescribesPoint() =>
		new("Describe") { SpecialisationArguments = { new TypeReference("Point") } };
}
