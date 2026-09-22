// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.ImGui.NodeEditor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests what a link dragged off a pin and dropped on empty canvas offers to create, and what
/// picking one of those entries does to the document.
/// </summary>
/// <remarks>
/// These drive <see cref="AstGraphEditor.CreatableFrom"/> and <see cref="AstGraphEditor.CreateFrom"/>
/// directly rather than through ImGui. Which entries a drop offers and what accepting one attaches
/// are rules, so they live where the rules are tested; the popup that lists them is part of the
/// surface <see cref="AstGraphEditorTests"/> covers.
/// </remarks>
[TestClass]
public class AstGraphEditorLinkDropTests
{
	/// <summary>
	/// The whole menu a parameter's output pin should offer: only a function has a signature to put
	/// one in.
	/// </summary>
	private static readonly string[] ExpectedForParameter = ["Function"];

	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, Literal.Number(1))));
		return function;
	}

	/// <summary>
	/// Tests that dropping a drag begun at a slot offers every node the slot would take, and nothing
	/// it would refuse.
	/// </summary>
	/// <remarks>
	/// A body takes any statement, which is everything in the palette except a parameter and an entry
	/// point — a parameter belongs to a signature and a program starts running at an entry point, so
	/// neither stands inside a body.
	/// </remarks>
	[TestMethod]
	public void CreatableFrom_InputPin_OffersEverythingTheSlotAccepts()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		AstNodeTemplate[] offered = [.. editor.CreatableFrom(InputPin(editor.Graph, function, "Body", 1))];

		Assert.IsGreaterThan(0, offered.Length);

		AstSlot body = AstSchema.SlotsOf(function).Single(slot => slot.Name == "Body");
		foreach (AstNodeTemplate template in offered)
		{
			Assert.IsTrue(
				AstSchema.Accepts(body, template.Create()),
				$"'{template.Label}' was offered for Body but the slot would refuse it");
		}

		CollectionAssert.DoesNotContain(Labels(offered), "Parameter");
		CollectionAssert.DoesNotContain(Labels(offered), "Entry point");
		CollectionAssert.Contains(Labels(offered), "Return");
	}

	/// <summary>
	/// Tests that dropping a drag begun at a node's output offers only nodes with a slot that node
	/// could sit in.
	/// </summary>
	/// <remarks>
	/// A parameter fits nothing in the palette but a function's signature, so that is the whole of the
	/// menu — the filter is doing real work here rather than trimming an entry or two.
	/// </remarks>
	[TestMethod]
	public void CreatableFrom_OutputPin_OffersOnlyNodesWithARoomForIt()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		Parameter parameter = function.Parameters[0];

		AstNodeTemplate[] offered = [.. editor.CreatableFrom(OutputPin(editor.Graph, parameter))];

		CollectionAssert.AreEquivalent(ExpectedForParameter, Labels(offered));
	}

	/// <summary>
	/// Tests that a slot the palette cannot fill offers nothing, rather than offering entries the
	/// connection would then refuse.
	/// </summary>
	/// <remarks>
	/// An enumeration's members take an <see cref="EnumMember"/> and the palette has no entry for one,
	/// so this is a genuinely empty answer rather than a lookup that failed.
	/// </remarks>
	[TestMethod]
	public void CreatableFrom_InputPin_OffersNothingWhenThePaletteCannotFillTheSlot()
	{
		EnumDeclaration enumeration = new("Colour");
		AstGraphEditor editor = new(enumeration);

		AstSlot members = AstSchema.SlotsOf(enumeration).Single();
		AstNodeTemplate[] offered = [.. editor.CreatableFrom(InputPin(editor.Graph, enumeration, members.Name, 0))];

		Assert.IsEmpty(offered);
	}

	/// <summary>
	/// Tests that a pin that is not in the graph offers nothing.
	/// </summary>
	[TestMethod]
	public void CreatableFrom_UnknownPin_OffersNothing()
	{
		AstGraphEditor editor = new(SampleFunction());

		Assert.IsEmpty(editor.CreatableFrom(-1));
	}

	/// <summary>
	/// Tests that picking an entry for a drag begun at a slot puts the new node in that slot.
	/// </summary>
	[TestMethod]
	public void CreateFrom_InputPin_FillsTheSlot()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		bool created = editor.CreateFrom(
			Template("Variable"),
			InputPin(editor.Graph, function, "Body", 1),
			new Vector2(40, 60));

		Assert.IsTrue(created);
		Assert.HasCount(2, function.Body);
		Assert.IsInstanceOfType<VariableDeclaration>(function.Body[1]);
	}

	/// <summary>
	/// Tests that picking an entry for a drag begun at a node's output moves that node into the new
	/// one, rather than leaving both detached.
	/// </summary>
	/// <remarks>
	/// This is the direction the issue is really about: the user dragged out of a value looking for
	/// somewhere to put it, so the node they pick has to arrive already holding it.
	/// </remarks>
	[TestMethod]
	public void CreateFrom_OutputPin_PutsTheDraggedNodeInTheNewOne()
	{
		ClassDeclaration holder = new("Holder");
		AstGraphEditor editor = new(holder);

		LiteralExpression<int> literal = Literal.Number(7);
		editor.Add(literal, new Vector2(10, 10));

		bool created = editor.CreateFrom(
			Template("Return"),
			OutputPin(editor.Graph, literal),
			new Vector2(80, 10));

		Assert.IsTrue(created);

		ReturnStatement statement = editor.Graph.Nodes.Values.OfType<ReturnStatement>().Single();
		Assert.AreSame(literal, statement.Expression);
	}

	/// <summary>
	/// Tests that creating the node and connecting it are two steps in the history.
	/// </summary>
	/// <remarks>
	/// The same two the user would have taken by hand, so the first undo takes the connection back and
	/// leaves the node they asked for on the canvas, rather than making the whole gesture all-or-nothing.
	/// </remarks>
	[TestMethod]
	public void CreateFrom_RecordsTheNodeAndTheConnectionSeparately()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);

		editor.CreateFrom(
			Template("Variable"),
			InputPin(editor.Graph, function, "Body", 1),
			new Vector2(40, 60));

		Assert.HasCount(2, function.Body);

		editor.Undo();
		Assert.HasCount(1, function.Body);
		Assert.IsTrue(editor.History.CanUndo, "the node's own creation should still be on the stack");

		editor.Undo();
		Assert.HasCount(1, function.Body);
		Assert.IsFalse(editor.History.CanUndo);
	}

	/// <summary>
	/// Tests that a pin that is not in the graph is refused without touching the document or the
	/// history.
	/// </summary>
	[TestMethod]
	public void CreateFrom_UnknownPin_RecordsNothing()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		int before = editor.Graph.Nodes.Count;

		Assert.IsFalse(editor.CreateFrom(Template("Variable"), -1, new Vector2(40, 60)));
		Assert.IsFalse(editor.History.CanUndo);
		Assert.AreEqual(before, editor.Graph.Nodes.Count);
		Assert.HasCount(1, function.Body);
	}

	/// <summary>
	/// Finds the palette entry with a given label.
	/// </summary>
	/// <param name="label">The label to look for.</param>
	/// <returns>The entry.</returns>
	private static AstNodeTemplate Template(string label) =>
		AstNodeCatalog.Templates.Single(template => template.Label == label);

	/// <summary>
	/// Lists the labels of a set of entries, for asserting on what a menu would show.
	/// </summary>
	/// <param name="templates">The entries.</param>
	/// <returns>Their labels, in order.</returns>
	private static string[] Labels(IEnumerable<AstNodeTemplate> templates) =>
		[.. templates.Select(template => template.Label)];

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
		Node parentNode = graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), parent));
		AstSlot slot = AstSchema.SlotsOf(parent).Single(s => s.Name == slotName);
		return parentNode.InputPins.Single(p => p.EffectiveDisplayName == AstGraph.PinLabel(slot, index)).Id;
	}

	/// <summary>
	/// Finds a node's output pin.
	/// </summary>
	/// <param name="graph">The graph under test.</param>
	/// <param name="node">The node to look at.</param>
	/// <returns>The output pin's id.</returns>
	private static int OutputPin(AstGraph graph, AstNode node) =>
		graph.Engine.Nodes.Single(n => ReferenceEquals(graph.AstNodeFor(n.Id), node)).OutputPins[0].Id;
}
