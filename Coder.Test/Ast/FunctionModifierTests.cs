// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="FunctionDeclaration.IsStatic"/> and <see cref="FunctionDeclaration.IsPure"/>:
/// what each language spells them as, and that they survive a round trip through YAML.
/// </summary>
/// <remarks>
/// Static is the more interesting of the two, because Python spells it by changing the signature
/// rather than by decorating it: a static method has no <c>self</c>. Purity has a spelling in only
/// two of the four languages, which is the ordinary case for something the AST models — the AST says
/// what is true and a generator says as much of it as its language can.
/// </remarks>
[TestClass]
public class FunctionModifierTests
{
	/// <summary>
	/// Builds a class with one static pure method, which is the shape most of these use.
	/// </summary>
	/// <returns>The class.</returns>
	private static ClassDeclaration SampleClass()
	{
		ClassDeclaration declaration = new("Points");

		FunctionDeclaration distance = new("distance")
		{
			ReturnType = "double",
			IsStatic = true,
			IsPure = true,
		};
		distance.Parameters.Add(new Parameter("scale", "double"));
		distance.Body.Add(new ReturnStatement(new VariableReference("scale")));

		declaration.Members.Add(distance);
		return declaration;
	}

	/// <summary>
	/// Both are off unless asked for, so an existing function is unaffected by their existence.
	/// </summary>
	[TestMethod]
	public void Neither_IsSetByDefault()
	{
		FunctionDeclaration function = new("area");

		Assert.IsFalse(function.IsStatic);
		Assert.IsFalse(function.IsPure);
	}

	/// <summary>
	/// C# writes <c>static</c> in the signature and <c>[Pure]</c> above it.
	/// </summary>
	[TestMethod]
	public void CSharp_WritesStaticAndThePureAttribute()
	{
		string code = new CSharpGenerator().Generate(SampleClass());

		Assert.Contains("[System.Diagnostics.Contracts.Pure]", code, StringComparison.Ordinal);
		Assert.Contains("public static double distance(", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// C++ writes <c>static</c>, and spells purity as the one thing the standard can say about it.
	/// </summary>
	[TestMethod]
	public void Cpp_WritesStaticAndNodiscard()
	{
		FunctionDeclaration function = new("distance")
		{
			ReturnType = "double",
			IsStatic = true,
			IsPure = true,
		};

		Assert.Contains(
			"[[nodiscard]] static double distance(",
			new CppGenerator().Generate(function),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Python spells static by dropping the receiver, which is the whole reason this modifier is not
	/// just a keyword the generators paste in.
	/// </summary>
	[TestMethod]
	public void Python_DropsSelfFromAStaticMethod()
	{
		string code = new PythonGenerator().Generate(SampleClass());

		Assert.Contains("@staticmethod", code, StringComparison.Ordinal);
		Assert.Contains("def distance(scale", code, StringComparison.Ordinal);
		Assert.DoesNotContain("self", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// An ordinary method still takes its receiver, and still separates it from the first parameter.
	/// </summary>
	[TestMethod]
	public void Python_KeepsSelfOnAnInstanceMethod()
	{
		ClassDeclaration declaration = SampleClass();
		((FunctionDeclaration)declaration.Members[0]).IsStatic = false;

		Assert.Contains(
			"def distance(self, scale",
			new PythonGenerator().Generate(declaration),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Purity has no spelling in Python, so nothing is emitted for it rather than something invented.
	/// </summary>
	[TestMethod]
	public void Python_SaysNothingAboutPurity()
	{
		FunctionDeclaration function = new("distance") { ReturnType = "double", IsPure = true };

		Assert.AreEqual(
			new PythonGenerator().Generate(new FunctionDeclaration("distance") { ReturnType = "double" }),
			new PythonGenerator().Generate(function));
	}

	/// <summary>
	/// JavaScript spells static on a class member and has nothing to say about purity.
	/// </summary>
	[TestMethod]
	public void JavaScript_WritesStaticOnAMethod()
	{
		string code = new JavaScriptGenerator().Generate(SampleClass());

		Assert.Contains("static distance(scale)", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// A static private member keeps both spellings, which are separate parts of the same declaration.
	/// </summary>
	[TestMethod]
	public void JavaScript_WritesStaticAlongsideThePrivatePrefix()
	{
		ClassDeclaration declaration = SampleClass();
		((FunctionDeclaration)declaration.Members[0]).Visibility = Visibility.Private;

		Assert.Contains(
			"static #distance(scale)",
			new JavaScriptGenerator().Generate(declaration),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Both survive a round trip through YAML.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsBothModifiers()
	{
		FunctionDeclaration original = new("distance")
		{
			ReturnType = "double",
			IsStatic = true,
			IsPure = true,
		};

		string yaml = new YamlSerializer().Serialize(original);
		FunctionDeclaration restored = (FunctionDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.IsTrue(restored.IsStatic);
		Assert.IsTrue(restored.IsPure);
	}

	/// <summary>
	/// A function that asked for neither writes neither key, so the document says nothing rather than
	/// saying false twice on every function ever written.
	/// </summary>
	[TestMethod]
	public void Yaml_WritesNothingForAModifierNobodyAskedFor()
	{
		string yaml = new YamlSerializer().Serialize(new FunctionDeclaration("distance"));

		Assert.DoesNotContain("isStatic", yaml, StringComparison.Ordinal);
		Assert.DoesNotContain("isPure", yaml, StringComparison.Ordinal);
	}

	/// <summary>
	/// A document written before these existed still opens, with both off.
	/// </summary>
	[TestMethod]
	public void Yaml_ReadsADocumentWrittenBeforeTheseExisted()
	{
		const string yaml = """
			functionDeclaration:
			  name: distance
			  returnType: double
			""";

		FunctionDeclaration restored = (FunctionDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.IsFalse(restored.IsStatic);
		Assert.IsFalse(restored.IsPure);
	}

	/// <summary>
	/// A clone carries both, so copying a function does not quietly drop what it was marked as.
	/// </summary>
	[TestMethod]
	public void Clone_CarriesBothModifiers()
	{
		FunctionDeclaration clone = (FunctionDeclaration)new FunctionDeclaration("distance")
		{
			IsStatic = true,
			IsPure = true,
		}.Clone();

		Assert.IsTrue(clone.IsStatic);
		Assert.IsTrue(clone.IsPure);
	}

	/// <summary>
	/// The inspector offers both as flags and writes what the user ticked.
	/// </summary>
	[TestMethod]
	public void Fields_OfferBothAsFlags()
	{
		FunctionDeclaration function = new("distance") { ReturnType = "double" };

		Assert.Contains(
			field => field.Name == "Static" && field.Kind == AstFieldKind.Flag,
			AstFields.Of(function));
		Assert.Contains(
			field => field.Name == "Pure" && field.Kind == AstFieldKind.Flag,
			AstFields.Of(function));

		Assert.IsTrue(AstFields.TryWrite(function, "Static", "true"));
		Assert.IsTrue(AstFields.TryWrite(function, "Pure", "true"));

		Assert.IsTrue(function.IsStatic);
		Assert.IsTrue(function.IsPure);
	}
}
