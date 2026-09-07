// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
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
