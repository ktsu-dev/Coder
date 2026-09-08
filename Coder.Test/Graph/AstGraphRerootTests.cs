// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.ImGuiNodeEditor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the document's own node can be adopted into something else — which is what making a
/// function a member of a class amounts to.
/// </summary>
/// <remarks>
/// A document opened as a function has that function as its root, and a class dragged out of the
/// palette is a loose node beside it. Connecting the one to the other is the obvious gesture for
/// "this function belongs to that class", and the only thing standing in its way was that the root
/// had nowhere to go. Re-rooting is what gives it one: the class becomes the document, and the
/// function becomes its member.
/// </remarks>
[TestClass]
public class AstGraphRerootTests
{
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("area") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("scale", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("scale"), BinaryOperator.Multiply, Literal.Number(2))));
		return function;
	}

	private static AstSlot MembersOf(ClassDeclaration declaration) =>
		AstSchema.SlotsOf(declaration).Single(slot => string.Equals(slot.Name, "Members", StringComparison.Ordinal));

	/// <summary>
	/// Tests that the document's root reports a place of its own, rather than looking like any other
	/// node without a parent.
	/// </summary>
	/// <remarks>
	/// Undo puts a node back where it came from, so a root that reported itself as merely detached
	/// would be restored as a loose node and the document would be left rooted at whatever adopted
	/// it.
	/// </remarks>
	[TestMethod]
	public void LocationOf_TellsTheRootApartFromALooseNode()
	{
		AstGraph graph = new(SampleFunction());
		VariableReference loose = new("loose");
		graph.AddDetached(loose, Vector2.Zero);

		Assert.AreEqual(AstLocation.Root, graph.LocationOf(graph.Root));
		Assert.AreEqual(AstLocation.Detached, graph.LocationOf(loose));
		Assert.AreNotEqual(AstLocation.Root, AstLocation.Detached, "both are parentless, and they are not the same place");
	}

	/// <summary>
	/// Tests that attaching the document's function to a loose class makes the class the document and
	/// the function its member.
	/// </summary>
	[TestMethod]
	public void MoveTo_AdoptsTheDocumentIntoALooseClass()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		ClassDeclaration declaration = new("Shape");
		graph.AddDetached(declaration, new Vector2(400, 100));

		Assert.IsTrue(graph.MoveTo(function, new AstLocation(declaration, MembersOf(declaration), 0)));

		Assert.AreSame(declaration, graph.Root);
		Assert.AreSame(function, declaration.Members.Single());
		Assert.AreEqual(0, graph.Detached.Count, "nothing is loose any more: the class is the document and the function is in it");
	}

	/// <summary>
	/// Tests that the whole document comes along, so a function's parameters and body are still the
	/// method's once it is a member.
	/// </summary>
	[TestMethod]
	public void MoveTo_CarriesTheFunctionsContentsIntoTheClass()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		ClassDeclaration declaration = new("Shape");
		graph.AddDetached(declaration, new Vector2(400, 100));

		graph.MoveTo(function, new AstLocation(declaration, MembersOf(declaration), 0));

		string code = new CSharpGenerator().Generate(graph.Root);
		StringAssert.Contains(code, "public class Shape", StringComparison.Ordinal);
		StringAssert.Contains(code, "public int area(int scale)", StringComparison.Ordinal);
		StringAssert.Contains(code, "return (scale * 2);", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that moving a node to the root puts the document back the way it was, which is the move
	/// undo makes.
	/// </summary>
	[TestMethod]
	public void MoveTo_RootRestoresTheDocumentAndLoosensWhatHeldIt()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		ClassDeclaration declaration = new("Shape");
		graph.AddDetached(declaration, new Vector2(400, 100));
		graph.MoveTo(function, new AstLocation(declaration, MembersOf(declaration), 0));

		Assert.IsTrue(graph.MoveTo(function, AstLocation.Root));

		Assert.AreSame(function, graph.Root);
		Assert.AreEqual(0, declaration.Members.Count, "the function is not in two places");
		Assert.IsTrue(graph.Detached.Contains(declaration), "the class is kept, loose, exactly as it was created");
	}

	/// <summary>
	/// Tests that the node already rooting the document is not re-rooted, since there would be
	/// nothing to do and nowhere to put what it replaced.
	/// </summary>
	[TestMethod]
	public void MoveTo_RootDoesNothingForTheNodeAlreadyThere()
	{
		AstGraph graph = new(SampleFunction());

		Assert.IsFalse(graph.MoveTo(graph.Root, AstLocation.Root));
		Assert.AreEqual(0, graph.Detached.Count);
	}

	/// <summary>
	/// Tests that a node the graph has never seen is not made the document, since that would replace
	/// the document with something the user cannot see.
	/// </summary>
	[TestMethod]
	public void MoveTo_RootRefusesANodeThatIsNotInTheGraph()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);

		Assert.IsFalse(graph.MoveTo(new ClassDeclaration("Elsewhere"), AstLocation.Root));
		Assert.AreSame(function, graph.Root);
	}

	/// <summary>
	/// Tests that the root is not adopted into its own document, which would make the tree a cycle.
	/// </summary>
	[TestMethod]
	public void MoveTo_RefusesToPutTheRootInsideItself()
	{
		ClassDeclaration outer = new("Outer");
		ClassDeclaration inner = new("Inner");
		outer.Members.Add(inner);
		AstGraph graph = new(outer);

		// The nested class is part of the document rather than loose, so there is nothing there to
		// adopt the root: it is already the root's own subtree.
		Assert.IsFalse(graph.MoveTo(outer, new AstLocation(inner, MembersOf(inner), 0)));

		Assert.AreSame(outer, graph.Root);
		Assert.AreEqual(0, inner.Members.Count);
		Assert.AreSame(inner, outer.Members.Single());
	}

	/// <summary>
	/// Tests that a slot which would not take the root leaves the document alone, rather than
	/// re-rooting for an attachment that then fails.
	/// </summary>
	[TestMethod]
	public void MoveTo_LeavesTheDocumentAloneWhenTheSlotRefusesTheRoot()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraph graph = new(function);
		ReturnStatement statement = new(AstSchema.Unfilled());
		graph.AddDetached(statement, new Vector2(400, 100));

		AstSlot expression = AstSchema.SlotsOf(statement).Single();

		Assert.IsFalse(graph.MoveTo(function, new AstLocation(statement, expression, 0)));
		Assert.AreSame(function, graph.Root);
		Assert.IsTrue(graph.Detached.Contains(statement));
	}

	/// <summary>
	/// Tests the gesture the user actually makes: dragging from a function's output pin to a class's
	/// members pin, and that one press of undo puts the document back.
	/// </summary>
	[TestMethod]
	public void Connect_MakesTheDocumentAMemberOfAClassAndUndoesInOneStep()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphEditor editor = new(function);
		ClassDeclaration declaration = new("Shape");
		editor.Add(declaration, new Vector2(400, 100));

		AstConnectResult result = Connect(editor, function, declaration, "Members", 0);

		Assert.IsTrue(result.Success, result.Message);
		Assert.AreSame(declaration, editor.Graph.Root);
		Assert.AreSame(function, declaration.Members.Single());

		editor.Undo();

		Assert.AreSame(function, editor.Graph.Root);
		Assert.AreEqual(0, declaration.Members.Count);
		Assert.IsTrue(editor.Graph.Detached.Contains(declaration), "undoing the connection leaves the class where creating it did");

		editor.Redo();

		Assert.AreSame(declaration, editor.Graph.Root);
		Assert.AreSame(function, declaration.Members.Single());
	}

	/// <summary>
	/// Connects a node's output pin to a slot pin, the way a drag in the editor does.
	/// </summary>
	/// <param name="editor">The editor under test.</param>
	/// <param name="child">The node to attach.</param>
	/// <param name="parent">The node whose slot to fill.</param>
	/// <param name="slotName">The slot's name.</param>
	/// <param name="index">The position within the slot.</param>
	/// <returns>The connection's outcome.</returns>
	private static AstConnectResult Connect(AstGraphEditor editor, AstNode child, AstNode parent, string slotName, int index)
	{
		Node childNode = editor.Graph.Engine.Nodes.Single(n => ReferenceEquals(editor.Graph.AstNodeFor(n.Id), child));
		Node parentNode = editor.Graph.Engine.Nodes.Single(n => ReferenceEquals(editor.Graph.AstNodeFor(n.Id), parent));
		AstSlot slot = AstSchema.SlotsOf(parent).Single(s => string.Equals(s.Name, slotName, StringComparison.Ordinal));
		Pin pin = parentNode.InputPins.Single(p => string.Equals(p.EffectiveDisplayName, AstGraph.PinLabel(slot, index), StringComparison.Ordinal));

		return editor.Connect(childNode.OutputPins[0].Id, pin.Id);
	}
}
