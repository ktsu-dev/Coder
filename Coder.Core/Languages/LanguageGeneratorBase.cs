// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Languages;

using System;
using System.Text;
using ktsu.Coder.Core.Ast;

/// <summary>
/// Provides a base implementation for language generators with common functionality.
/// </summary>
public abstract class LanguageGeneratorBase : ILanguageGenerator
{
	/// <summary>
	/// Gets the unique identifier for this language generator.
	/// </summary>
	public abstract string LanguageId { get; }

	/// <summary>
	/// Gets the display name for this language generator.
	/// </summary>
	public abstract string DisplayName { get; }

	/// <summary>
	/// Gets the file extension (without the dot) used for this language.
	/// </summary>
	public abstract string FileExtension { get; }

	/// <summary>
	/// Generates code in the target language from an AST node.
	/// </summary>
	/// <param name="astNode">The AST node to generate code from.</param>
	/// <returns>A string containing the generated code in the target language.</returns>
	public string Generate(AstNode astNode)
	{
		ArgumentNullException.ThrowIfNull(astNode);

		if (!CanGenerate(astNode))
		{
			throw new NotSupportedException($"Cannot generate code for node type: {astNode.GetNodeTypeName()}");
		}

		var builder = new StringBuilder();
		GenerateInternal(astNode, builder, 0);
		return builder.ToString();
	}

	/// <summary>
	/// Determines whether this generator can generate code for the specified AST node.
	/// </summary>
	/// <param name="astNode">The AST node to check.</param>
	/// <returns>True if this generator can generate code for the node; otherwise, false.</returns>
	public virtual bool CanGenerate(AstNode astNode)
	{
		return astNode != null && astNode is FunctionDeclaration
			or ReturnStatement
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>;
	}

	/// <summary>
	/// Internal method to generate code with proper indentation.
	/// </summary>
	/// <param name="node">The AST node to generate code from.</param>
	/// <param name="builder">The string builder to append code to.</param>
	/// <param name="indentLevel">The current indentation level.</param>
	protected abstract void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel);

<<<<<<< TODO: Unmerged change from project 'Coder.Core(net9.0)', Before:
    protected void Indent(StringBuilder builder, int indentLevel, int indentSize = 4)
    {
        builder.Append(' ', indentLevel * indentSize);
    }
} 
=======
    protected void Indent(StringBuilder builder, int indentLevel, int indentSize = 4) => builder.Append(' ', indentLevel * indentSize);
}
>>>>>>> After

	/// <summary>
	/// Adds indentation to the code based on the current indentation level.
	/// </summary>
	/// <param name="builder">The string builder to append indentation to.</param>
	/// <param name="indentLevel">The indentation level.</param>
	/// <param name="indentSize">The number of spaces per indent level. Default is 4.</param>
	protected static void Indent(StringBuilder builder, int indentLevel, int indentSize = 4) => builder.Append(' ', indentLevel * indentSize);
}
