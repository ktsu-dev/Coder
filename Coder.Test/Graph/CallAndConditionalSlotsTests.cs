// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that <see cref="AstSchema"/>, <see cref="AstFields"/> and <see cref="AstNodeCatalog"/> know
/// the three nodes that carry an expression's body: <see cref="CallExpression"/>,
/// <see cref="ConditionalExpression"/> and <see cref="ExpressionStatement"/>.
/// </summary>
/// <remarks>
/// A node the AST has and the schema does not is one the editor cannot wire up, and the failure is
/// silent — the palette offers it and its pins never appear. These cover the same ground for the new
/// nodes that <see cref="AstSchemaTests"/> covers for the rest.
/// </remarks>
[TestClass]
public class CallAndConditionalSlotsTests
{
	private static readonly string[] CallSlots = ["Receiver", "Arguments"];
	private static readonly string[] ConditionalSlots = ["Condition", "WhenTrue", "WhenFalse"];
	private static readonly string[] ExpressionStatementSlots = ["Expression"];

	private static AstSlot Slot(AstNode node, string name) =>
		AstSchema.SlotsOf(node).Single(slot => slot.Name == name);

	private static ConditionalExpression NewConditional() => new(
		new VariableReference("ready"),
		new VariableReference("go"),
		new VariableReference("wait"));

	/// <summary>
	/// Tests that each of the three nodes exposes the slots it should, in drawing order.
	/// </summary>
	[TestMethod]
	public void SlotsOf_DescribesTheNewNodes()
	{
		CollectionAssert.AreEqual(
			CallSlots,
			AstSchema.SlotsOf(new CallExpression("f")).Select(slot => slot.Name).ToArray());

		CollectionAssert.AreEqual(
			ConditionalSlots,
			AstSchema.SlotsOf(NewConditional()).Select(slot => slot.Name).ToArray());

		CollectionAssert.AreEqual(
			ExpressionStatementSlots,
			AstSchema.SlotsOf(new ExpressionStatement()).Select(slot => slot.Name).ToArray());
	}

	/// <summary>
	/// Tests that a call with no receiver reports the slot as empty rather than throwing, since a
	/// free function is a finished call rather than an unfinished member one.
	/// </summary>
	[TestMethod]
	public void ChildrenOf_ReportsAnAbsentReceiverAsEmpty()
	{
		CallExpression call = new("sqrt");

		Assert.AreEqual(0, AstSchema.ChildrenOf(call, Slot(call, "Receiver")).Count);
	}

	/// <summary>
	/// Tests that a receiver and arguments attach through the schema and read back in order.
	/// </summary>
	[TestMethod]
	public void TryAttach_FillsACall()
	{
		CallExpression call = new("translate");
		VariableReference receiver = new("point");

		Assert.IsTrue(AstSchema.TryAttach(call, Slot(call, "Receiver"), receiver));
		Assert.IsTrue(AstSchema.TryAttach(call, Slot(call, "Arguments"), new VariableReference("dx")));
		Assert.IsTrue(AstSchema.TryAttach(call, Slot(call, "Arguments"), new VariableReference("dy")));

		Assert.AreSame(receiver, call.Receiver);
		Assert.AreEqual(2, call.Arguments.Count);
		Assert.AreEqual("dx", ((VariableReference)call.Arguments[0]).Name);
		Assert.AreEqual("dy", ((VariableReference)call.Arguments[1]).Name);
	}

	/// <summary>
	/// Tests that attaching inside an existing argument list swaps that entry rather than appending,
	/// so reconnecting one pin does not reorder the others.
	/// </summary>
	[TestMethod]
	public void TryAttachAt_SwapsAnArgumentInPlace()
	{
		CallExpression call = new("clamp")
		{
			Arguments = { new VariableReference("a"), new VariableReference("b") },
		};

		Assert.IsTrue(AstSchema.TryAttachAt(call, Slot(call, "Arguments"), 0, new VariableReference("z")));

		Assert.AreEqual(2, call.Arguments.Count);
		Assert.AreEqual("z", ((VariableReference)call.Arguments[0]).Name);
		Assert.AreEqual("b", ((VariableReference)call.Arguments[1]).Name);
	}

	/// <summary>
	/// Tests that detaching a receiver clears it, leaving a free function rather than a placeholder.
	/// </summary>
	/// <remarks>
	/// The other operand slots cannot be cleared and take the placeholder instead, because a binary
	/// expression with one operand is unfinished. A call without a receiver is not.
	/// </remarks>
	[TestMethod]
	public void TryDetachAt_ClearsAReceiverRatherThanPlaceholderingIt()
	{
		CallExpression call = new(new VariableReference("point"), "translate");

		Assert.IsTrue(AstSchema.TryDetachAt(call, Slot(call, "Receiver"), 0));
		Assert.IsNull(call.Receiver);

		// Nothing left to detach, which is reported rather than repeated.
		Assert.IsFalse(AstSchema.TryDetachAt(call, Slot(call, "Receiver"), 0));
	}

	/// <summary>
	/// Tests that detaching an argument removes it from the list.
	/// </summary>
	[TestMethod]
	public void TryDetachAt_RemovesAnArgument()
	{
		CallExpression call = new("clamp")
		{
			Arguments = { new VariableReference("a"), new VariableReference("b") },
		};

		Assert.IsTrue(AstSchema.TryDetachAt(call, Slot(call, "Arguments"), 0));

		Assert.AreEqual(1, call.Arguments.Count);
		Assert.AreEqual("b", ((VariableReference)call.Arguments[0]).Name);
	}

