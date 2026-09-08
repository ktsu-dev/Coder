// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.NodeEditor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the force-directed layout actually runs.
/// </summary>
/// <remarks>
/// The engine's physics settings default to disabled, so a graph that never enables them steps the
/// simulation every frame and moves nothing — the layout appears frozen while the editor's "Auto
/// layout" toggle says it is running. These tests pin that it is switched on, and that stepping it
/// moves nodes, so the defect cannot come back silently.
/// </remarks>
[TestClass]
public class AstGraphLayoutTests
{
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, Literal.Number(1))));
		return function;
	}

	/// <summary>
	/// Tests that a graph enables the simulation, since the engine's default leaves it off.
	/// </summary>
	[TestMethod]
	public void Graph_EnablesThePhysicsSimulation()
	{
		AstGraph graph = new(SampleFunction());

		Assert.IsTrue(graph.Engine.PhysicsSettings.Enabled);
	}

	/// <summary>
	/// Tests that stepping the simulation moves nodes, which is what "auto layout" means to a user.
	/// </summary>
	[TestMethod]
	public void Graph_StepsMoveNodes()
	{
		AstGraph graph = new(SampleFunction());
		Vector2[] before = [.. graph.Engine.Nodes.Select(node => node.Position)];

		Step(graph, frames: 10);

		Vector2[] after = [.. graph.Engine.Nodes.Select(node => node.Position)];

		Assert.AreEqual(before.Length, after.Length);
		Assert.IsTrue(
			before.Zip(after).Any(pair => Vector2.Distance(pair.First, pair.Second) > 0.1f),
			"ten frames of simulation should have moved at least one node");
	}

	/// <summary>
	/// Tests that the layout calms down: it arranges the graph in the first moments and then all but
	/// stops, rather than throwing the nodes about for as long as the editor is open.
	/// </summary>
	/// <remarks>
	/// Measured as displacement rather than through the engine's own stability flag. That flag
	/// compares total kinetic energy against a fixed threshold, and a graph this size settles into a
	/// residual drift that sits just above it while amounting to a fraction of a pixel per frame —
	/// invisible, and not what "still moving" means to anyone watching.
	/// </remarks>
	[TestMethod]
	public void Graph_CalmsDownOnceLaidOut()
	{
		AstGraph graph = new(SampleFunction());

		Vector2[] start = [.. graph.Engine.Nodes.Select(node => node.Position)];
		Step(graph, frames: 60);
		float early = Furthest(start, graph);

		// Long enough to arrange the graph, which is the part that moves nodes a long way.
		Step(graph, frames: 1800);
		Vector2[] settled = [.. graph.Engine.Nodes.Select(node => node.Position)];
		Step(graph, frames: 60);
		float late = Furthest(settled, graph);

		Assert.IsTrue(early > 20f, $"the first second of layout only moved a node {early}");
		Assert.IsTrue(late < 10f, $"a settled layout still moved a node {late} in a second");
		Assert.IsTrue(late < early, "the layout should be calmer once it has arranged the graph");
	}

	/// <summary>
	/// Tests that the layout pushes nodes apart, which is what makes the graph readable and is the
	/// reason it runs at all.
	/// </summary>
	[TestMethod]
	public void Graph_SeparatesNodes()
	{
		AstGraph graph = new(new FunctionDeclaration("total") { ReturnType = "int" });

		// Two nodes dropped on the same spot: only the layout can be what moves them apart.
		graph.AddDetached(new VariableReference("a"), Vector2.Zero);
		graph.AddDetached(new VariableReference("b"), Vector2.Zero);

		Step(graph, frames: 300);

		Vector2[] positions = [.. graph.Engine.Nodes.Select(node => node.Position)];
		for (int i = 0; i < positions.Length; i++)
		{
			for (int j = i + 1; j < positions.Length; j++)
			{
				Assert.IsTrue(
					Vector2.Distance(positions[i], positions[j]) > 10f,
					$"nodes {i} and {j} are on top of each other");
			}
		}
	}

	/// <summary>
	/// Measures how far the furthest node has moved from a remembered arrangement.
	/// </summary>
	/// <param name="from">The positions to compare against.</param>
	/// <param name="graph">The graph holding the current positions.</param>
	/// <returns>The greatest distance any node has moved.</returns>
	private static float Furthest(Vector2[] from, AstGraph graph) =>
		from.Zip(graph.Engine.Nodes.Select(node => node.Position))
			.Max(pair => Vector2.Distance(pair.First, pair.Second));

	/// <summary>
	/// Tests that fitting brings a graph that has wandered off back to the middle of the view, and
	/// that it moves the arrangement rather than disturbing it.
	/// </summary>
	/// <remarks>
	/// Fitting has to move the nodes rather than pan the node editor: the renderer writes each node's
	/// position into the editor every frame, so panning is undone as soon as it is read back.
	/// </remarks>
	[TestMethod]
	public void FitView_CentresTheGraphOnTheOrigin()
	{
		AstGraphEditor editor = new(SampleFunction()) { LayoutRunning = false };
		editor.Graph.Engine.WorldOrigin = new Vector2(500, 300);
		Node[] before = [.. editor.Graph.Engine.Nodes];

		// Push the whole graph a long way off the top-left of the canvas.
		foreach (Node node in before)
		{
			editor.Graph.Engine.UpdateNodePosition(node.Id, node.Position - new Vector2(4000, 3000));
		}

		Assert.IsTrue(editor.FitView());

		Node[] after = [.. editor.Graph.Engine.Nodes];
		Vector2 centre = (after.Aggregate(new Vector2(float.MaxValue), (lowest, node) => Vector2.Min(lowest, node.Position))
			+ after.Aggregate(new Vector2(float.MinValue), (highest, node) => Vector2.Max(highest, node.Position + node.Dimensions))) * 0.5f;

		Assert.AreEqual(500f, centre.X, 0.01f, "the arrangement should be centred on the origin");
		Assert.AreEqual(300f, centre.Y, 0.01f, "the arrangement should be centred on the origin");

		// The arrangement is translated, not rearranged: every node keeps its offset from the first.
		for (int i = 1; i < before.Length; i++)
		{
			Assert.AreEqual(
				before[i].Position - before[0].Position,
				after[i].Position - after[0].Position,
				$"node {i} should have kept its place in the arrangement");
		}
	}

	/// <summary>
	/// Tests that fitting a graph too big for the canvas zooms out until it fits, rather than only
	/// centring the part of it that happens to be on screen.
	/// </summary>
	/// <remarks>
	/// The zooming is the node editor's, and covered by its own tests. What is this application's — and
	/// what this covers — is the canvas it hands over: the world origin is kept on the middle of the
	/// canvas, so the editor passes twice the origin. Getting that wrong would fit the graph to the
	/// wrong rectangle, which no test in the library could catch.
	/// </remarks>
	[TestMethod]
	public void FitView_ZoomsOutUntilTheGraphFits()
	{
		AstGraphEditor editor = new(SampleFunction()) { LayoutRunning = false };

		// A 600x400 canvas, with the origin on the middle of it.
		editor.Graph.Engine.WorldOrigin = new Vector2(300, 200);

		// Spread the arrangement well past the canvas's width.
		Node[] nodes = [.. editor.Graph.Engine.Nodes];
		for (int i = 0; i < nodes.Length; i++)
		{
			editor.Graph.Engine.UpdateNodePosition(nodes[i].Id, new Vector2(i * 400f, 0f));
			editor.Graph.Engine.UpdateNodeDimensions(nodes[i].Id, new Vector2(120f, 60f));
		}

		Assert.IsTrue(editor.FitView());

		float extent = ((nodes.Length - 1) * 400f) + 120f;
		Assert.IsTrue(editor.Zoom < 1f, $"a graph {extent} wide should not fit a 600 canvas at {editor.Zoom}");
		Assert.IsTrue(extent * editor.Zoom <= 600f, $"the graph still overflows the canvas at {editor.Zoom}");
	}

	/// <summary>
	/// Tests that the editor's zoom is the node editor's, rather than a second value beside it.
	/// </summary>
	/// <remarks>
	/// Asserted through the clamping, which is the renderer's: a value it would refuse coming back
	/// changed is what says the property forwarded rather than storing what it was given.
	/// </remarks>
	[TestMethod]
	public void Zoom_IsTheRenderersOwn()
	{
		AstGraphEditor editor = new(SampleFunction()) { Zoom = 50f };
		Assert.AreEqual(NodeEditorRenderer.MaxZoom, editor.Zoom);

		editor.Zoom = 0f;
		Assert.AreEqual(NodeEditorRenderer.MinZoom, editor.Zoom);
	}

	/// <summary>
	/// Tests that a rebuild leaves the world origin where the caller put it, since every structural
	/// edit rebuilds and clearing the engine resets it.
	/// </summary>
	[TestMethod]
	public void Rebuild_KeepsTheWorldOrigin()
	{
		AstGraph graph = new(SampleFunction());
		graph.Engine.WorldOrigin = new Vector2(500, 300);

		graph.AddDetached(new VariableReference("added"), new Vector2(120, 240));

		Assert.AreEqual(new Vector2(500, 300), graph.Engine.WorldOrigin);
	}

	/// <summary>
	/// Tests that a node arriving without a position of its own is seeded near the origin rather
	/// than in the corner of the canvas, which is where an origin of zero would put it.
	/// </summary>
	[TestMethod]
	public void Seeding_PlacesNewNodesAroundTheOrigin()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		AstGraph graph = new(function);
		graph.Engine.WorldOrigin = new Vector2(500, 300);

		// Attached rather than dropped from the palette: a palette node is placed where the pointer
		// is, while this one has no position of its own and has to be seeded.
		function.Body.Add(new ReturnStatement(Literal.Number(1)));
		graph.Rebuild();

		Vector2 seeded = graph.Engine.Nodes
			.Single(node => ReferenceEquals(graph.AstNodeFor(node.Id), function.Body[0]))
			.Position;

		Assert.IsTrue(
			Vector2.Distance(seeded, graph.Engine.WorldOrigin) < 400f,
			$"a seeded node landed at {seeded}, nowhere near the origin");
	}

	/// <summary>
	/// Tests that a whole document is seeded centred on the origin rather than hanging off one side
	/// of it.
	/// </summary>
	[TestMethod]
	public void Seeding_CentresADocumentOnTheOrigin()
	{
		AstGraph graph = new(SampleFunction());
		graph.Engine.WorldOrigin = new Vector2(500, 300);

		// Forget every position, so the whole document is seeded afresh against the new origin.
		graph.Load(SampleFunction());

		Vector2[] positions = [.. graph.Engine.Nodes.Select(node => node.Position)];
		Vector2 centre = (positions.Aggregate(new Vector2(float.MaxValue), Vector2.Min)
			+ positions.Aggregate(new Vector2(float.MinValue), Vector2.Max)) * 0.5f;

		Assert.AreEqual(500f, centre.X, 40f, "the document should be seeded around the origin");
		Assert.AreEqual(300f, centre.Y, 40f, "the document should be seeded around the origin");
	}

	/// <summary>
	/// Runs the simulation for a number of frames at sixty a second.
	/// </summary>
	/// <param name="graph">The graph to step.</param>
	/// <param name="frames">How many frames to run.</param>
	private static void Step(AstGraph graph, int frames)
	{
		for (int frame = 0; frame < frames; frame++)
		{
			graph.Engine.UpdatePhysics(1f / 60f);
		}
	}

	/// <summary>
	/// Tests that a rebuild leaves the simulation running, since every structural edit rebuilds and a
	/// layout that stopped after the first edit would be no better than one that never started.
	/// </summary>
	[TestMethod]
	public void Rebuild_LeavesTheSimulationEnabled()
	{
		AstGraph graph = new(SampleFunction());

		graph.AddDetached(new VariableReference("b"), new Vector2(400, 400));

		Assert.IsTrue(graph.Engine.PhysicsSettings.Enabled);

		Vector2 before = graph.Engine.Nodes[0].Position;
		Step(graph, frames: 10);

		Assert.AreNotEqual(before, graph.Engine.Nodes[0].Position);
	}
}
