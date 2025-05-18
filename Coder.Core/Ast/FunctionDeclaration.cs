// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

using System.Collections.Generic;

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


<<<<<<< TODO: Unmerged change from project 'Coder.Core(net9.0)', Before:
    public List<Parameter> Parameters { get; set; } = new List<Parameter>();

    /// <summary>
    /// Gets or sets a list of statements that make up the function body.
    /// </summary>
    public List<AstNode> Body { get; set; } = new List<AstNode>();
=======
    public List<Parameter> Parameters { get; set; } = [];

	/// <summary>
	/// Gets or sets a list of statements that make up the function body.
	/// </summary>
	public List<AstNode> Body { get; set; } = new List<AstNode>();
>>>>>>> After
	/// <summary>
	/// Gets or sets a list of parameters for the function.
	/// </summary>
	public List<Parameter> Parameters { get; set; } = [];

	/// <summary>
	/// Gets or sets a list of statements that make up the function body.
	/// </summary>
	public List<AstNode> Body { get; set; } = [];

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
		var clone = new FunctionDeclaration
		{
			Name = Name,
			ReturnType = ReturnType,
			Metadata = Metadata != null ? new Dictionary<string, object?>(Metadata) : null

<<<<<<< TODO: Unmerged change from project 'Coder.Core(net9.0)', Before:
        };

        // Clone parameters
        foreach (var parameter in Parameters)
=======
		};

		// Clone parameters
		foreach (var parameter in Parameters)
>>>>>>> After
		};

		// Clone parameters
		foreach (var parameter in Parameters)
		{
			clone.Parameters.Add((Parameter)parameter.Clone());
		}

		// Clone body statements
		foreach (var statement in Body)
		{
			clone.Body.Add(statement.Clone());
		}

		// Clone the children dictionary
		foreach (var (key, child) in Children)
		{
			clone.Children[key] = child.Clone();
		}

		return clone;

<<<<<<< TODO: Unmerged change from project 'Coder.Core(net9.0)', Before:
    }
} 
=======
	}
}
>>>>>>> After
	}
}
