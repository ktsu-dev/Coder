// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// What kind of type a <see cref="ClassDeclaration"/> declares.
/// </summary>
/// <remarks>
/// One node rather than three, because the three differ only in the keyword and in what a language
/// assumes about their members — nothing about the shape of the declaration changes. What each
/// generator does with the difference is its own business: C++ has no interface keyword and spells
/// one as a class whose members are all public, and Python and JavaScript have neither a struct nor
/// an interface and spell all three as a class.
/// </remarks>
public enum TypeDeclarationKind
{
	/// <summary>A reference type with private members by default.</summary>
	Class,

	/// <summary>A value type with public members by default.</summary>
	Struct,

	/// <summary>A set of members an implementation supplies, with no state of its own.</summary>
	Interface,
}
