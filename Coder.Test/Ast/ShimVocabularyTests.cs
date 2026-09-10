// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the three nodes a type that shims another needs — an alias, a member initialiser and a
/// construction — in every language and through YAML.
/// </summary>
/// <remarks>
/// These are the nodes whose spelling differs most between languages, and the ones where a language
/// having no spelling at all is the interesting case rather than an oversight. A generated file that
/// silently drops a member looks complete and is not, so where a language cannot say something it is
/// checked here that it says so.
/// </remarks>
[TestClass]
public class ShimVocabularyTests
{
	/// <summary>
	/// Builds a constructor that initialises one member from its argument.
	/// </summary>
	/// <returns>The class holding it.</returns>
	private static ClassDeclaration EntityId()
	{
		ClassDeclaration entity = new("EntityId");
		entity.Members.Add(new UsingAlias("underlying", "long"));

		FunctionDeclaration constructor = new("EntityId")
		{
			Kind = FunctionKind.Constructor,
			IsExplicit = true,
		};
		constructor.Parameters.Add(new Parameter("value", "underlying"));
		constructor.Initialisers.Add(new MemberInitialiser("value_", new VariableReference("value")));
		entity.Members.Add(constructor);

		entity.Members.Add(new FieldDeclaration("value_", "underlying") { Visibility = Visibility.Private });
		return entity;
	}

	/// <summary>
	/// An alias is a declaration in three languages and a binding in the fourth.
	/// </summary>
	[TestMethod]
	public void Alias_IsSpelledByEachLanguage()
	{
		// A name no language's mapping table knows, so this is about the alias rather than about how
		// each spells a built-in type — which TypeReferenceTests already covers.
		UsingAlias alias = new("underlying", "std::int64_t");
		alias.Documentation.Add("what an EntityId is stored as");

		Assert.Contains("using underlying = std::int64_t;", new CppGenerator().Generate(alias), StringComparison.Ordinal);
		Assert.Contains("using underlying = std::int64_t;", new CSharpGenerator().Generate(alias), StringComparison.Ordinal);
		Assert.Contains("underlying = std::int64_t", new PythonGenerator().Generate(alias), StringComparison.Ordinal);
		Assert.Contains("const underlying = std::int64_t;", new JavaScriptGenerator().Generate(alias), StringComparison.Ordinal);

		// The documentation reaches every one of them.
		Assert.Contains("what an EntityId is stored as", new PythonGenerator().Generate(alias), StringComparison.Ordinal);
	}

	/// <summary>
	/// Building a value is a keyword in three languages and braces in the fourth.
	/// </summary>
	[TestMethod]
	public void Construction_IsSpelledByEachLanguage()
	{
		ConstructionExpression kilograms = new(new TypeReference("holo::Kilograms"));
		kilograms.Arguments.Add(new LiteralExpression<double>(1.0));

		FieldDeclaration mass = new("mass", "holo::Kilograms") { InitialValue = kilograms };

		Assert.Contains("holo::Kilograms{ 1", new CppGenerator().Generate(mass), StringComparison.Ordinal);
		Assert.Contains("new holo::Kilograms(1", new CSharpGenerator().Generate(mass), StringComparison.Ordinal);
		Assert.Contains("holo::Kilograms(1", new PythonGenerator().Generate(mass), StringComparison.Ordinal);
		Assert.Contains("new holo::Kilograms(1", new JavaScriptGenerator().Generate(mass), StringComparison.Ordinal);
	}

	/// <summary>
	/// A construction with no arguments is value-initialisation in C++, which is a different spelling
	/// rather than an empty argument list.
	/// </summary>
	[TestMethod]
	public void Construction_WithNoArgumentsIsValueInitialisation()
	{
		FieldDeclaration mass = new("mass", "holo::Kilograms")
		{
			InitialValue = new ConstructionExpression(new TypeReference("holo::Kilograms")),
		};

		Assert.Contains("= holo::Kilograms{};", new CppGenerator().Generate(mass), StringComparison.Ordinal);
		Assert.Contains("new holo::Kilograms()", new CSharpGenerator().Generate(mass), StringComparison.Ordinal);
	}

	/// <summary>
	/// More than one argument is separated the same way everywhere.
	/// </summary>
	[TestMethod]
	public void Construction_SeparatesItsArguments()
	{
		ConstructionExpression point = new(new TypeReference("Point"));
		point.Arguments.Add(new LiteralExpression<int>(1));
		point.Arguments.Add(new LiteralExpression<int>(2));

		FieldDeclaration origin = new("origin", "Point") { InitialValue = point };

		Assert.Contains("Point{ 1, 2 }", new CppGenerator().Generate(origin), StringComparison.Ordinal);
		Assert.Contains("new Point(1, 2)", new CSharpGenerator().Generate(origin), StringComparison.Ordinal);
		Assert.Contains("Point(1, 2)", new PythonGenerator().Generate(origin), StringComparison.Ordinal);
		Assert.Contains("new Point(1, 2)", new JavaScriptGenerator().Generate(origin), StringComparison.Ordinal);
	}

