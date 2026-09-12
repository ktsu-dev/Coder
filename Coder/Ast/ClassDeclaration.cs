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
public class ClassDeclaration : AstCompositeNode, IHasVisibility, IHasDocumentation
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
	/// Gets or sets what kind of type this declares.
	/// </summary>
	public TypeDeclarationKind Kind { get; set; }

	/// <inheritdoc/>
	public Collection<string> Documentation { get; init; } = [];

	/// <summary>
	/// Gets or sets the type this class derives from, or null when it derives from nothing.
	/// </summary>
	public TypeReference? BaseType { get; set; }

	/// <summary>
	/// Gets the type arguments this declaration is the specialisation for, or nothing when it is
	/// an ordinary declaration.
	/// </summary>
	/// <remarks>
	/// An explicit specialisation is C++ and only C++, which is why this is a property on the
	/// ordinary declaration rather than a node of its own: the thing being declared is still a
	/// class, with the same members, the same visibility and the same documentation. What changes
	/// is which type it is the declaration <em>for</em>.
	/// <para>
	/// It exists because a generated table has to be reachable from the type it describes.
	/// <c>template&lt;&gt; struct Describe&lt;RigidBody&gt;</c> is how C++ attaches a fact to a type
	/// without touching the type, and a generator that could not say it would have to fall back on
	/// naming — a <c>DescribeRigidBody</c> that every consumer has to spell for itself, which is
	/// the thing a lookup by type exists to avoid.
	/// </para>
	/// <para>
	/// The precedent is <see cref="CompileTimeAssertion"/> and <see cref="SourceFile.Imports"/>:
	/// one generator honours it and the others write a comment, because a generated file that
	/// quietly drops what it was for looks like one that still means it. The arguments are
	/// <see cref="TypeReference"/> rather than text, though, which those two are not — a
	/// specialisation argument is a type, and the AST already knows how to be a type.
	/// </para>
	/// </remarks>
	public Collection<TypeReference> SpecialisationArguments { get; init; } = [];

	/// <summary>
	/// Gets a value indicating whether this declares a specialisation rather than a type.
	/// </summary>
	public bool IsSpecialisation => SpecialisationArguments.Count > 0;

	/// <summary>
	/// Gets or sets how widely the class is visible.
	/// </summary>
	public Visibility Visibility { get; set; }

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
			Kind = Kind,
			BaseType = BaseType?.Clone(),
			Visibility = Visibility
		};

		foreach (TypeReference argument in SpecialisationArguments)
		{
			clone.SpecialisationArguments.Add(argument.Clone());
		}

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

		foreach ((string key, AstNode child) in Children)
		{
			clone.Children[key] = child.Clone();
		}

		return clone;
	}
}
