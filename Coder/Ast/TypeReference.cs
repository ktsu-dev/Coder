// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;

/// <summary>
/// A type as the AST understands it: a name, the types it is parameterised by, and how the value is
/// reached.
/// </summary>
/// <remarks>
/// A type used to be a bare string on every node that carried one, which is enough for <c>Foo</c> and
/// hopeless for <c>span&lt;const Velocity&gt;</c> — a generator handed that string can only paste it,
/// so whoever built the node had to spell the target language themselves. Structure is what lets a
/// generator decide the spelling.
/// <para>
/// <see cref="Parse(string)"/> and <see cref="ToString"/> are inverses over everything
/// <see cref="Parse(string)"/> understands, and text it does not understand becomes a
/// <see cref="Name"/> holding that text verbatim — so a string that means nothing to this grammar
/// still survives a round trip intact rather than being dropped or half-read. That is what lets the
/// string-shaped properties on the nodes stay as they are while carrying structure underneath.
/// </para>
/// </remarks>
public sealed class TypeReference : IEquatable<TypeReference>
{
	/// <summary>The token <see cref="ToString"/> writes for <see cref="IsReadOnly"/>.</summary>
	private const string ConstKeyword = "const";

	/// <summary>The other token <see cref="Parse(string)"/> accepts for <see cref="IsReadOnly"/>.</summary>
	private const string ReadOnlyKeyword = "readonly";

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeReference"/> class.
	/// </summary>
	public TypeReference()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeReference"/> class with a name.
	/// </summary>
	/// <param name="name">The type's name, unparameterised.</param>
	public TypeReference(string name) => Name = name;