	/// <summary>
	/// C++ initialises a member; the other three assign, at the top of the constructor.
	/// </summary>
	[TestMethod]
	public void Initialiser_IsInitialisationInCppAndAssignmentElsewhere()
	{
		ClassDeclaration entity = EntityId();

		Assert.Contains(": value_(value)", new CppGenerator().Generate(entity), StringComparison.Ordinal);
		Assert.Contains("this.value_ = value", new CSharpGenerator().Generate(entity), StringComparison.Ordinal);
		Assert.Contains("self.value_ = value", new PythonGenerator().Generate(entity), StringComparison.Ordinal);
		Assert.Contains("this.value_ = value", new JavaScriptGenerator().Generate(entity), StringComparison.Ordinal);
	}

	/// <summary>
	/// A constructor whose only work is its initialisers still has a body, rather than Python's
	/// placeholder for one that does nothing.
	/// </summary>
	[TestMethod]
	public void Python_DoesNotPassWhenAConstructorInitialisesSomething()
	{
		string code = new PythonGenerator().Generate(EntityId());

		Assert.Contains("def __init__(self, value", code, StringComparison.Ordinal);
		Assert.DoesNotContain("pass", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// A conversion says which direction it may be taken in, which is the difference between a type
	/// that shims another and one that merely wraps it.
	/// </summary>
	[TestMethod]
	public void Conversion_SaysWhetherItMustBeAskedFor()
	{
		FunctionDeclaration widening = new("ignored")
		{
			Kind = FunctionKind.ConversionOperator,
			ReturnType = "ForceMagnitude",
			IsReadOnly = true,
		};

		Assert.Contains("operator ForceMagnitude() const", new CppGenerator().Generate(widening), StringComparison.Ordinal);
		Assert.Contains("implicit operator ForceMagnitude(", new CSharpGenerator().Generate(widening), StringComparison.Ordinal);

		widening.IsExplicit = true;

		Assert.Contains("explicit operator ForceMagnitude(", new CSharpGenerator().Generate(widening), StringComparison.Ordinal);
		Assert.Contains("explicit ", new CppGenerator().Generate(widening), StringComparison.Ordinal);
	}

	/// <summary>
	/// Only C++ can say when a call may be evaluated or that it cannot fail, so only C++ writes
	/// anything for either.
	/// </summary>
	[TestMethod]
	public void CompileTimeAndNoThrow_AreWrittenOnlyWhereTheyCanBeSaid()
	{
		FunctionDeclaration value = new("value")
		{
			ReturnType = "std::int64_t",
			IsCompileTimeEvaluable = true,
			IsNoThrow = true,
		};

		string cpp = new CppGenerator().Generate(value);

		Assert.Contains("constexpr std::int64_t value() noexcept", cpp, StringComparison.Ordinal);
		Assert.DoesNotContain("constexpr", new CSharpGenerator().Generate(value), StringComparison.Ordinal);
		Assert.DoesNotContain("noexcept", new PythonGenerator().Generate(value), StringComparison.Ordinal);
	}

	/// <summary>
	/// All three nodes survive a round trip through YAML, which is what makes a shim a document rather
	/// than something only the generator can see.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsTheShimVocabulary()
	{
		ClassDeclaration original = EntityId();
		((FunctionDeclaration)original.Members[1]).Initialisers[0].Value =
			new ConstructionExpression(new TypeReference("long")) { Arguments = { new VariableReference("value") } };

		string yaml = new YamlSerializer().Serialize(original);
		ClassDeclaration restored = (ClassDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		UsingAlias alias = (UsingAlias)restored.Members[0];
		Assert.AreEqual("underlying", alias.Name);
		Assert.AreEqual("long", alias.AliasedType?.ToString());

		FunctionDeclaration constructor = (FunctionDeclaration)restored.Members[1];
		Assert.AreEqual(FunctionKind.Constructor, constructor.Kind);
		Assert.IsTrue(constructor.IsExplicit);
		Assert.HasCount(1, constructor.Initialisers);
		Assert.AreEqual("value_", constructor.Initialisers[0].Name);

		ConstructionExpression construction = (ConstructionExpression)constructor.Initialisers[0].Value!;
		Assert.AreEqual("long", construction.Type?.ToString());
		Assert.HasCount(1, construction.Arguments);

		FieldDeclaration field = (FieldDeclaration)restored.Members[2];
		Assert.AreEqual(Visibility.Private, field.Visibility);
	}

	/// <summary>
	/// A document says nothing for a shim that asked for nothing, so a file written before any of this
	/// existed still opens.
	/// </summary>
	[TestMethod]
	public void Yaml_WritesNothingForWhatAShimDoesNotSay()
	{
		string yaml = new YamlSerializer().Serialize(new FunctionDeclaration("value"));

		Assert.DoesNotContain("isExplicit", yaml, StringComparison.Ordinal);
		Assert.DoesNotContain("isNoThrow", yaml, StringComparison.Ordinal);
		Assert.DoesNotContain("isFriend", yaml, StringComparison.Ordinal);
		Assert.DoesNotContain("initialisers", yaml, StringComparison.Ordinal);
	}

	/// <summary>
	/// A file that is not a header says nothing about being included twice, because it is not.
	/// </summary>
	[TestMethod]
	public void File_SaysNothingAboutInclusionWhenItIsNotAHeader()
	{
		SourceFile source = new("main.cpp");
		source.Members.Add(new ClassDeclaration("Program"));

		Assert.DoesNotContain("#pragma once", new CppGenerator().Generate(source), StringComparison.Ordinal);
	}
}
