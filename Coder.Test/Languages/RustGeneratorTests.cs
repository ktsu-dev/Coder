// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.CodeBlocker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="RustGenerator"/>.
/// </summary>
/// <remarks>
/// Rust answers most of what the AST says with something the language already has — a trait, a
/// derive, an attribute — rather than with a convention, so most of what is pinned here is which
/// feature each part of a declaration became, and the few places where the answer is still a note.
/// </remarks>
[TestClass]
public class RustGeneratorTests
{
	private RustGenerator Generator { get; } = new();

	private static string NewLine => CodeBlocker.DefaultNewLineString;

	/// <summary>
	/// Tests that the generator reports the identity the registry and file writer rely on.
	/// </summary>
	[TestMethod]
	public void Identity_IsRust()
	{
		Assert.AreEqual("rust", Generator.LanguageId);
		Assert.AreEqual("Rust", Generator.DisplayName);
		Assert.AreEqual("rs", Generator.FileExtension);
	}

	/// <summary>
	/// Tests that a function with nothing to answer writes no return type, since Rust names the unit
	/// only when it has to and nobody writes it.
	/// </summary>
	[TestMethod]
	public void EmptyFunction_WritesNoReturnType()
	{
		FunctionDeclaration function = new("do_nothing");

		Assert.AreEqual($"pub fn do_nothing() {{{NewLine}}}{NewLine}", Generator.Generate(function));
	}

