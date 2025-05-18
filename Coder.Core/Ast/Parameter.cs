// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

using System.Collections.Generic;

/// <summary>
/// Represents a parameter in a function declaration.
/// </summary>
public class Parameter : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Parameter"/> class.
	/// </summary>
	public Parameter()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Parameter"/> class with a name.
	/// </summary>
	/// <param name="name">The name of the parameter.</param>
	public Parameter(string name) => Name = name;

	/// <summary>
	/// Initializes a new instance of the <see cref="Parameter"/> class with a name and type.
	/// </summary>
	/// <param name="name">The name of the parameter.</param>
	/// <param name="type">The type of the parameter.</param>
	public Parameter(string name, string type)
	{
		Name = name;
		Type = type;
	}

	/// <summary>
	/// Gets or sets the name of the parameter.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the type of the parameter.
	/// </summary>
	public string? Type { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the parameter is optional.
	/// </summary>
	public bool IsOptional { get; set; }

	/// <summary>
	/// Gets or sets the default value of the parameter if it is optional.
	/// </summary>
	public string? DefaultValue { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "Parameter";

	/// <summary>
	/// Creates a deep clone of this parameter.
	/// </summary>
	/// <returns>A new instance of the parameter with the same properties.</returns>
	public override AstNode Clone()
	{
		return new Parameter
		{
			Name = Name,
			Type = Type,
			IsOptional = IsOptional,
			DefaultValue = DefaultValue,
			Metadata = Metadata != null ? new Dictionary<string, object?>(Metadata) : null

<<<<<<< TODO: Unmerged change from project 'Coder.Core(net9.0)', Before:
        };
    }
} 
=======
		};
	}
}
>>>>>>> After
		};
	}
}
