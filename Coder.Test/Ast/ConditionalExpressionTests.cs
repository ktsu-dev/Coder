// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="ConditionalExpression"/> and its generation in every target language.
/// </summary>
[TestClass]
public class ConditionalExpressionTests
{
	private static ConditionalExpression Choice() => new(
		new VariableReference("ready"),
		new VariableReference("go"),
		new VariableReference("wait"));

	/// <summary>
	/// Tests that a conditional keeps the three operands it was constructed with.
	/// </summary>
	[TestMethod]
	public void ConditionalExpression_ShouldCreateCorrectStructure()
	{
		VariableReference condition = new("ready");
		VariableReference whenTrue = new("go");
		VariableReference whenFalse = new("wait");

		ConditionalExpression conditional = new(condition, whenTrue, whenFalse);

		Assert.AreSame(condition, conditional.Condition);
		Assert.AreSame(whenTrue, conditional.WhenTrue);
		Assert.AreSame(whenFalse, conditional.WhenFalse);
	}

	/// <summary>
	/// Tests that a null operand is refused, since every emitter recurses into all three unconditionally.
	/// </summary>
	[TestMethod]
	public void ConditionalExpression_RejectsNullOperands()
	{
		VariableReference filled = new("value");

		Assert.ThrowsExactly<ArgumentNullException>(() => new ConditionalExpression(null!, filled, filled));
		Assert.ThrowsExactly<ArgumentNullException>(() => new ConditionalExpression(filled, null!, filled));
		Assert.ThrowsExactly<ArgumentNullException>(() => new ConditionalExpression(filled, filled, null!));
	}

	/// <summary>
	/// Tests that cloning copies the metadata and deep-copies all three operands.
	/// </summary>
	[TestMethod]
	public void ConditionalExpression_Clone_IsDeep()
	{
		ConditionalExpression original = Choice();
		original.ExpectedType = "int";
		original.Metadata["origin"] = "test";

		ConditionalExpression clone = (ConditionalExpression)original.Clone();

		Assert.AreEqual("int", clone.ExpectedType);
		Assert.AreEqual("test", clone.Metadata["origin"]);
		Assert.AreNotSame(original.Condition, clone.Condition);
		Assert.AreNotSame(original.WhenTrue, clone.WhenTrue);
		Assert.AreNotSame(original.WhenFalse, clone.WhenFalse);
		Assert.AreEqual("ready", ((VariableReference)clone.Condition).Name);
		Assert.AreEqual("go", ((VariableReference)clone.WhenTrue).Name);
		Assert.AreEqual("wait", ((VariableReference)clone.WhenFalse).Name);
	}

	/// <summary>
	/// Tests that the four C-family targets spell this with the ternary operator.
	/// </summary>
	[TestMethod]
	public void CFamilyGenerators_SpellItWithTheTernaryOperator()
	{
		ConditionalExpression conditional = Choice();

		Assert.AreEqual("(ready ? go : wait)", new CSharpGenerator().Generate(conditional), "C#");
		Assert.AreEqual("(ready ? go : wait)", new CppGenerator().Generate(conditional), "C++");
		Assert.AreEqual("(ready ? go : wait)", new CGenerator().Generate(conditional), "C");
		Assert.AreEqual("(ready ? go : wait)", new JavaScriptGenerator().Generate(conditional), "JavaScript");
	}

	/// <summary>
	/// Tests that Python reorders the operands around the condition, which is the whole reason this
	/// is a node rather than text.
	/// </summary>
	[TestMethod]
	public void Python_PutsTheConditionBetweenTheTwoValues() =>
		Assert.AreEqual("(go if ready else wait)", new PythonGenerator().Generate(Choice()));

	/// <summary>
	/// Tests that the operands are recursed into rather than stringified, so a conditional can
	/// choose between compound expressions.
	/// </summary>
	[TestMethod]
	public void ConditionalExpression_RecursesIntoCompoundOperands()
	{
		ConditionalExpression conditional = new(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.LessThan, new VariableReference("b")),
			new CallExpression("first"),
			new CallExpression("second"));

		Assert.AreEqual("((a < b) ? first() : second())", new CSharpGenerator().Generate(conditional));
		Assert.AreEqual("(first() if (a < b) else second())", new PythonGenerator().Generate(conditional));
	}

	/// <summary>
	/// Tests that a conditional is parenthesised, so nesting one inside another is unambiguous
	/// despite the AST carrying no precedence.
	/// </summary>
	[TestMethod]
	public void NestedConditionals_AreUnambiguous()
	{
		ConditionalExpression conditional = new(
			new VariableReference("outer"),
			Choice(),
			new VariableReference("fallback"));

		Assert.AreEqual("(outer ? (ready ? go : wait) : fallback)", new CSharpGenerator().Generate(conditional));
	}

	/// <summary>
	/// Tests that a conditional survives a round trip through YAML with all three operands in place.
	/// </summary>
	[TestMethod]
	public void ConditionalExpression_RoundTripsThroughYaml()
	{
		ConditionalExpression original = Choice();
		original.ExpectedType = "int";

		string yaml = new YamlSerializer().Serialize(original);
		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		Assert.IsInstanceOfType<ConditionalExpression>(deserialized);

		ConditionalExpression roundTripped = (ConditionalExpression)deserialized;
		Assert.AreEqual("int", roundTripped.ExpectedType);
		Assert.AreEqual("(ready ? go : wait)", new CSharpGenerator().Generate(roundTripped));
	}

	/// <summary>
	/// Tests that every generator accepts a conditional.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_AcceptsAConditional()
	{
		ConditionalExpression conditional = Choice();

		foreach (ILanguageGenerator generator in
			new ILanguageGenerator[] { new CSharpGenerator(), new CppGenerator(), new CGenerator(), new PythonGenerator(), new JavaScriptGenerator() })
		{
			Assert.IsTrue(generator.CanGenerate(conditional), $"{generator.DisplayName} should accept a conditional");
		}
	}
}
