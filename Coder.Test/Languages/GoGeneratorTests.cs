// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.CodeBlocker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="GoGenerator"/>.
/// </summary>
/// <remarks>
/// Go answers most of what the AST says with something it already had — a struct, an interface, an
/// embedded field, a receiver — so most of what is pinned here is which of those each part of a
/// declaration became. The rest is the handful of things Go left out on purpose, where what is
/// written is a name it had to invent or a note saying there is nothing to write.
/// </remarks>
[TestClass]
public class GoGeneratorTests
{
	private GoGenerator Generator { get; } = new();

	private static string NewLine => CodeBlocker.DefaultNewLineString;

	/// <summary>
	/// Tests that the generator reports the identity the registry and file writer rely on.
	/// </summary>
	[TestMethod]
	public void Identity_IsGo()
	{
		Assert.AreEqual("go", Generator.LanguageId);
		Assert.AreEqual("Go", Generator.DisplayName);
		Assert.AreEqual("go", Generator.FileExtension);
	}

	/// <summary>
	/// Tests that a function's statements carry no terminator, since Go's lexer supplies the one its
	/// grammar wants and gofmt deletes any that were written.
	/// </summary>
	[TestMethod]
	public void Statements_CarryNoSemicolon()
	{
		FunctionDeclaration function = new("answer") { ReturnType = "int" };
		function.Body.Add(new ReturnStatement(Literal.Number(4)));

		Assert.AreEqual(
			$"func answer() int {{{NewLine}\treturn 4{NewLine}}}{NewLine}",
			Generator.Generate(function));
	}

