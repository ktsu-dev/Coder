// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

using System.Collections.Generic;

/// <summary>
/// Represents a composite node in the AST structure - a node that can contain other nodes.
/// Examples include function declarations, code blocks, and other hierarchical elements.
/// </summary>
public abstract class AstCompositeNode : AstNode
{
	/// <summary>
	/// Gets or sets the collection of child nodes within this composite node.
	/// </summary>
	public Dictionary<string, AstNode> Children { get; set; } = [];

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

	/// <summary>
	/// Creates a deep clone of this composite node.
	/// This must be implemented by derived classes to ensure proper cloning.
	/// </summary>
	/// <returns>A new instance of the composite node with the same property values and cloned children.</returns>
	public abstract override AstNode Clone();

	/// <summary>
	/// Helper method for cloning the children of this composite node.
	/// </summary>
	/// <returns>A new dictionary containing cloned children.</returns>
	protected Dictionary<string, AstNode> CloneChildren()
	{
		var clonedChildren = new Dictionary<string, AstNode>();

		foreach (var (key, child) in Children)
		{
			clonedChildren[key] = child.Clone();
		}

		return clonedChildren;
	}
}
