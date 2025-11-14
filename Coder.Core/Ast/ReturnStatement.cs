// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Ast;

/// <summary>
/// Represents a return statement in the abstract syntax tree.
/// </summary>
public class ReturnStatement : AstCompositeNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReturnStatement"/> class.
	/// </summary>
	public ReturnStatement()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ReturnStatement"/> class with an expression.
	/// </summary>
	/// <param name="expression">The expression to return.</param>
	public ReturnStatement(AstNode expression) => SetExpression(expression);

	/// <summary>
	/// Initializes a new instance of the <see cref="ReturnStatement"/> class with an integer literal.
	/// </summary>
	/// <param name="value">The integer value to return.</param>
	public ReturnStatement(int value) => SetExpression(new AstLeafNode<int>(value));

	/// <summary>
	/// Initializes a new instance of the <see cref="ReturnStatement"/> class with a string literal.
	/// </summary>
	/// <param name="value">The string value to return.</param>
	public ReturnStatement(string value) => SetExpression(new AstLeafNode<string>(value));

	/// <summary>
	/// Initializes a new instance of the <see cref="ReturnStatement"/> class with a boolean literal.
	/// </summary>
	/// <param name="value">The boolean value to return.</param>
	public ReturnStatement(bool value) => SetExpression(new AstLeafNode<bool>(value));

	/// <summary>
	/// Gets the expression being returned, if any.
	/// </summary>
	public AstNode? Expression => GetChild("Expression");

	/// <summary>
	/// Sets the expression being returned.
	/// </summary>
	/// <param name="expression">The expression to return.</param>
	public void SetExpression(AstNode? expression)
	{
		if (expression != null)
		{
			SetChild("Expression", expression);
		}
		else
		{
			RemoveChild("Expression");
		}
	}

	/// <summary>
	/// Gets the type name of this node for serialization.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "ReturnStatement";

	/// <summary>
	/// Creates a deep clone of this return statement.
	/// </summary>
	/// <returns>A new instance of the return statement with the same properties and cloned children.</returns>
	public override AstNode Clone()
	{
		ReturnStatement clone = new();

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		// Clone the children dictionary which includes the Expression
		foreach ((string key, AstNode child) in Children)
		{
			clone.Children[key] = child.Clone();
		}

		return clone;
	}
}
