// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// A named, typed member that is read and written through code rather than stored directly.
/// </summary>
/// <remarks>
/// Three of the seven targets have one: C# spells it <c>T Name { get; set; }</c>, Python
/// <c>@property</c>, JavaScript <c>get name()</c>. The other four have the two halves of what it is
/// and no word joining them, which is what makes the interesting decision here not the syntax but
/// <em>where a property lands</em>.
/// <para>
/// It lands in one of two places depending on whether it has a body. A property with no accessor
/// bodies is exactly a field with a storage location the compiler supplies, so a target with no
/// properties writes it as a field — which is what it is, and what a person would have written.
/// One with a body is exactly a pair of functions, so the same target writes the pair. Neither is
/// an approximation: they are the two things a property is, separated.
/// </para>
/// <para>
/// That is also why <see cref="FieldDeclaration"/> is not reused. A field says where a value is
/// kept; this says how it is reached, and the difference is invisible in C# and load-bearing
/// everywhere else.
/// </para>
/// </remarks>
public class PropertyDeclaration : AstNode, IHasVisibility, IHasDocumentation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PropertyDeclaration"/> class.
	/// </summary>
	public PropertyDeclaration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PropertyDeclaration"/> class with a name.
	/// </summary>
	/// <param name="name">What the property is called.</param>
	/// <param name="type">The type of value it holds.</param>
	public PropertyDeclaration(string name, TypeReference? type = null)
	{
		Name = name;
		Type = type;
	}

	/// <summary>
	/// Gets or sets what the property is called.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the type of value it holds.
	/// </summary>
	public TypeReference? Type { get; set; }

	/// <summary>
	/// Gets or sets how widely the property is visible.
	/// </summary>
	public Visibility Visibility { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the property belongs to the type rather than to an
	/// instance of it.
	/// </summary>
	public bool IsStatic { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the property can be read.
	/// </summary>
	/// <remarks>
	/// True by default: a property that can be neither read nor written is not a member of
	/// anything, and a write-only one is rare enough to be worth asking for.
	/// </remarks>
	public bool HasGetter { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the property can be written.
	/// </summary>
	public bool HasSetter { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether it can be written only while the object is being
	/// built.
	/// </summary>
	/// <remarks>
	/// C#'s <c>init</c>, and nothing else has a word for it. Where it cannot be spelled the
	/// property is written as one that can be set, which is the safe direction to be wrong in: a
	/// setter that should have been an initialiser compiles every call the declaration meant to
	/// allow, and the reverse does not.
	/// </remarks>
	public bool SetterIsInitOnly { get; set; }

	/// <summary>
	/// Gets the statements run when the property is read, which may be none.
	/// </summary>
	/// <remarks>
	/// None means the storage is the language's to supply, which is what makes the property a field
	/// in a target that has no properties.
	/// </remarks>
	public Collection<AstNode> GetterBody { get; init; } = [];

	/// <summary>
	/// Gets the statements run when the property is written, which may be none.
	/// </summary>
	public Collection<AstNode> SetterBody { get; init; } = [];

	/// <inheritdoc/>
	public Collection<string> Documentation { get; init; } = [];

	/// <summary>
	/// Gets the metadata attached to this declaration, which may be none.
	/// </summary>
	public Collection<Annotation> Annotations { get; init; } = [];

	/// <summary>
	/// Gets a value indicating whether the property can be read.
	/// </summary>
	public bool CanRead => HasGetter || GetterBody.Count > 0;

	/// <summary>
	/// Gets a value indicating whether the property can be written.
	/// </summary>
	public bool CanWrite => HasSetter || SetterBody.Count > 0;

	/// <summary>
	/// Gets a value indicating whether the language supplies the storage and both accessors.
	/// </summary>
	/// <remarks>
	/// The question every generator asks first, because it is what decides whether the property is
	/// a field or a pair of functions.
	/// </remarks>
	public bool IsAutomatic => GetterBody.Count == 0 && SetterBody.Count == 0;

	/// <summary>
	/// The name a getter takes where the property has to become a function.
	/// </summary>
	/// <param name="style">How the target spells a member's name.</param>
	/// <returns>The name.</returns>
	public string GetterName(NamingStyle style) => NameStyles.Spell(Name ?? "value", style);

	/// <summary>
	/// The name a setter takes where the property has to become a function.
	/// </summary>
	/// <param name="style">How the target spells a member's name.</param>
	/// <returns>The name.</returns>
	/// <remarks>
	/// Prefixed rather than overloaded. Two functions of one name differing only in whether they
	/// take an argument is legal C++ and illegal in Rust, Go and C, and a reader of any of them
	/// reads <c>set_value</c> faster than an overload set.
	/// </remarks>
	public string SetterName(NamingStyle style) => NameStyles.Spell($"set {Name ?? "value"}", style);

	/// <inheritdoc/>
	public override string GetNodeTypeName() => "PropertyDeclaration";

	/// <inheritdoc/>
	public override AstNode Clone()
	{
		PropertyDeclaration clone = new()
		{
			Name = Name,
			Type = Type?.Clone(),
			Visibility = Visibility,
			IsStatic = IsStatic,
			HasGetter = HasGetter,
			HasSetter = HasSetter,
			SetterIsInitOnly = SetterIsInitOnly,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (string line in Documentation)
		{
			clone.Documentation.Add(line);
		}

		foreach (Annotation annotation in Annotations)
		{
			clone.Annotations.Add(annotation.Clone());
		}

		foreach (AstNode statement in GetterBody)
		{
			clone.GetterBody.Add(statement.Clone());
		}

		foreach (AstNode statement in SetterBody)
		{
			clone.SetterBody.Add(statement.Clone());
		}

		return clone;
	}
}
