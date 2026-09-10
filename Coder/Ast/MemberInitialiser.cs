// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// What one member of a type starts at, as part of constructing it.
/// </summary>
/// <remarks>
/// C++ initialises a member rather than assigning to it, which is the only way to start a member
/// that cannot be assigned at all — and is the difference between building a value and building an
/// empty one and then overwriting it.
/// <para>
/// A language without that distinction assigns instead, at the top of the constructor's body and in
/// declaration order, which is what the initialiser means there.
/// </para>
/// </remarks>
public class MemberInitialiser : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MemberInitialiser"/> class.
	/// </summary>
	public MemberInitialiser()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MemberInitialiser"/> class.
	/// </summary>
	/// <param name="name">The member being initialised.</param>
	/// <param name="value">What it starts at.</param>
	public MemberInitialiser(string name, Expression? value = null)
	{
		Name = name;
		Value = value;
	}

	/// <summary>
	/// Gets or sets the member being initialised.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets what it starts at.
	/// </summary>
	public Expression? Value { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "MemberInitialiser";

	/// <summary>
	/// Creates a deep clone of this initialiser.
	/// </summary>
	/// <returns>A new instance with the same properties.</returns>
	public override AstNode Clone()
	{
		MemberInitialiser clone = new()
		{
			Name = Name,
			Value = (Expression?)Value?.DeepClone(),
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}
