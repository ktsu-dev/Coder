// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// How a value of a type is reached.
/// </summary>
/// <remarks>
/// Only the distinctions the generators can act on are modelled. A language that has no indirection
/// of its own — Python, JavaScript — emits <see cref="Reference"/> and <see cref="Pointer"/> the same
/// way it emits <see cref="None"/>, because the distinction has no spelling there rather than because
/// it was lost.
/// </remarks>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
	Justification = "A pointer is what this member means. Naming it around the rule would leave the enum "
		+ "unable to say the one thing it exists to distinguish.")]
public enum TypeIndirection
{
	/// <summary>The value itself.</summary>
	None,

	/// <summary>A reference to the value, which is always bound. C++ spells this <c>&amp;</c>.</summary>
	Reference,

	/// <summary>A pointer to the value, which may be null. C++ spells this <c>*</c>.</summary>
	Pointer,
}
