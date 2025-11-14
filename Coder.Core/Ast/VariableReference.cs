// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Ast;

/// <summary>
/// Represents a reference to a variable in an expression.
/// Examples: "x", "result", "myVariable"
/// </summary>
public class VariableReference : Expression
{
	/// <summary>
	/// Gets or sets the name of the variable being referenced.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="VariableReference"/> class.
	/// </summary>
	/// <param name="name">The name of the variable.</param>
	public VariableReference(string name) => Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Name cannot be null or whitespace", nameof(name));

	/// <summary>
	/// Initializes a new instance of the <see cref="VariableReference"/> class.
	/// Used for deserialization.
	/// </summary>
	public VariableReference() => Name = string.Empty;

	/// <summary>
	/// Gets the node type name for this variable reference.
	/// </summary>
	/// <returns>The variable reference type name.</returns>
	public override string GetNodeTypeName() => "VariableReference";

	/// <summary>
	/// Creates a deep clone of this variable reference.
	/// </summary>
	/// <returns>A new instance with the same property values.</returns>
	public override AstNode Clone()
	{
		VariableReference clone = new(Name)
		{
			ExpectedType = ExpectedType
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}

	/// <summary>
	/// Returns a string representation of the variable reference.
	/// </summary>
	/// <returns>The variable name.</returns>
	public override string ToString() => Name;
}
