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
