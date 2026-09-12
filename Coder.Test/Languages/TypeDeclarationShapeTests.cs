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
		Assert.AreEqual(3, TypeParameter.Parse(written).Constraints.Count);
		Assert.AreEqual("U", TypeParameter.Parse("U").ToString());
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
