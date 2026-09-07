// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="UnaryExpression"/> and its generation in every target language.
/// </summary>
[TestClass]
public class UnaryExpressionTests
{
	/// <summary>
	/// Tests that a unary expression keeps the operator and operand it was constructed with.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_ShouldCreateCorrectStructure()
	{
		VariableReference operand = new("ready");

		UnaryExpression unary = new(UnaryOperator.LogicalNot, operand);

		Assert.AreEqual(UnaryOperator.LogicalNot, unary.Operator);
		Assert.AreSame(operand, unary.Operand);
	}

	/// <summary>
	/// Tests that a null operand is refused, since every emitter recurses into it unconditionally.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_RejectsNullOperand() =>
		Assert.ThrowsExactly<ArgumentNullException>(() => new UnaryExpression(UnaryOperator.Negate, null!));

	/// <summary>
	/// Tests that cloning copies the operator and metadata and deep-copies the operand.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_Clone_IsDeep()
	{
		UnaryExpression original = new(UnaryOperator.BitwiseNot, new VariableReference("mask"))
		{
			ExpectedType = "int"
		};
		original.Metadata["origin"] = "test";

		UnaryExpression clone = (UnaryExpression)original.Clone();

		Assert.AreEqual(UnaryOperator.BitwiseNot, clone.Operator);
		Assert.AreEqual("int", clone.ExpectedType);
		Assert.AreEqual("test", clone.Metadata["origin"]);
		Assert.AreNotSame(original.Operand, clone.Operand);
		Assert.AreEqual("mask", ((VariableReference)clone.Operand).Name);
	}

	/// <summary>
	/// Tests that every operator has a C-family spelling, so no generator can be handed one it
	/// cannot emit.
	/// </summary>
	[TestMethod]
	public void OperatorSymbols_SpellsEveryUnaryOperator()
	{
		foreach (UnaryOperator op in Enum.GetValues<UnaryOperator>())
		{
			Assert.IsTrue(OperatorSymbols.TryGetSymbol(op, out string? symbol), $"{op} should have a spelling");
			Assert.IsFalse(string.IsNullOrEmpty(symbol), $"{op}'s spelling should not be empty");
		}
	}

	/// <summary>
	/// Tests that an out-of-range operator is refused rather than silently mis-spelled.
	/// </summary>
	[TestMethod]
	public void OperatorSymbols_RejectsUnknownUnaryOperator()
	{
		Assert.IsFalse(OperatorSymbols.TryGetSymbol((UnaryOperator)999, out string? symbol));
		Assert.IsNull(symbol);
		Assert.ThrowsExactly<NotSupportedException>(() => OperatorSymbols.GetSymbol((UnaryOperator)999));
	}

	/// <summary>
	/// Tests the symbolic operators, which every target language spells the same way and joins to
	/// the operand with no space.
	/// </summary>
	/// <param name="op">The operator under test.</param>
	/// <param name="expected">The expected generated source.</param>
	[TestMethod]
	[DataRow(UnaryOperator.Negate, "(-value)")]
	[DataRow(UnaryOperator.Plus, "(+value)")]
	[DataRow(UnaryOperator.BitwiseNot, "(~value)")]
	[DataRow(UnaryOperator.LogicalNot, "(!value)")]
	public void SymbolicOperators_AreSpelledIdenticallyByEveryCFamilyGenerator(UnaryOperator op, string expected)
	{
		UnaryExpression unary = new(op, new VariableReference("value"));

		Assert.AreEqual(expected, new CSharpGenerator().Generate(unary), "C#");
		Assert.AreEqual(expected, new CppGenerator().Generate(unary), "C++");
		Assert.AreEqual(expected, new JavaScriptGenerator().Generate(unary), "JavaScript");
	}

	/// <summary>
	/// Tests that Python spells logical negation as a word, separated from its operand.
	/// </summary>
	[TestMethod]
	public void Python_SpellsLogicalNotAsAWord()
	{
		UnaryExpression unary = new(UnaryOperator.LogicalNot, new VariableReference("ready"));

		Assert.AreEqual("(not ready)", new PythonGenerator().Generate(unary));
	}

	/// <summary>
	/// Tests that Python still uses the symbolic spellings for the operators it shares, and that the
	/// separating space is added only for the word operator.
	/// </summary>
	/// <param name="op">The operator under test.</param>
	/// <param name="expected">The expected generated source.</param>
	[TestMethod]
	[DataRow(UnaryOperator.Negate, "(-count)")]
	[DataRow(UnaryOperator.Plus, "(+count)")]
	[DataRow(UnaryOperator.BitwiseNot, "(~count)")]
	public void Python_UsesSymbolicSpellingsForTheRest(UnaryOperator op, string expected)
	{
		UnaryExpression unary = new(op, new VariableReference("count"));

		Assert.AreEqual(expected, new PythonGenerator().Generate(unary));
	}

