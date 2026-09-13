// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Globalization;
using System.Linq;

/// <summary>
/// How a target spells a member's name.
/// </summary>
/// <remarks>
/// The AST renames nothing — a declaration's name is what the caller wrote, and a generator that
/// changed it would break every reference to it. This exists for the names a generator has to
/// <em>invent</em>, which is a different thing: a property becoming a pair of functions in a target
/// with no properties has no name for the setter until somebody makes one up, and making one up in
/// the wrong convention is how generated code announces itself.
/// </remarks>
public enum NamingStyle
{
	/// <summary>Each word capitalised, run together: <c>SetValue</c>.</summary>
	Pascal,

	/// <summary>The first word lower, the rest capitalised: <c>setValue</c>.</summary>
	Camel,

	/// <summary>All lower, words separated by underscores: <c>set_value</c>.</summary>
	Snake,
}

/// <summary>
/// Spells an invented name the way a target writes one.
/// </summary>
internal static class NameStyles
{
	/// <summary>
	/// Spells a name made of space-separated words.
	/// </summary>
	/// <param name="words">The name, as words separated by spaces.</param>
	/// <param name="style">How the target spells a member's name.</param>
	/// <returns>The name.</returns>
	/// <remarks>
	/// The words come in separated because the caller knows where they are and this cannot: a name
	/// already written as <c>setValue</c> or <c>set_value</c> would have to be taken apart first,
	/// and every rule for doing that is wrong about something.
	/// </remarks>
	internal static string Spell(string words, NamingStyle style)
	{
		string[] parts = [.. words.Split(' ').Where(part => part.Length > 0)];

		if (parts.Length == 0)
		{
			return string.Empty;
		}

		return style switch
		{
			NamingStyle.Snake => string.Join("_", parts.Select(part => part.ToLowerInvariant())),
			NamingStyle.Camel => Lower(parts[0]) + string.Concat(parts.Skip(1).Select(Upper)),
			_ => string.Concat(parts.Select(Upper)),
		};
	}

	private static string Upper(string word) =>
		char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..];

	private static string Lower(string word) =>
		char.ToLower(word[0], CultureInfo.InvariantCulture) + word[1..];
}
