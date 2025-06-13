// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a function declaration in the abstract syntax tree.
/// </summary>
public class FunctionDeclaration : AstCompositeNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FunctionDeclaration"/> class.
	/// </summary>
	public FunctionDeclaration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FunctionDeclaration"/> class with a name.
	/// </summary>
	/// <param name="name">The name of the function.</param>
	public FunctionDeclaration(string name) => Name = name;

	/// <summary>
	/// Gets or sets the name of the function.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the return type of the function.
	/// </summary>
	public string? ReturnType { get; set; }

	/// <summary>
	/// Gets or sets a list of parameters for the function.
	/// </summary>
	public Collection<Parameter> Parameters { get; init; } = [];

	/// <summary>
	/// Gets or sets a list of statements that make up the function body.
	/// </summary>
	public Collection<AstNode> Body { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "FunctionDeclaration";

	/// <summary>
	/// Creates a deep clone of this function declaration.
	/// </summary>
	/// <returns>A new instance of the function declaration with the same properties and cloned children.</returns>
	public override AstNode Clone()
	{
		FunctionDeclaration clone = new()
		{
			Name = Name,
			ReturnType = ReturnType
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		// Clone parameters
		foreach (Parameter parameter in Parameters)
		{
			clone.Parameters.Add((Parameter)parameter.Clone());
		}

		// Clone body statements
		foreach (AstNode statement in Body)
		{
			clone.Body.Add(statement.Clone());
		}

		// Clone the children dictionary
		foreach ((string key, AstNode child) in Children)
		{
			clone.Children[key] = child.Clone();
		}

		return clone;
	}
}
