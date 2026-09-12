// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.CodeBlocker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="CGenerator"/>.
/// </summary>
/// <remarks>
/// C is the one target with no classes, no namespaces, no overloading and no generics, so most of
/// what is pinned here is what it writes in their place — and that each of those substitutions is
/// the one C code written by hand uses, rather than a comment apologising for the language.
/// </remarks>
[TestClass]
public class CGeneratorTests
{
	private CGenerator Generator { get; } = new();

	private static string NewLine => CodeBlocker.DefaultNewLineString;

	/// <summary>
	/// Tests that the generator reports the identity the registry and file writer rely on.
	/// </summary>
	[TestMethod]
	public void Identity_IsC()
	{
		Assert.AreEqual("c", Generator.LanguageId);
		Assert.AreEqual("C", Generator.DisplayName);
		Assert.AreEqual("c", Generator.FileExtension);
	}

	/// <summary>
	/// Tests that a function with no parameters says so. An empty list in C declares a function whose
	/// parameters are unspecified, which is the opposite of what the declaration means.
	/// </summary>
	[TestMethod]
	public void EmptyFunction_TakesVoidAndReturnsVoid()
	{
		FunctionDeclaration function = new("doNothing");

		string code = Generator.Generate(function);

		Assert.AreEqual($"void doNothing(void){NewLine}{{{NewLine}}}{NewLine}", code);
	}

