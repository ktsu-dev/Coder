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
/// Covers the inspector as the user meets it: rows of a property grid, reached with the mouse and
/// the keyboard rather than by calling the editor's methods.
/// </summary>
/// <remarks>
/// What each edit does to the document is covered headlessly in
/// <see cref="AstGraphEditorInspectorTests"/>; what is covered here is that the panel's rows reach
/// those edits at all. The rows come from ktsu.ImGui.Widgets' property grid, which names each of
/// them for the probes, so a test finds a property by the label the user reads.
/// <para>
/// ImGui contexts are process-global, so only one harness can be live at a time and this class must
/// not run its methods in parallel.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class AstGraphEditorInspectorPanelTests
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
	/// Draws the whole editor, because selecting a node is ImNodes' business and it faults inside
	/// native code when its context is missing rather than throwing.
	/// </summary>
	/// <param name="editor">The editor to draw.</param>
	/// <returns>The configuration to hand the harness.</returns>
	private static ImGuiAppConfig ConfigFor(AstGraphEditor editor) => new()
	{
		Title = "AST inspector",
		OnRender = delta =>
		{
			ImGui.Begin("graph");
			editor.Draw(new Vector2(1200, 700), delta);
			ImGui.End();
		},
	};

	private static bool IsVisible(ImGuiAppHarness harness, string name) =>
		harness.Probe.WasSeenInFrame(name, harness.FrameCount - 1);

	/// <summary>
	/// Starts a harness with a node already selected, which is the state the inspector has anything
	/// to draw in.
	/// </summary>
	/// <param name="editor">The editor to draw.</param>
	/// <param name="node">The node to select once the graph has been drawn.</param>
	/// <returns>The running harness, for the caller to dispose.</returns>
	private static ImGuiAppHarness Inspecting(AstGraphEditor editor, AstNode node)
	{
		ImGuiAppHarness harness = ImGuiAppHarness.Start(ConfigFor(editor), Options);
		harness.Step(2);

		Assert.IsTrue(editor.Select(node), "the node to inspect should be in the graph");
		harness.Step(2);
		return harness;
	}

	/// <summary>
	/// Tests that every property of the selected node is a row of the grid, named for the probes the
	/// way it is labelled on screen.
	/// </summary>
	[TestMethod]
	public void Inspector_DrawsARowPerProperty()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Inspecting(editor, function);

		foreach (string row in new[] { "Name", "ReturnType", "Visibility" })
		{
			Assert.IsTrue(IsVisible(harness, row), $"The inspector is missing its '{row}' row.");
		}

		// A variadic slot's row is named for the buttons that change it, since the count beside them
		// is text rather than an editor the user can reach.
		foreach (string slot in new[] { "Parameters", "Body" })
		{
			Assert.IsTrue(IsVisible(harness, $"Add {slot}"), $"The inspector is missing its '{slot}' row.");
		}
	}

	/// <summary>
	/// Tests that a name typed into its row reaches the document when the box is left, and does so as
	/// one undoable step rather than one per character.
	/// </summary>
	[TestMethod]
	public void Inspector_CommitsTypedTextWhenTheBoxIsLeft()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Inspecting(editor, function);

		harness.Click("Name");
		harness.Step(2);

		harness.Keyboard.Press(ImGuiKey.A, ctrl: true);
		harness.Keyboard.Type("renamed");
		harness.Step(2);

		Assert.AreEqual("total", function.Name, "typing should not reach the document until the box is left");

		harness.Keyboard.Press(ImGuiKey.Enter);
		harness.Step(2);

		Assert.AreEqual("renamed", function.Name);
		Assert.IsTrue(editor.History.CanUndo);

		editor.Undo();
		Assert.AreEqual("total", function.Name);
		Assert.IsFalse(editor.History.CanUndo, "renaming should have been one step, not one per character");
	}

	/// <summary>
	/// Tests that a flag row is committed as it is clicked, since a checkbox has nowhere to hold a
	/// half-made change.
	/// </summary>
	[TestMethod]
	public void Inspector_CommitsAFlagAsItIsClicked()
	{
		FunctionDeclaration function = SampleFunction();
		Parameter parameter = function.Parameters[0];
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Inspecting(editor, parameter);

		harness.Click("Optional");
		harness.Step(2);

		Assert.IsTrue(parameter.IsOptional);

		editor.Undo();
		Assert.IsFalse(parameter.IsOptional);
	}

	/// <summary>
	/// Tests that an operator is picked by the label it reads as, which is why the choice row is
	/// composed rather than drawn by the grid's own enumeration row.
	/// </summary>
	[TestMethod]
	public void Inspector_PicksAnOperatorByItsReadableLabel()
	{
		FunctionDeclaration function = SampleFunction();
		BinaryExpression binary = (BinaryExpression)((ReturnStatement)function.Body[0]).Expression!;
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Inspecting(editor, binary);

		Assert.IsTrue(IsVisible(harness, "Operator"), "The inspector is missing its operator row.");

		harness.Click("Operator");
		harness.Step(2);

		harness.Click("*  Multiply");
		harness.Step(2);

		Assert.AreEqual(BinaryOperator.Multiply, binary.Operator);

		editor.Undo();
		Assert.AreEqual(BinaryOperator.Add, binary.Operator);
	}

	/// <summary>
	/// Tests that the buttons on a variadic slot's row grow and shrink it, which is the half of
	/// editing that dragging a link cannot express.
	/// </summary>
	[TestMethod]
	public void Inspector_AddsAndRemovesChildrenFromASlotRow()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		using ImGuiAppHarness harness = Inspecting(editor, function);

		harness.Click("Add Parameters");
		harness.Step(2);

		Assert.AreEqual(2, function.Parameters.Count);

		harness.Click("Remove Parameters");
		harness.Step(2);

		Assert.AreEqual(1, function.Parameters.Count);
	}
}
