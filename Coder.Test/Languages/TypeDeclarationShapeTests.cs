// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Linq;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.CodeBlocker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// What a type declaration says about itself beyond its name, in each target.
/// </summary>
/// <remarks>
/// Four things: the interfaces it implements, and whether the language supplies its value
/// semantics, whether the rest of it may be declared elsewhere, and whether any member of it
/// modifies it. Each target answers all four, and the answers are what is pinned here — including
/// the ones that are a comment, because "this language has no word for it" is a decision somebody
/// made rather than something that fell out.
/// <para>
/// The interfaces are the interesting half. Every target has a different amount of the idea: C++
/// does not distinguish an interface from a base class at all, C can give the first-member position
/// to only one of them, Rust answers it exactly for a trait and not at all for a struct, and Go
/// satisfies one structurally and so writes an assertion rather than a declaration.
/// </para>
/// </remarks>
[TestClass]
public class TypeDeclarationShapeTests
{
	private static string NewLine => CodeBlocker.DefaultNewLineString;

	/// <summary>
	/// A type implementing two interfaces and deriving from a base.
	/// </summary>
	/// <param name="kind">What kind of type it declares.</param>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration Widget(TypeDeclarationKind kind = TypeDeclarationKind.Class)
	{
		ClassDeclaration widget = new("Widget") { Kind = kind, BaseType = "Control" };
		widget.Interfaces.Add(TypeReference.Parse("Drawable"));
		widget.Interfaces.Add(TypeReference.Parse("Clickable"));
		return widget;
	}

	/// <summary>
	/// A type asking for all three modifiers at once.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration Money() =>
		new("Money")
		{
			Kind = TypeDeclarationKind.Struct,
			IsRecord = true,
			IsPartial = true,
			IsReadOnly = true,
		};

	private static string Generate(ILanguageGenerator generator, AstNode node) => generator.Generate(node);

	// ------------------------------------------------------------------ Interfaces

	/// <summary>
	/// C# writes the base and the interfaces in one list, base first, which is the order the
	/// language requires and the reason the AST keeps the two apart.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesOneInheritanceListWithTheBaseFirst()
	{
		string code = Generate(new CSharpGenerator(), Widget());

		StringAssert.Contains(code, "class Widget : Control, Drawable, Clickable");
	}

	/// <summary>
	/// C++ makes every entry public, and does not distinguish an interface from a base.
	/// </summary>
	/// <remarks>
	/// Public rather than the default, which for a <c>class</c> is private: a base nobody outside
	/// could use the type through is not what the declaration asked for.
	/// </remarks>
	[TestMethod]
	public void Cpp_InheritsPubliclyFromEveryOneOfThem()
	{
		string code = Generate(new CppGenerator(), Widget());

		StringAssert.Contains(code, "class Widget : public Control, public Drawable, public Clickable");
	}

	/// <summary>
	/// C embeds each as a member, and says which one holds the position that makes a pointer to the
	/// whole a pointer to it.
	/// </summary>
	[TestMethod]
	public void C_EmbedsEachAndSaysWhichOneIsLayoutCompatible()
	{
		string code = Generate(new CGenerator(), Widget());

		StringAssert.Contains(code, "base is first, so that a pointer to this is a pointer to it");
		StringAssert.Contains(code, "the rest are reached by taking their address");
		StringAssert.Contains(code, $"Control base;{NewLine}");
		StringAssert.Contains(code, $"Drawable drawable;{NewLine}");
		StringAssert.Contains(code, $"Clickable clickable;{NewLine}");
	}

	/// <summary>
	/// A trait takes all of them as supertraits, which is the one place a target answers the
	/// question exactly rather than working around it.
	/// </summary>
	[TestMethod]
	public void Rust_MakesEveryInterfaceOnATraitASupertrait()
	{
		string code = Generate(new RustGenerator(), Widget(TypeDeclarationKind.Interface));

		StringAssert.Contains(code, "trait Widget: Control + Drawable + Clickable");
	}