	/// <summary>
	/// Tests that the AST's language-neutral type names are mapped to Rust spellings.
	/// </summary>
	[TestMethod]
	public void Types_AreMappedToRustSpellings()
	{
		FunctionDeclaration function = new("greet") { ReturnType = "str" };
		function.Parameters.Add(new Parameter("times", "long"));
		function.Parameters.Add(new Parameter("ratio", "double"));

		StringAssert.Contains(
			Generator.Generate(function),
			"pub fn greet(times: i64, ratio: f64) -> String",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an unrecognized type name is passed through, so a caller can name a real Rust type.
	/// </summary>
	[TestMethod]
	public void UnknownType_IsPassedThrough()
	{
		FunctionDeclaration function = new("make") { ReturnType = "Widget" };

		StringAssert.Contains(Generator.Generate(function), "-> Widget", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the containers the AST names are spelled from the standard library, and that one
	/// named without arguments is a container of the most general thing there is.
	/// </summary>
	[TestMethod]
	public void Containers_AreSpelledFromTheStandardLibrary()
	{
		FunctionDeclaration function = new("lookup")
		{
			ReturnType = new TypeReference("dict")
			{
				TypeArguments = { new TypeReference("str"), new TypeReference("int") },
			},
		};
		function.Parameters.Add(new Parameter("keys")
		{
			Type = new TypeReference("list") { TypeArguments = { new TypeReference("str") } },
		});
		function.Parameters.Add(new Parameter("rest", "list"));

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "keys: Vec<String>", StringComparison.Ordinal);
		StringAssert.Contains(code, "rest: Vec<Box<dyn std::any::Any>>", StringComparison.Ordinal);
		StringAssert.Contains(code, "-> std::collections::HashMap<String, i32>", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a reference is a borrow, and that whether it is a mutable one is what the AST's
	/// read-only flag decides.
	/// </summary>
	[TestMethod]
	public void Reference_IsABorrow()
	{
		FunctionDeclaration function = new("touch");
		function.Parameters.Add(new Parameter("read")
		{
			Type = new TypeReference("Widget") { Indirection = TypeIndirection.Reference, IsReadOnly = true },
		});
		function.Parameters.Add(new Parameter("written")
		{
			Type = new TypeReference("Widget") { Indirection = TypeIndirection.Reference },
		});

		StringAssert.Contains(
			Generator.Generate(function),
			"(read: &Widget, written: &mut Widget)",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that borrowing an owned type gives the borrowed type rather than a reference to the
	/// owned one, which is the pair of spellings Rust asks you never to write.
	/// </summary>
	[TestMethod]
	public void BorrowedOwnedType_IsTheBorrowedType()
	{
		FunctionDeclaration function = new("read");
		function.Parameters.Add(new Parameter("text")
		{
			Type = new TypeReference("str") { Indirection = TypeIndirection.Reference, IsReadOnly = true },
		});
		function.Parameters.Add(new Parameter("items")
		{
			Type = new TypeReference("int") { IsArray = true, Indirection = TypeIndirection.Reference, IsReadOnly = true },
		});

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "text: &str", StringComparison.Ordinal);
		StringAssert.Contains(code, "items: &[i32]", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("&String", StringComparison.Ordinal));
		Assert.IsFalse(code.Contains("&Vec<", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a pointer is a raw pointer, which is the only thing Rust has that a pointer is.
	/// </summary>
	[TestMethod]
	public void Pointer_IsARawPointer()
	{
		FunctionDeclaration function = new("poke");
		function.Parameters.Add(new Parameter("mutable")
		{
			Type = new TypeReference("u8") { Indirection = TypeIndirection.Pointer },
		});
		function.Parameters.Add(new Parameter("readable")
		{
			Type = new TypeReference("u8") { Indirection = TypeIndirection.Pointer, IsReadOnly = true },
		});

		StringAssert.Contains(
			Generator.Generate(function),
			"(mutable: *mut u8, readable: *const u8)",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a type declaration splits into the struct holding the data and the block holding
	/// the behaviour, which is the split Rust insists on.
	/// </summary>
	[TestMethod]
	public void Type_SplitsIntoAStructAndAnImplBlock()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int"));
		point.Members.Add(new FunctionDeclaration("sum") { ReturnType = "int", IsReadOnly = true });

		string code = Generator.Generate(point);

		StringAssert.Contains(code, $"pub struct Point {{{NewLine}    pub x: i32,{NewLine}}}", StringComparison.Ordinal);
		StringAssert.Contains(code, "impl Point {", StringComparison.Ordinal);
		StringAssert.Contains(code, "pub fn sum(&self) -> i32 {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a member that promises not to modify what it is called on borrows the instance,
	/// and that one that does not borrows it mutably.
	/// </summary>
	[TestMethod]
	public void Receiver_IsBorrowedMutablyUnlessTheMemberIsReadOnly()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new FunctionDeclaration("read") { IsReadOnly = true });
		point.Members.Add(new FunctionDeclaration("write"));
		point.Members.Add(new FunctionDeclaration("build") { ReturnType = "Point", IsStatic = true });

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "pub fn read(&self) {", StringComparison.Ordinal);
		StringAssert.Contains(code, "pub fn write(&mut self) {", StringComparison.Ordinal);
		StringAssert.Contains(code, "pub fn build() -> Point {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constructor is an associated function answering <c>Self</c>, built from the
	/// initialiser list — and that a field initialised from a variable of its own name is written as
	/// that name alone.
	/// </summary>
	[TestMethod]
	public void Constructor_BuildsSelfWithTheFieldInitShorthand()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration create = new("Point") { Kind = FunctionKind.Constructor };
		create.Parameters.Add(new Parameter("x", "int"));
		create.Initialisers.Add(new MemberInitialiser("x") { Value = new VariableReference("x") });
		create.Initialisers.Add(new MemberInitialiser("y") { Value = new LiteralExpression<int>(0) });
		point.Members.Add(create);

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "pub fn new(x: i32) -> Self {", StringComparison.Ordinal);
		StringAssert.Contains(code, "Self { x, y: 0 }", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constructor with nothing to build from asks the type for its default, since Rust
	/// has no zero value to fall back on.
	/// </summary>
	[TestMethod]
	public void Constructor_WithNoInitialisers_AsksForTheDefault()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new FunctionDeclaration("Point") { Kind = FunctionKind.Constructor });

		StringAssert.Contains(Generator.Generate(point), "Self::default()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a destructor is the <c>Drop</c> implementation Rust calls for you, and that it does
	/// not land in the inherent block where it would be a second <c>drop</c>.
	/// </summary>
	[TestMethod]
	public void Destructor_IsADropImplementation()
	{
		ClassDeclaration buffer = new("Buffer");
		FunctionDeclaration destructor = new("Buffer") { Kind = FunctionKind.Destructor };
		destructor.Body.Add(new ReturnStatement());
		buffer.Members.Add(destructor);

		string code = Generator.Generate(buffer);

		StringAssert.Contains(code, $"impl Drop for Buffer {{{NewLine}    fn drop(&mut self) {{", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("impl Buffer {", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that an operator is the <c>std::ops</c> trait for it, naming the output the trait
	/// requires.
	/// </summary>
	[TestMethod]
	public void BinaryOperator_IsItsStdOpsTrait()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration plus = new("+") { Kind = FunctionKind.Operator, ReturnType = "Point" };
		plus.Parameters.Add(new Parameter("rhs", "Point"));
		point.Members.Add(plus);

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "impl std::ops::Add for Point {", StringComparison.Ordinal);
		StringAssert.Contains(code, "    type Output = Point;", StringComparison.Ordinal);
		StringAssert.Contains(code, "    fn add(self, rhs: Point) -> Point {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an operator taking no operand beside the instance is the unary trait rather than
	/// the binary one that shares its symbol.
	/// </summary>
	[TestMethod]
	public void UnaryOperator_IsTheUnaryTrait()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new FunctionDeclaration("-") { Kind = FunctionKind.Operator, ReturnType = "Point" });

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "impl std::ops::Neg for Point {", StringComparison.Ordinal);
		StringAssert.Contains(code, "    fn neg(self) -> Point {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that equality borrows what it compares and answers a bool, rather than consuming its
	/// operands and naming an output the way the arithmetic traits do.
	/// </summary>
	[TestMethod]
	public void Equality_IsPartialEq()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration equals = new("==") { Kind = FunctionKind.Operator, ReturnType = "bool" };
		equals.Parameters.Add(new Parameter("rhs", "Point"));
		point.Members.Add(equals);

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "impl PartialEq for Point {", StringComparison.Ordinal);
		StringAssert.Contains(code, "fn eq(&self, rhs: &Point) -> bool {", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("type Output", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that an operator Rust supplies from another one is a note naming the trait to implement
	/// instead, rather than an implementation the language would reject.
	/// </summary>
	[TestMethod]
	public void OperatorRustSupplies_IsANote()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration less = new("<") { Kind = FunctionKind.Operator, ReturnType = "bool" };
		less.Parameters.Add(new Parameter("rhs", "Point"));
		point.Members.Add(less);

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "// operator< on Point: implement PartialOrd", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("impl std::ops", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a conversion is the <c>From</c> implementation the standard library asks for.
	/// </summary>
	[TestMethod]
	public void Conversion_IsAFromImplementation()
	{
		ClassDeclaration weight = new("Weight") { Kind = TypeDeclarationKind.Struct };
		weight.Members.Add(new FunctionDeclaration("ignored")
		{
			Kind = FunctionKind.ConversionOperator,
			ReturnType = "double",
			IsReadOnly = true,
		});

		string code = Generator.Generate(weight);

		StringAssert.Contains(code, "impl From<Weight> for f64 {", StringComparison.Ordinal);
		StringAssert.Contains(code, "fn from(value: Weight) -> f64 {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an interface is a trait, that a base type is a supertrait, and that a member with
	/// no body is the requirement it is meant to be.
	/// </summary>
	[TestMethod]
	public void Interface_IsATrait()
	{
		ClassDeclaration shape = new("Shape")
		{
			Kind = TypeDeclarationKind.Interface,
			BaseType = "Drawable",
		};
		shape.Members.Add(new FunctionDeclaration("area") { ReturnType = "double", IsReadOnly = true, IsAbstract = true });

		string code = Generator.Generate(shape);

		StringAssert.Contains(code, "pub trait Shape: Drawable {", StringComparison.Ordinal);
		StringAssert.Contains(code, "    fn area(&self) -> f64;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a trait's members say nothing about who may see them. Rust rejects a visibility on
	/// an associated item rather than ignoring it, so this is the difference between a file that
	/// compiles and one that does not.
	/// </summary>
	[TestMethod]
	public void TraitMembers_CarryNoVisibility()
	{
		ClassDeclaration shape = new("Shape") { Kind = TypeDeclarationKind.Interface };
		shape.Members.Add(new FunctionDeclaration("area")
		{
			ReturnType = "double",
			Visibility = Visibility.Public,
			IsAbstract = true,
		});
		shape.Members.Add(new FieldDeclaration { Name = "SIDES", Type = "int", Visibility = Visibility.Public });

		string code = Generator.Generate(shape);

		StringAssert.Contains(code, "    fn area(&mut self) -> f64;", StringComparison.Ordinal);
		StringAssert.Contains(code, "    const SIDES: i32;", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("    pub ", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a declaration for a type rather than of one is the trait implementation Rust
	/// attaches facts with — which is what C++ writes an explicit specialisation for.
	/// </summary>
	[TestMethod]
	public void Specialisation_IsATraitImplementation()
	{
		ClassDeclaration describe = new("Describe")
		{
			SpecialisationArguments = { new TypeReference("RigidBody") },
		};
		describe.Members.Add(new FieldDeclaration
		{
			Name = "NAME",
			Type = new TypeReference("str") { Indirection = TypeIndirection.Reference, IsReadOnly = true },
			InitialValue = new LiteralExpression<string>("RigidBody"),
			Visibility = Visibility.Public,
		});

		string code = Generator.Generate(describe);

		StringAssert.Contains(code, "impl Describe for RigidBody {", StringComparison.Ordinal);
		StringAssert.Contains(code, "    const NAME: &str = \"RigidBody\";", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a base type on a struct is a field holding it, which is the one place Rust has
	/// nothing better than C did.
	/// </summary>
	[TestMethod]
	public void BaseType_OnAStruct_IsAFieldHoldingIt()
	{
		ClassDeclaration circle = new("Circle") { BaseType = "Shape" };
		circle.Members.Add(new VariableDeclaration("radius", "double"));

		string code = Generator.Generate(circle);

		int baseAt = code.IndexOf("pub base: Shape,", StringComparison.Ordinal);
		int radiusAt = code.IndexOf("pub radius: f64,", StringComparison.Ordinal);

		Assert.IsTrue(baseAt >= 0, "the base should be a field");
		Assert.IsTrue(baseAt < radiusAt, "the base should come first");
	}

	/// <summary>
	/// Tests that an enumeration needs no prefixing, since its variants are named through it, and
	/// that a fixed underlying type is the guarantee rather than a comment about one.
	/// </summary>
	[TestMethod]
	public void Enum_IsScopedAndCanFixItsRepresentation()
	{
		EnumDeclaration colour = new("Colour") { UnderlyingType = "int" };
		colour.Members.Add(new EnumMember("Red") { Value = "1" });
		colour.Members.Add(new EnumMember("Green"));

		string code = Generator.Generate(colour);

		Assert.AreEqual(
			$"#[repr(i32)]{NewLine}pub enum Colour {{{NewLine}    Red = 1,{NewLine}    Green,{NewLine}}}{NewLine}",
			code);
	}

	/// <summary>
	/// Tests that a constant is a <c>const</c> and an ordinary field a <c>static</c>, and that an
	/// array one is a borrowed slice — the bound-free spelling Rust does have, which is what a table
	/// with no length has to be.
	/// </summary>
	[TestMethod]
	public void ModuleScopeField_IsAConstOrAStatic()
	{
		FieldDeclaration limit = new()
		{
			Name = "MAX_ITEMS",
			Type = "int",
			IsConstant = true,
			InitialValue = new LiteralExpression<int>(16),
		};

		FieldDeclaration counter = new()
		{
			Name = "SEEN",
			Type = "int",
			InitialValue = new LiteralExpression<int>(0),
		};

		ConstructionExpression rows = new(type: null);
		ConstructionExpression row = new(new TypeReference("Point"));
		row.Arguments.Add(new MemberInitialiser("x") { Value = new LiteralExpression<int>(1) });
		rows.Arguments.Add(row);

		FieldDeclaration table = new()
		{
			Name = "ORIGINS",
			Type = new TypeReference("Point") { IsArray = true },
			IsConstant = true,
			InitialValue = rows,
		};

		Assert.AreEqual($"pub const MAX_ITEMS: i32 = 16;{NewLine}", Generator.Generate(limit));
		Assert.AreEqual($"pub static SEEN: i32 = 0;{NewLine}", Generator.Generate(counter));
		StringAssert.Contains(Generator.Generate(table), "pub const ORIGINS: &[Point] = &[", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a local binds mutably unless the declaration said its value never changes, which is
	/// the opposite of every other target here and the reason it has to be said.
	/// </summary>
	[TestMethod]
	public void Local_BindsMutablyUnlessConstant()
	{
		FunctionDeclaration function = new("run");
		function.Body.Add(new VariableDeclaration("limit", "int", new LiteralExpression<int>(4)) { IsConstant = true });
		function.Body.Add(new VariableDeclaration("seen", "int", new LiteralExpression<int>(0)));
		function.Body.Add(new VariableDeclaration("guessed", "int", new LiteralExpression<int>(1)) { IsTypeInferred = true });

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "    let limit: i32 = 4;", StringComparison.Ordinal);
		StringAssert.Contains(code, "    let mut seen: i32 = 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "    let mut guessed = 1;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a pure function earns <c>#[must_use]</c>, and that one answering nothing does not —
	/// where there is no result, there is nothing to ignore.
	/// </summary>
	[TestMethod]
	public void PureFunction_MustUseItsResult()
	{
		FunctionDeclaration answers = new("area") { ReturnType = "double", IsPure = true };
		FunctionDeclaration acts = new("reset") { IsPure = true };

		StringAssert.StartsWith(Generator.Generate(answers), "#[must_use]", StringComparison.Ordinal);
		Assert.IsFalse(Generator.Generate(acts).Contains("must_use", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a function whose value is fixed when the program is built is a <c>const fn</c>.
	/// </summary>
	[TestMethod]
	public void CompileTimeEvaluableFunction_IsAConstFn()
	{
		FunctionDeclaration function = new("zero") { ReturnType = "int", IsCompileTimeEvaluable = true };

		StringAssert.Contains(Generator.Generate(function), "pub const fn zero() -> i32", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a deleted declaration is a note rather than a signature, since writing the
	/// signature would make legal exactly what the declaration exists to forbid.
	/// </summary>
	[TestMethod]
	public void DeletedFunction_IsANote()
	{
		FunctionDeclaration function = new("clone") { Definition = FunctionDefinition.Deleted };

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "// clone is deleted", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("fn clone", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a defaulted declaration is a note naming the derive that supplies it.
	/// </summary>
	[TestMethod]
	public void DefaultedFunction_IsANote()
	{
		FunctionDeclaration function = new("Point") { Kind = FunctionKind.Constructor, Definition = FunctionDefinition.Defaulted };

		StringAssert.Contains(Generator.Generate(function), "// new is defaulted", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an assertion is Rust's own, which needs no macro crate: a constant nobody names
	/// still has to be evaluated for the program to build.
	/// </summary>
	[TestMethod]
	public void CompileTimeAssertion_IsAnEvaluatedConstant()
	{
		CompileTimeAssertion assertion = new()
		{
			Condition = "std::mem::size_of::<Point>() == 8",
			Message = "a Point is two i32s",
		};

		string code = Generator.Generate(assertion);

		StringAssert.StartsWith(code, "const _: () = assert!(std::mem::size_of::<Point>() == 8,", StringComparison.Ordinal);
		StringAssert.Contains(code, "\"a Point is two i32s\");", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an alias is a type alias.
	/// </summary>
	[TestMethod]
	public void UsingAlias_IsATypeAlias()
	{
		UsingAlias alias = new()
		{
			Name = "Radii",
			AliasedType = new TypeReference("double") { IsArray = true },
		};

		Assert.AreEqual($"pub type Radii = Vec<f64>;{NewLine}", Generator.Generate(alias));
	}

	/// <summary>
	/// Tests that a namespace is nested modules rather than one module with a long name, since that
	/// is what the name says and what a caller reaching into it would write either way.
	/// </summary>
	[TestMethod]
	public void Namespace_IsNestedModules()
	{
		NamespaceDeclaration ns = new("geo.shapes");
		ns.Members.Add(new FunctionDeclaration("area") { ReturnType = "double" });

		string code = Generator.Generate(ns);

		StringAssert.StartsWith(code, $"pub mod geo {{{NewLine}    pub mod shapes {{", StringComparison.Ordinal);
		StringAssert.Contains(code, "        pub fn area() -> f64 {", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an import is a <c>use</c>, and that one a caller wrote in full is left alone.
	/// </summary>
	[TestMethod]
	public void Imports_AreUseItems()
	{
		SourceFile file = new("shape");
		file.Imports.Add("std::fmt");
		file.Imports.Add("use std::collections::{HashMap, HashSet};");
		file.Members.Add(new FunctionDeclaration("area") { ReturnType = "double" });

		string code = Generator.Generate(file);

		StringAssert.Contains(code, "use std::fmt;", StringComparison.Ordinal);
		StringAssert.Contains(code, "use std::collections::{HashMap, HashSet};", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the entry point is Rust's <c>main</c>, and that a program wanting its arguments
	/// gets them from the standard library rather than from a parameter Rust does not offer.
	/// </summary>
	[TestMethod]
	public void EntryPoint_IsMain()
	{
		EntryPoint bare = new();
		EntryPoint withArguments = new() { AcceptsArguments = true };

		StringAssert.StartsWith(Generator.Generate(bare), "fn main() {", StringComparison.Ordinal);
		StringAssert.Contains(
			Generator.Generate(withArguments),
			"    let args: Vec<String> = std::env::args().collect();",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a program answering an exit code is written as a function that answers one and a
	/// <c>main</c> that exits with it — which is what keeps the body's own return meaning what it
	/// says, and the shape Python's guard takes here for the same reason.
	/// </summary>
	[TestMethod]
	public void EntryPoint_ReturningAnExitCode_ExitsWithWhatItAnswered()
	{
		EntryPoint entryPoint = new() { ReturnsExitCode = true };
		entryPoint.Body.Add(new ReturnStatement(new LiteralExpression<int>(0)));

		string code = Generator.Generate(entryPoint);

		StringAssert.Contains(code, "fn run() -> i32 {", StringComparison.Ordinal);
		StringAssert.Contains(code, "    return 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "    std::process::exit(run());", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests the three shapes a construction takes, which depend on what the expression is rather
	/// than on where it stands.
	/// </summary>
	[TestMethod]
	public void Construction_IsAStructLiteralACallOrAnArray()
	{
		ConstructionExpression literal = new(new TypeReference("Point"));
		literal.Arguments.Add(new MemberInitialiser("x") { Value = new LiteralExpression<int>(1) });

		ConstructionExpression call = new(new TypeReference("Wrapper"));
		call.Arguments.Add(new LiteralExpression<int>(1));
		call.Arguments.Add(new LiteralExpression<int>(2));

		ConstructionExpression array = new(type: null);
		array.Arguments.Add(new LiteralExpression<int>(1));

		Assert.AreEqual("Point { x: 1 }", Generate(literal));
		Assert.AreEqual("Wrapper(1, 2)", Generate(call));
		Assert.AreEqual("[ 1 ]", Generate(array));
	}

	/// <summary>
	/// Writes one expression on its own, for the assertions that are about the expression rather than
	/// about the declaration holding it.
	/// </summary>
	/// <param name="expression">The expression to write.</param>
	/// <returns>Its Rust source.</returns>
	private string Generate(Expression expression) => Generator.Generate(expression);
}
