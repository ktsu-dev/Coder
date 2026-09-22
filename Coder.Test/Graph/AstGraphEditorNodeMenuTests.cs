// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.App;
using ktsu.ImGui.App.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Covers the menu a right-clicked node opens as the user meets it: entries reached with the mouse,
/// by the label they read as, through the headless rasterizer.
/// </summary>
/// <remarks>
/// Which slots the menu offers is a fact about the node's shape and is covered headlessly in
/// <see cref="AstSchemaTests"/>. What is covered here is that the popup draws an entry per slot at
/// all, that picking one reaches the edit, and that a right-click over a node opens this menu rather
/// than the create-node palette — the half that only a rendered frame can establish, since ImGui
/// menus fault inside native code rather than throwing when misnested.
/// <para>
/// The menu is opened through <see cref="AstGraphEditor.RequestNodeMenu"/> in every test but the
/// gesture's own, because where a node lands on screen depends on the force-directed layout. The one
/// test that does click the node stops the layout first and asks ImNodes where the node ended up.
/// </para>
/// <para>
/// ImGui contexts are process-global, so only one harness can be live at a time and this class must
/// not run its methods in parallel.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class AstGraphEditorNodeMenuTests
{
	private static readonly HarnessOptions Options = new() { Width = 1280, Height = 800 };

	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, Literal.Number(1))));
		return function;
	}

	private static ImGuiAppConfig ConfigFor(AstGraphEditor editor) => new()
	{
		Title = "AST node menu",
		OnRender = delta =>
		{
			ImGui.Begin("graph");
			editor.Draw(new Vector2(1000, 600), delta);
			ImGui.End();
		},
	};

	private static bool IsVisible(ImGuiAppHarness harness, string name) =>
		harness.Probe.WasSeenInFrame(name, harness.FrameCount - 1);

	/// <summary>
	/// Finds the editor node standing for an AST node, the way the editor does.
	/// </summary>
	/// <param name="graph">The graph under test.</param>
	/// <param name="node">The AST node to find.</param>
	/// <returns>The editor node's id.</returns>
	private static int NodeId(AstGraph graph, AstNode node) =>
		graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), node)).Id;

	/// <summary>
	/// Starts a harness with a node's own menu already open.
	/// </summary>
	/// <param name="editor">The editor to draw.</param>
	/// <param name="node">The node to open the menu for.</param>
	/// <returns>The running harness, for the caller to dispose.</returns>
	private static ImGuiAppHarness Offering(AstGraphEditor editor, AstNode node)
	{
		ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsTrue(editor.RequestNodeMenu(NodeId(editor.Graph, node)), "the node to offer a menu for should be in the graph");
		harness.Step(2);
		return harness;
	}

	/// <summary>
	/// Tests that the menu offers one entry per slot the node can hold another child in.
	/// </summary>
	[TestMethod]
	public void Menu_OffersAnEntryPerSlotThatCanGrow()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, function);

		Assert.IsTrue(IsVisible(harness, "Node add Parameters"), "a function's parameter list can take another");
		Assert.IsTrue(IsVisible(harness, "Node add Body"), "a function's body can take another statement");
		Assert.IsFalse(IsVisible(harness, "Nothing to add"), "a function has slots to add to, so the menu is not empty");
	}

	/// <summary>
	/// Tests that picking an entry adds a child to that slot, through the menu rather than by calling
	/// the edit directly, and that the step is undoable.
	/// </summary>
	[TestMethod]
	public void Menu_AddsToThePickedSlot()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, function);

		harness.Click("Node add Parameters");
		harness.Step(2);

		Assert.HasCount(2, function.Parameters);
		Assert.HasCount(1, function.Body);
		Assert.IsTrue(editor.History.CanUndo, "adding through the menu should be undoable");

		editor.Undo();
		Assert.HasCount(1, function.Parameters);
	}

	/// <summary>
	/// Tests that a node whose every slot holds one child says so, rather than opening an empty menu.
	/// </summary>
	[TestMethod]
	public void Menu_SaysSoWhenTheNodeHasNoSlotToAddTo()
	{
		BinaryExpression sum = new(new VariableReference("a"), BinaryOperator.Add, Literal.Number(1));
		AstGraphEditor editor = new(sum);

		using ImGuiAppHarness harness = Offering(editor, sum);

		Assert.IsTrue(IsVisible(harness, "Nothing to add"), "both of a binary expression's slots hold one child");
		Assert.IsFalse(IsVisible(harness, "Node add Left"), "a slot holding one child is not offered");
	}

	/// <summary>
	/// Tests that dismissing the menu without picking anything leaves the document alone and forgets the
	/// node, rather than offering the menu again on the next frame.
	/// </summary>
	[TestMethod]
	public void Menu_DismissedWithoutPicking_ChangesNothing()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, function);
		Assert.IsTrue(IsVisible(harness, "Node add Parameters"), "the menu should be open to begin with");

		int before = editor.Graph.Nodes.Count;

		// Clicked away from the popup rather than dismissed with Escape: a click outside is what closes
		// an ImGui popup, and it is also what a user who changed their mind actually does.
		harness.Mouse.Click(1220, 60);
		harness.Step(3);

		Assert.IsFalse(IsVisible(harness, "Node add Parameters"), "the menu should be gone once dismissed");
		Assert.HasCount(before, editor.Graph.Nodes);
		Assert.IsFalse(editor.History.CanUndo, "dismissing should record nothing");
		Assert.HasCount(1, function.Parameters);
	}

	/// <summary>
	/// Tests that right-clicking a node opens that node's menu, where right-clicking empty canvas still
	/// opens the create-node palette.
	/// </summary>
	/// <remarks>
	/// The layout is stopped and the view left where the first frames fitted it, so the node is where
	/// ImNodes last put it rather than somewhere the simulation has since moved it.
	/// </remarks>
	[TestMethod]
	public void RightClickOnANode_OpensThatNodesMenu()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function) { LayoutRunning = false };

		Vector2 topLeft = Vector2.Zero;
		Vector2 size = Vector2.Zero;
		int nodeId = 0;

		ImGuiAppConfig config = new()
		{
			Title = "AST node menu gesture",
			OnRender = delta =>
			{
				ImGui.Begin("graph");
				editor.Draw(new Vector2(1000, 600), delta);

				// Read inside the frame that drew it: the node's screen position is ImNodes' own, and it
				// is only meaningful while its context is the current one.
				if (nodeId != 0)
				{
					topLeft = ImNodes.GetNodeScreenSpacePos(nodeId);
					size = ImNodes.GetNodeDimensions(nodeId);
				}

				ImGui.End();
			},
		};

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(config, Options);
		harness.Step(4);

		nodeId = NodeId(editor.Graph, function);
		harness.Step(2);

		Assert.IsGreaterThan(0f, size.X, "the node should have been measured by now");

		// The title bar rather than the middle of the body: a node's rows are the pins, and a click
		// landing on one is a different gesture.
		harness.Mouse.Click(topLeft.X + (size.X * 0.5f), topLeft.Y + 4f, 1);
		harness.Step(2);

		Assert.IsTrue(IsVisible(harness, "Node add Parameters"), "right-clicking the node should open its own menu");
	}

	/// <summary>
	/// Tests that a node that is not in the graph is refused, so no menu is offered for it.
	/// </summary>
	[TestMethod]
	public void RequestNodeMenu_UnknownNode_IsRefused()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsFalse(editor.RequestNodeMenu(-1));
		harness.Step(2);

		Assert.IsFalse(IsVisible(harness, "Node add Parameters"));
		Assert.IsFalse(IsVisible(harness, "Nothing to add"));
	}
}
