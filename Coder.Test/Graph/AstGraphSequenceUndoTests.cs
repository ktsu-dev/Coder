// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.NodeEditor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that edits to a sequence slot — a body, a parameter list, a member list — are undone without
/// losing a sibling.
/// </summary>
/// <remarks>
/// Taking a node out of a sequence shifts every later sibling down by one, so putting it back means
/// inserting it at its old position rather than writing over whatever has moved into it. And a node
/// dropped onto a filled pin displaces the occupant, which has to stay in the graph as a loose node
/// and go back on undo, rather than drop out of the document with no warning.
/// </remarks>
[TestClass]
public class AstGraphSequenceUndoTests
{
	private static FunctionDeclaration ThreeStatements()
	{
		FunctionDeclaration function = new("run");
		function.Body.Add(new ExpressionStatement(new CallExpression("s0")));
		function.Body.Add(new ExpressionStatement(new CallExpression("s1")));
		function.Body.Add(new ExpressionStatement(new CallExpression("s2")));
		return function;
	}

	/// <summary>
	/// Tests that removing a middle statement and undoing it restores the body exactly.
	/// </summary>
	[TestMethod]
	public void RemovingAMiddleStatement_IsUndoneWithoutLosingTheNextOne()
	{
		AstGraphEditor editor = new(ThreeStatements());
		FunctionDeclaration function = (FunctionDeclaration)editor.Graph.Root;
		AstNode[] original = [.. function.Body];

		Assert.IsTrue(editor.Remove(FindId(editor, original[1])));
		CollectionAssert.AreEqual(new[] { original[0], original[2] }, function.Body.ToArray());

		editor.Undo();

		CollectionAssert.AreEqual(original, function.Body.ToArray());
		Assert.AreEqual(0, editor.Graph.Detached.Count);
	}

	/// <summary>
	/// Tests that disconnecting a middle statement and undoing it restores the body exactly.
	/// </summary>
	[TestMethod]
	public void DisconnectingAMiddleStatement_IsUndoneWithoutLosingTheNextOne()
	{
		AstGraphEditor editor = new(ThreeStatements());
		FunctionDeclaration function = (FunctionDeclaration)editor.Graph.Root;
		AstNode[] original = [.. function.Body];

		Assert.IsTrue(editor.Disconnect(LinkInto(editor, original[1])));
		CollectionAssert.AreEqual(new[] { original[0], original[2] }, function.Body.ToArray());
		Assert.AreSame(original[1], editor.Graph.Detached.Single());

		editor.Undo();

		CollectionAssert.AreEqual(original, function.Body.ToArray());
		Assert.AreEqual(0, editor.Graph.Detached.Count);
	}

	/// <summary>
	/// Tests that connecting a statement onto a later pin of its own body replaces that pin's
	/// occupant, keeps the occupant in the graph, and is undone exactly — and redone the same way.
	/// </summary>
	[TestMethod]
	public void ConnectingWithinTheSameBody_ReplacesThePinsOccupantAndIsUndone()
	{
		AstGraphEditor editor = new(ThreeStatements());
		FunctionDeclaration function = (FunctionDeclaration)editor.Graph.Root;
		AstNode[] original = [.. function.Body];

		AstConnectResult result = editor.Connect(OutputPinOf(editor, original[0]), InputPinOf(editor, function, "Body", 1));

		Assert.IsTrue(result.Success, result.Message);
		CollectionAssert.AreEqual(new[] { original[0], original[2] }, function.Body.ToArray(), "s1 held the pin, so s1 is the one replaced");
		Assert.AreSame(original[1], editor.Graph.Detached.Single(), "the displaced statement stays in the graph");

		editor.Undo();

		CollectionAssert.AreEqual(original, function.Body.ToArray());
		Assert.AreEqual(0, editor.Graph.Detached.Count);

		editor.Redo();

		CollectionAssert.AreEqual(new[] { original[0], original[2] }, function.Body.ToArray());
		Assert.AreSame(original[1], editor.Graph.Detached.Single());
	}

	/// <summary>
	/// Tests that connecting a loose node onto a filled sequence pin keeps the occupant it displaces,
	/// and that undoing it puts both back where they were.
	/// </summary>
	[TestMethod]
	public void ConnectingALooseNodeOntoAFilledPin_KeepsTheOccupantAndIsUndone()
	{
		AstGraphEditor editor = new(ThreeStatements());
		FunctionDeclaration function = (FunctionDeclaration)editor.Graph.Root;
		AstNode[] original = [.. function.Body];
		ExpressionStatement loose = new(new CallExpression("x"));
		editor.Add(loose, new Vector2(400, 400));

		AstConnectResult result = editor.Connect(OutputPinOf(editor, loose), InputPinOf(editor, function, "Body", 0));

		Assert.IsTrue(result.Success, result.Message);
		CollectionAssert.AreEqual(new AstNode[] { loose, original[1], original[2] }, function.Body.ToArray());
		Assert.AreSame(original[0], editor.Graph.Detached.Single(), "the displaced statement stays in the graph");

		editor.Undo();

		CollectionAssert.AreEqual(original, function.Body.ToArray());
		Assert.AreSame(loose, editor.Graph.Detached.Single(), "the loose node goes back to being loose");
	}

	/// <summary>
	/// Tests that inserting into a sequence shifts the later entries up rather than writing over one.
	/// </summary>
	[TestMethod]
	public void TryInsertAt_ShiftsLaterEntriesUp()
	{
		FunctionDeclaration function = ThreeStatements();
		AstNode[] original = [.. function.Body];
		AstSlot body = AstSchema.SlotsOf(function).Single(s => s.Name == "Body");
		ExpressionStatement inserted = new(new CallExpression("x"));

		Assert.IsTrue(AstSchema.TryInsertAt(function, body, 1, inserted));

		CollectionAssert.AreEqual(new[] { original[0], inserted, original[1], original[2] }, function.Body.ToArray());
	}

	private static int FindId(AstGraphEditor editor, AstNode node) =>
		editor.Graph.Nodes.Single(pair => ReferenceEquals(pair.Value, node)).Key;

	private static int OutputPinOf(AstGraphEditor editor, AstNode node) =>
		editor.Graph.Engine.Nodes.Single(n => n.Id == FindId(editor, node)).OutputPins[0].Id;

	private static int InputPinOf(AstGraphEditor editor, AstNode parent, string slotName, int index)
	{
		Node parentNode = editor.Graph.Engine.Nodes.Single(n => n.Id == FindId(editor, parent));
		AstSlot slot = AstSchema.SlotsOf(parent).Single(s => s.Name == slotName);
		return parentNode.InputPins.Single(p => p.EffectiveDisplayName == AstGraph.PinLabel(slot, index)).Id;
	}

	private static int LinkInto(AstGraphEditor editor, AstNode child)
	{
		int outputPin = OutputPinOf(editor, child);
		return editor.Graph.Engine.Links.Single(l => l.OutputPinId == outputPin).Id;
	}
}
