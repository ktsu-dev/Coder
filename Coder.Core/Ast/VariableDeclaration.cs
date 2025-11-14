// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Ast;

/// <summary>
/// Represents a variable declaration statement.
/// Examples: int x; string name = "hello"; var result = 42;
/// </summary>
public class VariableDeclaration : AstNode
{
	/// <summary>
	/// Gets or sets the name of the variable.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Gets or sets the declared type of the variable.
	/// Can be null for type-inferred declarations.
	/// </summary>
	public string? Type { get; set; }

	/// <summary>
	/// Gets or sets the initial value expression.
	/// Can be null if no initial value is provided.
	/// </summary>
	public Expression? InitialValue { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is a constant declaration.
	/// </summary>
	public bool IsConstant { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the type should be inferred.
	/// </summary>
	public bool IsTypeInferred { get; set; }

	/// <summary>
	/// Gets or sets the visibility/access modifier (public, private, etc.).
	/// </summary>
	public string? AccessModifier { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="VariableDeclaration"/> class.
	/// </summary>
	/// <param name="name">The variable name.</param>
	/// <param name="type">The variable type (optional for inferred types).</param>
	/// <param name="initialValue">The initial value (optional).</param>
	public VariableDeclaration(string name, string? type = null, Expression? initialValue = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		Name = name;
		Type = type;
		InitialValue = initialValue;
		IsTypeInferred = type == null && initialValue != null;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="VariableDeclaration"/> class.
	/// Used for deserialization.
	/// </summary>
	public VariableDeclaration() => Name = string.Empty;

	/// <summary>
	/// Gets the node type name for this variable declaration.
	/// </summary>
	/// <returns>The node type name.</returns>
	public override string GetNodeTypeName() => "VariableDeclaration";

	/// <summary>
	/// Creates a deep clone of this variable declaration.
	/// </summary>
	/// <returns>A new instance with the same property values.</returns>
	public override AstNode Clone()
	{
		VariableDeclaration clone = new()
		{
			Name = Name,
			Type = Type,
			InitialValue = (Expression?)InitialValue?.DeepClone(),
			IsConstant = IsConstant,
			IsTypeInferred = IsTypeInferred,
			AccessModifier = AccessModifier
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}

	/// <summary>
	/// Returns a string representation of the variable declaration.
	/// </summary>
	/// <returns>String representation.</returns>
	public override string ToString()
	{
		string typeInfo = IsTypeInferred ? "var" : Type ?? "?";
		string valueInfo = InitialValue != null ? $" = {InitialValue}" : "";
		string modifiers = AccessModifier != null ? $"{AccessModifier} " : "";
		modifiers += IsConstant ? "const " : "";

		return $"{modifiers}{typeInfo} {Name}{valueInfo}";
	}
}
