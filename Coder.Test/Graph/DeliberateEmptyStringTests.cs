// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that a deliberate empty string is a value like any other, and is never mistaken for the
/// placeholder an unfilled operand holds (ktsu-dev/Coder#421).
/// </summary>
/// <remarks>
/// The placeholder used to be an empty string literal, and the graph tested for it by value, so
/// <c>return ""</c> reported "nothing in Expression" and blocked preview and export of the whole
/// document.
/// </remarks>
[TestClass]
public class DeliberateEmptyStringTests
{
	/// <summary>
	/// Tests the issue's repro: returning an empty string is a finished document.
	/// </summary>
	[TestMethod]
	public void Validate_AcceptsReturningAnEmptyString()
	{
		FunctionDeclaration function = new("greeting") { ReturnType = "str" };
		function.Body.Add(new ReturnStatement(new LiteralExpression<string>(string.Empty)));

		Assert.IsEmpty(new AstGraph(function).Validate());
	}

	/// <summary>
	/// Tests that comparing or assigning an empty string is not reported either.
	/// </summary>
	[TestMethod]
	public void Validate_AcceptsEmptyStringOperands()
	{
		FunctionDeclaration function = new("reset") { ReturnType = "bool" };
		function.Body.Add(new AssignmentStatement(new VariableReference("s"), Literal.Text(string.Empty)));
		function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("name"), BinaryOperator.Equal, Literal.Text(string.Empty))));

		Assert.IsEmpty(new AstGraph(function).Validate());
	}

	/// <summary>
	/// Tests that an operand that was never filled is still reported.
	/// </summary>
	[TestMethod]
	public void Validate_StillReportsAnUnfilledOperand()
	{
		FunctionDeclaration function = new("sum") { ReturnType = "int" };
		function.Body.Add(new ReturnStatement(new BinaryExpression(AstSchema.Unfilled(), BinaryOperator.Add, Literal.Number(1))));

		IReadOnlyList<AstGraphProblem> problems = new AstGraph(function).Validate();

		Assert.HasCount(1, problems);
		StringAssert.Contains(problems[0].Message, "Left");
	}

	/// <summary>
	/// Tests that a parameterless constructor's operands still read as unfilled.
	/// </summary>
	[TestMethod]
	public void ParameterlessConstructors_InstallThePlaceholder()
	{
		BinaryExpression binary = new();

		Assert.IsTrue(AstSchema.IsUnfilled(binary.Left));
		Assert.IsTrue(AstSchema.IsUnfilled(binary.Right));
		Assert.IsTrue(AstSchema.IsUnfilled(new UnaryExpression().Operand));
		Assert.IsTrue(AstSchema.IsUnfilled(new ExpressionStatement().Expression));
	}

	/// <summary>
	/// Tests that the placeholder and a deliberate empty string tell themselves apart.
	/// </summary>
	[TestMethod]
	public void IsUnfilled_IsFalseForADeliberateEmptyString()
	{
		Assert.IsFalse(AstSchema.IsUnfilled(Literal.Text(string.Empty)));
		Assert.IsFalse(AstSchema.IsUnfilled(new LiteralExpression<string>(string.Empty)));
		Assert.IsTrue(AstSchema.IsUnfilled(AstSchema.Unfilled()));
		Assert.IsTrue(AstSchema.IsUnfilled(AstSchema.Unfilled().Clone()));
	}

	/// <summary>
	/// Tests that replacing a node carries a deliberate empty string over to the replacement, as it
	/// would any other operand, instead of dropping it as a placeholder.
	/// </summary>
	[TestMethod]
	public void Replace_CarriesADeliberateEmptyStringOver()
	{
		LiteralExpression<string> empty = Literal.Text(string.Empty);
		BinaryExpression binary = new(empty, BinaryOperator.Add, new VariableReference("name"));
		FunctionDeclaration function = new("label") { ReturnType = "str" };
		function.Body.Add(new ReturnStatement(binary));
		AstGraph graph = new(function);

		BinaryExpression comparison = new(AstSchema.Unfilled(), BinaryOperator.Equal, AstSchema.Unfilled());
		graph.Replace(binary, comparison);

		Assert.AreSame(empty, comparison.Left);
	}

	/// <summary>
	/// Tests that a YAML round trip keeps a deliberate empty string and an unfilled operand apart.
	/// </summary>
	[TestMethod]
	public void YamlRoundTrip_KeepsEmptyStringAndPlaceholderDistinct()
	{
		BinaryExpression binary = new(Literal.Text(string.Empty), BinaryOperator.Add, AstSchema.Unfilled());

		string yaml = new YamlSerializer().Serialize(binary);
		BinaryExpression reloaded = Assert.IsInstanceOfType<BinaryExpression>(new YamlDeserializer().Deserialize(yaml));

		Assert.IsFalse(AstSchema.IsUnfilled(reloaded.Left), yaml);
		Assert.AreEqual(string.Empty, Assert.IsInstanceOfType<LiteralExpression<string>>(reloaded.Left).Value);
		Assert.IsTrue(AstSchema.IsUnfilled(reloaded.Right), yaml);
	}
}
