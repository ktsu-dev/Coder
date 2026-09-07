// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="AstSchema"/>, the uniform view over the AST's three ways of storing children.
/// </summary>
/// <remarks>
/// The schema is the library's contract with the AST, so these cover every node type it claims to
/// know: a slot the table forgets, or an attach case that silently refuses, would surface here as a
/// gap rather than as a node the editor cannot wire up.
/// </remarks>
[TestClass]
public class AstSchemaTests
{
	private static readonly string[] FunctionSlots = ["Parameters", "Body"];
	private static readonly string[] ReturnSlots = ["Expression"];
	private static readonly string[] BinarySlots = ["Left", "Right"];
	private static readonly string[] UnarySlots = ["Operand"];
	private static readonly string[] VariableSlots = ["InitialValue"];
	private static readonly string[] AssignmentSlots = ["Target", "Value"];
	private static readonly string[] TwoParameterNames = ["a", "b"];

	private static AstSlot Slot(AstNode node, string name) =>
		AstSchema.SlotsOf(node).Single(slot => slot.Name == name);

	/// <summary>
	/// Tests that every node type the schema knows exposes the slots it should, and that a leaf
	/// exposes none.
	/// </summary>
	[TestMethod]
	public void SlotsOf_DescribesEveryKnownNodeType()
	{
		CollectionAssert.AreEqual(
			FunctionSlots,
			AstSchema.SlotsOf(new FunctionDeclaration("f")).Select(s => s.Name).ToArray());

		CollectionAssert.AreEqual(
			ReturnSlots,
			AstSchema.SlotsOf(new ReturnStatement()).Select(s => s.Name).ToArray());

		CollectionAssert.AreEqual(
			BinarySlots,
			AstSchema.SlotsOf(NewBinary()).Select(s => s.Name).ToArray());

		CollectionAssert.AreEqual(
			UnarySlots,
			AstSchema.SlotsOf(NewUnary()).Select(s => s.Name).ToArray());

		CollectionAssert.AreEqual(
			VariableSlots,
			AstSchema.SlotsOf(new VariableDeclaration("v")).Select(s => s.Name).ToArray());

		CollectionAssert.AreEqual(
			AssignmentSlots,
			AstSchema.SlotsOf(NewAssignment()).Select(s => s.Name).ToArray());
	}

	/// <summary>
	/// Tests that the leaves expose no slots, so the editor draws them with no input pins.
	/// </summary>
	/// <param name="node">The leaf under test.</param>
	[TestMethod]
	[DynamicData(nameof(Leaves))]
	public void SlotsOf_IsEmptyForALeaf(AstNode node)
	{
		ArgumentNullException.ThrowIfNull(node);

		Assert.AreEqual(0, AstSchema.SlotsOf(node).Count, node.GetNodeTypeName());
	}

	/// <summary>
	/// Gets the leaf nodes, which hold no children.
	/// </summary>
	/// <returns>One leaf per row.</returns>
	public static IEnumerable<object[]> Leaves =>
	[
		[new Parameter("p", "int")],
		[new VariableReference("v")],
		[Literal.Text("t")],
		[Literal.Number(1)],
		[Literal.Bool(true)],
		[Literal.DecimalValue(1.5)],
		[new AstLeafNode<int>(3)],
	];

	/// <summary>
	/// Tests that a single-valued slot reads back the child sitting in it, across every node type
	/// that has one.
	/// </summary>
	[TestMethod]
	public void ChildrenOf_ReadsSingleValuedSlots()
	{
		VariableReference operand = new("x");

		ReturnStatement returnStmt = new(operand);
		Assert.AreSame(operand, AstSchema.ChildrenOf(returnStmt, Slot(returnStmt, "Expression")).Single());

		BinaryExpression binary = new(operand, BinaryOperator.Add, new VariableReference("y"));
		Assert.AreSame(operand, AstSchema.ChildrenOf(binary, Slot(binary, "Left")).Single());
		Assert.AreEqual("y", ((VariableReference)AstSchema.ChildrenOf(binary, Slot(binary, "Right")).Single()).Name);

		UnaryExpression unary = new(UnaryOperator.Negate, operand);
		Assert.AreSame(operand, AstSchema.ChildrenOf(unary, Slot(unary, "Operand")).Single());

		VariableDeclaration varDecl = new("v", "int", operand);
		Assert.AreSame(operand, AstSchema.ChildrenOf(varDecl, Slot(varDecl, "InitialValue")).Single());

		AssignmentStatement assignment = new(operand, new VariableReference("z"));
		Assert.AreSame(operand, AstSchema.ChildrenOf(assignment, Slot(assignment, "Target")).Single());
		Assert.AreEqual("z", ((VariableReference)AstSchema.ChildrenOf(assignment, Slot(assignment, "Value")).Single()).Name);
	}