	/// <summary>
	/// A struct's interfaces are written down rather than implemented, because an impl block needs
	/// the bodies the declaration does not have.
	/// </summary>
	[TestMethod]
	public void Rust_SaysWhatAStructImplementsRatherThanImplementingIt()
	{
		string code = Generate(new RustGenerator(), Widget());

		StringAssert.Contains(code, "// implements Drawable, Clickable");
		StringAssert.Contains(code, "which this declaration does not say how to fill");
	}

	/// <summary>
	/// Python has one list of bases and no separate notion of an interface.
	/// </summary>
	[TestMethod]
	public void Python_TakesThemAllAsBases()
	{
		string code = Generate(new PythonGenerator(), Widget());

		StringAssert.Contains(code, "class Widget(Control, Drawable, Clickable):");
	}

	/// <summary>
	/// JavaScript extends one thing and has no interfaces, so it writes down what it cannot say.
	/// </summary>
	[TestMethod]
	public void JavaScript_WritesDownWhatItCannotExtend()
	{
		string code = Generate(new JavaScriptGenerator(), Widget());

		StringAssert.Contains(code, "// implements Drawable, Clickable");
		StringAssert.Contains(code, "class Widget extends Control");
	}

	/// <summary>
	/// A Go interface embeds the others by name, which means every method of each.
	/// </summary>
	[TestMethod]
	public void Go_EmbedsEveryInterfaceInAnInterface()
	{
		string code = Generate(new GoGenerator(), Widget(TypeDeclarationKind.Interface));

		StringAssert.Contains(code, $"Control{NewLine}");
		StringAssert.Contains(code, $"Drawable{NewLine}");
		StringAssert.Contains(code, $"Clickable{NewLine}");
	}

	/// <summary>
	/// A Go struct asserts what it implements rather than declaring it, because Go satisfies an
	/// interface by having the methods and never says so.
	/// </summary>
	/// <remarks>
	/// The pointer form is the one that always holds: a method on the pointer receiver is not in
	/// the value's method set, and one on the value is in both.
	/// </remarks>
	[TestMethod]
	public void Go_AssertsWhatAStructImplements()
	{
		string code = Generate(new GoGenerator(), Widget());

		StringAssert.Contains(code, "var _ Drawable = (*Widget)(nil)");
		StringAssert.Contains(code, "var _ Clickable = (*Widget)(nil)");
	}

	/// <summary>
	/// A declaration with no interfaces writes nothing about them anywhere.
	/// </summary>
	/// <remarks>
	/// Over every generator at once, because "nothing to say" is the common case and a note that
	/// appeared on every type in a file would be worse than the gap it filled.
	/// </remarks>
	[TestMethod]
	public void EveryTarget_SaysNothingWhenThereAreNoInterfaces()
	{
		ILanguageGenerator[] generators =
		[
			new CSharpGenerator(),
			new CppGenerator(),
			new CGenerator(),
			new RustGenerator(),
			new PythonGenerator(),
			new JavaScriptGenerator(),
			new GoGenerator(),
		];

		foreach (ILanguageGenerator generator in generators)
		{
			string code = Generate(generator, new ClassDeclaration("Plain"));

			Assert.DoesNotContain("implements", code, $"{generator.DisplayName} wrote about interfaces it was not given.");
			Assert.DoesNotContain("var _ ", code, $"{generator.DisplayName} asserted an interface it was not given.");
		}
	}

	// ------------------------------------------------------------------ Modifiers

	/// <summary>
	/// C# is the one target with a word for all three, and writes them in the order it takes them.
	/// </summary>
	/// <remarks>
	/// <c>record</c> goes before the keyword rather than instead of it, which is what makes
	/// <c>record struct</c> reachable at all.
	/// </remarks>
	[TestMethod]
	public void CSharp_WritesAllThreeModifiers()
	{
		string code = Generate(new CSharpGenerator(), Money());

		StringAssert.Contains(code, "public readonly partial record struct Money");
	}

