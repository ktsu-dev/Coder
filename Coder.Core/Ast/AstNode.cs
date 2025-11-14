// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Ast;

/// <summary>
/// Represents an abstract base class for all AST (Abstract Syntax Tree) nodes.
/// AST nodes are used to represent code structures in a language-agnostic way.
/// </summary>
public abstract class AstNode
{
	/// <summary>
	/// Gets additional metadata about this node.
	/// This can be used to store language-specific information that doesn't fit
	/// into the standard node structure.
	/// </summary>
	public Dictionary<string, object?> Metadata { get; } = [];

	/// <summary>
	/// Gets the type name of this AST node, which is used during serialization.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public abstract string GetNodeTypeName();

	/// <summary>
	/// Creates a deep clone of this AST node.
	/// </summary>
	/// <returns>A new instance of the AST node with the same property values.</returns>
	public abstract AstNode Clone();

	/// <summary>
	/// Creates a deep clone of this AST node.
	/// </summary>
	/// <returns>A new instance of the AST node with the same property values.</returns>
	public AstNode DeepClone() => Clone();
}
