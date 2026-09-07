// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
using ktsu.Coder.Ast;

/// <summary>
/// Undo and redo for an <see cref="AstGraph"/>, by snapshotting the document.
/// </summary>
/// <remarks>
/// Snapshots rather than inverse operations. The AST already knows how to deep-clone itself, so a
/// snapshot is one call and is correct by construction; an inverse-operation stack would need every
/// edit to describe its own reversal, and would be wrong the first time one forgot. A code AST is
/// small enough that copying it per edit costs nothing a person can perceive.
/// <para>
/// The graph's <see cref="AstGraph.Root"/> is replaced wholesale on undo, so positions are keyed to
/// nodes that no longer exist and the restored document is laid out afresh. That is the honest
/// trade for snapshot undo, and the force-directed layout settles it quickly.
/// </para>
/// </remarks>
/// <param name="limit">How many undo steps to keep. Older steps fall off the bottom.</param>
public sealed class AstGraphHistory(int limit = 64)
{
	private readonly List<AstNode> past = [];
	private readonly List<AstNode> future = [];
	private readonly int limit = limit > 0
		? limit
		: throw new ArgumentOutOfRangeException(nameof(limit), "A history must keep at least one step.");

	/// <summary>
	/// Gets a value indicating whether there is anything to undo.
	/// </summary>
	public bool CanUndo => past.Count > 0;

	/// <summary>
	/// Gets a value indicating whether there is anything to redo.
	/// </summary>
	public bool CanRedo => future.Count > 0;

	/// <summary>
	/// Records the document as it stands, before an edit changes it.
	/// </summary>
	/// <param name="root">The document to snapshot.</param>
	/// <remarks>Call this immediately before mutating, not after: the snapshot is the state to come back to.</remarks>
	public void Record(AstNode root)
	{
		Ensure.NotNull(root);

		past.Add(root.DeepClone());
		if (past.Count > limit)
		{
			past.RemoveAt(0);
		}

		// A new edit is a new branch of history, so anything redone from here is unreachable.
		future.Clear();
	}

	/// <summary>
	/// Steps back one edit.
	/// </summary>
	/// <param name="current">The document as it stands, which becomes the redo step.</param>
	/// <returns>The previous document, or null if there is nothing to undo.</returns>
	public AstNode? Undo(AstNode current)
	{
		Ensure.NotNull(current);

		if (past.Count == 0)
		{
			return null;
		}

		AstNode previous = past[^1];
		past.RemoveAt(past.Count - 1);
		future.Add(current.DeepClone());
		return previous;
	}

	/// <summary>
	/// Steps forward one edit.
	/// </summary>
	/// <param name="current">The document as it stands, which becomes the undo step.</param>
	/// <returns>The next document, or null if there is nothing to redo.</returns>
	public AstNode? Redo(AstNode current)
	{
		Ensure.NotNull(current);

		if (future.Count == 0)
		{
			return null;
		}

		AstNode next = future[^1];
		future.RemoveAt(future.Count - 1);
		past.Add(current.DeepClone());
		return next;
	}

	/// <summary>
	/// Forgets every recorded step.
	/// </summary>
	public void Clear()
	{
		past.Clear();
		future.Clear();
	}
}
