// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for what a <see cref="FunctionDeclaration"/> declares and how: constructors, destructors and
/// operators, whether a derived type may or must replace it, and where its behaviour comes from.
/// </summary>
/// <remarks>
/// This is the half of the vocabulary an interface needs, and it is where the four languages diverge
/// most. C++ says all of it. C# says most of it and expresses a defaulted or deleted member by not
/// declaring one. Python and JavaScript have no declaration without a definition at all, so a method
/// a derived type must supply becomes a body that refuses.
/// </remarks>
[TestClass]
public class FunctionShapeTests
{
	/// <summary>
	/// Builds a class with a defaulted constructor and a deleted copy constructor.
	/// </summary>
	/// <returns>The class.</returns>
	private static ClassDeclaration SampleClass()
	{
		ClassDeclaration handle = new("Handle");

		handle.Members.Add(new FunctionDeclaration("Handle")
		{
			Kind = FunctionKind.Constructor,
			Definition = FunctionDefinition.Defaulted,
		});

		FunctionDeclaration copy = new("Handle")
		{
			Kind = FunctionKind.Constructor,
			Definition = FunctionDefinition.Deleted,
		};
		copy.Parameters.Add(new Parameter("other", "Handle"));
		handle.Members.Add(copy);

		return handle;
	}

	/// <summary>
	/// A declaration is an ordinary method whose body is its behaviour unless it says otherwise, so
	/// nothing that existed before these changed shape.
	/// </summary>
	[TestMethod]
	public void Shape_DefaultsToAnOrdinaryMethod()
	{
		FunctionDeclaration function = new("area");

		Assert.AreEqual(FunctionKind.Method, function.Kind);
		Assert.AreEqual(FunctionDefinition.Provided, function.Definition);
		Assert.IsFalse(function.IsVirtual);
		Assert.IsFalse(function.IsAbstract);
		Assert.IsFalse(function.IsReadOnly);
		Assert.IsFalse(function.MustUseResult);
	}

