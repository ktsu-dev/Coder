// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;

using Hexa.NET.ImGui;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.App;
using ktsu.ImGui.App.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Covers the panel that tunes the layout while the graph is on screen.
/// </summary>
/// <remarks>
/// The controls themselves belong to ktsu.ImGui.NodeEditor and are covered there. What is this
/// editor's own is the wiring: that the panel reaches the graph's engine, that its run toggle is the
/// same flag the toolbar's is, and that a change made on it survives into the simulation rather than
/// being discarded when the frame ends.
/// <para>
/// ImGui contexts are process-global, so only one harness can be live at a time and this class must
/// not run its methods in parallel.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class AstGraphEditorLayoutPanelTests
{
	private static readonly HarnessOptions Options = new() { Width = 900, Height = 1100 };

	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, Literal.Number(1))));
		return function;
	}

	/// <summary>Draws only the tuning panel, so nothing else can be what a probe finds.</summary>
	private static ImGuiAppConfig ConfigFor(AstGraphEditor editor) => new()
	{
		Title = "Layout tuning",
		OnRender = _ =>
		{
			ImGui.Begin("layout");
			editor.DrawLayoutSettings(new Vector2(880, 1050));
			ImGui.End();
		},
	};

	private static bool IsVisible(ImGuiAppHarness harness, string name) =>
		harness.Probe.WasSeenInFrame(name, harness.FrameCount - 1);

	[TestMethod]
	public void Panel_OffersEveryGroupOfSettings()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(3);

		foreach (string group in new[] { "Repulsion", "Link springs", "Link shaping", "Gravity", "Overlap", "Motion and limits" })
		{
			Assert.IsTrue(IsVisible(harness, group), $"The layout pane is missing its '{group}' group.");
		}

		Assert.IsTrue(IsVisible(harness, "Run simulation"), "the run toggle");
		Assert.IsTrue(IsVisible(harness, "Energy"), "the diagnostics readout");
	}

	[TestMethod]
	public void Panel_EditsTheGraphsOwnSimulation()
	{
		AstGraphEditor editor = new(SampleFunction());

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(3);

		// Reached through the panel's own group rather than set directly, so what is covered is that
		// the pane is wired to this graph's engine and not to a copy of its settings.
		harness.Click("Link shaping");
		harness.Step(2);

		Rectangle slider = harness.Probe.Rect("Untwisting")
			?? throw new AssertFailedException("The link shaping group should offer an untwisting slider.");

		double before = editor.Graph.Engine.PhysicsSettings.LinkUntwistStrength;
		float y = slider.MinY + (slider.Height / 2f);

		// A slider's item rectangle spans the track and the label beside it, so the drag stays in the
		// left portion to be sure it lands on the track.
		harness.Mouse.Drag(slider.MinX + (slider.Width * 0.1f), y, slider.MinX + (slider.Width * 0.45f), y);
		harness.Step(2);

		Assert.AreNotEqual(before, editor.Graph.Engine.PhysicsSettings.LinkUntwistStrength, 0.0001,
			"Dragging the panel's slider should have reached the graph's own simulation.");
	}

	[TestMethod]
	public void Panel_RunToggleIsTheSameFlagTheToolbarHolds()
	{
		AstGraphEditor editor = new(SampleFunction()) { LayoutRunning = true };

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(3);

		harness.Click("Run simulation");
		harness.Step(2);

		Assert.IsFalse(editor.LayoutRunning,
			"The panel's run toggle should stop the layout the same way the toolbar's checkbox does.");

		// The engine's own flag stays on: this editor stops the layout by not advancing it, and two
		// flags for one visible behaviour is the arrangement that leaves a graph stopped for a reason
		// the user cannot see.
		Assert.IsTrue(editor.Graph.Engine.PhysicsSettings.Enabled,
			"the engine's own Enabled should be left on");
	}
}
