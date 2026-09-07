// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using ktsu.Coder.Ast;

/// <summary>
/// One reason the graph is not yet a complete document.
/// </summary>
/// <param name="Node">The AST node the problem is about.</param>
/// <param name="Message">What is wrong, phrased for a problems panel.</param>
public sealed record AstGraphProblem(AstNode Node, string Message);

/// <summary>
/// The outcome of trying to connect two pins.
/// </summary>
/// <param name="Success">Whether the connection was made.</param>
/// <param name="Message">What happened, or why it was refused.</param>
/// <remarks>
/// A refusal is an ordinary outcome in a node editor — the user aimed at the wrong pin — so it is a
/// returned value rather than an exception.
/// </remarks>
public sealed record AstConnectResult(bool Success, string Message);

/// <summary>
/// Where a node sits: which slot of which parent, and at what position within it.
/// </summary>
/// <param name="Parent">The node holding it, or null when it has no parent.</param>
/// <param name="Slot">The slot it fills, or null when it has no parent.</param>
/// <param name="Index">Its position within a variadic slot; zero for a single-valued one.</param>
/// <remarks>
/// A location names AST nodes rather than editor pins, because pin identifiers are reassigned by
/// every rebuild while the nodes outlive them. That is what lets an undo step recorded before an
/// edit still mean the same thing afterwards.
/// </remarks>
public readonly record struct AstLocation(AstNode? Parent, AstSlot? Slot, int Index)
{
	/// <summary>
	/// Gets the location of a node that is in the graph but has no parent.
	/// </summary>
	public static AstLocation Detached => new(null, null, 0);
}
