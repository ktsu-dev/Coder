// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a class declaration in the abstract syntax tree.
/// </summary>
/// <remarks>
/// A class is a named group of members — functions, variables, and nested classes — so
/// <see cref="Members"/> holds <see cref="AstNode"/> rather than a narrower type. Which members a
/// target language will actually accept is the generator's business, not the AST's.
/// <para>
/// Only one base type is carried. Every language the generators target names a single base class in
/// its declaration syntax, and interfaces are a language feature the AST does not model yet.
/// </para>
/// </remarks>
public class ClassDeclaration : AstCompositeNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ClassDeclaration"/> class.
	/// </summary>
	public ClassDeclaration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ClassDeclaration"/> class with a name.
	/// </summary>
	/// <param name="name">The name of the class.</param>
	public ClassDeclaration(string name) => Name = name;

	/// <summary>
	/// Gets or sets the name of the class.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the type this class derives from, or null when it derives from nothing.
	/// </summary>
	public string? BaseType { get; set; }

	/// <summary>
	/// Gets or sets the visibility/access modifier (public, internal, …), or null for the language's default.
	/// </summary>
	public string? AccessModifier { get; set; }

	/// <summary>
	/// Gets the members the class declares, in the order they should be emitted.
	/// </summary>
	public Collection<AstNode> Members { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "ClassDeclaration";

	/// <summary>
	/// Creates a deep clone of this class declaration.
	/// </summary>
	/// <returns>A new instance of the class declaration with the same properties and cloned members.</returns>
	public override AstNode Clone()
	{
		ClassDeclaration clone = new()
		{
			Name = Name,
			BaseType = BaseType,
			AccessModifier = AccessModifier
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (AstNode member in Members)
		{
			clone.Members.Add(member.Clone());
		}

		foreach ((string key, AstNode child) in Children)
		{
			clone.Children[key] = child.Clone();
		}

		return clone;
	}
}
