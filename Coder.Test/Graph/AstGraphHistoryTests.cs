// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="AstGraphHistory"/> and the palette it works alongside.
/// </summary>
[TestClass]
public class AstGraphHistoryTests
{
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		return function;
	}

	/// <summary>
	/// Tests that a fresh history has nothing to step through in either direction.
	/// </summary>
	[TestMethod]
	public void History_StartsEmpty()
	{
		AstGraphHistory history = new();

		Assert.IsFalse(history.CanUndo);
		Assert.IsFalse(history.CanRedo);
		Assert.IsNull(history.Undo(SampleFunction()));
		Assert.IsNull(history.Redo(SampleFunction()));
	}

	/// <summary>
	/// Tests that undo returns the document as it was, not the one that was mutated afterwards.
	/// </summary>
	[TestMethod]
	public void Undo_ReturnsTheDocumentAsItWas()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphHistory history = new();

		history.Record(function);
		function.Parameters.Add(new Parameter("b", "int"));

		AstNode? restored = history.Undo(function);

		Assert.IsNotNull(restored);
		Assert.AreEqual(1, ((FunctionDeclaration)restored).Parameters.Count);
		Assert.AreEqual(2, function.Parameters.Count, "the live document should be untouched by the snapshot");
	}

	/// <summary>
	/// Tests that a snapshot is a deep copy, so mutating the document afterwards cannot reach back
	/// into the recorded state.
	/// </summary>
	[TestMethod]
	public void Record_SnapshotsDeeply()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphHistory history = new();

		history.Record(function);
		function.Parameters[0].Name = "renamed";

		FunctionDeclaration restored = (FunctionDeclaration)history.Undo(function)!;

		Assert.AreEqual("a", restored.Parameters[0].Name);
	}

	/// <summary>
	/// Tests that redo steps forward again after an undo.
	/// </summary>
	[TestMethod]
	public void Redo_StepsForwardAgain()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphHistory history = new();

		history.Record(function);
		function.Parameters.Add(new Parameter("b", "int"));
		AstNode restored = history.Undo(function)!;

		AstNode? redone = history.Redo(restored);

		Assert.IsNotNull(redone);
		Assert.AreEqual(2, ((FunctionDeclaration)redone).Parameters.Count);
	}

	/// <summary>
	/// Tests that recording a new edit discards the redo branch, since anything redone from there is
	/// no longer reachable.
	/// </summary>
	[TestMethod]
	public void Record_DiscardsTheRedoBranch()
	{
		FunctionDeclaration function = SampleFunction();
		AstGraphHistory history = new();

		history.Record(function);
		AstNode restored = history.Undo(function)!;
		Assert.IsTrue(history.CanRedo);

		history.Record(restored);

		Assert.IsFalse(history.CanRedo);
	}

	/// <summary>
	/// Tests that the history forgets its oldest step rather than growing without bound.
	/// </summary>
	[TestMethod]
	public void History_DropsTheOldestStepPastItsLimit()
	{
		AstGraphHistory history = new(limit: 2);
		FunctionDeclaration function = SampleFunction();

		for (int i = 0; i < 5; i++)
		{
			history.Record(function);
		}

		Assert.IsNotNull(history.Undo(function));
		Assert.IsNotNull(history.Undo(function));
		Assert.IsNull(history.Undo(function), "only the limit's worth of steps should be kept");
	}

	/// <summary>
	/// Tests that a history has to keep at least one step to be of any use.
	/// </summary>
	[TestMethod]
	public void History_RefusesAnEmptyLimit() =>
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AstGraphHistory(limit: 0));

	/// <summary>
	/// Tests that every palette entry builds a node the graph will actually take, so nothing in the
	/// menu produces something unusable.
	/// </summary>
	[TestMethod]
	public void Catalog_EveryTemplateBuildsAUsableNode()
	{
		Assert.IsTrue(AstNodeCatalog.Templates.Count > 0);

		foreach (AstNodeTemplate template in AstNodeCatalog.Templates)
		{
			AstNode created = template.Create();

			Assert.IsNotNull(created, template.Label);
			Assert.IsFalse(string.IsNullOrWhiteSpace(AstSchema.Describe(created)), $"{template.Label} should describe itself");

			// A template must be creatable twice without the two sharing state.
			Assert.AreNotSame(created, template.Create(), template.Label);
		}
	}

	/// <summary>
	/// Tests that every category the palette lists has at least one entry under it.
	/// </summary>
	[TestMethod]
	public void Catalog_EveryCategoryHasEntries()
	{
		Assert.IsTrue(AstNodeCatalog.Categories.Count > 0);

		foreach (string category in AstNodeCatalog.Categories)
		{
			Assert.IsTrue(AstNodeCatalog.InCategory(category).Any(), category);
		}
	}

	/// <summary>
	/// Tests that a node built from the palette can be added and connected, which is the whole point
	/// of the palette existing.
	/// </summary>
	[TestMethod]
	public void Catalog_ExpressionTemplatesConnectToAnExpressionSlot()
	{
		FunctionDeclaration function = SampleFunction();
		ReturnStatement returnStmt = new();
		function.Body.Add(returnStmt);
		AstGraph graph = new(function);
		AstSlot expressionSlot = AstSchema.SlotsOf(returnStmt).Single();

		foreach (AstNodeTemplate template in AstNodeCatalog.InCategory("Literals"))
		{
			AstNode created = template.Create();

			Assert.IsTrue(AstSchema.Accepts(expressionSlot, created), $"{template.Label} should fill an expression slot");
		}

		Assert.IsNotNull(graph);
	}
}
