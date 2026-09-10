// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Gives a type a second name.
/// </summary>
/// <remarks>
/// Worth declaring rather than repeating: a type that shims another has to say what it is stored as,
/// and saying it once beside the declaration is what stops every member of it restating the same
/// thing and one of them eventually disagreeing.
/// </remarks>
public class UsingAlias : AstNode, IHasVisibility, IHasDocumentation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UsingAlias"/> class.
	/// </summary>
	public UsingAlias()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UsingAlias"/> class with a name and a type.
	/// </summary>
	/// <param name="name">The name being introduced.</param>
	/// <param name="aliasedType">The type it names.</param>
	public UsingAlias(string name, TypeReference? aliasedType = null)
	{
		Name = name;
		AliasedType = aliasedType;
	}

	/// <summary>
	/// Gets or sets the name being introduced.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the type it names.
	/// </summary>
	public TypeReference? AliasedType { get; set; }

	/// <summary>
	/// Gets or sets how widely the alias is visible.
	/// </summary>
	public Visibility Visibility { get; set; }

	/// <inheritdoc/>
	public Collection<string> Documentation { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "UsingAlias";

	/// <summary>
	/// Creates a deep clone of this alias.
	/// </summary>
	/// <returns>A new instance with the same properties.</returns>
	public override AstNode Clone()
	{
		UsingAlias clone = new()
		{
			Name = Name,
			AliasedType = AliasedType?.Clone(),
			Visibility = Visibility,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (string line in Documentation)
		{
			clone.Documentation.Add(line);
		}

		return clone;
	}
}
