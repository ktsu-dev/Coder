// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Builds a value of a type.
/// </summary>
/// <remarks>
/// The one expression that needs a type rather than a name, which is why it could not exist before
/// <see cref="TypeReference"/> did. Every language spells it differently — a keyword in three of
/// them and braces in the fourth — so holding it as a type and a list of arguments is what lets each
/// write its own.
/// </remarks>
public class ConstructionExpression : Expression
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConstructionExpression"/> class.
	/// </summary>
	public ConstructionExpression()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConstructionExpression"/> class.
	/// </summary>
	/// <param name="type">The type to build.</param>
	public ConstructionExpression(TypeReference? type) => Type = type;

	/// <summary>
	/// Gets or sets the type to build.
	/// </summary>
	public TypeReference? Type { get; set; }

	/// <summary>
	/// Gets the arguments, in order.
	/// </summary>
	public Collection<AstNode> Arguments { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "ConstructionExpression";

	/// <summary>
	/// Creates a deep clone of this expression.
	/// </summary>
	/// <returns>A new instance with the same type and cloned arguments.</returns>
	public override AstNode Clone()
	{
		ConstructionExpression clone = new() { Type = Type?.Clone() };

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (AstNode argument in Arguments)
		{
			clone.Arguments.Add(argument.Clone());
		}

		return clone;
	}
}