	/// <summary>
	/// Rust asks for the record's members with derive, which is what derive is for.
	/// </summary>
	[TestMethod]
	public void Rust_DerivesWhatARecordAsksFor()
	{
		string code = Generate(new RustGenerator(), Money());

		StringAssert.Contains(code, "#[derive(Clone, Debug, PartialEq)]");
		Assert.DoesNotContain("// record:", code, "Rust said it in the derive; it should not also apologise for it.");
	}

	/// <summary>
	/// A target with no word for a promise writes the promise down.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <remarks>
	/// Both of these are claims about the type — it compares by value, no member of it modifies it —
	/// so a file that dropped one would look like a file that still made it.
	/// </remarks>
	[TestMethod]
	[DataRow("cpp")]
	[DataRow("c")]
	[DataRow("python")]
	[DataRow("javascript")]
	[DataRow("go")]
	public void TargetsWithNoWordForThem_WriteThePromisesDown(string language)
	{
		ILanguageGenerator generator = language switch
		{
			"cpp" => new CppGenerator(),
			"c" => new CGenerator(),
			"python" => new PythonGenerator(),
			"javascript" => new JavaScriptGenerator(),
			_ => new GoGenerator(),
		};

		string code = Generate(generator, Money());

		StringAssert.Contains(code, "record: compares by value, and copies and prints itself");
		StringAssert.Contains(code, "readonly: no member of this type modifies it");
	}

	/// <summary>
	/// Nothing writes a note about <c>partial</c>, which is the one modifier here that is dropped
	/// in silence.
	/// </summary>
	/// <remarks>
	/// It claims nothing about the type. <c>record</c> and <c>readonly</c> say how the type behaves;
	/// <c>partial</c> says the rest of it may be declared in another file, and a generator that has
	/// written the whole declaration has not used that permission for anything a reader of this file
	/// could be missing.
	/// </remarks>
	[TestMethod]
	public void Partial_IsDroppedInSilenceEverywhereButCSharp()
	{
		ClassDeclaration split = new("Split") { IsPartial = true };

		ILanguageGenerator[] generators =
		[
			new CppGenerator(),
			new CGenerator(),
			new RustGenerator(),
			new PythonGenerator(),
			new JavaScriptGenerator(),
			new GoGenerator(),
		];

		foreach (ILanguageGenerator generator in generators)
		{
			Assert.DoesNotContain(
				"partial",
				Generate(generator, split),
				$"{generator.DisplayName} wrote about partial, which says nothing about the type.");
		}

		StringAssert.Contains(Generate(new CSharpGenerator(), split), "public partial class Split");
	}

	// ------------------------------------------------------------------ Type parameters

	/// <summary>
	/// A type written over one parameter with the constraints ktsu.Semantics actually uses.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration Mass()
	{
		ClassDeclaration mass = new("Mass") { Kind = TypeDeclarationKind.Struct };
		mass.TypeParameters.Add(TypeParameter.Parse("T : struct, INumber<T>"));
		return mass;
	}

	/// <summary>
	/// C# writes the names beside the type and the requirements in a clause of their own.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesTheParametersAndAWhereClause()
	{
		string code = Generate(new CSharpGenerator(), Mass());

		StringAssert.Contains(code, "struct Mass<T>");
		StringAssert.Contains(code, "where T : struct, INumber<T>");
	}

	/// <summary>
	/// The order within a clause is the language's rather than the declaration's.
	/// </summary>
	/// <remarks>
	/// C# requires the class or struct constraint first and <c>new()</c> last and rejects any other
	/// order, which the AST has no reason to know. A caller listing them as they think of them
	/// should still get a file that compiles.
	/// </remarks>
	[TestMethod]
	public void CSharp_PutsTheConstraintsInTheOrderTheLanguageAccepts()
	{
		ClassDeclaration table = new("Table");
		table.TypeParameters.Add(TypeParameter.Parse("T : new(), IComparable<T>, class"));

		StringAssert.Contains(
			Generate(new CSharpGenerator(), table),
			"where T : class, IComparable<T>, new()");
	}

