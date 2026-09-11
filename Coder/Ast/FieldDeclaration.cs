// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a field of a type in the abstract syntax tree.
/// </summary>
/// <remarks>
/// Distinct from <see cref="VariableDeclaration"/>, which is a local. The two are spelled almost
/// identically and behave differently in the one way that matters here: a field with no initialiser
/// is a bug waiting to be found in a save file, so C++ writes <c>Type name{};</c> and the value a
/// default-constructed instance starts at is the value the declaration described. A local with no
/// initialiser is ordinary.
/// <para>
/// Telling them apart structurally rather than by asking where the node happens to sit is what lets
/// a generator emit each correctly without being handed its context.
/// </para>
/// </remarks>
public class FieldDeclaration : AstNode, IHasVisibility, IHasDocumentation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FieldDeclaration"/> class.
	/// </summary>
	public FieldDeclaration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldDeclaration"/> class with a name and type.
	/// </summary>
	/// <param name="name">The field's name.</param>
	/// <param name="type">The field's type.</param>
	public FieldDeclaration(string name, TypeReference? type = null)
	{
		Name = name;
		Type = type;
	}

	/// <summary>
	/// Gets or sets the field's name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the field's type.
	/// </summary>
	public TypeReference? Type { get; set; }

	/// <summary>
	/// Gets or sets what the field starts at, or null for the type's own default.
	/// </summary>
	public Expression? InitialValue { get; set; }

	/// <summary>
	/// Gets or sets how widely the field is visible.
	/// </summary>
	public Visibility Visibility { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the field belongs to the type rather than to an
	/// instance of it.
	/// </summary>
	public bool IsStatic { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the field's value is fixed and known where it is
	/// written.
	/// </summary>
	/// <remarks>
	/// The intent rather than the keyword, because no two of these languages spell it the same way
	/// and one of them spells it differently depending on where the field sits: C++ writes
	/// <c>inline constexpr</c> at namespace scope and <c>static constexpr</c> inside a class, since
	/// only the first of those has to say out loud that one definition is meant. C# writes
	/// <c>static readonly</c>, which is legal for every type where <c>const</c> is legal for a
	/// handful. A constant field is static whether or not <see cref="IsStatic"/> says so - there is
	/// no per-instance copy of a value fixed at compile time.
	/// </remarks>
	public bool IsConstant { get; set; }

	/// <inheritdoc/>
	public Collection<string> Documentation { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "FieldDeclaration";

	/// <summary>
	/// Creates a deep clone of this declaration.
	/// </summary>
	/// <returns>A new instance with the same properties and a cloned initialiser.</returns>
	public override AstNode Clone()
	{
		FieldDeclaration clone = new()
		{
			Name = Name,
			Type = Type?.Clone(),
			InitialValue = (Expression?)InitialValue?.DeepClone(),
			Visibility = Visibility,
			IsStatic = IsStatic,
			IsConstant = IsConstant,
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
