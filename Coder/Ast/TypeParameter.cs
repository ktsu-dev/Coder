// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>
/// One of the types a declaration is written over.
/// </summary>
/// <remarks>
/// A value rather than an <see cref="AstNode"/>, the same as
/// <see cref="ClassDeclaration.SpecialisationArguments"/>: a type parameter is part of the thing
/// being declared rather than a member of it. <see cref="Parse(string)"/> and
/// <see cref="ToString"/> are inverses, so a document carries a whole parameter, constraints and
/// all, as one line a person can read.
/// <para>
/// The name travels everywhere and the constraints do not. Four of the seven targets have type
/// parameters at all, and of those, what each can say about one differs so much that the
/// constraints are the part a generator has to decide about rather than translate — which is why
/// they are named intents in <see cref="TypeConstraintKind"/> rather than text.
/// </para>
/// </remarks>
public sealed class TypeParameter : IEquatable<TypeParameter>
{
	/// <summary>What separates a parameter's name from its constraints.</summary>
	private const char ConstraintSeparator = ':';

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeParameter"/> class.
	/// </summary>
	public TypeParameter()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeParameter"/> class with a name.
	/// </summary>
	/// <param name="name">What the parameter is called.</param>
	public TypeParameter(string name) => Name = name;

	/// <summary>
	/// Gets or sets what the parameter is called.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets what an argument for this parameter is required to be, which may be nothing.
	/// </summary>
	public Collection<TypeConstraint> Constraints { get; init; } = [];

	/// <summary>
	/// Reads a written parameter.
	/// </summary>
	/// <param name="text">The parameter as it is written, such as <c>T : struct, INumber&lt;T&gt;</c>.</param>
	/// <returns>The parameter.</returns>
	/// <remarks>
	/// The constraints are split at the commas that are not inside a generic argument list, for the
	/// reason <see cref="ClassDeclaration.SpecialisationArguments"/> is a sequence rather than one
	/// joined string: the comma in <c>IComparer&lt;T, U&gt;</c> belongs to it rather than separating
	/// two constraints.
	/// </remarks>
	public static TypeParameter Parse(string text)
	{
		string written = (text ?? string.Empty).Trim();
		int separator = written.IndexOf(ConstraintSeparator);

		if (separator < 0)
		{
			return new TypeParameter(written);
		}

		TypeParameter parameter = new(written[..separator].Trim());

		foreach (string constraint in SplitAtTopLevel(written[(separator + 1)..]))
		{
			parameter.Constraints.Add(TypeConstraint.Parse(constraint));
		}

		return parameter;
	}

	/// <summary>
	/// Splits a constraint list at the commas that separate its entries.
	/// </summary>
	/// <param name="text">The list, without the colon before it.</param>
	/// <returns>The entries, with nothing empty among them.</returns>
	private static IEnumerable<string> SplitAtTopLevel(string text)
	{
		int depth = 0;
		int start = 0;

		for (int index = 0; index < text.Length; index++)
		{
			switch (text[index])
			{
				case '<':
					depth++;
					break;

				case '>':
					depth--;
					break;

				case ',' when depth == 0:
					yield return text[start..index];
					start = index + 1;
					break;

				default:
					break;
			}
		}

		yield return text[start..];
	}

	/// <inheritdoc />
	public override string ToString() =>
		Constraints.Count == 0
			? Name
			: $"{Name} {ConstraintSeparator} {string.Join(", ", Constraints.Select(constraint => constraint.ToString()))}";

	/// <summary>
	/// Creates a copy.
	/// </summary>
	/// <returns>The copy.</returns>
	public TypeParameter Clone()
	{
		TypeParameter clone = new(Name);

		foreach (TypeConstraint constraint in Constraints)
		{
			clone.Constraints.Add(constraint.Clone());
		}

		return clone;
	}

	/// <summary>
	/// Reads a written parameter, so a caller with a name and nothing else can write just the name.
	/// </summary>
	/// <param name="text">The parameter as it is written.</param>
	public static implicit operator TypeParameter?(string? text) => text is null ? null : Parse(text);

	/// <summary>
	/// Reads a written parameter.
	/// </summary>
	/// <param name="text">The parameter as it is written.</param>
	/// <returns>The parameter, or null when there was no text.</returns>
	public static TypeParameter? FromString(string? text) => text is null ? null : Parse(text);

	/// <inheritdoc />
	public bool Equals(TypeParameter? other) =>
		other is not null && string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as TypeParameter);

	/// <inheritdoc />
	public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);
}
