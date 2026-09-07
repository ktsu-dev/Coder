// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
using ktsu.Coder.Ast;

/// <summary>
/// One kind of node the palette can create.
/// </summary>
/// <param name="Category">The group the entry is listed under.</param>
/// <param name="Label">The entry's name in the palette.</param>
/// <param name="Create">Builds a new instance of the node.</param>
/// <remarks>
/// Each entry builds a node that is valid on its own, so a freshly created node can be connected
/// straight away rather than needing fields filled in first. An operand a new node cannot have yet
/// is the same empty literal <see cref="AstSchema.Unfilled"/> produces, which
/// <see cref="AstGraph.Validate"/> then reports as outstanding.
/// </remarks>
public sealed record AstNodeTemplate(string Category, string Label, Func<AstNode> Create);

/// <summary>
/// The set of nodes a user can add to a graph.
/// </summary>
/// <remarks>
/// Deliberately not derived from the AST assembly by reflection. A palette is a curated list — the
/// order and grouping are part of the editor's usability, and an AST type that exists is not
/// necessarily one a user should be handed.
/// </remarks>
public static class AstNodeCatalog
{
	/// <summary>
	/// Gets every node the palette offers, grouped and ordered for display.
	/// </summary>
	public static IReadOnlyList<AstNodeTemplate> Templates { get; } =
	[
		new("Declarations", "Function", () => new FunctionDeclaration("newFunction") { ReturnType = "void" }),
		new("Declarations", "Parameter", () => new Parameter("value", "int")),
		new("Declarations", "Variable", () => new VariableDeclaration("value", "int")),

		new("Statements", "Return", () => new ReturnStatement()),
		new("Statements", "Assignment", () => new AssignmentStatement(
			new VariableReference("target"), AstSchema.Unfilled())),

		new("Expressions", "Binary", () => new BinaryExpression(
			AstSchema.Unfilled(), BinaryOperator.Add, AstSchema.Unfilled())),
		new("Expressions", "Unary", () => new UnaryExpression(UnaryOperator.Negate, AstSchema.Unfilled())),
		new("Expressions", "Variable reference", () => new VariableReference("value")),

		new("Literals", "Text", () => Literal.Text("text")),
		new("Literals", "Number", () => Literal.Number(0)),
		new("Literals", "Decimal", () => Literal.DecimalValue(0)),
		new("Literals", "Boolean", () => Literal.Bool(true)),
	];

	/// <summary>
	/// Gets the categories, in the order the palette should list them.
	/// </summary>
	public static IReadOnlyList<string> Categories { get; } =
		[.. Templates.Select(template => template.Category).Distinct()];

	/// <summary>
	/// Lists the entries in one category.
	/// </summary>
	/// <param name="category">The category to list.</param>
	/// <returns>The entries, in palette order.</returns>
	public static IEnumerable<AstNodeTemplate> InCategory(string category) =>
		Templates.Where(template => string.Equals(template.Category, category, StringComparison.Ordinal));
}
