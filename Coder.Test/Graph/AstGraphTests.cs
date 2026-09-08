// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGuiNodeEditor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="AstGraph"/>, the node-editor view of an AST.
/// </summary>
/// <remarks>
/// These drive the graph headlessly. Nothing here touches ImGui: the engine is pure state and
/// force-directed layout, which is the whole reason the view is kept separate from the renderer.
/// </remarks>
[TestClass]
public class AstGraphTests
{
	private static readonly string[] ExpectedFunctionPins =
		["Parameters[0]", "Parameters[1]", "Parameters[2]", "Parameters[3]", "Body[0]", "Body[1]", "Body[2]"];

	private static readonly string[] ExpectedBinaryPins = ["Left", "Right"];

	/// <summary>
	/// Builds a function whose body covers every slot shape: a variadic parameter list, a variadic
	/// body, and single-valued operands nested two deep.
	/// </summary>
	/// <returns>The function under test.</returns>
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Parameters.Add(new Parameter("b", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));
		return function;
	}

	/// <summary>
	/// Tests that every AST node in the tree becomes exactly one editor node.
	/// </summary>
	[TestMethod]
	public void Graph_HasOneNodePerAstNode()
	{
		FunctionDeclaration function = SampleFunction();

		AstGraph graph = new(function);

		// function, 2 parameters, return, binary, 2 variable references
		Assert.AreEqual(7, graph.Nodes.Count);
		Assert.AreEqual(7, graph.Engine.Nodes.Count);
	}

	/// <summary>
	/// Tests that every parent/child edge in the tree becomes a link, and no others.
	/// </summary>
	[TestMethod]
	public void Graph_LinksEveryParentChildEdge()
	{
		AstGraph graph = new(SampleFunction());

		// function->a, function->b, function->return, return->binary, binary->left, binary->right
		Assert.AreEqual(6, graph.Engine.Links.Count);
	}

	/// <summary>
	/// Tests that a single-valued slot gets one pin named for the slot, and a variadic one gets a pin
	/// per child plus spares to connect into.
	/// </summary>
	[TestMethod]
	public void VariadicSlot_OffersAPinPerChildPlusSpares()
	{
		AstGraph graph = new(SampleFunction());
		Node functionNode = graph.Engine.Nodes.Single(n => graph.AstNodeFor(n.Id) is FunctionDeclaration);

		string[] pinNames = [.. functionNode.InputPins.Select(p => p.EffectiveDisplayName)];

		// Two parameters and one body statement, each plus two spares.
		CollectionAssert.AreEqual(ExpectedFunctionPins, pinNames);
	}

	/// <summary>
	/// Tests that a single-valued slot's pin carries the bare slot name, since numbering it would
	/// suggest more than one could be connected.
	/// </summary>
	[TestMethod]
	public void SingleSlot_PinIsNamedForTheSlot()
	{
		AstGraph graph = new(SampleFunction());
		Node binaryNode = graph.Engine.Nodes.Single(n => graph.AstNodeFor(n.Id) is BinaryExpression);

		CollectionAssert.AreEqual(ExpectedBinaryPins, binaryNode.InputPins.Select(p => p.EffectiveDisplayName).ToArray());
	}

	/// <summary>
	/// Tests that a node created from the palette shows up even though nothing references it yet.
	/// </summary>
	[TestMethod]
	public void DetachedNode_IsDrawnBeforeItIsConnected()
	{
		AstGraph graph = new(SampleFunction());
		int before = graph.Nodes.Count;

		int id = graph.AddDetached(new VariableReference("c"), new Vector2(10, 10));

		Assert.AreEqual(before + 1, graph.Nodes.Count);
		Assert.IsInstanceOfType<VariableReference>(graph.AstNodeFor(id));
		Assert.AreEqual(1, graph.Detached.Count);
	}

	/// <summary>
	/// Tests that connecting a detached node into a slot writes through to the AST.
	/// </summary>
	[TestMethod]
	public void Connect_AttachesToTheAst()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		Parameter parameter = new("c", "int");
		graph.AddDetached(parameter, Vector2.Zero);

		AstConnectResult result = Connect(graph, parameter, function, "Parameters", 2);

		Assert.IsTrue(result.Success, result.Message);
		Assert.AreEqual(3, function.Parameters.Count);
		Assert.AreSame(parameter, function.Parameters[2]);
		Assert.AreEqual(0, graph.Detached.Count);
	}

	/// <summary>
	/// Tests that a slot refuses a node of the wrong shape, rather than producing an AST that cannot
	/// be generated from.
	/// </summary>
	[TestMethod]
	public void Connect_RefusesTheWrongShapeOfNode()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		ReturnStatement statement = new(Literal.Number(1));
		graph.AddDetached(statement, Vector2.Zero);

		AstConnectResult result = Connect(graph, statement, function, "Parameters", 2);

		Assert.IsFalse(result.Success);
		Assert.AreEqual(2, function.Parameters.Count);
	}

	/// <summary>
	/// Tests that a node cannot be connected to itself.
	/// </summary>
	[TestMethod]
	public void Connect_RefusesSelfConnection()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		BinaryExpression binary = (BinaryExpression)((ReturnStatement)function.Body[0]).Expression!;

		AstConnectResult result = Connect(graph, binary, binary, "Left", 0);

		Assert.IsFalse(result.Success);
		StringAssert.Contains(result.Message, "its own child", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an ancestor cannot be connected beneath one of its descendants, which would make
	/// the tree cyclic and every walk over it non-terminating.
	/// </summary>
	[TestMethod]
	public void Connect_RefusesACycle()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		ReturnStatement returnStmt = (ReturnStatement)function.Body[0];
		BinaryExpression binary = (BinaryExpression)returnStmt.Expression!;

		// The return statement is the binary expression's parent, so this asks for a loop.
		AstConnectResult result = Connect(graph, returnStmt, binary, "Left", 0);

		Assert.IsFalse(result.Success);
		StringAssert.Contains(result.Message, "inside itself", StringComparison.Ordinal);
		Assert.AreSame(binary, returnStmt.Expression, "a refused connection must not disturb the tree");
	}

	/// <summary>
	/// Tests that connecting a node that already has a parent moves it rather than leaving it in two
	/// places, since the AST is a tree.
	/// </summary>
	[TestMethod]
	public void Connect_MovesANodeThatAlreadyHasAParent()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		BinaryExpression binary = (BinaryExpression)((ReturnStatement)function.Body[0]).Expression!;
		Expression left = binary.Left;

		AstConnectResult result = Connect(graph, left, binary, "Right", 0);

		Assert.IsTrue(result.Success, result.Message);
		Assert.AreSame(left, binary.Right);
		Assert.AreNotSame(left, binary.Left);
		Assert.IsTrue(AstSchema.IsUnfilled(binary.Left), "the vacated operand should read as outstanding");
	}

	/// <summary>
	/// Tests that cutting a link removes the child from the AST but keeps it on screen, so a
	/// disconnected node can be reconnected rather than silently vanishing.
	/// </summary>
	[TestMethod]
	public void Disconnect_LeavesTheNodeOnScreenButOutOfTheDocument()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		Parameter first = function.Parameters[0];
		int linkId = LinkInto(graph, first);

		Assert.IsTrue(graph.Disconnect(linkId));

		Assert.AreEqual(1, function.Parameters.Count);
		CollectionAssert.Contains(graph.Detached.ToArray(), first);
		Assert.IsTrue(graph.Nodes.Values.Any(n => ReferenceEquals(n, first)), "a cut node should stay visible");
	}

	/// <summary>
	/// Tests that removing a node takes its subtree with it.
	/// </summary>
	[TestMethod]
	public void Remove_TakesTheSubtreeWithIt()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		ReturnStatement returnStmt = (ReturnStatement)function.Body[0];
		int returnNodeId = graph.Nodes.First(pair => ReferenceEquals(pair.Value, returnStmt)).Key;

		Assert.IsTrue(graph.Remove(returnNodeId));

		Assert.AreEqual(0, function.Body.Count);

		// The function and its two parameters are all that is left.
		Assert.AreEqual(3, graph.Nodes.Count);
	}

	/// <summary>
	/// Tests that the document's root cannot be removed, since the graph would then have nothing to
	/// generate from.
	/// </summary>
	[TestMethod]
	public void Remove_RefusesTheRoot()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		int rootId = graph.Nodes.First(pair => ReferenceEquals(pair.Value, function)).Key;

		Assert.IsFalse(graph.Remove(rootId));
		Assert.AreSame(function, graph.Root);
	}

	/// <summary>
	/// Tests that a complete document reports no problems.
	/// </summary>
	[TestMethod]
	public void Validate_IsSilentOnACompleteDocument()
	{
		AstGraph graph = new(SampleFunction());

		Assert.AreEqual(0, graph.Validate().Count);
	}

	/// <summary>
	/// Tests that an outstanding operand and a stranded node are both reported rather than throwing,
	/// since a half-built graph is a normal state to be in while editing.
	/// </summary>
	[TestMethod]
	public void Validate_ReportsOutstandingOperandsAndStrandedNodes()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		BinaryExpression binary = (BinaryExpression)((ReturnStatement)function.Body[0]).Expression!;
		int linkId = LinkInto(graph, binary.Left);

		Assert.IsTrue(graph.Disconnect(linkId));

		IReadOnlyList<AstGraphProblem> problems = graph.Validate();

		Assert.IsTrue(problems.Any(p => p.Message.Contains("nothing in Left", StringComparison.Ordinal)),
			"the vacated operand should be reported");
		Assert.IsTrue(problems.Any(p => p.Message.Contains("not connected", StringComparison.Ordinal)),
			"the cut node should be reported as stranded");
	}

	/// <summary>
	/// Tests that a rebuild keeps each node where it was, so an edit does not scatter the graph the
	/// user has arranged.
	/// </summary>
	[TestMethod]
	public void Rebuild_PreservesPositions()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		int nodeId = graph.Nodes.First(pair => pair.Value is VariableReference).Key;
		Vector2 moved = new(1234, 5678);
		graph.Engine.UpdateNodePosition(nodeId, moved);

		graph.Rebuild();

		AstNode movedAstNode = graph.AstNodeFor(nodeId)!;
		Assert.AreEqual(moved, graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), movedAstNode)).Position);
	}

	/// <summary>
	/// Tests that the caption names the node and the detail that tells two of a kind apart.
	/// </summary>
	[TestMethod]
	public void Describe_NamesTheIdentifyingDetail()
	{
		Assert.AreEqual("function total", AstSchema.Describe(SampleFunction()));
		Assert.AreEqual("binary +", AstSchema.Describe(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));
		Assert.AreEqual("unary !", AstSchema.Describe(
			new UnaryExpression(UnaryOperator.LogicalNot, new VariableReference("ready"))));
		Assert.AreEqual("count", AstSchema.Describe(new VariableReference("count")));
		Assert.AreEqual("\"hi\"", AstSchema.Describe(Literal.Text("hi")));
		Assert.AreEqual("42", AstSchema.Describe(Literal.Number(42)));
	}

	/// <summary>
	/// Tests that a node with no parent reports itself as detached, which is what an undo step
	/// recorded for a node created from the palette has to restore it to.
	/// </summary>
	[TestMethod]
	public void LocationOf_ReportsADetachedNode()
	{
		AstGraph graph = new(SampleFunction());
		VariableReference orphan = new("orphan");
		graph.AddDetached(orphan, Vector2.Zero);

		AstLocation location = graph.LocationOf(orphan);

		Assert.AreEqual(AstLocation.Detached, location);
		Assert.IsNull(location.Parent);
	}

	/// <summary>
	/// Tests that the document's root is not simply cast loose, since a graph with no root has
	/// nothing to generate from.
	/// </summary>
	/// <remarks>
	/// It can still be adopted into a loose subtree, which re-roots the document rather than leaving
	/// it without one; that is covered by <see cref="AstGraphRerootTests"/>.
	/// </remarks>
	[TestMethod]
	public void MoveTo_RefusesTheRoot()
	{
		AstGraph graph = new(SampleFunction());

		Assert.IsFalse(graph.MoveTo(graph.Root, AstLocation.Detached));
		Assert.AreEqual(0, graph.Detached.Count);
	}

	/// <summary>
	/// Tests that a node the target slot will not take is left in the graph unattached rather than
	/// dropped, which is what keeps an undo step from destroying a node when the document has moved
	/// on underneath it.
	/// </summary>
	[TestMethod]
	public void MoveTo_LeavesTheNodeInTheGraphWhenTheSlotRefusesIt()
	{
		FunctionDeclaration function = SampleFunction();
		ReturnStatement returnStmt = (ReturnStatement)function.Body[0];
		BinaryExpression binary = (BinaryExpression)returnStmt.Expression!;
		AstGraph graph = new(function);
		AstSlot leftSlot = AstSchema.SlotsOf(binary).Single(s => s.Name == "Left");

		// A statement is not an expression, so the operand slot refuses it.
		Assert.IsFalse(graph.MoveTo(returnStmt, new AstLocation(binary, leftSlot, 0)));

		CollectionAssert.Contains(graph.Detached.ToArray(), returnStmt);
		Assert.IsNotNull(graph.Nodes.Values.SingleOrDefault(n => ReferenceEquals(n, returnStmt)));
	}

	/// <summary>
	/// Connects a node to a named slot of a parent, looking the pins up the way the editor does.
	/// </summary>
	/// <param name="graph">The graph under test.</param>
	/// <param name="child">The node to attach.</param>
	/// <param name="parent">The node whose slot to fill.</param>
	/// <param name="slotName">The slot's name.</param>
	/// <param name="index">The position within the slot.</param>
	/// <returns>The connection's outcome.</returns>
	private static AstConnectResult Connect(AstGraph graph, AstNode child, AstNode parent, string slotName, int index)
	{
		Node childNode = graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), child));
		Node parentNode = graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), parent));
		AstSlot slot = AstSchema.SlotsOf(parent).Single(s => s.Name == slotName);
		Pin pin = parentNode.InputPins.Single(p => p.EffectiveDisplayName == AstGraph.PinLabel(slot, index));

		return graph.Connect(childNode.OutputPins[0].Id, pin.Id);
	}

	/// <summary>
	/// Finds the link that attaches a node to its parent.
	/// </summary>
	/// <param name="graph">The graph under test.</param>
	/// <param name="child">The attached node.</param>
	/// <returns>The link's id.</returns>
	private static int LinkInto(AstGraph graph, AstNode child)
	{
		Node childNode = graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), child));
		int outputPin = childNode.OutputPins[0].Id;
		return graph.Engine.Links.Single(l => l.OutputPinId == outputPin).Id;
	}
}
