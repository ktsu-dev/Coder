// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles what <see cref="RustGenerator"/> writes, with a real Rust compiler.
/// </summary>
/// <remarks>
/// The C generator earned a test like this because C's rules about linkage and constant expressions
/// are invisible in the text. Rust's reason is larger: a receiver that should have been
/// <c>&amp;mut self</c>, an associated item carrying a visibility it is not allowed, a trait
/// implementation missing the <c>Output</c> it has to name, a <c>const</c> that cannot be one —
/// each of those is a spelling a test can pin and a compiler rejects, and only one of them notices.
/// <para>
/// The exemplar is compiled as a library rather than run, because what is being checked is that the
/// declarations are well formed, not what they compute. The lint that fires on the output is allowed
/// by name at the top of the file rather than by silencing everything: parentheses come from the
/// AST carrying no operator precedence, which is a property of the AST rather than a mistake in this
/// generator.
/// </para>
/// <para>
/// The test is inconclusive rather than failing where no compiler is on the path, which is the
/// honest result: nothing was checked.
/// </para>
/// </remarks>
[TestClass]
public class RustGeneratedSourceCompilesTests
{
	/// <summary>
	/// Tests that a file holding one of everything the generator writes compiles.
	/// </summary>
	[TestMethod]
	public void GeneratedSource_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			// The AST carries no operator precedence, so every binary expression is parenthesised and
			// Rust says so. The lints are named rather than blanket-silenced: each one is a property
			// of what an exemplar is — declarations nothing calls, holding values nothing reads.
			string source = new RustGenerator().Generate(Exemplar());
			string allowed = "#![allow(unused_parens, dead_code, unused_variables, unused_mut)]";

			File.WriteAllText(
				Path.Combine(directory, "exemplar.rs"),
				$"{allowed}{Environment.NewLine}{source}");

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o exemplar.rlib exemplar.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a string literal compiles where an owned <c>String</c> receives it: returned from
	/// a function answering <c>str</c> or <c>string</c>, and initialising a local declared as one.
	/// </summary>
	[TestMethod]
	public void StringLiteralsReceivedAsOwnedStrings_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		FunctionDeclaration greet = new("greet") { ReturnType = "str" };
		greet.Body.Add(new ReturnStatement(Literal.Text("hello")));

		FunctionDeclaration named = new("named") { ReturnType = "string" };
		named.Body.Add(new VariableDeclaration("name", "str", Literal.Text("x")));
		named.Body.Add(new VariableDeclaration("guessed", null, Literal.Text("y")) { IsTypeInferred = true });
		named.Body.Add(new ReturnStatement(new VariableReference("name")));

		SourceFile file = new("strings");
		file.Members.Add(greet);
		file.Members.Add(named);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new RustGenerator().Generate(file);
			string allowed = "#![allow(dead_code, unused_variables, unused_mut)]";

			File.WriteAllText(
				Path.Combine(directory, "strings.rs"),
				$"{allowed}{Environment.NewLine}{source}");

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o strings.rlib strings.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that bitwise complement and unary plus compile: Rust has neither <c>~</c> nor a unary
	/// <c>+</c>, so both have to be spelled its way.
	/// </summary>
	[TestMethod]
	public void UnaryOperatorsRustSpellsDifferently_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		FunctionDeclaration complement = new("complement") { ReturnType = "int" };
		complement.Parameters.Add(new Parameter("value", "int"));
		complement.Body.Add(new ReturnStatement(new UnaryExpression(UnaryOperator.BitwiseNot, new VariableReference("value"))));

		FunctionDeclaration identity = new("identity") { ReturnType = "int" };
		identity.Parameters.Add(new Parameter("value", "int"));
		identity.Body.Add(new ReturnStatement(new UnaryExpression(UnaryOperator.Plus, new VariableReference("value"))));

		FunctionDeclaration minusOne = new("minus_one") { ReturnType = "int" };
		minusOne.Body.Add(new ReturnStatement(new UnaryExpression(UnaryOperator.Plus, Literal.Number(-1))));

		SourceFile file = new("unary");
		file.Members.Add(complement);
		file.Members.Add(identity);
		file.Members.Add(minusOne);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new RustGenerator().Generate(file);
			string allowed = "#![allow(unused_parens, dead_code)]";