	/// <summary>
	/// A generic method carries its own parameters, which are not its type's.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesAMethodsOwnTypeParameters()
	{
		FunctionDeclaration pick = new("Pick") { ReturnType = "T", IsStatic = true };
		pick.TypeParameters.Add(TypeParameter.Parse("T : IComparable<T>"));
		pick.Parameters.Add(new Parameter("first", "T"));

		string code = Generate(new CSharpGenerator(), pick);

		StringAssert.Contains(code, "Pick<T>(");
		StringAssert.Contains(code, "where T : IComparable<T>");
	}

	/// <summary>
	/// C++ writes the parameters as a template and the requirements as a note.
	/// </summary>
	/// <remarks>
	/// A concept is a predicate over a type and can ask anything at all, so there is nothing shared
	/// underneath <c>struct</c> and <c>std::floating_point</c> to translate between — and the
	/// standard ones need an include the AST does not carry, which is the same reason Python does
	/// not get its <c>@dataclass</c>.
	/// </remarks>
	[TestMethod]
	public void Cpp_WritesATemplateAndNotesWhatItCannotRequire()
	{
		string code = Generate(new CppGenerator(), Mass());

		StringAssert.Contains(code, "template <typename T>");
		StringAssert.Contains(code, "requires that T is a value type, and that T is INumber<T>");
	}

	/// <summary>
	/// Rust puts the bounds it has beside the parameter, and notes the one it does not.
	/// </summary>
	/// <remarks>
	/// A trait bound is exactly what <c>Implements</c> means, and <c>Default</c> is exactly what
	/// <c>new()</c> means. "Value type" is not a thing Rust says about a parameter at all — every
	/// type is one — so that is the one written down.
	/// </remarks>
	[TestMethod]
	public void Rust_BoundsWhatItCanAndNotesWhatItCannot()
	{
		string code = Generate(new RustGenerator(), Mass());

		StringAssert.Contains(code, "struct Mass<T: INumber<T>>");
		StringAssert.Contains(code, "requires that T is a value type");
		Assert.DoesNotContain("INumber<T>,", code, "the trait bound should not also be written down.");
	}

	/// <summary>
	/// Rust repeats the parameters on the impl block, where the methods need them.
	/// </summary>
	/// <remarks>
	/// The bounds go on the impl and the bare names on the type it is for —
	/// <c>impl&lt;T: Bound&gt; Mass&lt;T&gt;</c> — which is the one shape that compiles.
	/// </remarks>
	[TestMethod]
	public void Rust_CarriesTheParametersOntoTheImplBlock()
	{
		ClassDeclaration mass = Mass();
		mass.Members.Add(new FunctionDeclaration("value") { ReturnType = "T", IsReadOnly = true });

		StringAssert.Contains(Generate(new RustGenerator(), mass), "impl<T: INumber<T>> Mass<T>");
	}

	/// <summary>
	/// Go writes a generic free function, and writes a generic type's parameters down.
	/// </summary>
	/// <remarks>
	/// A Go method on a generic type needs the parameters in three places and spelled two ways —
	/// <c>NewPoint</c> for the constructor's name but <c>Point[T]</c> for its receiver and its
	/// result — so making a type generic is a change to how this generator writes a whole type
	/// rather than to how it writes one line. A free function has none of that.
	/// </remarks>
	[TestMethod]
	public void Go_WritesAGenericFunctionAndNotesAGenericType()
	{
		FunctionDeclaration first = new("First") { ReturnType = "T" };
		first.TypeParameters.Add(TypeParameter.Parse("T : Ordered"));

		StringAssert.Contains(Generate(new GoGenerator(), first), "First[T Ordered](");
		StringAssert.Contains(Generate(new GoGenerator(), Mass()), "over T : struct, INumber<T>");
	}

