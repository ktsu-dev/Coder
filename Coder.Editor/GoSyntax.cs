// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using ktsu.SyntaxHighlighting;

/// <summary>
/// What the syntax highlighter needs in order to read Go.
/// </summary>
/// <remarks>
/// The highlighter ships definitions for fifteen languages and Go is not one of them, so the preview
/// pane would draw generated Go as plain text — the one failure mode <c>ImGuiSyntaxHighlighting</c>
/// has, and a silent one. This is the same arrangement <see cref="RustSyntax"/> is, handed to the
/// registry by <see cref="EditorSyntax"/> for the same reason.
/// <para>
/// The keyword list is the language's, not the generator's. A highlighter reads whatever is in the
/// pane — a file somebody opened, or output from a generator that has learned a new spelling since —
/// so listing only what this generator emits today would make the pane wrong tomorrow.
/// </para>
/// </remarks>
internal static class GoSyntax
{
	/// <summary>
	/// The language name the generator reports and the highlighter is asked for.
	/// </summary>
	private const string LanguageName = "go";

	/// <summary>
	/// Gets what the tokenizer needs in order to read Go.
	/// </summary>
	/// <remarks>
	/// Go has one comment marker rather than two, because its documentation is an ordinary comment in
	/// the right place — which is why there is no <c>DocComment</c> rule here and why a doc comment in
	/// the pane is drawn as the comment it is.
	/// </remarks>
	public static LanguageDefinition Definition => new()
	{
		Name = LanguageName,
		Aliases = ["golang"],
		CaseSensitive = true,

		LineComments = [new LineCommentRule { Prefix = "//" }],
		BlockComments = [new BlockCommentRule { Open = "/*", Close = "*/" }],

		// The third of Go's string forms is the raw one, which runs to the next backquote and honours
		// no escape — a rule the tokenizer's open-and-close pair already describes exactly.
		Strings =
		[
			new StringRule { Open = "\"", Close = "\"" },
			new StringRule { Open = "'", Close = "'" },
			new StringRule { Open = "`", Close = "`" },
		],

		Keywords =
		[
			"chan", "const", "defer", "func", "go", "import", "interface", "map", "package", "range",
			"struct", "type", "var",
		],
		ControlKeywords =
		[
			"break", "case", "continue", "default", "else", "fallthrough", "for", "goto", "if",
			"return", "select", "switch",
		],
		Types =
		[
			"any", "bool", "byte", "comparable", "complex64", "complex128", "error", "float32",
			"float64", "int", "int8", "int16", "int32", "int64", "rune", "string", "uint", "uint8",
			"uint16", "uint32", "uint64", "uintptr",
		],
		Constants = ["true", "false", "nil", "iota"],

		HighlightFunctionCalls = true,
		IdentifierCharacters = "_",
		IdentifierStartCharacters = "_",
		OperatorCharacters = "+-*/%=<>!&|^~:",
		PunctuationCharacters = "(){}[];,.",
	};
}