			File.WriteAllText(
				Path.Combine(directory, "unary.rs"),
				$"{allowed}{Environment.NewLine}{source}");

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o unary.rlib unary.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a parameter whose field the body assigns, and an operator's <c>self</c> or operand
	/// whose field its body assigns, are bound <c>mut</c> — which Rust asks for to assign through a
	/// field of a value just as it does to assign the whole of it.
	/// </summary>
	[TestMethod]
	public void AssigningAFieldOfAParameterOrSelf_BindsItMut()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int"));
		point.Members.Add(new VariableDeclaration("y", "int"));

		// self.x += rhs.x; rhs.y = 0; return self;
		FunctionDeclaration plus = new("+") { Kind = FunctionKind.Operator, ReturnType = "Point" };
		plus.Parameters.Add(new Parameter("rhs", "Point"));
		plus.Body.Add(new AssignmentStatement(
			new VariableReference("self.x"), new VariableReference("rhs.x"), AssignmentOperator.AddAssign));
		plus.Body.Add(new AssignmentStatement(new VariableReference("rhs.y"), Literal.Number(0)));
		plus.Body.Add(new ReturnStatement(new VariableReference("self")));
		point.Members.Add(plus);

		// p.x += 1; return p;
		FunctionDeclaration shifted = new("shifted") { ReturnType = "Point" };
		shifted.Parameters.Add(new Parameter("p", "Point"));
		shifted.Body.Add(new AssignmentStatement(
			new VariableReference("p.x"), Literal.Number(1), AssignmentOperator.AddAssign));
		shifted.Body.Add(new ReturnStatement(new VariableReference("p")));

		SourceFile file = new("fields");
		file.Members.Add(point);
		file.Members.Add(shifted);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new RustGenerator().Generate(file);
			string allowed = "#![allow(unused_parens, unused_assignments, dead_code)]";

			Assert.Contains("fn add(mut self, mut rhs: Point)", source, StringComparison.Ordinal, source);
			Assert.Contains("mut p: Point", source, StringComparison.Ordinal, source);

			File.WriteAllText(
				Path.Combine(directory, "fields.rs"),
				$"{allowed}{Environment.NewLine}{source}");

			(int exitCode, string output) = ToolchainHarness.Run(
				"rustc",
				"--crate-type lib --edition 2021 -o fields.rlib fields.rs",
				directory);

			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{source}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that an operator whose body assigns nothing still takes its operands as they are, since
	/// a <c>mut</c> nothing needs is a warning in Rust.
	/// </summary>
	[TestMethod]
	public void AnOperatorThatAssignsNothing_BindsNothingMut()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int"));
		point.Members.Add(CompiledExemplar.Plus());

		string source = new RustGenerator().Generate(point);

		Assert.Contains("fn add(self, rhs: Point)", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that the arguments an entry point is handed are the ones the user typed, without the
	/// program's own path that <c>std::env::args</c> puts first — which is what they are in every
	/// other target.
	/// </summary>
	[TestMethod]
	public void EntryPointArguments_LeaveOutTheProgramPath()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		EntryPoint entryPoint = new() { AcceptsArguments = true, ReturnsExitCode = true };
		entryPoint.Body.Add(new ReturnStatement(new VariableReference("args.len() as i32")));

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string program = OperatingSystem.IsWindows() ? "counting.exe" : "counting";
			File.WriteAllText(Path.Combine(directory, "counting.rs"), new RustGenerator().Generate(entryPoint));

			(int built, string output) = ToolchainHarness.Run(
				"rustc",
				$"--edition 2021 -o {program} counting.rs",
				directory);
			Assert.AreEqual(0, built, $"rustc rejected the generated source:{Environment.NewLine}{output}");

			(int counted, _) = ToolchainHarness.Run(Path.Combine(directory, program), "first second", directory);
			Assert.AreEqual(2, counted, "the entry point was not handed exactly the two arguments typed");
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

		ClassDeclaration point = CompiledExemplar.Point();
		point.Members.Add(CompiledExemplar.Sum("sum"));
		point.Members.Add(CompiledExemplar.Shift("shift"));
		point.Members.Add(CompiledExemplar.Plus());
		point.Members.Add(CompiledExemplar.Negate());
		point.Members.Add(CompiledExemplar.ToDouble("value.x as f64"));
		point.Members.Add(CompiledExemplar.Pick("pick", "shift"));

		ClassDeclaration circle = new("Circle") { BaseType = "Point" };
		circle.Documentation.Add("A shape with one radius.");
		circle.Members.Add(new VariableDeclaration("radius", "double"));

		FunctionDeclaration drop = new("Circle") { Kind = FunctionKind.Destructor };
		drop.Body.Add(new VariableDeclaration("going", "bool", new LiteralExpression<bool>(true)));
		circle.Members.Add(drop);

		NamespaceDeclaration geometry = new("geo.shapes");
		geometry.Members.Add(CompiledExemplar.Colour());
		geometry.Members.Add(point);
		geometry.Members.Add(CompiledExemplar.Shape());
		geometry.Members.Add(circle);
		geometry.Members.Add(Describe());
		geometry.Members.Add(DescribesPoint());
		geometry.Members.Add(CompiledExemplar.OriginAlias());
		geometry.Members.Add(CompiledExemplar.OriginTable());
		geometry.Members.Add(CompiledExemplar.Measure());
		geometry.Members.Add(Boxed());
		geometry.Members.Add(Clamp());
		geometry.Members.Add(new CompileTimeAssertion
		{
			Condition = "std::mem::size_of::<i32>() == 4",
			Message = "an i32 is four bytes",
		});

		file.Members.Add(geometry);
		return file;
	}

