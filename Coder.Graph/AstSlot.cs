// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

/// <summary>
/// One named place a child node can sit inside its parent.
/// </summary>
/// <param name="Name">The slot's name, as the editor labels its pin.</param>
/// <param name="Cardinality">Whether the slot holds one child or a sequence of them.</param>
/// <param name="Accepts">The shape of node the slot will take.</param>
/// <remarks>
/// The AST stores children three different ways — typed properties (<c>BinaryExpression.Left</c>),
/// collections (<c>FunctionDeclaration.Body</c>) and <see cref="ktsu.Coder.Ast.AstCompositeNode"/>'s
/// keyed dictionary (<c>ReturnStatement.Expression</c>). A slot is the uniform view over all three,
/// so the editor can draw a pin per slot without knowing which storage a given node uses.
/// </remarks>
public sealed record AstSlot(string Name, AstSlotCardinality Cardinality, AstSlotKind Accepts);

/// <summary>
/// How many children a slot holds.
/// </summary>
public enum AstSlotCardinality
{
	/// <summary>The slot holds at most one child, and connecting a second replaces the first.</summary>
	One,

	/// <summary>The slot holds an ordered sequence, and the editor draws one pin per child plus a free one.</summary>
	Many,
}

/// <summary>
/// The shape of node a slot will take, which is what makes a connection legal or not.
/// </summary>
public enum AstSlotKind
{
	/// <summary>Anything that evaluates to a value, including the legacy leaf nodes.</summary>
	Expression,

	/// <summary>Anything that can stand as a statement in a body.</summary>
	Statement,

	/// <summary>A function parameter, which is neither an expression nor a statement.</summary>
	Parameter,

	/// <summary>A declaration a class can hold: a method, a field, or a nested class.</summary>
	Member,
}
