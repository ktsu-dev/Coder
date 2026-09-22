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
/// Covers the menu a dropped link opens as the user meets it: entries reached with the mouse, by the
/// label they read as, through the headless rasterizer.
/// </summary>
/// <remarks>
/// What the entries are and what picking one attaches is covered headlessly in
/// <see cref="AstGraphEditorLinkDropTests"/>. What is covered here is that the popup draws those
/// entries at all and that clicking one reaches the edit — the half that only a rendered frame can
/// establish, since ImGui menus fault inside native code rather than throwing when misnested.
/// <para>
/// The menu is opened through <see cref="AstGraphEditor.RequestCreateFrom"/> rather than by dragging
/// a link onto a pin, because where a pin lands on screen depends on the force-directed layout. The
/// drag itself is covered by <c>Editor_HandlesALinkDroppedOnEmptyCanvas</c>.
/// </para>
/// <para>
/// ImGui contexts are process-global, so only one harness can be live at a time and this class must
/// not run its methods in parallel.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class AstGraphEditorLinkDropMenuTests
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
		Title = "AST link drop",
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
	/// Starts a harness with the create-node menu already open for a pin.
	/// </summary>
	/// <param name="editor">The editor to draw.</param>
	/// <param name="pinId">The pin to offer nodes for.</param>
	/// <returns>The running harness, for the caller to dispose.</returns>
	private static ImGuiAppHarness Offering(AstGraphEditor editor, int pinId)
	{
		ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsTrue(editor.RequestCreateFrom(pinId), "the pin to offer nodes for should be in the graph");
		harness.Step(2);
		return harness;
	}

	/// <summary>
	/// Tests that the menu lists a category per group of entries that would connect, and leaves out
	/// the ones that filtered empty.
	/// </summary>
	[TestMethod]
	public void Menu_ListsOnlyTheCategoriesThatHaveSomethingToOffer()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		// The Parameters slot takes only a parameter, which lives under Declarations.
		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 1));

		Assert.IsTrue(IsVisible(harness, "Create Declarations"), "the menu should offer Declarations");
		Assert.IsFalse(IsVisible(harness, "Create Literals"), "no literal can fill a Parameters slot");
		Assert.IsFalse(IsVisible(harness, "Nothing connects"), "a parameter is creatable, so the menu is not empty");
	}

	/// <summary>
	/// Tests that clicking an entry creates the node and connects it, through the menu rather than by
	/// calling the edit directly.
	/// </summary>
	[TestMethod]
	public void Menu_CreatesAndConnectsThePickedEntry()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 1));

		harness.Click("Create Declarations");
		harness.Step(2);

		harness.Click("Create Parameter");
		harness.Step(2);

		Assert.HasCount(2, function.Parameters);
		Assert.IsTrue(editor.History.CanUndo, "creating through the menu should be undoable");
	}

	/// <summary>
	/// Tests that a slot the palette cannot fill says so, rather than opening an empty menu.
	/// </summary>
	[TestMethod]
	public void Menu_SaysSoWhenNothingConnects()
	{
		EnumDeclaration enumeration = new("Colour");
		AstGraphEditor editor = new(enumeration);

		AstSlot members = AstSchema.SlotsOf(enumeration).Single();
		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, enumeration, members.Name, 0));

		Assert.IsTrue(IsVisible(harness, "Nothing connects"), "an EnumMember slot has no palette entry to offer");
		Assert.IsFalse(IsVisible(harness, "Create Declarations"), "nothing connects, so no category should be listed");
	}

	/// <summary>
	/// Tests that the menu offers a submenu for a category whose entries are grouped, which is where
	/// the eighteen binary operators live.
	/// </summary>
	[TestMethod]
	public void Menu_NestsAGroupedCategorysEntries()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		// A Body slot takes any statement, so the grouped assignment operators are on offer.
		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Body", 1));

		harness.Click("Create Statements");
		harness.Step(2);

		Assert.IsTrue(IsVisible(harness, "Create Statements Assignment"), "the grouped operators should be a submenu");
	}

	/// <summary>
	/// Tests that dismissing the menu without picking anything leaves the document alone and forgets
	/// the pin, rather than offering the menu again on the next frame.
	/// </summary>
	/// <remarks>
	/// ImNodes discards a dropped link itself, so there is nothing half-made to undo — cancelling has
	/// to be a true no-op, and this is what says so.
	/// </remarks>
	[TestMethod]
	public void Menu_DismissedWithoutPicking_ChangesNothing()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Parameters", 1));
		Assert.IsTrue(IsVisible(harness, "Create Declarations"), "the menu should be open to begin with");

		int before = editor.Graph.Nodes.Count;

		// Clicked away from the popup rather than dismissed with Escape: a click outside is what
		// closes an ImGui popup, and it is also what a user who changed their mind actually does.
		harness.Mouse.Click(1220, 60);
		harness.Step(3);

		Assert.IsFalse(IsVisible(harness, "Create Declarations"), "the menu should be gone once dismissed");
		Assert.HasCount(before, editor.Graph.Nodes);
		Assert.IsFalse(editor.History.CanUndo, "cancelling should record nothing");
		Assert.HasCount(1, function.Parameters);
	}

	/// <summary>
	/// Tests that opening a grouped submenu lists the entries inside it, which is where the operator
	/// entries live.
	/// </summary>
	[TestMethod]
	public void Menu_OpensAGroupedSubmenuToItsEntries()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Offering(editor, InputPin(editor.Graph, function, "Body", 1));

		harness.Click("Create Statements");
		harness.Step(2);

		harness.Click("Create Statements Assignment");
		harness.Step(2);

		// Read from the catalogue rather than spelled here, so the assertion follows the operator set
		// rather than pinning one operator's label.
		string first = AstNodeCatalog.InGroup("Statements", "Assignment").First().Label;
		Assert.IsTrue(IsVisible(harness, $"Create {first}"), $"the submenu should list '{first}'");
	}

	/// <summary>
	/// Tests that a pin that is not in the graph is refused, so no menu is offered for it.
	/// </summary>
	[TestMethod]
	public void RequestCreateFrom_UnknownPin_IsRefused()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsFalse(editor.RequestCreateFrom(-1));
		harness.Step(2);

		Assert.IsFalse(IsVisible(harness, "Create Declarations"));
		Assert.IsFalse(IsVisible(harness, "Nothing connects"));
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
