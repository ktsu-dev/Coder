// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests the edits the inspector panel makes: a node's own properties, and how many children its
/// variadic slots hold.
/// </summary>
/// <remarks>
/// Driven through <see cref="AstGraphEditor"/>'s methods rather than through ImGui, for the same
/// reason the history tests are: what an edit does and what undoing it restores is not decided by
/// the widget that happened to call it.
/// </remarks>
[TestClass]
public class AstGraphEditorInspectorTests
{
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, Literal.Number(1))));
		return function;
	}

	private static AstSlot SlotOf(AstNode node, string name) =>
		AstSchema.SlotsOf(node).Single(slot => string.Equals(slot.Name, name, StringComparison.Ordinal));

	/// <summary>
	/// Tests that editing a literal's value is one undoable step, which is what makes a typed-in
	/// number part of the document's history rather than a change that cannot be taken back.
	/// </summary>
	[TestMethod]
	public void SetField_EditsALiteralAndIsUndoable()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		LiteralExpression<int> literal = (LiteralExpression<int>)((BinaryExpression)
			((ReturnStatement)function.Body[0]).Expression!).Right;

		Assert.IsTrue(editor.SetField(literal, "Value", "42"));
		Assert.AreEqual(42, literal.Value);

		editor.Undo();
		Assert.AreEqual(1, literal.Value);

		editor.Redo();
		Assert.AreEqual(42, literal.Value);
	}

	/// <summary>
	/// Tests that choosing a different operator rewrites the expression and retitles its node, since
	/// the caption is how the user reads which operator an expression has.
	/// </summary>
	[TestMethod]
	public void SetField_ChangesAnOperatorAndTheCaption()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		BinaryExpression binary = (BinaryExpression)((ReturnStatement)function.Body[0]).Expression!;

		Assert.IsTrue(editor.SetField(binary, "Operator", nameof(BinaryOperator.Multiply)));
		Assert.AreEqual(BinaryOperator.Multiply, binary.Operator);
		Assert.IsTrue(editor.Graph.Nodes.Values.Any(node => ReferenceEquals(node, binary)));
		Assert.AreEqual("binary *", AstSchema.Describe(binary));

		editor.Undo();
		Assert.AreEqual(BinaryOperator.Add, binary.Operator);
	}

	/// <summary>
	/// Tests that a refused edit records nothing, so undo still steps back to the last real change.
	/// </summary>
	[TestMethod]
	public void SetField_RecordsNothingWhenTheValueIsRefused()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		LiteralExpression<int> literal = (LiteralExpression<int>)((BinaryExpression)
			((ReturnStatement)function.Body[0]).Expression!).Right;

		Assert.IsFalse(editor.SetField(literal, "Value", "not a number"));
		Assert.IsFalse(editor.History.CanUndo);

		// Nor for a value the field already holds.
		Assert.IsFalse(editor.SetField(literal, "Value", "1"));
		Assert.IsFalse(editor.History.CanUndo);
	}

	/// <summary>
	/// Tests that a function can be given another parameter in one step, which is what "change the
	/// number of parameters" means to a user.
	/// </summary>
	[TestMethod]
	public void AddChild_GrowsTheParameterList()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		Assert.IsTrue(editor.AddChild(function, SlotOf(function, "Parameters")));
		Assert.AreEqual(2, function.Parameters.Count);

		// The new parameter is a real node in the graph, not just an entry in the AST.
		Assert.IsTrue(editor.Graph.Nodes.Values.Any(node => ReferenceEquals(node, function.Parameters[1])));

		editor.Undo();
		Assert.AreEqual(1, function.Parameters.Count);

		editor.Redo();
		Assert.AreEqual(2, function.Parameters.Count);
	}

	/// <summary>
	/// Tests that a function body grows the same way, so a second statement does not have to be
	/// dragged in from the palette.
	/// </summary>
	[TestMethod]
	public void AddChild_GrowsTheBody()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		Assert.IsTrue(editor.AddChild(function, SlotOf(function, "Body")));
		Assert.AreEqual(2, function.Body.Count);
		Assert.IsInstanceOfType<ReturnStatement>(function.Body[1]);
	}

	/// <summary>
	/// Tests that a class grows by methods, since that is the only member the editor can offer
	/// without asking what kind was meant.
	/// </summary>
	[TestMethod]
	public void AddChild_GrowsAClassByAMethod()
	{
		ClassDeclaration declaration = new("Shape");
		AstGraphEditor editor = new(declaration);

		Assert.IsTrue(editor.AddChild(declaration, SlotOf(declaration, "Members")));
		Assert.AreEqual(1, declaration.Members.Count);
		Assert.IsInstanceOfType<FunctionDeclaration>(declaration.Members[0]);
	}

	/// <summary>
	/// Tests that shrinking a slot takes the child out of the document entirely, and that undoing it
	/// puts it back where it was rather than leaving it stranded.
	/// </summary>
	[TestMethod]
	public void RemoveLastChild_ShrinksTheSlotAndIsUndoable()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		Parameter removed = function.Parameters[0];

		Assert.IsTrue(editor.RemoveLastChild(function, SlotOf(function, "Parameters")));
		Assert.AreEqual(0, function.Parameters.Count);
		Assert.IsFalse(editor.Graph.Detached.Contains(removed), "a removed parameter should not linger as a loose node");

		editor.Undo();
		Assert.AreEqual(1, function.Parameters.Count);
		Assert.AreSame(removed, function.Parameters[0]);
	}

	/// <summary>
	/// Tests that shrinking an empty slot does nothing at all, rather than putting a step that undoes
	/// nothing on the history.
	/// </summary>
	[TestMethod]
	public void RemoveLastChild_DoesNothingToAnEmptySlot()
	{
		FunctionDeclaration function = new("empty") { ReturnType = "void" };
		AstGraphEditor editor = new(function);

		Assert.IsFalse(editor.RemoveLastChild(function, SlotOf(function, "Parameters")));
		Assert.IsFalse(editor.History.CanUndo);
	}

	/// <summary>
	/// Tests that a slot holding at most one child is not grown, since a second child there would
	/// only replace the first.
	/// </summary>
	[TestMethod]
	public void AddChild_RefusesASlotThatHoldsOne()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		ReturnStatement returnStmt = (ReturnStatement)function.Body[0];

		Assert.IsFalse(editor.AddChild(returnStmt, SlotOf(returnStmt, "Expression")));
		Assert.IsFalse(editor.History.CanUndo);
	}
}
