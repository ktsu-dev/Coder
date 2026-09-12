// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using ktsu.SyntaxHighlighting;

/// <summary>
/// Teaches the syntax highlighter the languages it does not already know.
/// </summary>
/// <remarks>
/// The highlighter ships definitions for fifteen languages, and two of the generators here target
/// one it does not: an id it has never heard of is not an error to it, so the preview pane would go
/// on rendering generated Rust or Go in one colour and nobody would be told why. Registering is what
/// closes that, and there is one of these rather than one per language because the closing is the
/// same each time and only the definition differs.
/// <para>
/// It lives in the editor rather than in the library for the reason the library has no UI dependency
/// at all: what a language looks like on a screen is the editor's business, and a generator would
/// not otherwise know that a highlighter exists.
/// </para>
/// </remarks>
internal static class EditorSyntax
{
	/// <summary>
	/// Registers every definition the editor carries.
	/// </summary>
	public static void Register()
	{
		Register(RustSyntax.Definition);
		Register(GoSyntax.Definition);
	}

	/// <summary>
	/// Registers one definition, if it is not registered already.
	/// </summary>
	/// <param name="definition">The definition to register.</param>
	/// <remarks>
	/// Idempotent, and deliberately: the registry is process-global, the editor builds more than one
	/// application object over a test run, and registering twice would replace a definition with an
	/// identical one for no reason.
	/// </remarks>
	private static void Register(LanguageDefinition definition)
	{
		if (LanguageRegistry.TryGet(definition.Name, out _))
		{
			return;
		}

		LanguageRegistry.Register(definition);
	}
}
