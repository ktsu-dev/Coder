// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.App;
using ktsu.ImGui.App.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Covers the menu a right-clicked pin opens as the user meets it: entries reached with the mouse, by
/// the label they read as, through the headless rasterizer.
/// </summary>
/// <remarks>
/// What fills a pin is covered headlessly in <see cref="AstGraphTests"/>. What is covered here is
/// that the popup draws the entries the pin's state calls for, and that picking one reaches the edit —
/// the half that only a rendered frame can establish, since ImGui menus fault inside native code
/// rather than throwing when misnested.
/// <para>
/// The menu is opened through <see cref="AstGraphEditor.RequestPinMenu"/> rather than by right-clicking
/// a pin, because where a pin lands on screen depends on the force-directed layout and ImNodes
/// publishes no position for one. That is the same reason
/// <see cref="AstGraphEditorLinkDropMenuTests"/> opens its menu through the editor.
/// </para>
/// <para>
/// ImGui contexts are process-global, so only one harness can be live at a time and this class must
/// not run its methods in parallel.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class AstGraphEditorPinMenuTests
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
		Title = "AST pin menu",
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
	/// Starts a harness with a pin's own menu already open.
	/// </summary>
	/// <param name="editor">The editor to draw.</param>
	/// <param name="pinId">The pin to open the menu for.</param>
	/// <returns>The running harness, for the caller to dispose.</returns>
	private static ImGuiAppHarness Offering(AstGraphEditor editor, int pinId)
	{
		ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsTrue(editor.RequestPinMenu(pinId), "the pin to offer a menu for should be one of this graph's slots");
		harness.Step(2);
		return harness;
	}

	/// <summary>
	/// Tests that a pin holding a child offers what can be done to that child as well as what can fill
	/// the pin.
	/// </summary>
	[TestMethod]
	public void Menu_OffersDisconnectAndDeleteForAFilledPin()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 0));

		Assert.IsTrue(IsVisible(harness, "Pin create"), "a pin can always be offered something to fill it");
		Assert.IsTrue(IsVisible(harness, "Pin disconnect"), "the pin holds a parameter, which can be cut loose");
		Assert.IsTrue(IsVisible(harness, "Pin delete"), "the pin holds a parameter, which can be removed");
	}

	/// <summary>
	/// Tests that the free pin a variadic slot draws past its last child offers only what would fill
	/// it, since there is nothing in it to act on.
	/// </summary>
	[TestMethod]
	public void Menu_OffersOnlyCreateForAFreePin()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 1));

		Assert.IsTrue(IsVisible(harness, "Pin create"));
		Assert.IsFalse(IsVisible(harness, "Pin disconnect"), "nothing fills the pin, so nothing can be cut loose");
		Assert.IsFalse(IsVisible(harness, "Pin delete"), "nothing fills the pin, so nothing can be removed");
	}

	/// <summary>
	/// Tests that disconnecting cuts the child loose from the slot and leaves it in the graph, rather
	/// than removing it.
	/// </summary>
	[TestMethod]
	public void Menu_DisconnectCutsTheChildLoose()
	{
		FunctionDeclaration function = SampleFunction();
		Parameter parameter = function.Parameters[0];
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 0));

		harness.Click("Pin disconnect");
		harness.Step(2);

		Assert.IsEmpty(function.Parameters);
		Assert.Contains(parameter, editor.Graph.Detached, "a disconnected node stays in the graph");
		Assert.IsTrue(editor.History.CanUndo, "disconnecting through the menu should be undoable");

		editor.Undo();
		Assert.AreSame(parameter, function.Parameters.Single());
	}

	/// <summary>
	/// Tests that deleting takes the child out of the document altogether, which is what tells it apart
	/// from disconnecting.
	/// </summary>
	[TestMethod]
	public void Menu_DeleteRemovesTheChild()
	{
		FunctionDeclaration function = SampleFunction();
		Parameter parameter = function.Parameters[0];
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 0));

		harness.Click("Pin delete");
		harness.Step(2);

		Assert.IsEmpty(function.Parameters);
		Assert.DoesNotContain(parameter, editor.Graph.Nodes.Values, "a deleted node leaves the graph");
		Assert.IsTrue(editor.History.CanUndo, "deleting through the menu should be undoable");

		editor.Undo();
		Assert.AreSame(parameter, function.Parameters.Single());
	}

	/// <summary>
	/// Tests that asking to fill a pin opens the menu of nodes that would fill it, which is the one a
	/// dropped link already opens.
	/// </summary>
	[TestMethod]
	public void Menu_CreateOffersTheNodesThatWouldFillThePin()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 1));

		harness.Click("Pin create");
		harness.Step(3);

		Assert.IsTrue(IsVisible(harness, "Create Declarations"), "a Parameters slot takes a parameter, which is a declaration");
		Assert.IsFalse(IsVisible(harness, "Pin create"), "the pin's own menu should have closed behind it");
	}

	/// <summary>
	/// Tests that dismissing the menu without picking anything leaves the document alone and forgets the
	/// pin, rather than offering the menu again on the next frame.
	/// </summary>
	[TestMethod]
	public void Menu_DismissedWithoutPicking_ChangesNothing()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 0));
		Assert.IsTrue(IsVisible(harness, "Pin delete"), "the menu should be open to begin with");

		int before = editor.Graph.Nodes.Count;

		// Clicked away from the popup rather than dismissed with Escape: a click outside is what closes
		// an ImGui popup, and it is also what a user who changed their mind actually does.
		harness.Mouse.Click(1220, 60);
		harness.Step(3);

		Assert.IsFalse(IsVisible(harness, "Pin delete"), "the menu should be gone once dismissed");
		Assert.HasCount(before, editor.Graph.Nodes);
		Assert.IsFalse(editor.History.CanUndo, "dismissing should record nothing");
		Assert.HasCount(1, function.Parameters);
	}

	/// <summary>
	/// Tests that a node's output pin is refused, since it stands for the node rather than for a place
	/// in the document, and that a pin that is not in the graph is refused too.
	/// </summary>
	[TestMethod]
	public void RequestPinMenu_AnythingButASlotPin_IsRefused()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		int outputPin = editor.Graph.Engine.Nodes
			.Single(n => ReferenceEquals(editor.Graph.AstNodeFor(n.Id), function))
			.OutputPins[0].Id;

		Assert.IsFalse(editor.RequestPinMenu(outputPin));
		Assert.IsFalse(editor.RequestPinMenu(-1));
		harness.Step(2);

		Assert.IsFalse(IsVisible(harness, "Pin create"));
	}

	/// <summary>
	/// Tests that a right-click that lands on a node but not on one of its pins is not taken for a pin,
	/// so the create-node palette still answers it.
	/// </summary>
	/// <remarks>
	/// The click lands on the node's title bar, which is the one part of a node with no pin in it. The
	/// opposite case — a click that does land on a pin — is not covered here: ImNodes publishes no
	/// screen position for a pin, so a test could only guess at one, and the menu it opens is covered
	/// through <see cref="AstGraphEditor.RequestPinMenu"/> above.
	/// </remarks>
	[TestMethod]
	public void RightClickOffAPin_DoesNotOpenThePinMenu()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function) { LayoutRunning = false };

		Vector2 topLeft = Vector2.Zero;
		Vector2 size = Vector2.Zero;
		int nodeId = 0;

		ImGuiAppConfig config = new()
		{
			Title = "AST pin menu gesture",
			OnRender = delta =>
			{
				ImGui.Begin("graph");
				editor.Draw(new Vector2(1000, 600), delta);

				// Read inside the frame that drew it: the node's screen position is ImNodes' own, and it
				// is only meaningful while its context is the current one.
				if (nodeId != 0)
				{
					topLeft = Hexa.NET.ImNodes.ImNodes.GetNodeScreenSpacePos(nodeId);
					size = Hexa.NET.ImNodes.ImNodes.GetNodeDimensions(nodeId);
				}

				ImGui.End();
			},
		};

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(config, Options);
		harness.Step(4);

		nodeId = editor.Graph.Engine.Nodes.Single(n => ReferenceEquals(editor.Graph.AstNodeFor(n.Id), function)).Id;
		harness.Step(2);

		Assert.IsGreaterThan(0f, size.X, "the node should have been measured by now");

		harness.Mouse.Click(topLeft.X + (size.X * 0.5f), topLeft.Y + 4f, 1);
		harness.Step(2);

		Assert.IsFalse(IsVisible(harness, "Pin create"), "no pin is under the title bar, so no pin menu is offered");
		Assert.IsFalse(editor.History.CanUndo, "a right-click records nothing either way");
	}

	/// <summary>
	/// Finds the pin standing for one place in a node's slot, the way the editor does.
	/// </summary>
	/// <param name="graph">The graph under test.</param>
	/// <param name="parent">The node whose slot to look at.</param>
	/// <param name="slotName">The slot's name.</param>
	/// <param name="index">The position within the slot.</param>
	/// <returns>The input pin's id.</returns>
	private static int InputPin(AstGraph graph, AstNode parent, string slotName, int index)
	{
		ktsu.ImGui.NodeEditor.Node parentNode =
			graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), parent));
		AstSlot slot = AstSchema.SlotsOf(parent).Single(s => s.Name == slotName);
		return parentNode.InputPins.Single(p => p.EffectiveDisplayName == AstGraph.PinLabel(slot, index)).Id;
	}
}