	/// <summary>
	/// A target with no generics at all writes the whole parameter down, constraints and all.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	[TestMethod]
	[DataRow("c")]
	[DataRow("python")]
	[DataRow("javascript")]
	public void TargetsWithNoGenerics_WriteTheWholeParameterDown(string language)
	{
		ILanguageGenerator generator = language switch
		{
			"c" => new CGenerator(),
			"python" => new PythonGenerator(),
			_ => new JavaScriptGenerator(),
		};

		StringAssert.Contains(Generate(generator, Mass()), "over T : struct, INumber<T>");
	}

	/// <summary>
	/// Parsing a written parameter and writing it back gives the same text.
	/// </summary>
	/// <remarks>
	/// Which is what lets a document carry a whole parameter on one line. The comma inside
	/// <c>IComparer&lt;T, U&gt;</c> belongs to it rather than separating two constraints, so the
	/// split has to respect the brackets.
	/// </remarks>
	[TestMethod]
	public void TypeParameter_ParseAndToStringAreInverses()
	{
		const string written = "T : class, IComparer<T, U>, new()";

		Assert.AreEqual(written, TypeParameter.Parse(written).ToString());
		Assert.HasCount(3, TypeParameter.Parse(written).Constraints);
		Assert.AreEqual("U", TypeParameter.Parse("U").ToString());
	}

	// ------------------------------------------------------------------ Annotations

	/// <summary>
	/// A declaration carrying the attribute ktsu.Semantics puts on its physics operators.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static FunctionDeclaration Suppressed()
	{
		FunctionDeclaration multiply = new("Multiply") { ReturnType = "Energy", IsStatic = true };
		Annotation suppress = new("SuppressMessage");
		suppress.Arguments.Add("\"Usage\"");
		suppress.Arguments.Add("\"CA2225:Operator overloads have named alternates\"");
		multiply.Annotations.Add(suppress);
		return multiply;
	}

	/// <summary>
	/// The four targets with a metadata syntax each write the same name in their own brackets.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <param name="expected">The line it should write.</param>
	/// <remarks>
	/// The name and the arguments are the caller's, written verbatim — a <c>[TestMethod]</c> means
	/// nothing outside the framework that reads it, so there is nothing underneath for the AST to
	/// translate. What is around them is the language's, and that is the whole of what these four
	/// disagree about.
	/// </remarks>
	[TestMethod]
	[DataRow("csharp", "[Obsolete(\"use Mass\")]")]
	[DataRow("cpp", "[[Obsolete(\"use Mass\")]]")]
	[DataRow("rust", "#[Obsolete(\"use Mass\")]")]
	[DataRow("python", "@Obsolete(\"use Mass\")")]
	public void TargetsWithMetadataSyntax_WriteItInTheirOwnBrackets(string language, string expected)
	{
		ClassDeclaration mass = new("Mass");
		Annotation obsolete = new("Obsolete");
		obsolete.Arguments.Add("\"use Mass\"");
		mass.Annotations.Add(obsolete);

		ILanguageGenerator generator = language switch
		{
			"csharp" => new CSharpGenerator(),
			"cpp" => new CppGenerator(),
			"rust" => new RustGenerator(),
			_ => new PythonGenerator(),
		};

		StringAssert.Contains(Generate(generator, mass), expected);
	}

	/// <summary>
	/// A target with no metadata syntax writes the annotation down rather than dropping it.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <remarks>
	/// A file that quietly loses its <c>[Obsolete]</c> looks like a file that never had one.
	/// </remarks>
	[TestMethod]
	[DataRow("c")]
	[DataRow("javascript")]
	[DataRow("go")]
	public void TargetsWithNoMetadataSyntax_WriteItDown(string language)
	{
		ILanguageGenerator generator = language switch
		{
			"c" => new CGenerator(),
			"javascript" => new JavaScriptGenerator(),
			_ => new GoGenerator(),
		};

		ClassDeclaration mass = new("Mass");
		mass.Annotations.Add(new Annotation("Obsolete"));

		StringAssert.Contains(Generate(generator, mass), "// annotated Obsolete");
	}

