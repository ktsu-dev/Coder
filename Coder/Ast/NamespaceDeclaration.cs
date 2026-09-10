// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a namespace holding declarations.
/// </summary>
/// <remarks>
/// <see cref="Name"/> is written with either separator — <c>holo::components</c> or
/// <c>Holo.Components</c> — and each generator splits it and rejoins with its own. That is the one
/// place a name is taken apart rather than kept whole, because here the separator genuinely differs
/// between languages rather than merely looking different.
/// <para>
/// Python and JavaScript have no namespace: a module is the unit of naming in both, and a file is
/// the module. They emit the members and nothing around them.
/// </para>
/// </remarks>
public class NamespaceDeclaration : AstNode, IHasDocumentation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="NamespaceDeclaration"/> class.
	/// </summary>
	public NamespaceDeclaration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="NamespaceDeclaration"/> class with a name.
	/// </summary>
	/// <param name="name">The namespace's name, in either separator.</param>
	public NamespaceDeclaration(string name) => Name = name;

	/// <summary>
	/// Gets or sets the namespace's name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets the declarations the namespace holds, in the order they should be emitted.
	/// </summary>
	public Collection<AstNode> Members { get; init; } = [];

	/// <inheritdoc/>
	public Collection<string> Documentation { get; init; } = [];

	/// <summary>
	/// Splits a namespace name into its parts, accepting either separator.
	/// </summary>
	/// <param name="name">The name to split.</param>
	/// <returns>The parts, in order.</returns>
	public static IReadOnlyList<string> Split(string? name) =>
		string.IsNullOrEmpty(name)
			? []
			: [.. name.Replace("::", ".", StringComparison.Ordinal)
				.Split('.', StringSplitOptions.RemoveEmptyEntries)];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "NamespaceDeclaration";

	/// <summary>
	/// Creates a deep clone of this declaration.
	/// </summary>
	/// <returns>A new instance with the same properties and cloned members.</returns>
	public override AstNode Clone()
	{
		NamespaceDeclaration clone = new() { Name = Name };

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (string line in Documentation)
		{
			clone.Documentation.Add(line);
		}

		foreach (AstNode member in Members)
		{
			clone.Members.Add(member.Clone());
		}

		return clone;
	}
}