	/// <summary>
	/// Tests that an unfilled optional slot reads back as empty rather than throwing.
	/// </summary>
	[TestMethod]
	public void ChildrenOf_IsEmptyForAnUnfilledOptionalSlot()
	{
		ReturnStatement returnStmt = new();
		Assert.AreEqual(0, AstSchema.ChildrenOf(returnStmt, Slot(returnStmt, "Expression")).Count);

		VariableDeclaration varDecl = new("v", "int");
		Assert.AreEqual(0, AstSchema.ChildrenOf(varDecl, Slot(varDecl, "InitialValue")).Count);
	}

	/// <summary>
	/// Tests that a variadic slot reads back its whole sequence, in order.
	/// </summary>
	[TestMethod]
	public void ChildrenOf_ReadsVariadicSlotsInOrder()
	{
		FunctionDeclaration function = new("f");
		function.Parameters.Add(new Parameter("a"));
		function.Parameters.Add(new Parameter("b"));
		function.Body.Add(new ReturnStatement());

		CollectionAssert.AreEqual(
			TwoParameterNames,
			AstSchema.ChildrenOf(function, Slot(function, "Parameters")).Cast<Parameter>().Select(p => p.Name).ToArray());

		Assert.AreEqual(1, AstSchema.ChildrenOf(function, Slot(function, "Body")).Count);
	}

	/// <summary>
	/// Tests that asking a node for a slot it does not have is refused loudly, since that can only
	/// mean the caller mixed up two nodes.
	/// </summary>
	[TestMethod]
	public void ChildrenOf_RejectsAForeignSlot()
	{
		BinaryExpression binary = NewBinary();
		AstSlot foreign = Slot(new FunctionDeclaration("f"), "Body");

		Assert.ThrowsExactly<ArgumentException>(() => AstSchema.ChildrenOf(binary, foreign));
	}

	/// <summary>
	/// Tests that attaching fills each single-valued slot, replacing whatever was there.
	/// </summary>
	[TestMethod]
	public void TryAttach_FillsEverySingleValuedSlot()
	{
		VariableReference fresh = new("fresh");

		BinaryExpression binary = NewBinary();
		Assert.IsTrue(AstSchema.TryAttach(binary, Slot(binary, "Left"), fresh));
		Assert.AreSame(fresh, binary.Left);
		Assert.IsTrue(AstSchema.TryAttach(binary, Slot(binary, "Right"), fresh));
		Assert.AreSame(fresh, binary.Right);

		UnaryExpression unary = NewUnary();
		Assert.IsTrue(AstSchema.TryAttach(unary, Slot(unary, "Operand"), fresh));
		Assert.AreSame(fresh, unary.Operand);

		AssignmentStatement assignment = NewAssignment();
		Assert.IsTrue(AstSchema.TryAttach(assignment, Slot(assignment, "Target"), fresh));
		Assert.AreSame(fresh, assignment.Target);
		Assert.IsTrue(AstSchema.TryAttach(assignment, Slot(assignment, "Value"), fresh));
		Assert.AreSame(fresh, assignment.Value);

		VariableDeclaration varDecl = new("v", "int");
		Assert.IsTrue(AstSchema.TryAttach(varDecl, Slot(varDecl, "InitialValue"), fresh));
		Assert.AreSame(fresh, varDecl.InitialValue);

		ReturnStatement returnStmt = new();
		Assert.IsTrue(AstSchema.TryAttach(returnStmt, Slot(returnStmt, "Expression"), fresh));
		Assert.AreSame(fresh, returnStmt.Expression);
	}