	/// <summary>
	/// A function's annotations are written above it, arguments and all.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesAFunctionsAnnotations()
	{
		StringAssert.Contains(
			Generate(new CSharpGenerator(), Suppressed()),
			"[SuppressMessage(\"Usage\", \"CA2225:Operator overloads have named alternates\")]");
	}

	/// <summary>
	/// An annotation survives a trip through YAML, and the comma inside an argument stays inside it.
	/// </summary>
	/// <remarks>
	/// Which is the whole reason the arguments are a sequence rather than one string:
	/// <c>SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")</c> has two
	/// arguments and three commas.
	/// </remarks>
	[TestMethod]
	public void Yaml_CarriesAnAnnotationWithCommasInsideItsArguments()
	{
		FunctionDeclaration original = Suppressed();
		original.Annotations.Add(new Annotation("Pure"));

		ktsu.Coder.Serialization.YamlSerializer serializer = new();
		ktsu.Coder.Serialization.YamlDeserializer deserializer = new();

		FunctionDeclaration restored = (FunctionDeclaration)deserializer.Deserialize(serializer.Serialize(original))!;

		Assert.HasCount(2, restored.Annotations);
		Assert.AreEqual("SuppressMessage", restored.Annotations[0].Name);
		Assert.AreSequenceEqual(
			(string[])["\"Usage\"", "\"CA2225:Operator overloads have named alternates\""],
			[.. restored.Annotations[0].Arguments]);
		Assert.AreEqual("Pure", restored.Annotations[1].Name);
		Assert.IsEmpty(restored.Annotations[1].Arguments);
	}

	// ------------------------------------------------------------------ Properties

