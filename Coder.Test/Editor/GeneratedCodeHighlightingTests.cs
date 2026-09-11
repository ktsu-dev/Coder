// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Editor;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.ImGui.SyntaxHighlighting;
using ktsu.SyntaxHighlighting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the preview pane's syntax highlighting actually applies to what the generators emit.
/// </summary>
/// <remarks>
/// The pane hands the highlighter a generator's <see cref="ILanguageGenerator.LanguageId"/> as the
/// language name. An id the highlighter does not know is not an error to it — it falls back to plain
/// text, which is the right thing to do with a name that usually comes from a markdown fence or a
/// user's choice of file. Here it would be a silent one: the preview would go on rendering, in one
/// colour, and nobody would be told why. So this pins the agreement between the two rather than
/// leaving it to be noticed.
/// </remarks>
[TestClass]
public class GeneratedCodeHighlightingTests
{
	/// <summary>
	/// Builds a function with enough in it that every language spells some keyword generating it.
	/// </summary>
	/// <returns>The function.</returns>
	private static FunctionDeclaration SampleFunction()
	{
		FunctionDeclaration function = new("area") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("scale", "int"));
		function.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("scale"), BinaryOperator.Multiply, Literal.Number(2))));
		return function;
	}

	private static ILanguageGenerator[] Generators() =>
		[new CSharpGenerator(), new PythonGenerator(), new CppGenerator(), new CGenerator(), new JavaScriptGenerator()];

	/// <summary>
	/// Tests that every generator's language id is one the highlighter recognises, by generating real
	/// source in it and finding the keywords the highlighter would colour.
	/// </summary>
	/// <remarks>
	/// Asserted through the tokens rather than by asking the registry, because what matters is not
	/// that a language of that name exists but that it classifies this generator's output. A name
	/// resolving to a definition that then found nothing in the code would pass the weaker test and
	/// still leave the pane one colour.
	/// </remarks>
	[TestMethod]
	public void EveryGenerator_ProducesSourceTheHighlighterClassifies()
	{
		foreach (ILanguageGenerator generator in Generators())
		{
			string code = generator.Generate(SampleFunction());

			HighlightedToken[] tokens =
				[.. ImGuiSyntaxHighlighting.Highlight(code, generator.LanguageId).SelectMany(line => line.Tokens)];

			// Keywords are the discriminating evidence: the plain-text definition the highlighter falls
			// back to has none at all, so finding one means the generator's id resolved to a real
			// language and that language read this generator's output.
			Assert.IsTrue(
				tokens.Any(token => token.Kind is TokenKind.Keyword or TokenKind.ControlKeyword),
				$"{generator.DisplayName} generated source the highlighter found no keyword in, so '{generator.LanguageId}' is not a language it knows");
		}
	}

	/// <summary>
	/// Tests that a language nobody knows is highlighted as plain text rather than throwing, since
	/// the pane draws whatever the selected language is and a refusal there would take the preview
	/// down with it.
	/// </summary>
	[TestMethod]
	public void AnUnknownLanguage_FallsBackToPlainText()
	{
		string code = new CSharpGenerator().Generate(SampleFunction());

		HighlightedToken[] tokens =
			[.. ImGuiSyntaxHighlighting.Highlight(code, "not-a-language").SelectMany(line => line.Tokens)];

		Assert.IsTrue(tokens.Length > 0, "the code should still be rendered");
		Assert.IsFalse(
			tokens.Any(token => token.Kind is TokenKind.Keyword or TokenKind.ControlKeyword),
			"an unknown language has no keywords to find, which is what the test above relies on");

		// Whatever it classified, the text itself is intact: a fallback loses the colour, not the code.
		Assert.AreEqual(
			code.ReplaceLineEndings("\n").TrimEnd('\n'),
			string.Join("\n", ImGuiSyntaxHighlighting.Highlight(code, "not-a-language").Select(line => line.ToText())));
	}
}
