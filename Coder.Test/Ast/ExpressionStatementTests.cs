// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using ktsu.CodeBlocker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="ExpressionStatement"/> and its generation in every target language.
/// </summary>
[TestClass]
public class ExpressionStatementTests
{
	private static ExpressionStatement Effect() =>
		new(new CallExpression("assert") { Arguments = { new VariableReference("ready") } });

	/// <summary>
	/// Tests that a statement keeps the expression it was constructed with.
	/// </summary>
	[TestMethod]
	public void ExpressionStatement_ShouldCreateCorrectStructure()
	{
		CallExpression call = new("reset");

		ExpressionStatement statement = new(call);

		Assert.AreSame(call, statement.Expression);
	}

	/// <summary>
	/// Tests that the parameterless constructor installs the same placeholder the rest of the AST
	/// uses, so a freshly created statement is a node the library already understands.
	/// </summary>
	[TestMethod]
	public void ExpressionStatement_StartsUnfilled() =>
		Assert.IsInstanceOfType<LiteralExpression<string>>(new ExpressionStatement().Expression);

	/// <summary>
	/// Tests that a null expression is refused, since every emitter recurses into it unconditionally.
	/// </summary>
	[TestMethod]
	public void ExpressionStatement_RejectsNullExpression() =>
		Assert.ThrowsExactly<ArgumentNullException>(() => new ExpressionStatement(null!));

	/// <summary>
	/// Tests that cloning copies the metadata and deep-copies the expression.
	/// </summary>
	[TestMethod]
	public void ExpressionStatement_Clone_IsDeep()
	{
		ExpressionStatement original = Effect();
		original.Metadata["origin"] = "test";

		ExpressionStatement clone = (ExpressionStatement)original.Clone();

		Assert.AreEqual("test", clone.Metadata["origin"]);
		Assert.AreNotSame(original.Expression, clone.Expression);
		Assert.AreEqual("assert", ((CallExpression)clone.Expression).Callee);
	}

	/// <summary>
	/// Tests that the five languages with a statement terminator end this with one.
	/// </summary>
	[TestMethod]
	public void LanguagesWithATerminator_EndTheStatementWithOne()
	{
		string expected = $"assert(ready);{CodeBlocker.DefaultNewLineString}";

		Assert.AreEqual(expected, new CSharpGenerator().Generate(Effect()), "C#");
		Assert.AreEqual(expected, new CppGenerator().Generate(Effect()), "C++");
		Assert.AreEqual(expected, new CGenerator().Generate(Effect()), "C");
		Assert.AreEqual(expected, new JavaScriptGenerator().Generate(Effect()), "JavaScript");
		Assert.AreEqual(expected, new RustGenerator().Generate(Effect()), "Rust");
	}

	/// <summary>
	/// Tests that Python writes no terminator, its statements ending at the line break the enclosing
	/// body writes.
	/// </summary>
	[TestMethod]
	public void Python_WritesNoTerminator() =>
		Assert.AreEqual("assert(ready)", new PythonGenerator().Generate(Effect()));

	/// <summary>
	/// Tests that a void call reaches a function body, which is the case the node exists for: before
	/// it there was nowhere in the AST for a call made for its effect to stand.
	/// </summary>
	[TestMethod]
	public void VoidCall_ReachesAFunctionBody()
	{
		FunctionDeclaration function = new("update")
		{
			ReturnType = "void",
			Body = { Effect(), new ExpressionStatement(new CallExpression(new VariableReference("items"), "clear")) },
		};

		string generated = new CSharpGenerator().Generate(function);

		StringAssert.Contains(generated, "assert(ready);", StringComparison.Ordinal);
		StringAssert.Contains(generated, "items.clear();", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an expression statement can hold something other than a call, since what makes one
	/// is that the value is beside the point rather than what kind of expression produced it.
	/// </summary>
	[TestMethod]
	public void ExpressionStatement_AcceptsAnyExpression()
	{
		ExpressionStatement statement = new(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b")));

		Assert.AreEqual($"(a + b);{CodeBlocker.DefaultNewLineString}", new CSharpGenerator().Generate(statement));
	}

	/// <summary>
	/// Tests that a statement survives a round trip through YAML with its expression in place.
	/// </summary>
	[TestMethod]
	public void ExpressionStatement_RoundTripsThroughYaml()
	{
		string yaml = new YamlSerializer().Serialize(Effect());
		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		Assert.IsInstanceOfType<ExpressionStatement>(deserialized);

		ExpressionStatement roundTripped = (ExpressionStatement)deserialized;
		Assert.IsInstanceOfType<CallExpression>(roundTripped.Expression);
		Assert.AreEqual("assert", ((CallExpression)roundTripped.Expression).Callee);
	}

	/// <summary>
	/// Tests that every generator accepts an expression statement.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_AcceptsAnExpressionStatement()
	{
		ExpressionStatement statement = Effect();

		foreach (ILanguageGenerator generator in
			new ILanguageGenerator[] { new CSharpGenerator(), new CppGenerator(), new CGenerator(), new PythonGenerator(), new JavaScriptGenerator(), new RustGenerator() })
		{
			Assert.IsTrue(generator.CanGenerate(statement), $"{generator.DisplayName} should accept an expression statement");
		}
	}
}