	/// <summary>
	/// Tests that attaching past the end of a variadic slot appends.
	/// </summary>
	[TestMethod]
	public void TryAttach_AppendsToAVariadicSlot()
	{
		FunctionDeclaration function = new("f");
		Parameter first = new("a");
		Parameter second = new("b");

		Assert.IsTrue(AstSchema.TryAttach(function, Slot(function, "Parameters"), first));
		Assert.IsTrue(AstSchema.TryAttach(function, Slot(function, "Parameters"), second));
		Assert.IsTrue(AstSchema.TryAttach(function, Slot(function, "Body"), new ReturnStatement()));

		CollectionAssert.AreEqual(new[] { first, second }, function.Parameters.ToArray());
		Assert.AreEqual(1, function.Body.Count);
	}

	/// <summary>
	/// Tests that attaching inside an existing sequence replaces that entry rather than appending,
	/// which is what makes dropping onto a filled pin swap its occupant.
	/// </summary>
	[TestMethod]
	public void TryAttachAt_ReplacesInsideASequence()
	{
		FunctionDeclaration function = new("f");
		function.Parameters.Add(new Parameter("a"));
		function.Parameters.Add(new Parameter("b"));
		Parameter replacement = new("replaced");

		Assert.IsTrue(AstSchema.TryAttachAt(function, Slot(function, "Parameters"), 0, replacement));

		Assert.AreEqual(2, function.Parameters.Count);
		Assert.AreSame(replacement, function.Parameters[0]);
		Assert.AreEqual("b", function.Parameters[1].Name);
	}

	/// <summary>
	/// Tests that a slot refuses a node of the wrong shape.
	/// </summary>
	[TestMethod]
	public void TryAttach_RefusesTheWrongShape()
	{
		FunctionDeclaration function = new("f");
		BinaryExpression binary = NewBinary();

		// A statement is not a parameter, and a parameter is not an expression.
		Assert.IsFalse(AstSchema.TryAttach(function, Slot(function, "Parameters"), new ReturnStatement()));
		Assert.IsFalse(AstSchema.TryAttach(binary, Slot(binary, "Left"), new Parameter("p")));
	}

	/// <summary>
	/// Tests that detaching a non-nullable operand leaves the AST's own "not filled in yet"
	/// placeholder rather than a null the rest of the library would not expect.
	/// </summary>
	[TestMethod]
	public void TryDetachAt_LeavesAPlaceholderInANonNullableOperand()
	{
		BinaryExpression binary = NewBinary();
		Assert.IsTrue(AstSchema.TryDetachAt(binary, Slot(binary, "Left"), 0));
		Assert.IsTrue(AstSchema.IsUnfilled(binary.Left));
		Assert.IsTrue(AstSchema.TryDetachAt(binary, Slot(binary, "Right"), 0));
		Assert.IsTrue(AstSchema.IsUnfilled(binary.Right));

		UnaryExpression unary = NewUnary();
		Assert.IsTrue(AstSchema.TryDetachAt(unary, Slot(unary, "Operand"), 0));
		Assert.IsTrue(AstSchema.IsUnfilled(unary.Operand));

		AssignmentStatement assignment = NewAssignment();
		Assert.IsTrue(AstSchema.TryDetachAt(assignment, Slot(assignment, "Target"), 0));
		Assert.IsTrue(AstSchema.IsUnfilled(assignment.Target));
		Assert.IsTrue(AstSchema.TryDetachAt(assignment, Slot(assignment, "Value"), 0));
		Assert.IsTrue(AstSchema.IsUnfilled(assignment.Value));
	}

