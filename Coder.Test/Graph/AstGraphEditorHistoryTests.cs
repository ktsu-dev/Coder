// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.NodeEditor;
using ktsu.UndoRedo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests the undo stack <see cref="AstGraphEditor"/> keeps, which is <c>ktsu.UndoRedo</c>'s.
/// </summary>
/// <remarks>
/// These drive the editor's editing methods directly rather than through ImGui: what is under test
/// is what an edit records and what undoing it restores, and none of that is decided by the draw
/// call that happens to invoke it. The surface itself is covered by
/// <see cref="AstGraphEditorTests"/>.
/// </remarks>
[TestClass]
public class AstGraphEditorHistoryTests
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
	/// Tests that a fresh editor has nothing to step through and no unsaved work.
	/// </summary>
	[TestMethod]
	public void History_StartsEmpty()
	{
		AstGraphEditor editor = new(SampleFunction());

		Assert.IsFalse(editor.History.CanUndo);
		Assert.IsFalse(editor.History.CanRedo);
		Assert.IsFalse(editor.History.HasUnsavedChanges);

		// Stepping either way with nothing recorded must be a no-op rather than a fault.
		editor.Undo();
		editor.Redo();
		Assert.AreEqual(6, editor.Graph.Nodes.Count);
	}

	/// <summary>
	/// Tests that adding a node from the palette is one undoable step, and that undoing it takes the
	/// node back out rather than leaving it stranded.
	/// </summary>
	[TestMethod]
	public void Add_IsUndoneByTakingTheNodeBackOut()
	{
		AstGraphEditor editor = new(SampleFunction());
		int before = editor.Graph.Nodes.Count;

		editor.Add(new VariableReference("added"), new Vector2(120, 240));

		Assert.AreEqual(before + 1, editor.Graph.Nodes.Count);
		Assert.IsTrue(editor.History.CanUndo);

		editor.Undo();
		Assert.AreEqual(before, editor.Graph.Nodes.Count);

		editor.Redo();
		Assert.AreEqual(before + 1, editor.Graph.Nodes.Count);
	}

	/// <summary>
	/// Tests that removing a node is undone by putting it back where it was, rather than back into
	/// the graph unattached.
	/// </summary>
	/// <remarks>
	/// This is the case snapshot undo got right by accident and an inverse-operation undo has to get
	/// right on purpose, so it is worth stating: the restored node is attached to the same parent, in
	/// the same slot, at the same index.
	/// </remarks>
	[TestMethod]
	public void Remove_IsUndoneByPuttingTheNodeBack()
	{
		AstGraphEditor editor = new(SampleFunction());
		ReturnStatement returnStmt = (ReturnStatement)((FunctionDeclaration)editor.Graph.Root).Body[0];
		BinaryExpression binary = (BinaryExpression)returnStmt.Expression!;
		VariableReference left = (VariableReference)binary.Left!;
		int nodeId = editor.Graph.Nodes.Single(pair => ReferenceEquals(pair.Value, left)).Key;

		Assert.IsTrue(editor.Remove(nodeId));
		Assert.IsTrue(AstSchema.IsUnfilled(binary.Left!), "the slot should be back to its placeholder");

		editor.Undo();

		Assert.AreSame(left, binary.Left, "undo should put it back in the slot it came out of");
		Assert.AreEqual(0, editor.Graph.Detached.Count);
	}

	/// <summary>
	/// Tests that the document's root cannot be removed and that refusing it leaves the history
	/// alone, rather than recording a step that undoes nothing.
	/// </summary>
	[TestMethod]
	public void Remove_RefusesTheRootWithoutRecordingAStep()
	{
		AstGraphEditor editor = new(SampleFunction());
		int rootId = editor.Graph.Nodes.Single(pair => ReferenceEquals(pair.Value, editor.Graph.Root)).Key;

		Assert.IsFalse(editor.Remove(rootId));
		Assert.IsFalse(editor.History.CanUndo);
	}

	/// <summary>
	/// Tests that connecting a node is undone by putting it back where it was, and that the step is
	/// described by what it did.
	/// </summary>
	[TestMethod]
	public void Connect_IsUndoneByReturningTheNodeToItsOldHome()
	{
		AstGraphEditor editor = new(SampleFunction());
		FunctionDeclaration function = (FunctionDeclaration)editor.Graph.Root;
		ReturnStatement returnStmt = (ReturnStatement)function.Body[0];
		BinaryExpression binary = (BinaryExpression)returnStmt.Expression!;
		LiteralExpression<int> literal = (LiteralExpression<int>)binary.Right!;

		// Move the literal from the binary expression's right operand into its left.
		VariableReference left = (VariableReference)binary.Left!;
		Assert.IsTrue(editor.Remove(FindId(editor, left)));

		AstConnectResult result = editor.Connect(OutputPinOf(editor, literal), InputPinOf(editor, binary, "Left", 0));

		Assert.IsTrue(result.Success, result.Message);
		Assert.AreSame(literal, binary.Left);
		Assert.IsTrue(AstSchema.IsUnfilled(binary.Right!), "the operand it left should be back to its placeholder");

		editor.Undo();

		Assert.AreSame(literal, binary.Right, "undo should return it to the operand it came from");
		Assert.IsTrue(AstSchema.IsUnfilled(binary.Left!));
	}

	/// <summary>
	/// Tests that a connection the schema refuses records nothing, so the undo stack never holds a
	/// step for an edit that did not happen.
	/// </summary>
	[TestMethod]
	public void Connect_RecordsNothingWhenItIsRefused()
	{
		AstGraphEditor editor = new(SampleFunction());
		FunctionDeclaration function = (FunctionDeclaration)editor.Graph.Root;
		ReturnStatement returnStmt = (ReturnStatement)function.Body[0];

		// A statement cannot fill a parameter slot.
		AstConnectResult result = editor.Connect(
			OutputPinOf(editor, returnStmt),
			InputPinOf(editor, function, "Parameters", 1));

		Assert.IsFalse(result.Success);
		Assert.IsFalse(editor.History.CanUndo);
	}

	/// <summary>
	/// Tests that cutting a link is undone by reattaching the node, and that cutting a link that is
	/// not there records nothing.
	/// </summary>
	[TestMethod]
	public void Disconnect_IsUndoneByReattaching()
	{
		AstGraphEditor editor = new(SampleFunction());
		FunctionDeclaration function = (FunctionDeclaration)editor.Graph.Root;
		ReturnStatement returnStmt = (ReturnStatement)function.Body[0];
		BinaryExpression binary = (BinaryExpression)returnStmt.Expression!;

		Assert.IsFalse(editor.Disconnect(linkId: 9999));
		Assert.IsFalse(editor.History.CanUndo);

		Assert.IsTrue(editor.Disconnect(LinkInto(editor, binary)));
		Assert.IsNull(returnStmt.Expression);
		Assert.AreEqual(1, editor.Graph.Detached.Count);

		editor.Undo();

		Assert.AreSame(binary, returnStmt.Expression);
		Assert.AreEqual(0, editor.Graph.Detached.Count);
	}

	/// <summary>
	/// Tests that the history says what each step was, which is what the status bar reports and the
	/// reason the stack holds commands rather than snapshots.
	/// </summary>
	[TestMethod]
	public void History_DescribesEachStep()
	{
		AstGraphEditor editor = new(SampleFunction());
		VariableReference added = new("added");

		editor.Add(added, Vector2.Zero);

		ICommand recorded = editor.History.Commands[editor.History.CurrentPosition];
		Assert.AreEqual($"Add {AstSchema.Describe(added)}", recorded.Description);
		Assert.AreEqual(ChangeType.Insert, recorded.Metadata.ChangeType);
		CollectionAssert.Contains(recorded.Metadata.AffectedItems.ToArray(), AstSchema.Describe(added));
	}

	/// <summary>
	/// Tests that a new edit made after an undo discards the redo branch, since nothing on it is
	/// reachable from the document any more.
	/// </summary>
	[TestMethod]
	public void History_DiscardsTheRedoBranchOnANewEdit()
	{
		AstGraphEditor editor = new(SampleFunction());

		editor.Add(new VariableReference("first"), Vector2.Zero);
		editor.Undo();
		Assert.IsTrue(editor.History.CanRedo);

		editor.Add(new VariableReference("second"), Vector2.Zero);

		Assert.IsFalse(editor.History.CanRedo);
	}

	/// <summary>
	/// Tests that the stack tracks where the document was last saved, so a caller can tell whether
	/// there is anything to write — and that undoing back across the save reports it clean again,
	/// which is the whole reason this is asked of the stack rather than of a flag.
	/// </summary>
	[TestMethod]
	public void History_TracksWhetherThereIsUnsavedWork()
	{
		AstGraphEditor editor = new(SampleFunction());

		editor.Add(new VariableReference("added"), Vector2.Zero);
		Assert.IsTrue(editor.History.HasUnsavedChanges);

		editor.History.MarkAsSaved("Saved");
		Assert.IsFalse(editor.History.HasUnsavedChanges);

		editor.Add(new VariableReference("another"), Vector2.Zero);
		Assert.IsTrue(editor.History.HasUnsavedChanges);

		editor.Undo();
		Assert.IsFalse(editor.History.HasUnsavedChanges, "undoing back to the save point leaves nothing to write");
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