	/// <summary>
	/// A type with one property of each kind: the language supplies the storage for one, and the
	/// other has a body.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration Box()
	{
		ClassDeclaration box = new("Box");
		box.Members.Add(new PropertyDeclaration("Value", "int")
		{
			HasSetter = true,
			Visibility = Visibility.Public,
		});

		PropertyDeclaration doubled = new("Doubled", "int") { Visibility = Visibility.Public };
		doubled.GetterBody.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("Value"), BinaryOperator.Multiply, Literal.Number(2))));
		box.Members.Add(doubled);

		return box;
	}

	/// <summary>
	/// The three targets that have properties write them as properties.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <param name="automatic">What the property with no body should look like.</param>
	/// <param name="computed">What the property with a body should start with.</param>
	[TestMethod]
	[DataRow("csharp", "public int Value { get; set; }", "public int Doubled")]
	[DataRow("python", "Value: int = None", "def Doubled(self) -> int:")]
	[DataRow("javascript", "Value;", "get Doubled() ")]
	public void TargetsWithProperties_WriteThemAsProperties(string language, string automatic, string computed)
	{
		ILanguageGenerator generator = language switch
		{
			"csharp" => new CSharpGenerator(),
			"python" => new PythonGenerator(),
			_ => new JavaScriptGenerator(),
		};

		string code = Generate(generator, Box());

		StringAssert.Contains(code, automatic);
		StringAssert.Contains(code, computed);
	}

	/// <summary>
	/// A property with no body is a field, in every target that has no properties.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <param name="expected">The field as that target writes one.</param>
	/// <remarks>
	/// Not an approximation of a property: a property whose accessors have no bodies <em>is</em> a
	/// field with a storage location the compiler supplies, and this is what a person would have
	/// written.
	/// </remarks>
	[TestMethod]
	[DataRow("rust", "pub Value: i32,")]
	[DataRow("go", "Value int")]
	[DataRow("cpp", "int Value{};")]
	[DataRow("c", "int Value;")]
	public void TargetsWithoutProperties_WriteAnAutomaticOneAsAField(string language, string expected)
	{
		ILanguageGenerator generator = language switch
		{
			"rust" => new RustGenerator(),
			"go" => new GoGenerator(),
			"cpp" => new CppGenerator(),
			_ => new CGenerator(),
		};

		StringAssert.Contains(Generate(generator, Box()), expected);
	}

	/// <summary>
	/// A property with a body is a function, and lands where that target puts its functions.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <param name="expected">The function as that target writes one.</param>
	/// <remarks>
	/// The landing is the point. Rust puts data in a <c>struct</c> and behaviour in an <c>impl</c>,
	/// Go writes fields in the type and methods beside it, and C lowers a method to a free function
	/// taking the instance — so a property separated at the point it is written would arrive after
	/// the routing that decides all of that and come out wherever it happened to be standing. It is
	/// separated from the member list first, and each generator's own routing then sees an ordinary
	/// field or an ordinary function.
	/// </remarks>
	[TestMethod]
	[DataRow("rust", "pub fn doubled(&self) -> i32")]
	[DataRow("go", "func (self Box) Doubled() int")]
	[DataRow("cpp", "int doubled() const")]
	[DataRow("c", "int Box_doubled(const Box* self)")]
	public void TargetsWithoutProperties_WriteAComputedOneAsAFunction(string language, string expected)
	{
		ILanguageGenerator generator = language switch
		{
			"rust" => new RustGenerator(),
			"go" => new GoGenerator(),
			"cpp" => new CppGenerator(),
			_ => new CGenerator(),
		};

		StringAssert.Contains(Generate(generator, Box()), expected);
	}

	/// <summary>
	/// A setter a target has to invent a name for gets one in that target's own convention.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <param name="expected">The name it should invent.</param>
	/// <remarks>
	/// The AST renames nothing it was given — a generator that changed a declaration's name would
	/// break every reference to it — but a setter separated out of a property has no name until
	/// somebody makes one up, and making one up in the wrong convention is how generated code
	/// announces itself. rustc warns on a member name that is not snake case, and an unexported Go
	/// name cannot be called from outside its package, so two of these are more than taste.
	/// </remarks>
	[TestMethod]
	[DataRow("rust", "set_value")]
	[DataRow("go", "SetValue")]
	[DataRow("cpp", "set_value")]
	[DataRow("c", "set_value")]
	public void AnInventedSetterName_FollowsTheTargetsOwnConvention(string language, string expected)
	{
		ClassDeclaration box = new("Box");
		PropertyDeclaration guarded = new("Value", "int") { HasSetter = true, Visibility = Visibility.Public };
		guarded.GetterBody.Add(new ReturnStatement(new VariableReference("Value")));
		guarded.SetterBody.Add(new ReturnStatement());
		box.Members.Add(guarded);

		ILanguageGenerator generator = language switch
		{
			"rust" => new RustGenerator(),
			"go" => new GoGenerator(),
			"cpp" => new CppGenerator(),
			_ => new CGenerator(),
		};

		StringAssert.Contains(Generate(generator, box), expected);
	}

	/// <summary>
	/// The one thing a field cannot carry is said rather than dropped.
	/// </summary>
	/// <remarks>
	/// A property may be readable and not writable, and a field is neither or both. Said in the
	/// field's own documentation rather than in a note beside it, so it travels with the
	/// declaration to wherever the target puts it.
	/// </remarks>
	[TestMethod]
	public void AReadOnlyAutomaticProperty_SaysSoWhereItBecomesAField()
	{
		ClassDeclaration box = new("Box");
		box.Members.Add(new PropertyDeclaration("Value", "int") { Visibility = Visibility.Public });

		StringAssert.Contains(
			Generate(new RustGenerator(), box),
			"Value is read-only, which a field is not.");
	}

	/// <summary>
	/// C# writes <c>init</c> where the declaration asked for it.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesAnInitOnlySetter()
	{
		ClassDeclaration box = new("Box");
		box.Members.Add(new PropertyDeclaration("Value", "int")
		{
			HasSetter = true,
			SetterIsInitOnly = true,
			Visibility = Visibility.Public,
		});

		StringAssert.Contains(Generate(new CSharpGenerator(), box), "public int Value { get; init; }");
	}

	/// <summary>
	/// A property survives a trip through YAML, accessor bodies and all.
	/// </summary>
	[TestMethod]
	public void Yaml_CarriesAProperty()
	{
		PropertyDeclaration original = new("Doubled", "int")
		{
			HasGetter = true,
			HasSetter = true,
			SetterIsInitOnly = true,
			Visibility = Visibility.Public,
		};
		original.GetterBody.Add(new ReturnStatement(Literal.Number(4)));

		ktsu.Coder.Serialization.YamlSerializer serializer = new();
		ktsu.Coder.Serialization.YamlDeserializer deserializer = new();

		PropertyDeclaration restored = (PropertyDeclaration)deserializer.Deserialize(serializer.Serialize(original))!;

		Assert.AreEqual("Doubled", restored.Name);
		Assert.AreEqual("int", restored.Type?.ToString());
		Assert.IsTrue(restored.HasSetter);
		Assert.IsTrue(restored.SetterIsInitOnly);
		Assert.AreEqual(Visibility.Public, restored.Visibility);
		Assert.HasCount(1, restored.GetterBody);
		Assert.IsEmpty(restored.SetterBody);
		Assert.IsFalse(restored.IsAutomatic);
	}

	// ------------------------------------------------------------------ Round trip

	/// <summary>
	/// All four survive a trip through YAML and back.
	/// </summary>
	/// <remarks>
	/// The interfaces are written as a sequence rather than one joined string, for the reason the
	/// specialisation arguments already are: an interface can have type arguments, so a comma
	/// inside one is part of it as often as it separates two.
	/// </remarks>
	[TestMethod]
	public void Yaml_CarriesTheInterfacesAndTheModifiers()
	{
		ClassDeclaration original = Money();
		original.BaseType = "Value";
		original.Interfaces.Add(TypeReference.Parse("Comparable<Money>"));
		original.Interfaces.Add(TypeReference.Parse("Formattable"));

		ktsu.Coder.Serialization.YamlSerializer serializer = new();
		ktsu.Coder.Serialization.YamlDeserializer deserializer = new();

		AstNode? read = deserializer.Deserialize(serializer.Serialize(original));

		ClassDeclaration restored = (ClassDeclaration)read!;
		Assert.IsTrue(restored.IsRecord);
		Assert.IsTrue(restored.IsPartial);
		Assert.IsTrue(restored.IsReadOnly);
		Assert.AreEqual("Value", restored.BaseType?.ToString());
		Assert.AreSequenceEqual(
			(string[])["Comparable<Money>", "Formattable"],
			[.. restored.Interfaces.Select(contract => contract.ToString())]);
	}

	/// <summary>
	/// A clone carries them too, which is what the editor's undo stack puts back.
	/// </summary>
	[TestMethod]
	public void Clone_CarriesTheInterfacesAndTheModifiers()
	{
		ClassDeclaration original = Money();
		original.Interfaces.Add(TypeReference.Parse("Formattable"));

		ClassDeclaration clone = (ClassDeclaration)original.Clone();
		clone.Interfaces[0] = TypeReference.Parse("Something else");

		Assert.IsTrue(clone.IsRecord);
		Assert.IsTrue(clone.IsPartial);
		Assert.IsTrue(clone.IsReadOnly);
		Assert.AreEqual("Formattable", original.Interfaces[0].ToString(), "the clone shares the collection with its original.");
	}
}