	/// <summary>
	/// Tests that a body is indented with a tab, which is the one thing about a Go file's shape that
	/// nobody gets a say in.
	/// </summary>
	[TestMethod]
	public void Indentation_IsATab()
	{
		FunctionDeclaration function = new("answer");
		function.Body.Add(new ReturnStatement());

		StringAssert.Contains(Generator.Generate(function), $"{NewLine}\treturn{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a function answering nothing writes no result at all, rather than naming an empty
	/// one.
	/// </summary>
	[TestMethod]
	public void EmptyFunction_WritesNoResult()
	{
		FunctionDeclaration function = new("doNothing");

		Assert.AreEqual($"func doNothing() {{{NewLine}}}{NewLine}", Generator.Generate(function));
	}

	/// <summary>
	/// Tests that the AST's language-neutral type names are mapped to Go spellings.
	/// </summary>
	[TestMethod]
	public void Types_AreMappedToGoSpellings()
	{
		FunctionDeclaration function = new("greet") { ReturnType = "str" };
		function.Parameters.Add(new Parameter("times", "long"));
		function.Parameters.Add(new Parameter("ratio", "double"));

		StringAssert.Contains(
			Generator.Generate(function),
			"func greet(times int64, ratio float64) string",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an unrecognized type name is passed through, so a caller can name a real Go type.
	/// </summary>
	[TestMethod]
	public void UnknownType_IsPassedThrough()
	{
		FunctionDeclaration function = new("make") { ReturnType = "Widget" };

		StringAssert.Contains(Generator.Generate(function), ") Widget", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the containers the AST names are spelled out of Go's grammar rather than from a
	/// package, and that one named without arguments holds the most general thing there is.
	/// </summary>
	[TestMethod]
	public void Containers_AreSpelledFromTheGrammar()
	{
		FunctionDeclaration function = new("lookup")
		{
			ReturnType = new TypeReference("dict")
			{
				TypeArguments = { new TypeReference("str"), new TypeReference("int") },
			},
		};
		function.Parameters.Add(new Parameter("keys") { Type = new TypeReference("list") });

		StringAssert.Contains(
			Generator.Generate(function),
			"func lookup(keys []any) map[string]int",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a type with arguments is written with square brackets, which is where Go put its
	/// generics.
	/// </summary>
	[TestMethod]
	public void GenericType_IsWrittenWithSquareBrackets()
	{
		FunctionDeclaration function = new("read")
		{
			ReturnType = new TypeReference("Result")
			{
				TypeArguments = { new TypeReference("Handle"), new TypeReference("Error") },
			},
		};

		StringAssert.Contains(Generator.Generate(function), ") Result[Handle, Error]", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that both of the AST's indirections become the one Go has.
	/// </summary>
	[TestMethod]
	public void Indirection_IsAlwaysThePointer()
	{
		FunctionDeclaration function = new("touch");
		function.Parameters.Add(new Parameter("held") { Type = new TypeReference("Widget") { Indirection = TypeIndirection.Reference } });
		function.Parameters.Add(new Parameter("raw") { Type = new TypeReference("Widget") { Indirection = TypeIndirection.Pointer } });

		StringAssert.Contains(Generator.Generate(function), "(held *Widget, raw *Widget)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a reference to something already holding a pointer is left as it is, since a
	/// pointer to a slice or a string is a pointer to a pointer and nobody means that.
	/// </summary>
	[TestMethod]
	public void ReferenceToAView_IsTheViewItself()
	{
		FunctionDeclaration function = new("measure");
		function.Parameters.Add(new Parameter("label") { Type = new TypeReference("str") { Indirection = TypeIndirection.Reference } });
		function.Parameters.Add(new Parameter("sizes")
		{
			Type = new TypeReference("int") { IsArray = true, Indirection = TypeIndirection.Reference },
		});

		StringAssert.Contains(Generator.Generate(function), "(label string, sizes []int)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a type declaration becomes a struct with the data in it and the behaviour beside
	/// it, which is where Go keeps a method.
	/// </summary>
	[TestMethod]
	public void Class_BecomesAStructAndMethodsBesideIt()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("X", "int"));
		point.Members.Add(new FunctionDeclaration("Reset"));

		string generated = Generator.Generate(point);

		StringAssert.Contains(generated, $"type Point struct {{{NewLine}\tX int{NewLine}}}", StringComparison.Ordinal);
		StringAssert.Contains(generated, "func (self *Point) Reset()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a struct with no fields is written on one line, which is what gofmt does with one.
	/// </summary>
	[TestMethod]
	public void EmptyStruct_IsWrittenOnOneLine()
	{
		ClassDeclaration marker = new("Marker");

		Assert.AreEqual($"type Marker struct{{}}{NewLine}", Generator.Generate(marker));
	}

	/// <summary>
	/// Tests that a member promising not to modify what it is called on takes the value and one that
	/// does takes a pointer to it, which is how Go makes that promise.
	/// </summary>
	[TestMethod]
	public void Receiver_IsAValueOnlyWhereTheMemberPromisesNotToModify()
	{
		ClassDeclaration point = new("Point");
		point.Members.Add(new FunctionDeclaration("Sum") { ReturnType = "int", IsReadOnly = true });
		point.Members.Add(new FunctionDeclaration("Shift"));

		string generated = Generator.Generate(point);

		StringAssert.Contains(generated, "func (self Point) Sum() int", StringComparison.Ordinal);
		StringAssert.Contains(generated, "func (self *Point) Shift()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a static member becomes a package-level function carrying the type's name, since
	/// there is nothing else to scope it.
	/// </summary>
	[TestMethod]
	public void StaticMember_TakesTheTypeIntoItsName()
	{
		ClassDeclaration point = new("Point");
		point.Members.Add(new FunctionDeclaration("zero") { ReturnType = "Point", IsStatic = true });

		StringAssert.Contains(Generator.Generate(point), "func PointZero() Point", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constructor becomes the function every Go package writes instead, building the
	/// value from the initialiser list.
	/// </summary>
	[TestMethod]
	public void Constructor_BecomesANewFunction()
	{
		ClassDeclaration point = new("Point");
		FunctionDeclaration create = new("Point") { Kind = FunctionKind.Constructor };
		create.Parameters.Add(new Parameter("x", "int"));
		create.Initialisers.Add(new MemberInitialiser("x") { Value = new VariableReference("x") });
		point.Members.Add(create);

		StringAssert.Contains(
			Generator.Generate(point),
			$"func NewPoint(x int) Point {{{NewLine}\treturn Point{{x: x}}{NewLine}}}",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constructor with nothing to build from answers the zero value, which Go gives
	/// every type and which is exactly what the declaration was asking for.
	/// </summary>
	[TestMethod]
	public void ConstructorWithNoInitialisers_AnswersTheZeroValue()
	{
		ClassDeclaration point = new("Point");
		point.Members.Add(new FunctionDeclaration("Point") { Kind = FunctionKind.Constructor });

		StringAssert.Contains(Generator.Generate(point), "\treturn Point{}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a destructor becomes the convention Go has instead, which is a method the caller
	/// has to call.
	/// </summary>
	[TestMethod]
	public void Destructor_BecomesClose()
	{
		ClassDeclaration handle = new("Handle");
		handle.Members.Add(new FunctionDeclaration("Handle") { Kind = FunctionKind.Destructor });

		StringAssert.Contains(Generator.Generate(handle), "func (self *Handle) Close()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an operator becomes a method named for what it does, since Go has no overloading.
	/// </summary>
	[TestMethod]
	public void Operator_BecomesAMethodNamedForWhatItDoes()
	{
		ClassDeclaration point = new("Point");
		FunctionDeclaration plus = new("+") { Kind = FunctionKind.Operator, ReturnType = "Point", IsReadOnly = true };
		plus.Parameters.Add(new Parameter("rhs", "Point"));
		point.Members.Add(plus);

		StringAssert.Contains(
			Generator.Generate(point),
			"func (self Point) Add(rhs Point) Point",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a unary operator is named from the unary vocabulary rather than after the binary
	/// operator sharing its symbol, which is what keeps a type declaring both from declaring one name
	/// twice.
	/// </summary>
	[TestMethod]
	public void UnaryOperator_IsNamedForTheUnaryReadingOfItsSymbol()
	{
		ClassDeclaration point = new("Point");
		FunctionDeclaration minus = new("-") { Kind = FunctionKind.Operator, ReturnType = "Point", IsReadOnly = true };
		minus.Parameters.Add(new Parameter("rhs", "Point"));
		point.Members.Add(minus);
		point.Members.Add(new FunctionDeclaration("-") { Kind = FunctionKind.Operator, ReturnType = "Point", IsReadOnly = true });

		string generated = Generator.Generate(point);

		StringAssert.Contains(generated, "func (self Point) Subtract(rhs Point) Point", StringComparison.Ordinal);
		StringAssert.Contains(generated, "func (self Point) Negate() Point", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a conversion to a string becomes the method the standard library asks for, so that
	/// everything printing a value finds it.
	/// </summary>
	[TestMethod]
	public void ConversionToAString_BecomesStringer()
	{
		ClassDeclaration point = new("Point");
		point.Members.Add(new FunctionDeclaration("ignored")
		{
			Kind = FunctionKind.ConversionOperator,
			ReturnType = "str",
			IsReadOnly = true,
		});

		StringAssert.Contains(Generator.Generate(point), "func (self Point) String() string", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that every other conversion becomes a method named after what it answers.
	/// </summary>
	[TestMethod]
	public void Conversion_BecomesAMethodNamedForItsTarget()
	{
		ClassDeclaration point = new("Point");
		point.Members.Add(new FunctionDeclaration("ignored")
		{
			Kind = FunctionKind.ConversionOperator,
			ReturnType = "double",
			IsReadOnly = true,
		});

		StringAssert.Contains(Generator.Generate(point), "func (self Point) ToFloat64() float64", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an interface becomes one, holding signatures with no receiver and no body.
	/// </summary>
	[TestMethod]
	public void Interface_BecomesAnInterfaceOfSignatures()
	{
		ClassDeclaration shape = new("Shape") { Kind = TypeDeclarationKind.Interface };
		FunctionDeclaration draw = new("Draw") { IsAbstract = true };
		draw.Parameters.Add(new Parameter("scale", "double"));
		shape.Members.Add(draw);
		shape.Members.Add(new FunctionDeclaration("Area") { ReturnType = "double", IsReadOnly = true, IsAbstract = true });

		Assert.AreEqual(
			$"type Shape interface {{{NewLine}\tDraw(scale float64){NewLine}\tArea() float64{NewLine}}}{NewLine}",
			Generator.Generate(shape));
	}

	/// <summary>
	/// Tests that a base type on an interface is embedded, which is how Go says an implementation
	/// must do everything the other one requires.
	/// </summary>
	[TestMethod]
	public void InterfaceBaseType_IsEmbedded()
	{
		ClassDeclaration named = new("Named") { Kind = TypeDeclarationKind.Interface, BaseType = "Stringer" };
		named.Members.Add(new FunctionDeclaration("Name") { ReturnType = "str", IsAbstract = true });

		Assert.AreEqual(
			$"type Named interface {{{NewLine}\tStringer{NewLine}\tName() string{NewLine}}}{NewLine}",
			Generator.Generate(named));
	}

	/// <summary>
	/// Tests that a base type on a struct is an embedded field, and is said to be one — Go promotes
	/// what it holds rather than deriving from it, which is close enough to inheritance to be worth
	/// telling apart from it.
	/// </summary>
	[TestMethod]
	public void StructBaseType_IsAnEmbeddedFieldWithANote()
	{
		ClassDeclaration circle = new("Circle") { BaseType = "Point" };
		circle.Members.Add(new VariableDeclaration("Radius", "double"));

		string generated = Generator.Generate(circle);

		StringAssert.Contains(generated, "// the base, embedded:", StringComparison.Ordinal);
		StringAssert.Contains(generated, $"{NewLine}\tPoint{NewLine}\tRadius float64{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an enumeration becomes a named type and a block of constants, each prefixed with
	/// the type's name because Go's constants share the package's scope.
	/// </summary>
	[TestMethod]
	public void Enum_BecomesANamedTypeAndPrefixedConstants()
	{
		EnumDeclaration colour = new("Colour") { UnderlyingType = "long" };
		colour.Members.Add(new EnumMember("Red"));
		colour.Members.Add(new EnumMember("Green"));

		Assert.AreEqual(
			$"type Colour int64{NewLine}{NewLine}const ({NewLine}\tColourRed Colour = iota{NewLine}\tColourGreen{NewLine}){NewLine}",
			Generator.Generate(colour));
	}

	/// <summary>
	/// Tests that an enumeration whose members say nothing takes the default underlying type.
	/// </summary>
	[TestMethod]
	public void EnumWithNoUnderlyingType_IsAnInt()
	{
		EnumDeclaration weekday = new("Weekday");
		weekday.Members.Add(new EnumMember("Monday"));

		StringAssert.Contains(Generator.Generate(weekday), $"type Weekday int{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a member following one that named a value is written as the one before it plus one,
	/// which is what C's rule means and the only way to keep meaning it once iota has stopped
	/// counting.
	/// </summary>
	[TestMethod]
	public void EnumMemberAfterAValue_NamesTheOneBeforeIt()
	{
		EnumDeclaration colour = new("Colour");
		colour.Members.Add(new EnumMember("Red") { Value = "1" });
		colour.Members.Add(new EnumMember("Green"));

		Assert.AreEqual(
			$"type Colour int{NewLine}{NewLine}const ({NewLine}\tColourRed   Colour = 1{NewLine}\tColourGreen Colour = ColourRed + 1{NewLine}){NewLine}",
			Generator.Generate(colour));
	}

	/// <summary>
	/// Tests that a member already carrying the type's name is not given it twice.
	/// </summary>
	[TestMethod]
	public void EnumMemberAlreadyPrefixed_IsNotPrefixedAgain()
	{
		EnumDeclaration colour = new("Colour");
		colour.Members.Add(new EnumMember("ColourRed"));

		StringAssert.Contains(Generator.Generate(colour), "\tColourRed Colour = iota", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an alias is one, rather than a second type with the same shape.
	/// </summary>
	[TestMethod]
	public void UsingAlias_BecomesATypeAlias()
	{
		UsingAlias alias = new("Origin", "Point");

		Assert.AreEqual($"type Origin = Point{NewLine}", Generator.Generate(alias));
	}

	/// <summary>
	/// Tests that a compile-time assertion is one, spelled as the duplicate map key Go refuses.
	/// </summary>
	[TestMethod]
	public void CompileTimeAssertion_IsADuplicateMapKey()
	{
		CompileTimeAssertion assertion = new()
		{
			Condition = "unsafe.Sizeof(Point{}) == 8",
			Message = "Point must stay eight bytes",
		};

		string generated = Generator.Generate(assertion);

		StringAssert.Contains(generated, "// Point must stay eight bytes", StringComparison.Ordinal);
		StringAssert.Contains(
			generated,
			"var _ = map[bool]struct{}{false: {}, unsafe.Sizeof(Point{}) == 8: {}}",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a namespace becomes the file's package, named for the last part of its path.
	/// </summary>
	[TestMethod]
	public void Namespace_BecomesThePackageClause()
	{
		SourceFile file = new("shapes");
		NamespaceDeclaration geometry = new("geo.shapes");
		geometry.Members.Add(new UsingAlias("Origin", "Point"));
		file.Members.Add(geometry);

		string generated = Generator.Generate(file);

		StringAssert.StartsWith(generated, $"package shapes{NewLine}", StringComparison.Ordinal);
		StringAssert.Contains(generated, "// geo/shapes: a Go package is named for one directory", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a file with an entry point is in the package Go runs one in, whatever its namespace
	/// says.
	/// </summary>
	[TestMethod]
	public void FileWithAnEntryPoint_IsPackageMain()
	{
		SourceFile file = new("tool");
		NamespaceDeclaration geometry = new("geo");
		geometry.Members.Add(new EntryPoint());
		file.Members.Add(geometry);

		StringAssert.StartsWith(Generator.Generate(file), $"package main{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that one import is written on the line, as a Go file with one import is.
	/// </summary>
	[TestMethod]
	public void OneImport_IsWrittenOnTheLine()
	{
		SourceFile file = new("tool");
		file.Imports.Add("fmt");

		StringAssert.Contains(Generator.Generate(file), $"import \"fmt\"{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that several imports become a block, sorted within each group and with the groups kept
	/// apart — which is what gofmt does to one.
	/// </summary>
	[TestMethod]
	public void SeveralImports_AreABlockSortedWithinItsGroups()
	{
		SourceFile file = new("tool");
		file.Imports.Add("strings");
		file.Imports.Add("fmt");
		file.Imports.Add(string.Empty);
		file.Imports.Add("example.com/thing");

		StringAssert.Contains(
			Generator.Generate(file),
			$"import ({NewLine}\t\"fmt\"{NewLine}\t\"strings\"{NewLine}{NewLine}\t\"example.com/thing\"{NewLine}){NewLine}",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an entry point reaching the command line or answering an exit code is given the
	/// import it needs, since Go has no way to name a package without importing it.
	/// </summary>
	[TestMethod]
	public void EntryPointNeedingTheRuntime_ImportsIt()
	{
		SourceFile file = new("tool");
		file.Members.Add(new EntryPoint { AcceptsArguments = true, ReturnsExitCode = true });

		string generated = Generator.Generate(file);

		StringAssert.Contains(generated, $"import \"os\"{NewLine}", StringComparison.Ordinal);
		StringAssert.Contains(generated, "func run(args []string) int", StringComparison.Ordinal);
		StringAssert.Contains(generated, "\tos.Exit(run(os.Args))", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an entry point that neither reads its arguments nor answers an exit code is a plain
	/// main with nothing imported for it.
	/// </summary>
	[TestMethod]
	public void PlainEntryPoint_IsJustMain()
	{
		SourceFile file = new("tool");
		file.Members.Add(new EntryPoint());

		string generated = Generator.Generate(file);

		Assert.IsFalse(generated.Contains("import", StringComparison.Ordinal), generated);
		StringAssert.Contains(generated, $"func main() {{{NewLine}}}{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a declaration whose type is to be inferred uses the short form, and one that names
	/// its type uses the long one.
	/// </summary>
	[TestMethod]
	public void LocalDeclaration_IsShortOnlyWhereItsTypeIsInferred()
	{
		FunctionDeclaration function = new("count");
		function.Body.Add(new VariableDeclaration("total", "int", Literal.Number(0)));
		function.Body.Add(new VariableDeclaration("guessed", null, Literal.Number(1)) { IsTypeInferred = true });
		function.Body.Add(new VariableDeclaration("later", "int"));

		string generated = Generator.Generate(function);

		StringAssert.Contains(generated, "\tvar total int = 0", StringComparison.Ordinal);
		StringAssert.Contains(generated, "\tguessed := 1", StringComparison.Ordinal);
		StringAssert.Contains(generated, "\tvar later int", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constant declaration is one where Go can hold it, and a var with a note where it
	/// cannot — a Go constant is a value the compiler worked out, never a value with fields in it.
	/// </summary>
	[TestMethod]
	public void Constant_IsAConstOnlyWhereGoCanHoldOne()
	{
		FunctionDeclaration function = new("limits");
		function.Body.Add(new VariableDeclaration("limit", "int", Literal.Number(7)) { IsConstant = true });
		function.Body.Add(new VariableDeclaration("origin", "Point", new ConstructionExpression(new TypeReference("Point")))
		{
			IsConstant = true,
		});

		string generated = Generator.Generate(function);

		StringAssert.Contains(generated, "\tconst limit int = 7", StringComparison.Ordinal);
		StringAssert.Contains(generated, "// origin is constant: a Go const is a number, a string or a bool", StringComparison.Ordinal);
		StringAssert.Contains(generated, "\tvar origin Point = Point{}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a list with no type of its own is given the declaration's, which is the only way Go
	/// takes one — and is the opposite of what C asks for in the same position.
	/// </summary>
	[TestMethod]
	public void BareListInitialiser_TakesTheDeclarationsType()
	{
		ConstructionExpression table = new(type: null);
		table.Arguments.Add(Literal.Number(1));
		table.Arguments.Add(Literal.Number(2));

		FieldDeclaration sizes = new()
		{
			Name = "Sizes",
			Type = new TypeReference("int") { IsArray = true },
			InitialValue = table,
		};

		Assert.AreEqual($"var Sizes []int = []int{{1, 2}}{NewLine}", Generator.Generate(sizes));
	}

	/// <summary>
	/// Tests that a list whose elements are themselves lists is written one per line, so that adding
	/// a row to a generated table touches one line.
	/// </summary>
	[TestMethod]
	public void TableOfRows_IsWrittenOnePerLine()
	{
		ConstructionExpression row = new(new TypeReference("Point"));
		row.Arguments.Add(new MemberInitialiser("X") { Value = Literal.Number(0) });

		ConstructionExpression table = new(type: null);
		table.Arguments.Add(row);

		FieldDeclaration origins = new()
		{
			Name = "Origins",
			Type = new TypeReference("Point") { IsArray = true },
			InitialValue = table,
		};

		Assert.AreEqual(
			$"var Origins []Point = []Point{{{NewLine}\tPoint{{X: 0}},{NewLine}}}{NewLine}",
			Generator.Generate(origins));
	}

	/// <summary>
	/// Tests that a static field becomes a package-level declaration carrying the type's name, since
	/// Go has no static data member and a note in place of the table would lose it.
	/// </summary>
	[TestMethod]
	public void StaticField_BecomesAPackageLevelDeclaration()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new FieldDeclaration
		{
			Name = "Limit",
			Type = "int",
			IsStatic = true,
			IsConstant = true,
			InitialValue = Literal.Number(7),
		});

		StringAssert.Contains(Generator.Generate(holder), $"const HolderLimit int = 7{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a declaration whose name disagrees with the visibility it asked for is told so,
	/// since Go says visibility with the name and renaming it here would not rename what refers to
	/// it.
	/// </summary>
	[TestMethod]
	public void VisibilityTheNameContradicts_IsANote()
	{
		ClassDeclaration hidden = new("Marker") { Visibility = Visibility.Private };
		ClassDeclaration shown = new("marker") { Visibility = Visibility.Public };

		StringAssert.Contains(
			Generator.Generate(hidden),
			"// Marker is private: in Go that is the case of the first letter, so the name says exported instead",
			StringComparison.Ordinal);
		StringAssert.Contains(
			Generator.Generate(shown),
			"// marker is public: in Go that is the case of the first letter, so the name says unexported instead",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a declaration whose name already says what it asked for is left to say it, and that
	/// one asking for nothing is left alone too.
	/// </summary>
	[TestMethod]
	public void VisibilityTheNameAgreesWith_IsNotMentioned()
	{
		ClassDeclaration shown = new("Marker") { Visibility = Visibility.Public };
		ClassDeclaration hidden = new("marker") { Visibility = Visibility.Internal };
		ClassDeclaration unsaid = new("marker");

		foreach (string generated in new[] { shown, hidden, unsaid }.Select(Generator.Generate))
		{
			Assert.IsFalse(generated.Contains("//", StringComparison.Ordinal), generated);
		}
	}

	/// <summary>
	/// Tests that a declaration the language cannot refuse a call to is written as a note rather than
	/// as a declaration that would allow one.
	/// </summary>
	[TestMethod]
	public void DeletedDeclaration_IsANote()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new FunctionDeclaration("Copy") { Definition = FunctionDefinition.Deleted });

		StringAssert.Contains(Generator.Generate(holder), "// Copy is deleted: Go cannot refuse a call", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a declaration the language supplies is written as a note naming what it supplies
	/// instead.
	/// </summary>
	[TestMethod]
	public void DefaultedDeclaration_IsANote()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new FunctionDeclaration("Holder")
		{
			Kind = FunctionKind.Constructor,
			Definition = FunctionDefinition.Defaulted,
		});

		StringAssert.Contains(
			Generator.Generate(holder),
			"// NewHolder is defaulted: Go gives every type a zero value instead",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a member with no body on a struct is a note, since only an interface may require
	/// one.
	/// </summary>
	[TestMethod]
	public void AbstractMemberOfAStruct_IsANote()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new FunctionDeclaration("Draw") { IsAbstract = true });

		StringAssert.Contains(
			Generator.Generate(holder),
			"// Draw has no body: only an interface may require one",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a specialisation is a note, because Go declares a method only in the package
	/// declaring its type and so has nothing that attaches facts to one from outside.
	/// </summary>
	[TestMethod]
	public void Specialisation_IsANote()
	{
		ClassDeclaration describe = new("Describe")
		{
			SpecialisationArguments = { new TypeReference("Point") },
		};

		StringAssert.Contains(
			Generator.Generate(describe),
			"// Describe for Point: Go attaches a method only in the package declaring its type",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constant on an interface is a note, since a Go interface holds methods.
	/// </summary>
	[TestMethod]
	public void ConstantOnAnInterface_IsANote()
	{
		ClassDeclaration describe = new("Describe") { Kind = TypeDeclarationKind.Interface };
		describe.Members.Add(new FieldDeclaration { Name = "Name", Type = "str" });

		StringAssert.Contains(
			Generator.Generate(describe),
			"// Name: a Go interface holds methods, so a constant belongs to what implements it",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a type declared inside another is written beside it, since Go nests nothing but a
	/// function.
	/// </summary>
	[TestMethod]
	public void NestedType_IsWrittenBesideTheOneDeclaringIt()
	{
		ClassDeclaration outer = new("Outer");
		outer.Members.Add(new ClassDeclaration("Inner"));

		StringAssert.StartsWith(Generator.Generate(outer), $"type Inner struct{{}}{NewLine}", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a parameter's default value is written beside it, since Go has none and the caller
	/// has to pass one.
	/// </summary>
	[TestMethod]
	public void OptionalParameter_KeepsItsDefaultAsAComment()
	{
		FunctionDeclaration function = new("greet");
		function.Parameters.Add(new Parameter("times", "int") { IsOptional = true, DefaultValue = "1" });

		StringAssert.Contains(Generator.Generate(function), "(times int /* = 1 */)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that bitwise complement is spelled the way Go spells it, which is the one operator in
	/// the AST's vocabulary it does not share with the C family.
	/// </summary>
	[TestMethod]
	public void BitwiseNot_IsSpelledWithACaret()
	{
		FunctionDeclaration function = new("mask") { ReturnType = "int" };
		function.Body.Add(new ReturnStatement(
			new UnaryExpression(UnaryOperator.BitwiseNot, new VariableReference("bits"))));

		StringAssert.Contains(Generator.Generate(function), "\treturn (^bits)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that documentation is written as the ordinary comment Go reads as documentation, with no
	/// second marker to make it one.
	/// </summary>
	[TestMethod]
	public void Documentation_IsAnOrdinaryComment()
	{
		ClassDeclaration point = new("Point");
		point.Documentation.Add("Somewhere on a surface.");

		StringAssert.StartsWith(Generator.Generate(point), $"// Somewhere on a surface.{NewLine}type Point", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a struct's fields have their types lined up, which is what gofmt does and therefore
	/// what the file has to look like already.
	/// </summary>
	[TestMethod]
	public void StructFields_AreLinedUp()
	{
		ClassDeclaration circle = new("Circle");
		circle.Members.Add(new VariableDeclaration("X", "int"));
		circle.Members.Add(new VariableDeclaration("Radius", "double"));

		Assert.AreEqual(
			$"type Circle struct {{{NewLine}\tX      int{NewLine}\tRadius float64{NewLine}}}{NewLine}",
			Generator.Generate(circle));
	}
}