	/// <summary>
	/// A constructor and a destructor are named after the type they belong to, which the class emitter
	/// supplies rather than the declaration holding a second copy of.
	/// </summary>
	[TestMethod]
	public void Cpp_NamesAConstructorAfterItsType()
	{
		ClassDeclaration handle = SampleClass();
		handle.Members.Add(new FunctionDeclaration("ignored")
		{
			Kind = FunctionKind.Destructor,
			IsVirtual = true,
			Definition = FunctionDefinition.Defaulted,
		});

		string code = new CppGenerator().Generate(handle);

		Assert.Contains("Handle() = default;", code, StringComparison.Ordinal);
		Assert.Contains("virtual ~Handle() = default;", code, StringComparison.Ordinal);
		Assert.DoesNotContain("ignored", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// C++ says all three of defaulted, deleted and abstract.
	/// </summary>
	[TestMethod]
	public void Cpp_SpellsEachKindOfDefinition()
	{
		string code = new CppGenerator().Generate(SampleClass());

		Assert.Contains("Handle() = default;", code, StringComparison.Ordinal);
		Assert.Contains("Handle(Handle other) = delete;", code, StringComparison.Ordinal);

		Assert.Contains(
			"virtual void step() = 0;",
			new CppGenerator().Generate(new FunctionDeclaration("step") { ReturnType = "void", IsAbstract = true }),
			StringComparison.Ordinal);
	}

	/// <summary>
	/// A trailing const says a call leaves the receiver unchanged, which C# spells readonly.
	/// </summary>
	[TestMethod]
	public void ReadOnly_IsSpelledWhereALanguageHasIt()
	{
		FunctionDeclaration find = new("find") { ReturnType = "int", IsReadOnly = true };

		Assert.Contains("int find() const", new CppGenerator().Generate(find), StringComparison.Ordinal);
		Assert.Contains("readonly int find(", new CSharpGenerator().Generate(find), StringComparison.Ordinal);
	}

	/// <summary>
	/// Ignoring a result that may be a failure is a mistake, which C++ spells the same way it spells
	/// the consequence of purity.
	/// </summary>
	[TestMethod]
	public void MustUseResult_EarnsNodiscardWithoutClaimingPurity()
	{
		FunctionDeclaration spawn = new("spawn") { ReturnType = "Result", MustUseResult = true };
		string code = new CppGenerator().Generate(spawn);

		Assert.Contains("[[nodiscard]] Result spawn(", code, StringComparison.Ordinal);
		Assert.DoesNotContain("Pure", new CSharpGenerator().Generate(spawn), StringComparison.Ordinal);
	}

	/// <summary>
	/// C# expresses a deleted member by not declaring it, and says which one went rather than dropping
	/// it silently.
	/// </summary>
	[TestMethod]
	public void CSharp_NamesWhatItCannotDeclare()
	{
		string code = new CSharpGenerator().Generate(SampleClass());

		// A defaulted constructor is the exception: a type declaring any other constructor stops
		// getting one for free, so it is written with the empty body that `= default` means there.
		Assert.Contains("public Handle()", code, StringComparison.Ordinal);
		Assert.Contains("// Handle is deleted, which C# expresses by not declaring it.", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// Python has no declaration without a definition, so a method a derived type must supply is one
	/// whose body refuses.
	/// </summary>
	[TestMethod]
	public void Python_RefusesInTheBodyOfAnAbstractMethod()
	{
		ClassDeclaration shape = new("Shape");
		shape.Members.Add(new FunctionDeclaration("area") { ReturnType = "double", IsAbstract = true });

		Assert.Contains("raise NotImplementedError", new PythonGenerator().Generate(shape), StringComparison.Ordinal);
	}

	/// <summary>
	/// JavaScript does the same, in the only way it has of saying it.
	/// </summary>
	[TestMethod]
	public void JavaScript_ThrowsFromTheBodyOfAnAbstractMethod()
	{
		ClassDeclaration shape = new("Shape");
		shape.Members.Add(new FunctionDeclaration("area") { IsAbstract = true });

		Assert.Contains("throw new Error(\"area must be implemented\");", new JavaScriptGenerator().Generate(shape), StringComparison.Ordinal);
	}

	/// <summary>
	/// Python names a constructor for itself rather than for its type, and has no spelling at all for
	/// an operator.
	/// </summary>
	[TestMethod]
	public void Python_UsesItsOwnNamesAndSaysSoWhereItHasNone()
	{
		ClassDeclaration handle = new("Handle");
		handle.Members.Add(new FunctionDeclaration("Handle") { Kind = FunctionKind.Constructor });
		handle.Members.Add(new FunctionDeclaration("=") { Kind = FunctionKind.Operator });

		string code = new PythonGenerator().Generate(handle);

		Assert.Contains("def __init__(self)", code, StringComparison.Ordinal);
		Assert.Contains("# operator = has no Python spelling.", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// All six survive a round trip through YAML, and a document says nothing for what a declaration
	/// did not ask for.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsTheWholeShape()
	{
		FunctionDeclaration original = new("find")
		{
			ReturnType = "int",
			Kind = FunctionKind.Operator,
			Definition = FunctionDefinition.Deleted,
			IsVirtual = true,
			IsAbstract = true,
			IsReadOnly = true,
			MustUseResult = true,
		};

		string yaml = new YamlSerializer().Serialize(original);
		FunctionDeclaration restored = (FunctionDeclaration)new YamlDeserializer().Deserialize(yaml)!;

		Assert.AreEqual(FunctionKind.Operator, restored.Kind);
		Assert.AreEqual(FunctionDefinition.Deleted, restored.Definition);
		Assert.IsTrue(restored.IsVirtual);
		Assert.IsTrue(restored.IsAbstract);
		Assert.IsTrue(restored.IsReadOnly);
		Assert.IsTrue(restored.MustUseResult);

		string plain = new YamlSerializer().Serialize(new FunctionDeclaration("find"));

		Assert.DoesNotContain("kind", plain, StringComparison.Ordinal);
		Assert.DoesNotContain("definition", plain, StringComparison.Ordinal);
		Assert.DoesNotContain("isVirtual", plain, StringComparison.Ordinal);
	}

	/// <summary>
	/// The inspector offers each, so a declaration's shape is something the editor can change.
	/// </summary>
	[TestMethod]
	public void Fields_OfferTheWholeShape()
	{
		FunctionDeclaration function = new("find");

		Assert.IsTrue(AstFields.TryWrite(function, "Kind", "Destructor"));
		Assert.IsTrue(AstFields.TryWrite(function, "Definition", "Deleted"));
		Assert.IsTrue(AstFields.TryWrite(function, "Abstract", "true"));
		Assert.IsTrue(AstFields.TryWrite(function, "ReadOnly", "true"));
		Assert.IsTrue(AstFields.TryWrite(function, "MustUseResult", "true"));

		Assert.AreEqual(FunctionKind.Destructor, function.Kind);
		Assert.AreEqual(FunctionDefinition.Deleted, function.Definition);
		Assert.IsTrue(function.IsAbstract);
		Assert.IsTrue(function.IsReadOnly);
		Assert.IsTrue(function.MustUseResult);
	}
}
