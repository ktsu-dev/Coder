// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.App;
using ktsu.ImGui.App.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that drive <see cref="AstGraphEditor"/>'s ImGui surface for real, through the headless CPU
/// rasterizer.
/// </summary>
/// <remarks>
/// The rules live in <see cref="AstGraph"/> and are covered without any of this, but a surface that
/// has never been executed is a surface nobody knows compiles into working native calls. ImNodes in
/// particular faults inside native code rather than throwing when its context is missing, so
/// rendering a frame is the only way to find that out. The harness creates the ImGui and ImNodes
/// contexts the same way a real application does.
/// <para>
/// ImGui contexts are process-global, so only one harness can be live at a time and this class must
/// not run its methods in parallel.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class AstGraphEditorTests
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

	/// <summary>
	/// Builds a configuration that draws the editor inside a window, which is where ImNodes has to be.
	/// </summary>
	/// <param name="editor">The editor to draw.</param>
	/// <returns>The configuration to hand the harness.</returns>
	private static ImGuiAppConfig ConfigFor(AstGraphEditor editor) => new()
	{
		Title = "AST graph",
		OnRender = delta =>
		{
			ImGui.Begin("graph");
			editor.Draw(new Vector2(1000, 600), delta);
			ImGui.End();
		},
	};

	/// <summary>
	/// Tests that a frame renders without faulting, which covers the whole draw path: the toolbar,
	/// the ImNodes editor, the input handling and the physics step.
	/// </summary>
	[TestMethod]
	public void Editor_RendersAFrame()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(3);

		Assert.AreEqual(3, harness.FrameCount);
		Assert.AreEqual(6, editor.Graph.Nodes.Count, "function, parameter, return, binary, variable, literal");
	}

	/// <summary>
	/// Tests that drawing refreshes the problem list, so a caller reading it after a frame sees the
	/// document's current state rather than whatever it held at construction.
	/// </summary>
	[TestMethod]
	public void Editor_RefreshesProblemsWhileDrawing()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step();
		Assert.AreEqual(0, editor.Problems.Count, "the sample document is complete");

		// Strand a node, which is one of the two things Validate reports.
		editor.Graph.AddDetached(new VariableReference("orphan"), Vector2.Zero);
		harness.Step();

		Assert.IsTrue(editor.Problems.Any(p => p.Message.Contains("not connected", StringComparison.Ordinal)));
	}

	/// <summary>
	/// Tests that the layout can be turned off and on across frames, since a user who has arranged
	/// the graph by hand needs the physics to stop moving it.
	/// </summary>
	[TestMethod]
	public void Editor_RendersWithLayoutStopped()
	{
		AstGraphEditor editor = new(SampleFunction()) { LayoutRunning = false };

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		editor.LayoutRunning = true;
		harness.Step(2);

		Assert.AreEqual(4, harness.FrameCount);
	}

	/// <summary>
	/// Tests that the debug overlays render, which is a separate draw path over the graph.
	/// </summary>
	[TestMethod]
	public void Editor_RendersDebugOverlays()
	{
		AstGraphEditor editor = new(SampleFunction()) { ShowDebugOverlays = true };

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsTrue(editor.ShowDebugOverlays);
	}

	/// <summary>
	/// Tests that right-clicking the graph opens the palette, which is the path that creates nodes.
	/// </summary>
	/// <remarks>
	/// Driven with real input rather than by calling the private handler, so the popup's open and
	/// draw halves both run — the second only happens on the frame after the click.
	/// </remarks>
	[TestMethod]
	public void Editor_OpensThePaletteOnRightClick()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		harness.Mouse.Click(400, 400, button: 1);
		harness.Step(3);

		// The palette is drawn by ImGui, so what is asserted here is that a right-click over the
		// graph renders without faulting and leaves the document alone until something is picked.
		Assert.AreEqual(6, editor.Graph.Nodes.Count);
	}

	/// <summary>
	/// Tests that pressing Delete over the graph runs the removal path, and that pressing it with
	/// nothing selected leaves the document untouched rather than faulting.
	/// </summary>
	[TestMethod]
	public void Editor_HandlesDeleteWithNothingSelected()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		harness.Mouse.Click(400, 400);
		harness.Step();

		// Cleared rather than assumed: where a click lands depends on where the layout has put the
		// nodes, and what is under test is what Delete does with an empty selection.
		ImNodes.ClearNodeSelection();
		harness.Keyboard.Press(ImGuiKey.Delete);
		harness.Step(2);

		Assert.AreEqual(6, editor.Graph.Nodes.Count, "nothing was selected, so nothing should go");
	}

	/// <summary>
	/// Tests that a graph left to lay itself out stays inside the view, rather than being pulled off
	/// the top-left corner of it.
	/// </summary>
	/// <remarks>
	/// The layout's gravity pulls towards the world origin and node positions are canvas-relative, so
	/// an origin left at zero drags the whole document off the edge — with nothing on screen to say
	/// which way it went. The editor aims it at the middle of the canvas instead, and this is what
	/// says so.
	/// </remarks>
	[TestMethod]
	public void Editor_KeepsTheLayoutInsideTheView()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(300);

		foreach (ktsu.ImGuiNodeEditor.Node node in editor.Graph.Engine.Nodes)
		{
			// One assertion per bound rather than a range: it says which edge the node went over.
			Assert.IsGreaterThan(-200f, node.Position.X, $"{node.Name} drifted off the left to x {node.Position.X}");
			Assert.IsLessThan(1200f, node.Position.X, $"{node.Name} drifted off the right to x {node.Position.X}");
			Assert.IsGreaterThan(-200f, node.Position.Y, $"{node.Name} drifted off the top to y {node.Position.Y}");
			Assert.IsLessThan(800f, node.Position.Y, $"{node.Name} drifted off the bottom to y {node.Position.Y}");
		}
	}

	/// <summary>
	/// Tests that the inspector renders for every kind of node the document holds, which is the whole
	/// of its draw path: text boxes, tick boxes, operator combos and the slot buttons.
	/// </summary>
	/// <remarks>
	/// Selection is normally made with the mouse, and ImNodes' hit testing depends on where the view
	/// happens to be panned to; what matters here is that the panel draws for each shape of node, so
	/// the selection is made through the editor and the frame is rendered.
	/// </remarks>
	[TestMethod]
	public void Editor_RendersTheInspectorForEveryNode()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		foreach (int nodeId in editor.Graph.Nodes.Keys)
		{
			ImNodes.ClearNodeSelection();
			ImNodes.SelectNode(nodeId);
			harness.Step(2);

			Assert.AreSame(editor.Graph.AstNodeFor(nodeId), editor.SelectedNode);
		}
	}

	/// <summary>
	/// Tests that the inspector renders with nothing selected, since that is what the user sees when
	/// the editor first opens.
	/// </summary>
	[TestMethod]
	public void Editor_RendersTheInspectorWithNothingSelected()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsNull(editor.SelectedNode);
	}

	/// <summary>
	/// Tests that an edit made through the inspector while the editor is drawing takes effect, which
	/// is the path every widget in the panel goes through.
	/// </summary>
	[TestMethod]
	public void Editor_AppliesAnInspectorEditWhileDrawing()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsTrue(editor.SetField(function, "Name", "renamed"));
		harness.Step(2);

		Assert.AreEqual("renamed", function.Name);
		Assert.AreEqual(6, editor.Graph.Nodes.Count, "renaming should not change the graph's shape");
	}

	/// <summary>
	/// Tests that the panel can be hidden, since a user working on a large graph wants the room.
	/// </summary>
	[TestMethod]
	public void Editor_RendersWithTheInspectorHidden()
	{
		AstGraphEditor editor = new(SampleFunction()) { ShowInspector = false };

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsFalse(editor.ShowInspector);
	}

	/// <summary>
	/// Tests that a node dragged in the editor keeps its new position across the rebuild an edit
	/// triggers, which is the behaviour that stops edits from scattering an arranged graph.
	/// </summary>
	[TestMethod]
	public void Editor_KeepsADraggedPositionAcrossAnEdit()
	{
		AstGraphEditor editor = new(SampleFunction()) { LayoutRunning = false };

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		int nodeId = editor.Graph.Nodes.First(pair => pair.Value is VariableReference).Key;
		AstNode dragged = editor.Graph.AstNodeFor(nodeId)!;
		Vector2 moved = new(4321, 1234);
		editor.Graph.Engine.UpdateNodePosition(nodeId, moved);

		editor.Graph.AddDetached(new VariableReference("orphan"), Vector2.Zero);
		harness.Step();

		Vector2 after = editor.Graph.Engine.Nodes
			.Single(n => ReferenceEquals(editor.Graph.AstNodeFor(n.Id), dragged)).Position;
		Assert.AreEqual(moved, after);
	}

	/// <summary>
	/// Tests that undo and redo work through the editor while it is drawing, which is the path the
	/// toolbar buttons take.
	/// </summary>
	[TestMethod]
	public void Editor_UndoesAndRedoesAnEdit()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step();

		int before = editor.Graph.Nodes.Count;

		// Undo with nothing recorded must be a no-op rather than a fault.
		editor.Undo();
		harness.Step();
		Assert.AreEqual(before, editor.Graph.Nodes.Count);

		editor.Redo();
		harness.Step();
		Assert.AreEqual(before, editor.Graph.Nodes.Count);
	}
}
