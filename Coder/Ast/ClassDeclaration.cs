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
/// A base type and a list of interfaces are separate because the languages separate them: C# writes
/// them in one list but takes at most one class in it, C++ writes <c>public</c> before each and does
/// not distinguish them at all, Rust turns the first into a supertrait and has nowhere to put the
/// rest, and C can make exactly one of them layout-compatible with the whole. Carrying one list
/// would leave every generator guessing which entry was the class.
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
	/// Gets the types this declaration is written over, which may be none.
	/// </summary>
	/// <remarks>
	/// Separate from <see cref="SpecialisationArguments"/> and the opposite of it: these are the
	/// parameters a declaration takes, and those are the arguments a specialisation supplies. A
	/// declaration has one or the other and not both, since a full specialisation by definition
	/// leaves nothing open.
	/// </remarks>
	public Collection<TypeParameter> TypeParameters { get; init; } = [];

	/// <summary>
	/// Gets the interfaces this type implements, which may be none.
	/// </summary>
	/// <remarks>
	/// Separate from <see cref="BaseType"/> rather than folded into one list, because what a target
	/// does with the two is different often enough to matter. Rust makes a base type on a trait a
	/// supertrait and has no answer for a struct's interfaces at all; C embeds the base as the first
	/// member, which is the position that makes a pointer to the one a pointer to the other, and can
	/// only give that position to one thing; Go satisfies an interface structurally, so declaring
	/// one is an assertion rather than a declaration. Every one of those decisions needs to know
	/// which entry is the class.
	/// </remarks>
	public Collection<TypeReference> Interfaces { get; init; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the language should supply this type's value
	/// semantics rather than the declaration spelling them out.
	/// </summary>
	/// <remarks>
	/// Not a fourth <see cref="TypeDeclarationKind"/>, because it is orthogonal to the three: C#
	/// has a <c>record</c> and a <c>record struct</c>, and an enumeration would have to carry the
	/// product of the two ideas rather than either of them.
	/// <para>
	/// What it asks for is one thing — equality, a readable form and a copy, written by the
	/// compiler rather than by hand — and three targets have a way to ask for exactly that:
	/// <c>record</c>, Rust's <c>#[derive(…)]</c> and Python's <c>@dataclass</c>. The rest write a
	/// comment, because a type that quietly stops comparing by value is a type that still looks
	/// like it does.
	/// </para>
	/// </remarks>
	public bool IsRecord { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the rest of this type may be declared elsewhere.
	/// </summary>
	/// <remarks>
	/// The one modifier here that is dropped in silence where it cannot be spelled, and the reason
	/// is what it says. <see cref="IsRecord"/> and <see cref="IsReadOnly"/> are claims about the
	/// type — it compares by value, it does not mutate — so a file that loses one looks like a file
	/// that still makes it. <c>partial</c> claims nothing about the type; it is permission to
	/// declare the rest of it in another file, and a generator that has written the whole
	/// declaration has not used the permission for anything a reader could miss.
	/// </remarks>
	public bool IsPartial { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether no member of this type modifies it.
	/// </summary>
	/// <remarks>
	/// A promise about the type rather than about any one member, which is what distinguishes it
	/// from <see cref="FunctionDeclaration.IsReadOnly"/>: that one says a call does not modify the
	/// receiver, and this says none of them does. Only C# has a word for it, so the others write a
	/// comment — marking every member <c>const</c> in C++ would be the same promise made in a
	/// different place, and would be wrong for a static one.
	/// </remarks>
	public bool IsReadOnly { get; set; }

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
			IsRecord = IsRecord,
			IsPartial = IsPartial,
			IsReadOnly = IsReadOnly,
			Visibility = Visibility
		};

		foreach (TypeReference argument in SpecialisationArguments)
		{
			clone.SpecialisationArguments.Add(argument.Clone());
		}

		foreach (TypeReference contract in Interfaces)
		{
			clone.Interfaces.Add(contract.Clone());
		}

		foreach (TypeParameter parameter in TypeParameters)
		{
			clone.TypeParameters.Add(parameter.Clone());
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
