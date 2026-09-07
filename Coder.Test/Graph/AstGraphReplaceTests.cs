// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for converting one node into another: <see cref="AstGraph.Replace"/> and the undoable edit
/// <see cref="AstGraphEditor.Convert"/> wraps it in.
/// </summary>
/// <remarks>
/// Conversion is what a user reaches for when they built the wrong kind of node — a number where
/// text was meant, an addition where a comparison was meant — and it has to keep both the node's
/// place in the tree and everything hanging off it.
/// </remarks>
[TestClass]
public class AstGraphReplaceTests
{
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, Literal.Number(1))));
		return function;
	}

	private static BinaryExpression BinaryIn(FunctionDeclaration function) =>
		(BinaryExpression)((ReturnStatement)function.Body[0]).Expression!;

	/// <summary>
	/// Tests that a replacement takes the node's place in the tree, which is the point of converting
	/// rather than deleting and rebuilding.
	/// </summary>
	[TestMethod]
	public void Replace_PutsTheNewNodeInThePlaceOfTheOld()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		LiteralExpression<int> number = (LiteralExpression<int>)BinaryIn(function).Right;
		LiteralExpression<string> text = Literal.Text("one");

		graph.Replace(number, text);

		Assert.AreSame(text, BinaryIn(function).Right);
		Assert.IsTrue(graph.Nodes.Values.Any(node => ReferenceEquals(node, text)));
		Assert.IsFalse(graph.Nodes.Values.Any(node => ReferenceEquals(node, number)), "the replaced literal held nothing, so it goes");
	}

	/// <summary>
	/// Tests that a replacement inside a sequence keeps its position, rather than moving to the end.
	/// </summary>
	[TestMethod]
	public void Replace_KeepsThePositionWithinASequence()
	{
		FunctionDeclaration function = new("ordered") { ReturnType = "void" };
		function.Body.Add(new ReturnStatement(Literal.Number(1)));
		function.Body.Add(new ReturnStatement(Literal.Number(2)));
		function.Body.Add(new ReturnStatement(Literal.Number(3)));
		AstGraph graph = new(function);

		AssignmentStatement replacement = new(new VariableReference("x"), Literal.Number(9));
		graph.Replace(function.Body[1], replacement);

		Assert.AreSame(replacement, function.Body[1]);
		Assert.AreEqual(3, function.Body.Count);
	}

	/// <summary>
	/// Tests that the operands a replacement can take move across, so converting one expression into
	/// another does not mean reconnecting everything under it.
	/// </summary>
	[TestMethod]
	public void Replace_MovesTheOperandsItCanTake()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		BinaryExpression binary = BinaryIn(function);
		VariableReference left = (VariableReference)binary.Left;
		LiteralExpression<int> right = (LiteralExpression<int>)binary.Right;

		BinaryExpression comparison = new(AstSchema.Unfilled(), BinaryOperator.LessThan, AstSchema.Unfilled());
		IReadOnlyList<(AstNode Child, AstLocation From)> moved = graph.Replace(binary, comparison);

		Assert.AreSame(left, comparison.Left);
		Assert.AreSame(right, comparison.Right);
		Assert.AreEqual(2, moved.Count);
		Assert.AreSame(comparison, ((ReturnStatement)function.Body[0]).Expression);
	}

	/// <summary>
	/// Tests that a child the replacement has no room for is kept rather than deleted, since losing
	/// part of the document to a menu click would be far worse than an extra loose node.
	/// </summary>
	[TestMethod]
	public void Replace_KeepsAChildTheReplacementCannotTake()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		BinaryExpression binary = BinaryIn(function);
		VariableReference left = (VariableReference)binary.Left;
		LiteralExpression<int> right = (LiteralExpression<int>)binary.Right;

		// A unary expression has one operand, so only the first of the two can move.
		UnaryExpression unary = new(UnaryOperator.Negate, AstSchema.Unfilled());
		graph.Replace(binary, unary);

		Assert.AreSame(left, unary.Operand);
		Assert.AreSame(unary, ((ReturnStatement)function.Body[0]).Expression);

		// The one that could not move is still in the graph, under the node it came from.
		Assert.AreSame(right, binary.Right);
		Assert.IsTrue(graph.Detached.Contains(binary));
		Assert.IsTrue(graph.Nodes.Values.Any(node => ReferenceEquals(node, right)));
	}

	/// <summary>
	/// Tests that a placeholder operand is not carried over, since the replacement brings its own and
	/// two of them would be one more thing to clear out.
	/// </summary>
	[TestMethod]
	public void Replace_LeavesPlaceholdersBehind()
	{
		FunctionDeclaration function = new("empty") { ReturnType = "void" };
		BinaryExpression binary = new(AstSchema.Unfilled(), BinaryOperator.Add, AstSchema.Unfilled());
		function.Body.Add(new ReturnStatement(binary));
		AstGraph graph = new(function);

		UnaryExpression unary = new(UnaryOperator.Negate, AstSchema.Unfilled());
		IReadOnlyList<(AstNode Child, AstLocation From)> moved = graph.Replace(binary, unary);

		Assert.AreEqual(0, moved.Count);
		Assert.IsFalse(graph.Detached.Contains(binary), "an expression holding only placeholders is not worth keeping");
	}

	/// <summary>
	/// Tests that a loose node can be converted too, since one is often created from the palette and
	/// only then found to be the wrong kind.
	/// </summary>
	[TestMethod]
	public void Replace_ConvertsALooseNode()
	{
		AstGraph graph = new(SampleFunction());
		VariableReference loose = new("orphan");
		graph.AddDetached(loose, new Vector2(300, 300));

		LiteralExpression<int> number = Literal.Number(5);
		graph.Replace(loose, number);

		Assert.IsTrue(graph.Detached.Contains(number));
		Assert.IsFalse(graph.Detached.Contains(loose));
	}

	/// <summary>
	/// Tests that the document's own node is not replaced, since the graph would then be viewing a
	/// document nobody asked to open.
	/// </summary>
	[TestMethod]
	public void Replace_RefusesTheRoot()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);

		Assert.AreEqual(0, graph.Replace(function, new ClassDeclaration("Other")).Count);
		Assert.AreSame(function, graph.Root);
	}

	/// <summary>
	/// Tests that converting is one undoable step, and that undoing it restores both the node and the
	/// operands that moved off it.
	/// </summary>
	[TestMethod]
	public void Convert_IsUndoneByPuttingEverythingBack()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		BinaryExpression binary = BinaryIn(function);
		VariableReference left = (VariableReference)binary.Left;
		LiteralExpression<int> right = (LiteralExpression<int>)binary.Right;

		UnaryExpression unary = new(UnaryOperator.LogicalNot, AstSchema.Unfilled());
		Assert.IsTrue(editor.Convert(binary, unary));
		Assert.AreSame(unary, ((ReturnStatement)function.Body[0]).Expression);

		editor.Undo();

		Assert.AreSame(binary, ((ReturnStatement)function.Body[0]).Expression);
		Assert.AreSame(left, binary.Left);
		Assert.AreSame(right, binary.Right);
		Assert.IsFalse(editor.Graph.Nodes.Values.Any(node => ReferenceEquals(node, unary)), "the conversion should be gone");

		editor.Redo();
		Assert.AreSame(unary, ((ReturnStatement)function.Body[0]).Expression);
		Assert.AreSame(left, unary.Operand);
	}

	/// <summary>
	/// Tests that only conversions the node's slot would accept are offered, so nothing in the menu
	/// breaks the document.
	/// </summary>
	[TestMethod]
	public void ConversionsFor_OffersOnlyWhatTheSlotAccepts()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		AstSlot expressionSlot = AstSchema.SlotsOf((ReturnStatement)function.Body[0]).Single();

		foreach (AstNodeTemplate template in editor.ConversionsFor(BinaryIn(function)))
		{
			Assert.IsTrue(AstSchema.Accepts(expressionSlot, template.Create()), template.Label);
		}

		// A parameter sits in a slot that takes nothing else, so it has exactly one conversion.
		Assert.IsTrue(editor.ConversionsFor(function.Parameters[0]).All(template => template.Create() is Parameter));

		// The document's own node is not converted at all.
		Assert.IsFalse(editor.ConversionsFor(function).Any());
	}

	/// <summary>
	/// Tests that converting the root is refused rather than half-done.
	/// </summary>
	[TestMethod]
	public void Convert_RefusesTheRootAndRecordsNothing()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		Assert.IsFalse(editor.Convert(function, new ClassDeclaration("Other")));
		Assert.IsFalse(editor.History.CanUndo);
	}
}
