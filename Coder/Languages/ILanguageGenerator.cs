// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Languages;

using ktsu.Coder.Ast;

/// <summary>
/// Defines the interface for language-specific code generators.
/// </summary>
public interface ILanguageGenerator
{
	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public string LanguageId { get; }

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public string DisplayName { get; }

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public string FileExtension { get; }

	/// <summary>
	/// Gets the file extension (without the dot) for the source generated from a particular node.
	/// </summary>
	/// <param name="astNode">The AST node the source is generated from.</param>
	/// <returns>The extension a file holding that source should have.</returns>
	/// <remarks>
	/// Usually <see cref="FileExtension"/>. A language that splits a program into headers and
	/// implementation files names the two differently, and only the node says which one it is.
	/// </remarks>
	public string FileExtensionFor(AstNode astNode) => FileExtension;

	/// <summary>
	/// Generates code in the target language from an AST node.
	/// </summary>
	/// <param name="astNode">The AST node to generate code from.</param>
	/// <returns>A string containing the generated code in the target language.</returns>
	public string Generate(AstNode astNode);

	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public bool CanGenerate(AstNode astNode);
}