	/// <summary>
	/// Tests that detaching a nullable slot empties it, and that detaching an already-empty one
	/// reports that nothing happened.
	/// </summary>
	[TestMethod]
	public void TryDetachAt_EmptiesNullableSlots()
	{
		ReturnStatement returnStmt = new(new VariableReference("x"));
		Assert.IsTrue(AstSchema.TryDetachAt(returnStmt, Slot(returnStmt, "Expression"), 0));
		Assert.IsNull(returnStmt.Expression);
		Assert.IsFalse(AstSchema.TryDetachAt(returnStmt, Slot(returnStmt, "Expression"), 0));

		VariableDeclaration varDecl = new("v", "int", new VariableReference("x"));
		Assert.IsTrue(AstSchema.TryDetachAt(varDecl, Slot(varDecl, "InitialValue"), 0));
		Assert.IsNull(varDecl.InitialValue);
		Assert.IsFalse(AstSchema.TryDetachAt(varDecl, Slot(varDecl, "InitialValue"), 0));
	}

	/// <summary>
	/// Tests that detaching from a variadic slot removes that entry, and that an index past the end
	/// reports that nothing happened.
	/// </summary>
	[TestMethod]
	public void TryDetachAt_RemovesFromAVariadicSlot()
	{
		FunctionDeclaration function = new("f");
		function.Parameters.Add(new Parameter("a"));
		function.Parameters.Add(new Parameter("b"));
		function.Body.Add(new ReturnStatement());

		Assert.IsTrue(AstSchema.TryDetachAt(function, Slot(function, "Parameters"), 0));
		Assert.AreEqual("b", function.Parameters.Single().Name);

		Assert.IsTrue(AstSchema.TryDetachAt(function, Slot(function, "Body"), 0));
		Assert.AreEqual(0, function.Body.Count);

		Assert.IsFalse(AstSchema.TryDetachAt(function, Slot(function, "Parameters"), 9));
		Assert.IsFalse(AstSchema.TryDetachAt(function, Slot(function, "Body"), 9));
	}

	/// <summary>
	/// Tests what each kind of slot will and will not take.
	/// </summary>
	[TestMethod]
	public void Accepts_GatesByKind()
	{
		FunctionDeclaration function = new("f");
		AstSlot parameters = Slot(function, "Parameters");
		AstSlot body = Slot(function, "Body");
		BinaryExpression binary = NewBinary();
		AstSlot expression = Slot(binary, "Left");

		Assert.IsTrue(AstSchema.Accepts(parameters, new Parameter("p")));
		Assert.IsFalse(AstSchema.Accepts(parameters, new VariableReference("v")));

		Assert.IsTrue(AstSchema.Accepts(body, new ReturnStatement()));
		Assert.IsTrue(AstSchema.Accepts(body, new VariableReference("v")), "an expression can stand as a statement");
		Assert.IsFalse(AstSchema.Accepts(body, new Parameter("p")));

		Assert.IsTrue(AstSchema.Accepts(expression, new VariableReference("v")));
		Assert.IsFalse(AstSchema.Accepts(expression, new Parameter("p")));
		Assert.IsFalse(AstSchema.Accepts(expression, new ReturnStatement()));
	}

	/// <summary>
	/// Tests that the legacy leaf shapes count as expressions, since every generator emits them
	/// wherever an expression is expected even though they are not <c>Expression</c> subclasses.
	/// </summary>
	[TestMethod]
	public void IsExpression_AcceptsLiteralsAndLegacyLeaves()
	{
		Assert.IsTrue(AstSchema.IsExpression(new VariableReference("v")));
		Assert.IsTrue(AstSchema.IsExpression(Literal.Text("t")));
		Assert.IsTrue(AstSchema.IsExpression(new AstLeafNode<string>("t")));
		Assert.IsTrue(AstSchema.IsExpression(new AstLeafNode<int>(1)));
		Assert.IsTrue(AstSchema.IsExpression(new AstLeafNode<bool>(true)));
		Assert.IsTrue(AstSchema.IsExpression(new AstLeafNode<double>(1.5)));

		Assert.IsFalse(AstSchema.IsExpression(new ReturnStatement()));
		Assert.IsFalse(AstSchema.IsExpression(new Parameter("p")));
	}

