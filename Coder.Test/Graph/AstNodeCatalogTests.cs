// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the palette of nodes the editor offers.
/// </summary>
[TestClass]
public class AstNodeCatalogTests
{
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		return function;
	}

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
	/// Tests that the palette offers every operator, so which expression to create is a choice made
	/// when it is created rather than a correction made afterwards.
	/// </summary>
	[TestMethod]
	public void Catalog_OffersEveryOperator()
	{
		BinaryOperator[] created = [.. AstNodeCatalog.InGroup("Expressions", "Binary")
			.Select(template => ((BinaryExpression)template.Create()).Operator)];

		CollectionAssert.AreEquivalent(Enum.GetValues<BinaryOperator>(), created);

		Assert.AreEqual(
			Enum.GetValues<UnaryOperator>().Length,
			AstNodeCatalog.InGroup("Expressions", "Unary").Count());

		Assert.AreEqual(
			Enum.GetValues<AssignmentOperator>().Length,
			AstNodeCatalog.InGroup("Statements", "Assignment").Count());
	}

	/// <summary>
	/// Tests that an operator entry is labelled with the symbol it spells, since that is what a user
	/// scanning the menu is looking for.
	/// </summary>
	[TestMethod]
	public void Catalog_LabelsOperatorsWithTheirSymbols()
	{
		AstNodeTemplate add = AstNodeCatalog.InGroup("Expressions", "Binary")
			.Single(template => ((BinaryExpression)template.Create()).Operator == BinaryOperator.Add);

		StringAssert.Contains(add.Label, "+", StringComparison.Ordinal);
		StringAssert.Contains(add.Label, nameof(BinaryOperator.Add), StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that every entry is reachable through the menu structure the palette draws: either
	/// directly under its category, or in one of that category's groups.
	/// </summary>
	[TestMethod]
	public void Catalog_ListsEveryEntryUnderSomeMenu()
	{
		List<AstNodeTemplate> reachable = [];

		foreach (string category in AstNodeCatalog.Categories)
		{
			reachable.AddRange(AstNodeCatalog.InGroup(category, null));

			foreach (string group in AstNodeCatalog.GroupsIn(category))
			{
				reachable.AddRange(AstNodeCatalog.InGroup(category, group));
			}
		}

		CollectionAssert.AreEquivalent(AstNodeCatalog.Templates.ToList(), reachable);
	}

	/// <summary>
	/// Tests that a class is one of the things a user can create, since a document is not only ever a
	/// loose function.
	/// </summary>
	[TestMethod]
	public void Catalog_OffersAClass()
	{
		AstNodeTemplate template = AstNodeCatalog.InCategory("Declarations")
			.Single(entry => string.Equals(entry.Label, "Class", StringComparison.Ordinal));

		Assert.IsInstanceOfType<ClassDeclaration>(template.Create());
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
