// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents an enumeration declaration in the abstract syntax tree.
/// </summary>
/// <remarks>
/// Declared inside a type as often as beside one: two types in the same file may each want a member
/// called <c>Kind</c>, and nesting is what stops the second colliding with the first. So an
/// enumeration is an ordinary member of <see cref="ClassDeclaration.Members"/> as well as a
/// declaration in its own right.
/// </remarks>
public class EnumDeclaration : AstNode, IHasVisibility, IHasDocumentation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EnumDeclaration"/> class.
	/// </summary>
	public EnumDeclaration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EnumDeclaration"/> class with a name.
	/// </summary>
	/// <param name="name">The enumeration's name.</param>
	public EnumDeclaration(string name) => Name = name;

	/// <summary>
	/// Gets or sets the enumeration's name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the type the values are stored as, or null to let the language choose.
	/// </summary>
	/// <remarks>
	/// Worth saying when the values cross a boundary: an enumeration stored as a machine word can
	/// dominate the size of the type holding it, and a saved file or a network packet is written in
	/// whatever width the declaration chose. Only C++ and C# can say it; the others ignore it.
	/// </remarks>
	public TypeReference? UnderlyingType { get; set; }

	/// <summary>
	/// Gets the members, in the order they should be emitted.
	/// </summary>
	/// <remarks>
	/// Order is meaning here in the same way field order is layout: a member with no explicit value
	/// takes its number from its position, so reordering two of them renumbers both.
	/// </remarks>
	public Collection<EnumMember> Members { get; init; } = [];

	/// <summary>
	/// Gets or sets how widely the enumeration is visible.
	/// </summary>
	public Visibility Visibility { get; set; }

	/// <inheritdoc/>
	public Collection<string> Documentation { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "EnumDeclaration";

	/// <summary>
	/// Creates a deep clone of this declaration.
	/// </summary>
	/// <returns>A new instance with the same properties and cloned members.</returns>
	public override AstNode Clone()
	{
		EnumDeclaration clone = new()
		{
			Name = Name,
			UnderlyingType = UnderlyingType?.Clone(),
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

		foreach (EnumMember member in Members)
		{
			clone.Members.Add((EnumMember)member.Clone());
		}

		return clone;
	}
}