	/// <summary>
	/// Tests that the operand is recursed into rather than stringified, so a unary expression can
	/// wrap a compound expression.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_RecursesIntoACompoundOperand()
	{
		BinaryExpression sum = new(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"));
		UnaryExpression negated = new(UnaryOperator.Negate, sum);

		Assert.AreEqual("(-(a + b))", new CppGenerator().Generate(negated));
	}

	/// <summary>
	/// Tests that unary expressions nest, which is the case the parentheses exist for: without them
	/// a double negation would collapse into the decrement operator.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_Nests_WithoutCollidingWithDecrement()
	{
		UnaryExpression inner = new(UnaryOperator.Negate, new VariableReference("x"));
		UnaryExpression outer = new(UnaryOperator.Negate, inner);

		Assert.AreEqual("(-(-x))", new CSharpGenerator().Generate(outer));
	}

	/// <summary>
	/// Tests that a unary expression is accepted wherever an expression is, by generating one inside
	/// a return statement in a function body.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_GeneratesInsideAFunctionBody()
	{
		FunctionDeclaration function = new("negate") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("value", "int"));
		function.Body.Add(new ReturnStatement(new UnaryExpression(UnaryOperator.Negate, new VariableReference("value"))));

		StringAssert.Contains(new CppGenerator().Generate(function), "    return (-value);", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that every generator reports a unary expression as generatable, so <c>Generate</c> does
	/// not refuse it before dispatch.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_CanGenerateUnaryExpressions()
	{
		UnaryExpression unary = new(UnaryOperator.Negate, new VariableReference("x"));

		Assert.IsTrue(new CSharpGenerator().CanGenerate(unary), "C#");
		Assert.IsTrue(new CppGenerator().CanGenerate(unary), "C++");
		Assert.IsTrue(new JavaScriptGenerator().CanGenerate(unary), "JavaScript");
		Assert.IsTrue(new PythonGenerator().CanGenerate(unary), "Python");
	}

	/// <summary>
	/// Tests that a unary expression survives a YAML round-trip with its operator and operand intact.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_SurvivesAYamlRoundTrip()
	{
		FunctionDeclaration original = new("isIdle");
		original.Body.Add(new ReturnStatement(
			new UnaryExpression(UnaryOperator.LogicalNot, new VariableReference("busy"))));

		string yaml = new YamlSerializer().Serialize(original);
		AstNode? restored = new YamlDeserializer().Deserialize(yaml);

		Assert.IsInstanceOfType<FunctionDeclaration>(restored);
		ReturnStatement returnStmt = (ReturnStatement)((FunctionDeclaration)restored).Body[0];
		Assert.IsInstanceOfType<UnaryExpression>(returnStmt.Expression);

		UnaryExpression unary = (UnaryExpression)returnStmt.Expression;
		Assert.AreEqual(UnaryOperator.LogicalNot, unary.Operator);
		Assert.AreEqual("busy", ((VariableReference)unary.Operand).Name);
	}

	/// <summary>
	/// Tests that the serialized form names the operator and carries the operand under its own key,
	/// so the YAML is readable rather than only round-trippable.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_SerializesToNamedKeys()
	{
		UnaryExpression unary = new(UnaryOperator.BitwiseNot, new VariableReference("mask"))
		{
			ExpectedType = "int"
		};

		string yaml = new YamlSerializer().Serialize(unary);

		StringAssert.Contains(yaml, "unaryExpression:", StringComparison.Ordinal);
		StringAssert.Contains(yaml, "operator: BitwiseNot", StringComparison.Ordinal);
		StringAssert.Contains(yaml, "operand:", StringComparison.Ordinal);
		StringAssert.Contains(yaml, "expectedType: int", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that hand-written YAML naming the node in PascalCase deserializes, and that
	/// <see cref="Expression.ExpectedType"/> is restored. The serializer only ever emits camelCase,
	/// so a round-trip alone exercises neither.
	/// </summary>
	[TestMethod]
	public void UnaryExpression_DeserializesFromPascalCaseYaml()
	{
		string yaml = string.Join(
			Environment.NewLine,
			"UnaryExpression:",
			"  operator: LogicalNot",
			"  operand:",
			"    VariableReference:",
			"      name: busy",
			"  expectedType: bool");

		AstNode? restored = new YamlDeserializer().Deserialize(yaml);

		Assert.IsInstanceOfType<UnaryExpression>(restored);
		UnaryExpression unary = (UnaryExpression)restored;
		Assert.AreEqual(UnaryOperator.LogicalNot, unary.Operator);
		Assert.AreEqual("bool", unary.ExpectedType);
		Assert.AreEqual("busy", ((VariableReference)unary.Operand).Name);
	}
}