	/// <summary>
	/// Tests that each of a conditional's three operands attaches and detaches independently, and
	/// that detaching leaves the placeholder the rest of the AST uses for an outstanding operand.
	/// </summary>
	/// <param name="slotName">The slot under test.</param>
	[TestMethod]
	[DataRow("Condition")]
	[DataRow("WhenTrue")]
	[DataRow("WhenFalse")]
	public void ConditionalOperands_AttachAndDetachIndependently(string slotName)
	{
		ConditionalExpression conditional = NewConditional();
		AstSlot slot = Slot(conditional, slotName);
		VariableReference replacement = new("replaced");

		Assert.IsTrue(AstSchema.TryAttach(conditional, slot, replacement));
		Assert.AreSame(replacement, AstSchema.ChildrenOf(conditional, slot).Single());

		Assert.IsTrue(AstSchema.TryDetachAt(conditional, slot, 0));
		Assert.IsTrue(AstSchema.IsUnfilled(AstSchema.ChildrenOf(conditional, slot).Single()));
	}

	/// <summary>
	/// Tests that an expression statement's slot attaches and detaches the same way.
	/// </summary>
	[TestMethod]
	public void ExpressionStatementSlot_AttachesAndDetaches()
	{
		ExpressionStatement statement = new();
		AstSlot slot = Slot(statement, "Expression");
		CallExpression call = new("reset");

		Assert.IsTrue(AstSchema.TryAttach(statement, slot, call));
		Assert.AreSame(call, statement.Expression);

		Assert.IsTrue(AstSchema.TryDetachAt(statement, slot, 0));
		Assert.IsTrue(AstSchema.IsUnfilled(statement.Expression));
	}

	/// <summary>
	/// Tests that a statement slot takes a call, which is what makes a void call reachable from the
	/// editor at all.
	/// </summary>
	[TestMethod]
	public void AFunctionBody_TakesAnExpressionStatement()
	{
		FunctionDeclaration function = new("update");
		AstSlot body = Slot(function, "Body");

		Assert.IsTrue(AstSchema.TryAttach(function, body, new ExpressionStatement(new CallExpression("reset"))));

		Assert.AreEqual(1, function.Body.Count);
	}

	/// <summary>
	/// Tests that the new nodes get captions naming what distinguishes them, rather than falling
	/// back to their type name.
	/// </summary>
	[TestMethod]
	public void Describe_NamesTheNewNodes()
	{
		Assert.AreEqual("call sqrt", AstSchema.Describe(new CallExpression("sqrt")));
		Assert.AreEqual("call <unnamed>", AstSchema.Describe(new CallExpression()));
		Assert.AreEqual("conditional", AstSchema.Describe(NewConditional()));
		Assert.AreEqual("expression", AstSchema.Describe(new ExpressionStatement()));
	}

	/// <summary>
	/// Tests that a call's callee is editable from the inspector, so a call created from the palette
	/// can be pointed at something without editing the YAML by hand.
	/// </summary>
	[TestMethod]
	public void Callee_IsEditableFromTheInspector()
	{
		CallExpression call = new("placeholder");

		AstField callee = AstFields.Of(call).Single(field => field.Name == "Callee");
		Assert.AreEqual(AstFieldKind.Text, callee.Kind);
		Assert.AreEqual("placeholder", callee.Value);

		Assert.IsTrue(AstFields.TryWrite(call, "Callee", "sqrt"));
		Assert.AreEqual("sqrt", call.Callee);
	}

	/// <summary>
	/// Tests that an empty callee is refused, matching how a variable reference's name behaves: a
	/// half-typed field leaves the document alone rather than writing a call to nothing.
	/// </summary>
	[TestMethod]
	public void AnEmptyCallee_IsRefused()
	{
		CallExpression call = new("sqrt");

		Assert.IsFalse(AstFields.TryWrite(call, "Callee", string.Empty));
		Assert.AreEqual("sqrt", call.Callee);
	}

	/// <summary>
	/// Tests that the palette offers all three, since a node the catalog omits cannot be created.
	/// </summary>
	[TestMethod]
	public void ThePalette_OffersTheNewNodes()
	{
		Assert.IsTrue(AstNodeCatalog.Templates.Any(template => template.Create() is CallExpression), "call");
		Assert.IsTrue(AstNodeCatalog.Templates.Any(template => template.Create() is ConditionalExpression), "conditional");
		Assert.IsTrue(AstNodeCatalog.Templates.Any(template => template.Create() is ExpressionStatement), "expression statement");
	}

	/// <summary>
	/// Tests that what the palette creates is valid on its own, which is what lets a freshly created
	/// node be connected rather than filled in first.
	/// </summary>
	[TestMethod]
	public void WhatThePaletteCreates_IsReadyToConnect()
	{
		foreach (AstNodeTemplate template in AstNodeCatalog.Templates
			.Where(template => template.Create() is CallExpression or ConditionalExpression or ExpressionStatement))
		{
			AstNode node = template.Create();

			foreach (AstSlot slot in AstSchema.SlotsOf(node))
			{
				// Reading a slot must not throw, whether or not anything is in it yet.
				AstSchema.ChildrenOf(node, slot);
			}
		}
	}
}