	/// <summary>
	/// Gets or sets the type's name, without its type arguments or qualifiers.
	/// </summary>
	/// <remarks>
	/// A qualified name — <c>std::vector</c>, <c>System.Collections.Generic.List</c> — is one name,
	/// not a path the AST walks. Which separator a language uses is the generator's business.
	/// </remarks>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets the types this one is parameterised by, in declaration order.
	/// </summary>
	public Collection<TypeReference> TypeArguments { get; init; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the value may not be written through.
	/// </summary>
	/// <remarks>
	/// Named for what it means rather than for how any one language spells it: C++ writes
	/// <c>const</c>, C# writes <c>in</c> on a parameter, and Python writes nothing.
	/// </remarks>
	public bool IsReadOnly { get; set; }

	/// <summary>
	/// Gets or sets how the value is reached.
	/// </summary>
	public TypeIndirection Indirection { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is an array of the type rather than one of it.
	/// </summary>
	/// <remarks>
	/// A bound is deliberately not modelled. What a generated table needs is an array whose length
	/// is its initialiser's, which every language writes by leaving the bound out; a fixed bound is
	/// a different thing that would have to be an expression rather than a number, and nothing asks
	/// for it yet. Where the brackets go is the generator's business - C++ puts them after the name
	/// being declared and C# after the type - which is exactly the kind of difference this class
	/// exists to absorb.
	/// </remarks>
	public bool IsArray { get; set; }

	/// <summary>
	/// Reads a type from its text form.
	/// </summary>
	/// <param name="text">The text to read.</param>
	/// <returns>
	/// The type the text describes, or one whose <see cref="Name"/> is the text verbatim when the
	/// text is not something this grammar can read.
	/// </returns>
	/// <remarks>
	/// The grammar is deliberately small: an optional <c>const</c> or <c>readonly</c>, a name, an
	/// optional angle-bracketed argument list, an optional <c>[]</c>, and any number of
	/// <c>&amp;</c> or <c>*</c> suffixes.
	/// It exists to read what the string-shaped properties already hold, not to parse a language.
	/// </remarks>
	public static TypeReference Parse(string text)
	{
		Ensure.NotNull(text);

		int position = 0;
		TypeReference? parsed = TryRead(text, ref position);
		SkipWhitespace(text, ref position);

		// Anything left over means the grammar read only part of the text, which would silently
		// discard the rest. The whole text as a name is wrong-but-lossless; a half-read type is not.
		return parsed is not null && position == text.Length
			? parsed
			: new TypeReference(text);
	}

	/// <summary>
	/// Reads a type from its text form, so that a plain name can be written where a type is wanted.
	/// </summary>
	/// <param name="text">The text to read, or <see langword="null"/> for no type.</param>
	/// <returns>The type the text describes, or <see langword="null"/>.</returns>
	/// <remarks>
	/// Implicit because a name is the overwhelmingly common case and requiring
	/// <see cref="Parse(string)"/> at every one of them would be noise. The named alternate is
	/// <see cref="Parse(string)"/>; the inverse is <see cref="ToString"/>.
	/// </remarks>
	[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates",
		Justification = "Parse is the named alternate for this direction and ToString for the other.")]
	public static implicit operator TypeReference?(string? text) => text is null ? null : Parse(text);

	/// <summary>
	/// Writes this type in the AST's own text form.
	/// </summary>
	/// <returns>The text form, which <see cref="Parse(string)"/> reads back to an equal type.</returns>
	/// <remarks>
	/// This is the AST's spelling, not any target language's. A generator that cares about the
	/// difference reads the structure instead — which is the whole reason the structure exists.
	/// </remarks>
	public override string ToString()
	{
		StringBuilder text = new();

		if (IsReadOnly)
		{
			text.Append(ConstKeyword).Append(' ');
		}

		text.Append(Name);

		if (TypeArguments.Count > 0)
		{
			text.Append('<');
			for (int index = 0; index < TypeArguments.Count; index++)
			{
				if (index > 0)
				{
					text.Append(", ");
				}

				text.Append(TypeArguments[index].ToString());
			}

			text.Append('>');
		}

		if (IsArray)
		{
			text.Append("[]");
		}

		return text.Append(Indirection switch
		{
			TypeIndirection.Reference => "&",
			TypeIndirection.Pointer => "*",
			_ => string.Empty,
		}).ToString();
	}

	/// <summary>
	/// Creates a deep copy of this type.
	/// </summary>
	/// <returns>A new type equal to this one, sharing none of its argument instances.</returns>
	public TypeReference Clone()
	{
		TypeReference clone = new()
		{
			Name = Name,
			IsReadOnly = IsReadOnly,
			Indirection = Indirection,
			IsArray = IsArray,
		};

		foreach (TypeReference argument in TypeArguments)
		{
			clone.TypeArguments.Add(argument.Clone());
		}

		return clone;
	}

	/// <inheritdoc/>
	public bool Equals(TypeReference? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return string.Equals(Name, other.Name, StringComparison.Ordinal)
			&& IsReadOnly == other.IsReadOnly
			&& Indirection == other.Indirection
			&& IsArray == other.IsArray
			&& TypeArguments.SequenceEqual(other.TypeArguments);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => Equals(obj as TypeReference);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hash = new();
		hash.Add(Name, StringComparer.Ordinal);
		hash.Add(IsReadOnly);
		hash.Add(Indirection);
		hash.Add(IsArray);
		foreach (TypeReference argument in TypeArguments)
		{
			hash.Add(argument);
		}

		return hash.ToHashCode();
	}

	/// <summary>Compares two types for equality.</summary>
	/// <param name="left">The first type.</param>
	/// <param name="right">The second type.</param>
	/// <returns><see langword="true"/> when both describe the same type.</returns>
	public static bool operator ==(TypeReference? left, TypeReference? right) =>
		left is null ? right is null : left.Equals(right);

	/// <summary>Compares two types for inequality.</summary>
	/// <param name="left">The first type.</param>
	/// <param name="right">The second type.</param>
	/// <returns><see langword="true"/> when they describe different types.</returns>
	public static bool operator !=(TypeReference? left, TypeReference? right) => !(left == right);

	/// <summary>
	/// Reads one type starting at <paramref name="position"/>, leaving it after the last character
	/// consumed.
	/// </summary>
	/// <param name="text">The text being read.</param>
	/// <param name="position">Where to start, updated to where reading stopped.</param>
	/// <returns>The type read, or <see langword="null"/> when the text at that point is not one.</returns>
	private static TypeReference? TryRead(string text, ref int position)
	{
		SkipWhitespace(text, ref position);

		bool isReadOnly = TryReadQualifier(text, ref position);
		string name = ReadName(text, ref position);
		if (name.Length == 0)
		{
			return null;
		}

		TypeReference type = new(name) { IsReadOnly = isReadOnly };

		SkipWhitespace(text, ref position);
		if (position < text.Length && text[position] == '<' && !TryReadArguments(text, ref position, type))
		{
			return null;
		}

		SkipWhitespace(text, ref position);
		if (position + 1 < text.Length && text[position] == '[' && text[position + 1] == ']')
		{
			type.IsArray = true;
			position += 2;
		}

		SkipWhitespace(text, ref position);
		if (position < text.Length && (text[position] == '&' || text[position] == '*'))
		{
			type.Indirection = text[position] == '&' ? TypeIndirection.Reference : TypeIndirection.Pointer;
			position++;
		}

		return type;
	}

	/// <summary>
	/// Reads the angle-bracketed argument list into <paramref name="type"/>.
	/// </summary>
	/// <param name="text">The text being read.</param>
	/// <param name="position">The position of the opening bracket, updated past the closing one.</param>
	/// <param name="type">The type to add the arguments to.</param>
	/// <returns><see langword="true"/> when a well-formed list was read.</returns>
	private static bool TryReadArguments(string text, ref int position, TypeReference type)
	{
		position++;

		while (true)
		{
			TypeReference? argument = TryRead(text, ref position);
			if (argument is null)
			{
				return false;
			}

			type.TypeArguments.Add(argument);

			SkipWhitespace(text, ref position);
			if (position >= text.Length)
			{
				return false;
			}

			if (text[position] == ',')
			{
				position++;
				continue;
			}

			if (text[position] == '>')
			{
				position++;
				return true;
			}

			return false;
		}
	}

	/// <summary>
	/// Reads a leading <c>const</c> or <c>readonly</c>, if one is there.
	/// </summary>
	/// <param name="text">The text being read.</param>
	/// <param name="position">Where to start, updated past the keyword when one was read.</param>
	/// <returns><see langword="true"/> when a qualifier was read.</returns>
	private static bool TryReadQualifier(string text, ref int position)
	{
		foreach (string keyword in new[] { ConstKeyword, ReadOnlyKeyword })
		{
			// The space matters: `constant` starts with `const` and is a name, not a qualified one.
			if (position + keyword.Length < text.Length
				&& string.CompareOrdinal(text, position, keyword, 0, keyword.Length) == 0
				&& char.IsWhiteSpace(text[position + keyword.Length]))
			{
				position += keyword.Length;
				SkipWhitespace(text, ref position);
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Reads a name: everything up to a character the grammar gives its own meaning.
	/// </summary>
	/// <param name="text">The text being read.</param>
	/// <param name="position">Where to start, updated past the name.</param>
	/// <returns>The name read, which is empty when there is none.</returns>
	private static string ReadName(string text, ref int position)
	{
		int start = position;
		while (position < text.Length && !IsPunctuation(text[position]) && !char.IsWhiteSpace(text[position]))
		{
			position++;
		}

		return text[start..position];
	}

	/// <summary>
	/// Reports whether a character is one the grammar reads rather than one a name may contain.
	/// </summary>
	/// <param name="character">The character to test.</param>
	/// <returns><see langword="true"/> when the character ends a name.</returns>
	private static bool IsPunctuation(char character) =>
		character is '<' or '>' or ',' or '&' or '*' or '[' or ']';

	/// <summary>
	/// Advances past any whitespace.
	/// </summary>
	/// <param name="text">The text being read.</param>
	/// <param name="position">Where to start, updated past the whitespace.</param>
	private static void SkipWhitespace(string text, ref int position)
	{
		while (position < text.Length && char.IsWhiteSpace(text[position]))
		{
			position++;
		}
	}
}
