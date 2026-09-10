// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// One named value of an <see cref="EnumDeclaration"/>.
/// </summary>
public class EnumMember : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EnumMember"/> class.
	/// </summary>
	public EnumMember()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EnumMember"/> class with a name.
	/// </summary>
	/// <param name="name">The member's name.</param>
	public EnumMember(string name) => Name = name;

	/// <summary>
	/// Gets or sets the member's name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the value the member is fixed to, or null to let it follow the one before it.
	/// </summary>
	/// <remarks>
	/// Text rather than a number: what a language accepts here differs — a literal, another member,
	/// an expression over them — and the AST has no reason to read it. A member with no value is the
	/// ordinary case, and pinning one is what a wire format or a saved file needs.
	/// </remarks>
	public string? Value { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "EnumMember";

	/// <summary>
	/// Creates a deep clone of this member.
	/// </summary>
	/// <returns>A new instance with the same properties.</returns>
	public override AstNode Clone()
	{
		EnumMember clone = new()
		{
			Name = Name,
			Value = Value,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}
