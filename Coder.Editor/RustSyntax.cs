// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using ktsu.SyntaxHighlighting;

/// <summary>
/// Teaches the syntax highlighter to read Rust.
/// </summary>
/// <remarks>
/// The highlighter ships definitions for fifteen languages and Rust is not one of them, so the
/// preview pane would draw generated Rust as plain text — the one failure mode
/// <c>ImGuiSyntaxHighlighting</c> has, and a silent one. Definitions are plain data and the registry
/// takes one from an application, which is what this is.
/// <para>
/// It lives in the editor rather than in the library for the reason the library has no UI
/// dependency at all: what a language looks like on a screen is the editor's business, and
/// <c>RustGenerator</c> would not otherwise know that a highlighter exists.
/// </para>
/// <para>
/// The keyword list is the language's, not the generator's. A highlighter reads whatever is in the
/// pane — a file somebody opened, or output from a generator that has learned a new spelling since
/// — so listing only what this generator emits today would make the pane wrong tomorrow.
/// </para>
/// </remarks>
internal static class RustSyntax
{
	/// <summary>
	/// The language name the generator reports and the highlighter is asked for.
	/// </summary>
	private const string LanguageName = "rust";

	/// <summary>
	/// Registers the definition, if it is not registered already.
	/// </summary>
	/// <remarks>
	/// Idempotent, and deliberately: the registry is process-global, the editor builds more than one
	/// application object over a test run, and registering twice would replace a definition with an
	/// identical one for no reason.
	/// </remarks>
	public static void Register()
	{
		if (LanguageRegistry.TryGet(LanguageName, out _))
		{
			return;
		}

		LanguageRegistry.Register(Definition);
	}

	/// <summary>
	/// Gets what the tokenizer needs in order to read Rust.
	/// </summary>
	private static LanguageDefinition Definition => new()
	{
		Name = LanguageName,
		Aliases = ["rs"],
		CaseSensitive = true,

		// `///` and `//!` are documentation and are drawn as such; `//` is an ordinary comment. The
		// documentation forms come first, because the first rule that matches wins and every one of
		// them starts with the ordinary one.
		LineComments =
		[
			new LineCommentRule { Prefix = "///", Kind = TokenKind.DocComment },
			new LineCommentRule { Prefix = "//!", Kind = TokenKind.DocComment },
			new LineCommentRule { Prefix = "//" },
		],
		BlockComments = [new BlockCommentRule { Open = "/*", Close = "*/" }],
		Strings =
		[
			new StringRule { Open = "\"", Close = "\"" },
			new StringRule { Open = "'", Close = "'" },
		],

		Keywords =
		[
			"as", "async", "await", "const", "crate", "dyn", "enum", "extern", "fn", "impl", "in",
			"let", "mod", "move", "mut", "pub", "ref", "self", "Self", "static", "struct", "super",
			"trait", "type", "union", "unsafe", "use", "where",
		],
		ControlKeywords = ["break", "continue", "else", "for", "if", "loop", "match", "return", "while"],
		Types =
		[
			"bool", "char", "f32", "f64", "i8", "i16", "i32", "i64", "i128", "isize", "str",
			"u8", "u16", "u32", "u64", "u128", "usize", "String", "Vec", "Box", "Option", "Result",
			"HashMap", "HashSet",
		],
		Constants = ["true", "false", "None", "Some", "Ok", "Err"],

		HighlightFunctionCalls = true,
		IdentifierCharacters = "_",
		IdentifierStartCharacters = "_",
		OperatorCharacters = "+-*/%=<>!&|^~?:",
		PunctuationCharacters = "(){}[];,.",
	};
}
