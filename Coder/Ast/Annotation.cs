// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System;
using System.Collections.ObjectModel;

/// <summary>
/// One piece of metadata attached to a declaration.
/// </summary>
/// <remarks>
/// C# calls it an attribute, Rust and C++ call it an attribute and spell it differently again,
/// Python calls it a decorator and Java calls it an annotation. The name here is the one that is
/// nobody's keyword.
/// <para>
/// The <see cref="Name"/> and the <see cref="Arguments"/> are text, written verbatim, for the
/// reason <see cref="CallExpression.Callee"/> is: <c>[Obsolete]</c>, <c>#[serde(rename = "x")]</c>
/// and <c>@staticmethod</c> have nothing underneath them for the AST to hold, and one of them
/// usually means nothing at all in the others. What <em>is</em> shared, and is what each generator
/// supplies, is the syntax around them — <c>[…]</c>, <c>#[…]</c>, <c>[[…]]</c>, <c>@…</c> — and
/// whether the target has any at all.
/// </para>
/// <para>
/// So an annotation is written for a language, the same as <see cref="SourceFile.Imports"/> are,
/// and the three targets with no metadata syntax write down the one they were given rather than
/// dropping it. A file that quietly loses its <c>[Obsolete]</c> looks like a file that never had
/// one.
/// </para>
/// </remarks>
public sealed class Annotation : IEquatable<Annotation>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Annotation"/> class.
	/// </summary>
	public Annotation()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Annotation"/> class with a name.
	/// </summary>
	/// <param name="name">What the annotation is called, as the target language spells it.</param>
	public Annotation(string name) => Name = name;

	/// <summary>
	/// Gets or sets what the annotation is called, as the target language spells it.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets the arguments it is given, each written the way the target language writes it.
	/// </summary>
	/// <remarks>
	/// A sequence rather than one string, so a comma inside an argument stays inside it:
	/// <c>SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")</c> has two
	/// arguments and three commas.
	/// </remarks>
	public Collection<string> Arguments { get; init; } = [];

	/// <summary>
	/// Creates a copy.
	/// </summary>
	/// <returns>The copy.</returns>
	public Annotation Clone()
	{
		Annotation clone = new(Name);

		foreach (string argument in Arguments)
		{
			clone.Arguments.Add(argument);
		}

		return clone;
	}

	/// <inheritdoc />
	/// <remarks>
	/// The name and its arguments with no syntax around them, which is the part every target shares
	/// and the part a target with no metadata syntax writes down.
	/// </remarks>
	public override string ToString() =>
		Arguments.Count == 0 ? Name : $"{Name}({string.Join(", ", Arguments)})";

	/// <inheritdoc />
	public bool Equals(Annotation? other) =>
		other is not null && string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as Annotation);

	/// <inheritdoc />
	public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);
}