	/// <summary>
	/// Tests that the placeholder is recognised in both the literal and the legacy leaf spelling, and
	/// that a deliberate non-empty string is not mistaken for it.
	/// </summary>
	[TestMethod]
	public void IsUnfilled_RecognisesOnlyTheEmptyPlaceholder()
	{
		Assert.IsTrue(AstSchema.IsUnfilled(AstSchema.Unfilled()));
		Assert.IsTrue(AstSchema.IsUnfilled(new AstLeafNode<string>(string.Empty)));
		Assert.IsFalse(AstSchema.IsUnfilled(Literal.Text("something")));
		Assert.IsFalse(AstSchema.IsUnfilled(Literal.Number(0)));
	}

	/// <summary>
	/// Tests that the caption names every node kind, including the detail that tells two of a kind
	/// apart on screen.
	/// </summary>
	[TestMethod]
	public void Describe_CoversEveryNodeKind()
	{
		Assert.AreEqual("function f", AstSchema.Describe(new FunctionDeclaration("f")));
		Assert.AreEqual("function <unnamed>", AstSchema.Describe(new FunctionDeclaration()));
		Assert.AreEqual("param p", AstSchema.Describe(new Parameter("p")));
		Assert.AreEqual("param <unnamed>", AstSchema.Describe(new Parameter()));
		Assert.AreEqual("return", AstSchema.Describe(new ReturnStatement()));
		Assert.AreEqual("binary +", AstSchema.Describe(NewBinary()));
		Assert.AreEqual("unary -", AstSchema.Describe(NewUnary()));
		Assert.AreEqual("assign =", AstSchema.Describe(NewAssignment()));
		Assert.AreEqual("var v", AstSchema.Describe(new VariableDeclaration("v")));
		Assert.AreEqual("x", AstSchema.Describe(new VariableReference("x")));
		Assert.AreEqual("\"t\"", AstSchema.Describe(Literal.Text("t")));
		Assert.AreEqual("7", AstSchema.Describe(Literal.Number(7)));
		Assert.AreEqual("1.5", AstSchema.Describe(Literal.DecimalValue(1.5)));
		Assert.AreEqual("true", AstSchema.Describe(Literal.Bool(true)));
		Assert.AreEqual("false", AstSchema.Describe(Literal.Bool(false)));
		Assert.AreEqual("\"t\"", AstSchema.Describe(new AstLeafNode<string>("t")));
		Assert.AreEqual("7", AstSchema.Describe(new AstLeafNode<int>(7)));
		Assert.AreEqual("true", AstSchema.Describe(new AstLeafNode<bool>(true)));
		Assert.AreEqual("false", AstSchema.Describe(new AstLeafNode<bool>(false)));
	}

	/// <summary>
	/// Tests that a caption never throws on an operator with no spelling, since a node that cannot be
	/// drawn is worse than one drawn by its enum name.
	/// </summary>
	[TestMethod]
	public void Describe_FallsBackToTheOperatorName()
	{
		BinaryExpression binary = NewBinary();
		binary.Operator = (BinaryOperator)999;
		Assert.AreEqual("binary 999", AstSchema.Describe(binary));

		UnaryExpression unary = NewUnary();
		unary.Operator = (UnaryOperator)999;
		Assert.AreEqual("unary 999", AstSchema.Describe(unary));

		AssignmentStatement assignment = NewAssignment();
		assignment.Operator = (AssignmentOperator)999;
		Assert.AreEqual("assign 999", AstSchema.Describe(assignment));
	}

	/// <summary>
	/// Tests that a node the schema has no caption for is named by its type rather than refused.
	/// </summary>
	[TestMethod]
	public void Describe_FallsBackToTheNodeTypeName() =>
		Assert.AreEqual("Leaf<Double>", AstSchema.Describe(new AstLeafNode<double>(1.5)));

	/// <summary>
	/// Tests that a null string literal describes as an empty pair of quotes rather than throwing.
	/// </summary>
	[TestMethod]
	public void Describe_HandlesANullLiteralValue() =>
		Assert.AreEqual("\"\"", AstSchema.Describe(new LiteralExpression<string>()));

	private static BinaryExpression NewBinary() =>
		new(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"));

	private static UnaryExpression NewUnary() =>
		new(UnaryOperator.Negate, new VariableReference("a"));

	private static AssignmentStatement NewAssignment() =>
		new(new VariableReference("a"), new VariableReference("b"));
}
