// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// How widely a declaration is visible: the language-agnostic form of <c>public</c>,
/// <c>protected</c>, <c>internal</c> and <c>private</c>.
/// </summary>
/// <remarks>
/// An enumeration rather than the modifier's text, because no two target languages spell visibility
/// the same way — C# writes a keyword, C++ groups members under an access label, JavaScript has one
/// real modifier and Python has none at all. A string would only be right for whichever language it
/// was typed for.
/// <para>
/// <see cref="Unspecified"/> is the default so that a declaration nobody has given a visibility to
/// comes out the way that language would write it anyway, rather than acquiring a modifier the
/// author never asked for.
/// </para>
/// </remarks>
public enum Visibility
{
	/// <summary>The language's own default, which is what a declaration carries until it is told otherwise.</summary>
	Unspecified,

	/// <summary>Visible to everything.</summary>
	Public,

	/// <summary>Visible to the declaring type and the types deriving from it.</summary>
	Protected,

	/// <summary>Visible within the assembly, module or package the declaration belongs to.</summary>
	Internal,

	/// <summary>Visible only within the declaring type.</summary>
	Private,
}

/// <summary>
/// Implemented by the declarations that can carry a <see cref="Ast.Visibility"/>.
/// </summary>
/// <remarks>
/// A generator needs a member's visibility without caring which kind of member it is — C++ has to
/// group whatever a class holds under access labels — so the property is reachable through this
/// rather than through a switch repeated in every generator.
/// </remarks>
public interface IHasVisibility
{
	/// <summary>
	/// Gets or sets how widely the declaration is visible.
	/// </summary>
	public Visibility Visibility { get; set; }
}
