// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

using System.Collections.Generic;
using ktsu.DeepClone;

/// <summary>
/// Represents a composite node in the AST structure - a node that can contain other nodes.
/// Examples include function declarations, code blocks, and other hierarchical elements.
/// </summary>
public abstract class AstCompositeNode : AstNode
{
	/// <summary>
	/// Gets or sets the collection of child nodes within this composite node.
	/// </summary>
	public Dictionary<string, AstNode> Children { get; private init; } = [];

	/// <summary>
	/// Gets the child node with the specified key.
	/// </summary>
	/// <param name="key">The key of the child node to retrieve.</param>
	/// <returns>The child node if found; otherwise, null.</returns>
	public AstNode? GetChild(string key) => Children.TryGetValue(key, out var child) ? child : null;

	/// <summary>
	/// Adds or updates a child node with the specified key.
	/// </summary>
	/// <param name="key">The key to associate with the child node.</param>
	/// <param name="node">The child node to add or update.</param>
	public void SetChild(string key, AstNode node) => Children[key] = node;

	/// <summary>
	/// Removes the child node with the specified key.
	/// </summary>
	/// <param name="key">The key of the child node to remove.</param>
	/// <returns>True if the child node was found and removed; otherwise, false.</returns>
	public bool RemoveChild(string key) => Children.Remove(key);

	public override AstNode DeepClone()
	{
		return new AstCompositeNode
		{
			Children = new Dictionary<string, AstNode>(Children),
			Metadata = new Dictionary<string, object?>(Metadata)
		};
	}
}
