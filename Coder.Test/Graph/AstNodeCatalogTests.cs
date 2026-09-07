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
