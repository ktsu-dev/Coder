// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
using System.Linq;
using ktsu.Coder.Ast;

/// <summary>
/// One kind of node the palette can create.
/// </summary>
/// <param name="Category">The group the entry is listed under.</param>
/// <param name="Label">The entry's name in the palette.</param>
/// <param name="Create">Builds a new instance of the node.</param>
/// <param name="Group">The submenu within the category, or null to list the entry directly under it.</param>
/// <remarks>
/// Each entry builds a node that is valid on its own, so a freshly created node can be connected
/// straight away rather than needing fields filled in first. An operand a new node cannot have yet
/// is the same empty literal <see cref="AstSchema.Unfilled"/> produces, which
/// <see cref="AstGraph.Validate"/> then reports as outstanding.
/// <para>
/// <paramref name="Group"/> exists because there are eighteen binary operators: listing them flat
/// would bury the rest of the palette, and offering only one of them would mean every expression is
/// created as an addition and then corrected in the inspector.
/// </para>
/// </remarks>
public sealed record AstNodeTemplate(string Category, string Label, Func<AstNode> Create, string? Group = null);

/// <summary>
/// The set of nodes a user can add to a graph.
/// </summary>
/// <remarks>
/// Deliberately not derived from the AST assembly by reflection. A palette is a curated list — the
/// order and grouping are part of the editor's usability, and an AST type that exists is not
/// necessarily one a user should be handed. The operator entries are the exception: they are
/// generated from the operator enumerations, since an operator the AST gains is one the palette
/// should offer without anyone remembering to add it here.
/// </remarks>
public static class AstNodeCatalog
{
	/// <summary>
	/// Gets every node the palette offers, grouped and ordered for display.
	/// </summary>
	public static IReadOnlyList<AstNodeTemplate> Templates { get; } =
	[
		new("Declarations", "Class", () => new ClassDeclaration("NewClass")),
		new("Declarations", "Function", () => new FunctionDeclaration("newFunction") { ReturnType = "void" }),
		new("Declarations", "Parameter", () => new Parameter("value", "int")),
		new("Declarations", "Variable", () => new VariableDeclaration("value", "int")),
		new("Declarations", "Constant", () => new VariableDeclaration("VALUE", "int", Literal.Number(0)) { IsConstant = true }),
		new("Declarations", "Entry point", () => new EntryPoint()),

		new("Statements", "Return", () => new ReturnStatement()),
		.. Enum.GetValues<AssignmentOperator>().Select(op => new AstNodeTemplate(
			"Statements",
			Spell(op),
			() => new AssignmentStatement(new VariableReference("target"), AstSchema.Unfilled(), op),
			"Assignment")),

		.. Enum.GetValues<BinaryOperator>().Select(op => new AstNodeTemplate(
			"Expressions",
			Spell(op),
			() => new BinaryExpression(AstSchema.Unfilled(), op, AstSchema.Unfilled()),
			"Binary")),
		.. Enum.GetValues<UnaryOperator>().Select(op => new AstNodeTemplate(
			"Expressions",
			Spell(op),
			() => new UnaryExpression(op, AstSchema.Unfilled()),
			"Unary")),
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

	/// <summary>
	/// Lists the submenus a category holds, in palette order.
	/// </summary>
	/// <param name="category">The category to look inside.</param>
	/// <returns>The group names, without the entries listed directly under the category.</returns>
	public static IEnumerable<string> GroupsIn(string category) =>
		InCategory(category)
			.Select(template => template.Group)
			.Where(group => group is not null)
			.Distinct()
			.Select(group => group!);

	/// <summary>
	/// Lists the entries in one submenu of a category.
	/// </summary>
	/// <param name="category">The category to look inside.</param>
	/// <param name="group">The submenu to list, or null for the entries listed directly under the category.</param>
	/// <returns>The entries, in palette order.</returns>
	public static IEnumerable<AstNodeTemplate> InGroup(string category, string? group) =>
		InCategory(category).Where(template => string.Equals(template.Group, group, StringComparison.Ordinal));

	/// <summary>
	/// Labels an operator entry with the symbol it spells and the name it carries.
	/// </summary>
	/// <typeparam name="TOperator">The operator enumeration.</typeparam>
	/// <param name="op">The operator to label.</param>
	/// <returns>The label the palette lists the entry under.</returns>
	/// <remarks>
	/// Both halves are wanted: the symbol is what the user is looking for, and the name is what
	/// distinguishes operators whose symbols read alike at a glance.
	/// </remarks>
	private static string Spell<TOperator>(TOperator op)
		where TOperator : struct, Enum
	{
		string? symbol = op switch
		{
			BinaryOperator binary when OperatorSymbols.TryGetSymbol(binary, out string? found) => found,
			UnaryOperator unary when OperatorSymbols.TryGetSymbol(unary, out string? found) => found,
			AssignmentOperator assignment when OperatorSymbols.TryGetSymbol(assignment, out string? found) => found,
			_ => null,
		};

		return symbol is null ? op.ToString() : $"{symbol}  {op}";
	}
}
