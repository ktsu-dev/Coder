// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A generator is registered as a singleton and the editor keeps using it after a preview fails, so
/// a <c>Generate</c> call that throws inside a type must not change what the next call writes.
/// </summary>
[TestClass]
public sealed class FailedGenerationStateTests
{
	[TestMethod]
	public void CGenerator_AfterAFailureInsideAType_WritesWhatAFreshInstanceWrites() =>
		AssertUnaffectedByFailure(() => new CGenerator(), ClassHoldingAnEnumMember());

	[TestMethod]
	public void CppGenerator_AfterAFailureInsideAType_WritesWhatAFreshInstanceWrites() =>
		AssertUnaffectedByFailure(() => new CppGenerator(), ClassHoldingAnEnumMember());

	[TestMethod]
	public void RustGenerator_AfterAFailureInsideAType_WritesWhatAFreshInstanceWrites() =>
		AssertUnaffectedByFailure(() => new RustGenerator(), ClassWithAnUnknownOperatorDefault());

	[TestMethod]
	public void RustGenerator_AfterAFailureInsideATrait_WritesWhatAFreshInstanceWrites() =>
		AssertUnaffectedByFailure(() => new RustGenerator(), TraitWithAnUnknownOperatorBody(), FreeFunctions);

	[TestMethod]
	public void RustGenerator_AfterAFailureInsideASpecialisation_WritesWhatAFreshInstanceWrites() =>
		AssertUnaffectedByFailure(() => new RustGenerator(), SpecialisationWithAnUnknownOperatorBody(), FreeFunctions);

	[TestMethod]
	public void GoGenerator_AfterAFailureInsideAnInterface_WritesWhatAFreshInstanceWrites() =>
		AssertUnaffectedByFailure(() => new GoGenerator(), InterfaceWithAnUnknownOperatorDefault(), FreeFunctions);

	private static void AssertUnaffectedByFailure(Func<LanguageGeneratorBase> create, AstNode failing) =>
		AssertUnaffectedByFailure(create, failing, FileScopeConstant);

	private static void AssertUnaffectedByFailure(Func<LanguageGeneratorBase> create, AstNode failing, Func<SourceFile> next)
	{
		string expected = create().Generate(next());

		LanguageGeneratorBase generator = create();
		Assert.ThrowsExactly<NotSupportedException>(() => generator.Generate(failing), "Precondition: the document fails inside the type.");

		Assert.AreEqual(expected, generator.Generate(next()), generator.DisplayName);
	}

	/// <summary>
	/// A file holding a free function that returns a value and one with no statements, which a
	/// generator that thinks it is inside a trait or an interface writes as bare signatures.
	/// </summary>
	private static SourceFile FreeFunctions()
	{
		FunctionDeclaration answer = new("Answer") { ReturnType = TypeReference.Parse("int") };
		answer.Body.Add(new ReturnStatement(1));
		SourceFile file = new("m");
		file.Members.Add(answer);
		file.Members.Add(new FunctionDeclaration("Noop"));
		return file;
	}

	/// <summary>
	/// An expression no generator can spell, as YAML can produce from a number.
	/// </summary>
	private static BinaryExpression UnknownOperator() =>
		new(Literal.Number(1), (BinaryOperator)999, Literal.Number(2));

	/// <summary>
	/// An interface whose method returns an expression Rust refuses while writing the trait.
	/// </summary>
	private static SourceFile TraitWithAnUnknownOperatorBody()
	{
		FunctionDeclaration area = new("Area") { ReturnType = TypeReference.Parse("int") };
		area.Body.Add(new ReturnStatement(UnknownOperator()));
		ClassDeclaration shape = new("Shape") { Kind = TypeDeclarationKind.Interface };
		shape.Members.Add(area);
		SourceFile file = new("m");
		file.Members.Add(shape);
		return file;
	}

	/// <summary>
	/// A specialisation whose method returns an expression Rust refuses while writing the impl.
	/// </summary>
	private static SourceFile SpecialisationWithAnUnknownOperatorBody()
	{
		FunctionDeclaration area = new("Area") { ReturnType = TypeReference.Parse("int") };
		area.Body.Add(new ReturnStatement(UnknownOperator()));
		ClassDeclaration describe = new("Describe") { SpecialisationArguments = { new TypeReference("Box") } };
		describe.Members.Add(area);
		SourceFile file = new("m");
		file.Members.Add(describe);
		return file;
	}

	/// <summary>
	/// An interface whose method has a default Go refuses while writing it into a comment.
	/// </summary>
	private static SourceFile InterfaceWithAnUnknownOperatorDefault()
	{
		FunctionDeclaration area = new("Area") { ReturnType = TypeReference.Parse("int") };
		area.Parameters.Add(new Parameter("scale", "int") { IsOptional = true, DefaultValue = UnknownOperator() });
		ClassDeclaration shape = new("Shape") { Kind = TypeDeclarationKind.Interface };
		shape.Members.Add(area);
		SourceFile file = new("m");
		file.Members.Add(shape);
		return file;
	}

	/// <summary>
	/// A file holding only a static constant, whose spelling depends on whether the generator thinks
	/// it is inside a type.
	/// </summary>
	private static SourceFile FileScopeConstant()
	{
		SourceFile file = new("m");
		file.Members.Add(new FieldDeclaration("limit", TypeReference.Parse("int"))
		{
			IsStatic = true,
			IsConstant = true,
			InitialValue = Literal.Number(3),
		});
		return file;
	}

	/// <summary>
	/// A class with an enum member, which C and C++ refuse once they are inside the struct.
	/// </summary>
	private static SourceFile ClassHoldingAnEnumMember()
	{
		ClassDeclaration box = new("Box");
		box.Members.Add(new FieldDeclaration("w", TypeReference.Parse("int")));
		box.Members.Add(new EnumMember("Red"));
		SourceFile file = new("m");
		file.Members.Add(box);
		return file;
	}

	/// <summary>
	/// A struct whose field starts at an operator no generator can spell, as YAML can produce from a
	/// number, which Rust refuses while writing the struct's fields.
	/// </summary>
	private static SourceFile ClassWithAnUnknownOperatorDefault()
	{
		ClassDeclaration box = new("Box");
		box.Members.Add(new VariableDeclaration("w", "int", new BinaryExpression(Literal.Number(1), (BinaryOperator)999, Literal.Number(2))));
		SourceFile file = new("m");
		file.Members.Add(box);
		return file;
	}
}