	/// <summary>
	/// Tests that the AST's language-neutral type names are mapped to C spellings, in both the return
	/// position and the parameter list.
	/// </summary>
	[TestMethod]
	public void Types_AreMappedToCSpellings()
	{
		FunctionDeclaration function = new("greet") { ReturnType = "str" };
		function.Parameters.Add(new Parameter("name", "str"));
		function.Parameters.Add(new Parameter("times", "long"));

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "const char* greet(const char* name, long long times)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an unrecognized type name is passed through, so a caller can name a real C type.
	/// </summary>
	[TestMethod]
	public void UnknownType_IsPassedThrough()
	{
		FunctionDeclaration function = new("make") { ReturnType = "struct Widget" };

		StringAssert.Contains(Generator.Generate(function), "struct Widget make(void)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a list becomes a pointer to its element. C has no container type, and the length is
	/// a second thing the program carries beside it.
	/// </summary>
	[TestMethod]
	public void ListType_BecomesAPointerToItsElement()
	{
		FunctionDeclaration function = new("names")
		{
			ReturnType = new TypeReference("list") { TypeArguments = { new TypeReference("int") } },
		};

		StringAssert.Contains(Generator.Generate(function), "int* names(void)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a type's arguments are folded into its name, which is what the macro that generated
	/// such a type in C would have called it.
	/// </summary>
	[TestMethod]
	public void GenericType_FoldsItsArgumentsIntoTheName()
	{
		FunctionDeclaration function = new("first")
		{
			ReturnType = new TypeReference("Vector") { TypeArguments = { new TypeReference("int") } },
		};

		StringAssert.Contains(Generator.Generate(function), "Vector_int first(void)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a reference is a pointer. C has no references, and the one thing the distinction
	/// carries cannot be said about a C parameter either way.
	/// </summary>
	[TestMethod]
	public void Reference_IsAPointer()
	{
		FunctionDeclaration function = new("touch");
		function.Parameters.Add(new Parameter("value")
		{
			Type = new TypeReference("Widget") { Indirection = TypeIndirection.Reference },
		});

		StringAssert.Contains(Generator.Generate(function), "void touch(Widget* value)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a member function becomes a free function named for its type and taking the
	/// instance as its first parameter, and that a member that promises not to modify what it is
	/// called on receives a pointer to const.
	/// </summary>
	[TestMethod]
	public void MemberFunction_TakesTheInstanceAsItsFirstParameter()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration translate = new("translate");
		translate.Parameters.Add(new Parameter("dx", "int"));
		point.Members.Add(translate);
		point.Members.Add(new FunctionDeclaration("length") { ReturnType = "double", IsReadOnly = true });

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "void Point_translate(Point* self, int dx)", StringComparison.Ordinal);
		StringAssert.Contains(code, "double Point_length(const Point* self)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a static member acts on no instance, so it takes none — and that a member function
	/// with nothing else to take says <c>void</c>.
	/// </summary>
	[TestMethod]
	public void StaticMember_TakesNoInstance()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new FunctionDeclaration("origin") { ReturnType = "Point", IsStatic = true });

		StringAssert.Contains(Generator.Generate(point), "Point Point_origin(void)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constructor becomes a function returning the value it built, with the initialiser
	/// list written as the designated initialiser C invented.
	/// </summary>
	[TestMethod]
	public void Constructor_ReturnsTheValueItBuilt()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration create = new("Point") { Kind = FunctionKind.Constructor };
		create.Parameters.Add(new Parameter("x", "int"));
		create.Initialisers.Add(new MemberInitialiser("x") { Value = new VariableReference("x") });
		point.Members.Add(create);

		string code = Generator.Generate(point);

		StringAssert.Contains(code, "Point Point_create(int x)", StringComparison.Ordinal);
		StringAssert.Contains(code, "    Point self = { .x = x };", StringComparison.Ordinal);
		StringAssert.Contains(code, "    return self;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a constructor with nothing to initialise starts from a zeroed value, so every
	/// member of what it returns has a value whether or not anybody named it.
	/// </summary>
	[TestMethod]
	public void Constructor_WithNoInitialisers_StartsFromZero()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new FunctionDeclaration("Point") { Kind = FunctionKind.Constructor });

		StringAssert.Contains(Generator.Generate(point), "    Point self = {0};", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a destructor is a function acting on an instance, since C frees nothing by itself.
	/// </summary>
	[TestMethod]
	public void Destructor_ActsOnTheInstance()
	{
		ClassDeclaration buffer = new("Buffer");
		buffer.Members.Add(new FunctionDeclaration("Buffer") { Kind = FunctionKind.Destructor });

		StringAssert.Contains(Generator.Generate(buffer), "void Buffer_destroy(Buffer* self)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an operator, which C cannot declare as one, is named by the word for what it does.
	/// </summary>
	[TestMethod]
	public void Operator_IsNamedByItsWord()
	{
		ClassDeclaration vector = new("Vec") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration plus = new("+") { Kind = FunctionKind.Operator, ReturnType = "Vec", IsReadOnly = true };
		plus.Parameters.Add(new Parameter("other", "Vec"));
		vector.Members.Add(plus);

		StringAssert.Contains(
			Generator.Generate(vector),
			"Vec Vec_add(const Vec* self, Vec other)",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a conversion is named for what it converts to, which is the only part of it C can
	/// keep.
	/// </summary>
	[TestMethod]
	public void ConversionOperator_IsNamedForItsTarget()
	{
		ClassDeclaration weight = new("Weight") { Kind = TypeDeclarationKind.Struct };
		weight.Members.Add(new FunctionDeclaration("ignored")
		{
			Kind = FunctionKind.ConversionOperator,
			ReturnType = "double",
			IsReadOnly = true,
		});

		StringAssert.Contains(
			Generator.Generate(weight),
			"double Weight_to_double(const Weight* self)",
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a private function takes internal linkage, which is the only privacy C has.
	/// </summary>
	[TestMethod]
	public void PrivateFunction_IsStatic()
	{
		FunctionDeclaration function = new("helper") { Visibility = Visibility.Private };

		StringAssert.StartsWith(Generator.Generate(function), "static void helper(void)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a declaration with no definition — abstract, or one the language would have
	/// supplied — becomes the prototype that is all C has to offer for either.
	/// </summary>
	[TestMethod]
	public void DeclarationWithoutADefinition_IsAPrototype()
	{
		FunctionDeclaration abstractFunction = new("draw") { IsAbstract = true };
		FunctionDeclaration defaulted = new("copy") { Definition = FunctionDefinition.Defaulted };

		Assert.AreEqual($"void draw(void);{NewLine}", Generator.Generate(abstractFunction));
		Assert.AreEqual($"void copy(void);{NewLine}", Generator.Generate(defaulted));
	}

	/// <summary>
	/// Tests that a deleted declaration is a note rather than a prototype. C cannot refuse a call, so
	/// writing the prototype would make legal exactly what the declaration exists to forbid.
	/// </summary>
	[TestMethod]
	public void DeletedFunction_IsANoteRatherThanAPrototype()
	{
		ClassDeclaration handle = new("Handle") { Kind = TypeDeclarationKind.Struct };
		handle.Members.Add(new FunctionDeclaration("Handle")
		{
			Kind = FunctionKind.Constructor,
			Definition = FunctionDefinition.Deleted,
		});

		string code = Generator.Generate(handle);

		StringAssert.Contains(code, "// Handle_create is deleted", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("Handle Handle_create", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that an interface becomes a struct of function pointers, each taking the instance as an
	/// untyped pointer — which is how C dispatches dynamically, and what an interface is for.
	/// </summary>
	[TestMethod]
	public void Interface_BecomesAStructOfFunctionPointers()
	{
		ClassDeclaration shape = new("Shape") { Kind = TypeDeclarationKind.Interface };
		FunctionDeclaration draw = new("draw") { IsAbstract = true };
		draw.Parameters.Add(new Parameter("scale", "double"));
		shape.Members.Add(draw);
		shape.Members.Add(new FunctionDeclaration("area") { ReturnType = "double", IsReadOnly = true, IsAbstract = true });

		string code = Generator.Generate(shape);

		StringAssert.Contains(code, "typedef struct Shape", StringComparison.Ordinal);
		StringAssert.Contains(code, "    void (*draw)(void* self, double scale);", StringComparison.Ordinal);
		StringAssert.Contains(code, "    double (*area)(const void* self);", StringComparison.Ordinal);
		StringAssert.Contains(code, "} Shape;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a base type becomes the first member. A struct whose first member is another is
	/// layout-compatible with it, so the position is what makes the substitution legal rather than
	/// merely conventional.
	/// </summary>
	[TestMethod]
	public void BaseType_BecomesTheFirstMember()
	{
		ClassDeclaration circle = new("Circle") { BaseType = "Shape" };
		circle.Members.Add(new VariableDeclaration("radius", "double"));

		string code = Generator.Generate(circle);

		int baseAt = code.IndexOf("Shape base;", StringComparison.Ordinal);
		int radiusAt = code.IndexOf("double radius;", StringComparison.Ordinal);

		Assert.IsTrue(baseAt >= 0, "the base should be a member");
		Assert.IsTrue(baseAt < radiusAt, "the base should come first");
	}

	/// <summary>
	/// Tests that a type is written as a typedef of a struct that also keeps its tag, so a caller
	/// writes <c>Point</c> rather than <c>struct Point</c> and a struct can still point at its own
	/// kind.
	/// </summary>
	[TestMethod]
	public void Struct_IsTypedefedAndKeepsItsTag()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int"));

		string code = Generator.Generate(point);

		Assert.AreEqual(
			$"typedef struct Point{NewLine}{{{NewLine}    int x;{NewLine}}} Point;{NewLine}",
			code);
	}

	/// <summary>
	/// Tests that a member's initial value becomes a note. C has no default member initialisers, and
	/// whoever writes the initialiser for the struct is the one who needs to know what it was.
	/// </summary>
	[TestMethod]
	public void StructMember_Initialiser_IsANote()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Members.Add(new VariableDeclaration("x", "int", new LiteralExpression<int>(3)));

		StringAssert.Contains(Generator.Generate(point), "int x;  // defaults to 3", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an enumeration is typedefed and its members qualified by its name. A C enumeration
	/// is unscoped, so two enumerations with a <c>None</c> each would otherwise be one name declared
	/// twice.
	/// </summary>
	[TestMethod]
	public void Enum_IsTypedefedAndItsMembersQualified()
	{
		EnumDeclaration colour = new("Colour");
		colour.Members.Add(new EnumMember("Red") { Value = "1" });
		colour.Members.Add(new EnumMember("Green"));

		string code = Generator.Generate(colour);

		Assert.AreEqual(
			$"typedef enum Colour{NewLine}{{{NewLine}    Colour_Red = 1,{NewLine}    Colour_Green,{NewLine}}} Colour;{NewLine}",
			code);
	}

	/// <summary>
	/// Tests that a member already named for its enumeration is left alone, rather than qualified a
	/// second time by a generator that cannot tell it has already been done.
	/// </summary>
	[TestMethod]
	public void Enum_AlreadyQualifiedMember_IsNotQualifiedTwice()
	{
		EnumDeclaration colour = new("Colour");
		colour.Members.Add(new EnumMember("Colour_Red"));

		string code = Generator.Generate(colour);

		StringAssert.Contains(code, "    Colour_Red,", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("Colour_Colour_Red", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a fixed underlying type is written as a note rather than as syntax, since the
	/// spelling that pins it is C23's and the guarantee is better asserted than assumed.
	/// </summary>
	[TestMethod]
	public void Enum_UnderlyingType_IsANote()
	{
		EnumDeclaration colour = new("Colour") { UnderlyingType = "int" };
		colour.Members.Add(new EnumMember("Red"));

		string code = Generator.Generate(colour);

		StringAssert.StartsWith(code, "// underlying type int", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("enum Colour : int", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a constant is <c>static const</c>. A file-scope <c>const</c> in C has external
	/// linkage, so a header declaring one and included twice is the same object defined twice.
	/// </summary>
	[TestMethod]
	public void ConstantField_IsStaticConst()
	{
		FieldDeclaration limit = new()
		{
			Name = "MaxItems",
			Type = new TypeReference("int") { IsReadOnly = true },
			IsConstant = true,
			InitialValue = new LiteralExpression<int>(16),
		};

		string code = Generator.Generate(limit);

		Assert.AreEqual($"static const int MaxItems = 16;{NewLine}", code);
	}

	/// <summary>
	/// Tests that a constant table is written as an initialiser rather than as a compound literal.
	/// Only the braced form is a constant expression, which is what an object with static storage
	/// duration has to be initialised by.
	/// </summary>
	[TestMethod]
	public void ConstantTable_IsAnInitialiserRatherThanACompoundLiteral()
	{
		ConstructionExpression rows = new(type: null);
		ConstructionExpression row = new(new TypeReference("Point"));
		row.Arguments.Add(new MemberInitialiser("x") { Value = new LiteralExpression<int>(1) });
		rows.Arguments.Add(row);

		FieldDeclaration table = new()
		{
			Name = "origins",
			Type = new TypeReference("Point") { IsArray = true },
			IsConstant = true,
			InitialValue = rows,
		};

		string code = Generator.Generate(table);

		StringAssert.Contains(code, "static const Point origins[] = {", StringComparison.Ordinal);
		StringAssert.Contains(code, "    { .x = 1 },", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("(Point){", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a construction standing where a value is wanted keeps its type, since that is the
	/// one position where C needs a compound literal to know what is being built.
	/// </summary>
	[TestMethod]
	public void Construction_AsAValue_IsACompoundLiteral()
	{
		ConstructionExpression origin = new(new TypeReference("Point"));
		origin.Arguments.Add(new MemberInitialiser("x") { Value = new LiteralExpression<int>(1) });

		FunctionDeclaration make = new("make") { ReturnType = "Point" };
		make.Body.Add(new ReturnStatement(origin));

		StringAssert.Contains(Generator.Generate(make), "return (Point){ .x = 1 };", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an assertion uses C11's own spelling and always carries a message, since C11
	/// requires one and the condition is a better default than nothing.
	/// </summary>
	[TestMethod]
	public void CompileTimeAssertion_UsesStaticAssertAndAlwaysHasAMessage()
	{
		CompileTimeAssertion withMessage = new()
		{
			Condition = "sizeof(Point) == 8",
			Message = "Point must stay two ints",
		};
		CompileTimeAssertion bare = new() { Condition = "sizeof(Point) == 8" };

		StringAssert.Contains(
			Generator.Generate(withMessage),
			"_Static_assert(sizeof(Point) == 8,",
			StringComparison.Ordinal);
		StringAssert.Contains(
			Generator.Generate(withMessage),
			"\"Point must stay two ints\");",
			StringComparison.Ordinal);
		StringAssert.Contains(Generator.Generate(bare), "\"sizeof(Point) == 8\");", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an alias is a typedef, and that an alias for an array puts its brackets after the
	/// name being declared rather than after the type.
	/// </summary>
	[TestMethod]
	public void UsingAlias_IsATypedef()
	{
		UsingAlias alias = new()
		{
			Name = "Radii",
			AliasedType = new TypeReference("double") { IsArray = true },
		};

		Assert.AreEqual($"typedef double Radii[];{NewLine}", Generator.Generate(alias));
	}

	/// <summary>
	/// Tests that a parameter's default value is written beside it as a comment. C has no default
	/// arguments, so the caller has to pass one — and the value the declaration chose is what they
	/// need in order to pass the same thing.
	/// </summary>
	[TestMethod]
	public void OptionalParameter_KeepsItsDefaultAsAComment()
	{
		FunctionDeclaration function = new("repeat");
		function.Parameters.Add(new Parameter("times", "int") { IsOptional = true, DefaultValue = "1" });

		StringAssert.Contains(Generator.Generate(function), "void repeat(int times /* = 1 */)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the entry point is C's <c>main</c>, saying <c>void</c> where it takes nothing.
	/// </summary>
	[TestMethod]
	public void EntryPoint_IsMain()
	{
		EntryPoint bare = new();
		EntryPoint withArguments = new() { AcceptsArguments = true };

		StringAssert.Contains(Generator.Generate(bare), "int main(void)", StringComparison.Ordinal);
		StringAssert.Contains(Generator.Generate(withArguments), "int main(int argc, char* argv[])", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a namespace is written as a comment over flat members. Folding the name into every
	/// declaration would rename the declarations without renaming the references to them.
	/// </summary>
	[TestMethod]
	public void Namespace_IsACommentOverFlatMembers()
	{
		NamespaceDeclaration ns = new("geo.shapes");
		ns.Members.Add(new FunctionDeclaration("area") { ReturnType = "double" });

		string code = Generator.Generate(ns);

		StringAssert.StartsWith(code, "// namespace geo_shapes", StringComparison.Ordinal);
		StringAssert.Contains(code, $"{NewLine}double area(void)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a header says so once and writes its includes, keeping the delimiters the caller
	/// chose and quoting a path that carries none.
	/// </summary>
	[TestMethod]
	public void Header_WritesPragmaOnceAndItsIncludes()
	{
		SourceFile file = new("shape") { IsHeader = true };
		file.Imports.Add("<stdbool.h>");
		file.Imports.Add("geometry/vector.h");
		file.Members.Add(new FunctionDeclaration("area") { ReturnType = "double" });

		string code = Generator.Generate(file);

		StringAssert.StartsWith(code, "#pragma once", StringComparison.Ordinal);
		StringAssert.Contains(code, "#include <stdbool.h>", StringComparison.Ordinal);
		StringAssert.Contains(code, "#include \"geometry/vector.h\"", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a source file that is not a header writes no include guard, since nothing includes
	/// it.
	/// </summary>
	[TestMethod]
	public void NonHeader_WritesNoPragmaOnce()
	{
		SourceFile file = new("main");
		file.Members.Add(new EntryPoint());

		Assert.IsFalse(Generator.Generate(file).Contains("#pragma once", StringComparison.Ordinal));
	}

	/// <summary>
	/// Tests that a local declaration keeps the type it was given and that a constant one is
	/// <c>const</c> — with no linkage to collide with, a local needs nothing more.
	/// </summary>
	[TestMethod]
	public void LocalDeclaration_WritesItsTypeAndConst()
	{
		FunctionDeclaration function = new("run");
		function.Body.Add(new VariableDeclaration("limit", "int", new LiteralExpression<int>(4)) { IsConstant = true });
		function.Body.Add(new VariableDeclaration("name", "str"));

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "    const int limit = 4;", StringComparison.Ordinal);
		StringAssert.Contains(code, "    const char* name;", StringComparison.Ordinal);
	}
}
