// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

/// <summary>
/// Represents a leaf node in the AST structure - a node that contains a value but no child nodes.
/// Examples include literals (strings, numbers), identifiers, etc.
/// </summary>
/// <typeparam name="T">The type of value contained in this leaf node.</typeparam>
public class AstLeafNode<T> : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AstLeafNode{T}"/> class.
	/// </summary>
	public AstLeafNode()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AstLeafNode{T}"/> class with the specified value.
	/// </summary>
	/// <param name="value">The value of this leaf node.</param>
	public AstLeafNode(T value) => Value = value;

	/// <summary>
	/// Gets or sets the value contained in this leaf node.
	/// </summary>
	public T? Value { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type including the generic type parameter.</returns>
	public override string GetNodeTypeName() => $"Leaf<{typeof(T).Name}>";

	/// <summary>
	/// Creates a deep clone of this leaf node.
	/// </summary>
	/// <returns>A new instance of the leaf node with the same value.</returns>
	public override AstNode Clone()
	{
		AstLeafNode<T> clone = new()
		{
			Value = Value
		};

		// Copy metadata
		foreach ((string key, object value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}