	/// <summary>
	/// A function whose body assigns to one of its parameters.
	/// </summary>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// Every other target lets a parameter be reassigned; Rust binds it immutably, and without a
	/// <c>mut</c> on the declaration the assignment is error E0384.
	/// </remarks>
	private static FunctionDeclaration Clamp()
	{
		FunctionDeclaration clamp = new("clamp") { ReturnType = "int" };
		clamp.Parameters.Add(new Parameter("value", "int"));
		clamp.Body.Add(new AssignmentStatement(new VariableReference("value"), new LiteralExpression<int>(0)));
		clamp.Body.Add(new ReturnStatement(new VariableReference("value")));
		return clamp;
	}

	/// <summary>
	/// A type written over one parameter, so its impl block has to carry the parameter too.
	/// </summary>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// The whole of what makes this worth compiling: a generic struct's inherent impl has to repeat
	/// the parameter and bound it — <c>impl&lt;T: Clone&gt; Boxed&lt;T&gt;</c> — and a generator
	/// that wrote <c>impl Boxed</c> beside a <c>struct Boxed&lt;T&gt;</c> would produce something
	/// that reads correctly and does not build. The bound is load-bearing rather than decoration:
	/// without it the body's call to <c>clone</c> does not resolve.
	/// </remarks>
	private static ClassDeclaration Boxed()
	{
		ClassDeclaration boxed = new("Boxed") { Kind = TypeDeclarationKind.Struct };
		boxed.Documentation.Add("Holds one of whatever it was given.");
		boxed.TypeParameters.Add(TypeParameter.Parse("T : Clone"));
		boxed.Members.Add(new FieldDeclaration("held", "T"));
		boxed.Members.Add(new FieldDeclaration("CAPACITY", "int")
		{
			IsStatic = true,
			IsConstant = true,
			InitialValue = new LiteralExpression<int>(8),
		});

		FunctionDeclaration copy = new("copy") { ReturnType = "T", IsReadOnly = true };
		copy.Body.Add(new ReturnStatement(
			new CallExpression(new VariableReference("self.held"), "clone")));
		boxed.Members.Add(copy);

		// Named through the type, which only compiles if the constant is an associated item of it
		// rather than a field of each instance.
		FunctionDeclaration capacity = new("capacity") { ReturnType = "int", IsReadOnly = true };
		capacity.Body.Add(new ReturnStatement(new VariableReference("Self::CAPACITY")));
		boxed.Members.Add(capacity);

		return boxed;
	}

	/// <summary>
	/// Builds a trait whose constant every implementation has to supply.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration Describe()
	{
		ClassDeclaration describe = new("Describe") { Kind = TypeDeclarationKind.Interface };
		describe.Documentation.Add("What a type says about itself.");
		describe.Members.Add(new FieldDeclaration { Name = "NAME", Type = BorrowedString() });
		return describe;
	}

	/// <summary>
	/// Builds the declaration that is for a type rather than of one.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration DescribesPoint()
	{
		ClassDeclaration specialisation = new("Describe")
		{
			SpecialisationArguments = { new TypeReference("Point") },
		};

		specialisation.Members.Add(new FieldDeclaration
		{
			Name = "NAME",
			Type = BorrowedString(),
			InitialValue = new LiteralExpression<string>("Point"),
		});

		return specialisation;
	}

	/// <summary>
	/// Gets a string the holder does not own, which is the one Rust asks for wherever it is only read.
	/// </summary>
	/// <returns>The type.</returns>
	private static TypeReference BorrowedString() =>
		new("str") { Indirection = TypeIndirection.Reference, IsReadOnly = true };
}
