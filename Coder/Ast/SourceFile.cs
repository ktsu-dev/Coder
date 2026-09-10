// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a whole source file: what it says about itself, what it depends on, and what it
/// declares.
/// </summary>
/// <remarks>
/// <see cref="Imports"/> is the one part of the AST that does not translate. A C++ include path, a
/// C# namespace and a Python module are different kinds of thing that happen to appear in the same
/// position, and no mapping between them exists to be written — so each is carried as the text the
/// file it is for needs, and a file is built for a language rather than for all of them. Everything
/// below the imports is language-agnostic as usual.
/// <para>
/// An empty import separates groups: it emits a blank line rather than an import of nothing, which
/// is how the standard headers are told apart from the project's own without the AST having to know
/// which is which.
/// </para>
/// </remarks>
public class SourceFile : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SourceFile"/> class.
	/// </summary>
	public SourceFile()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SourceFile"/> class with a name.
	/// </summary>
	/// <param name="name">The file's name.</param>
	public SourceFile(string name) => Name = name;

	/// <summary>
	/// Gets or sets the file's name, which is what it should be written as.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets the banner comment lines, emitted above everything else.
	/// </summary>
	/// <remarks>
	/// An ordinary comment rather than a documentation one: this describes the file, and a
	/// documentation comment describes the declaration that follows it — which, at the top of a file,
	/// would be whichever declaration happens to come first.
	/// </remarks>
	public Collection<string> HeaderComment { get; init; } = [];

	/// <summary>
	/// Gets what the file depends on, in the order it should be written.
	/// </summary>
	public Collection<string> Imports { get; init; } = [];

	/// <summary>
	/// Gets the declarations the file holds, in the order they should be emitted.
	/// </summary>
	public Collection<AstNode> Members { get; init; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the file exists to be included by others.
	/// </summary>
	/// <remarks>
	/// C++ is the only target here that splits a file in two, and a header needs to say that
	/// including it twice is including it once. Every other language ignores this.
	/// </remarks>
	public bool IsHeader { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "SourceFile";

	/// <summary>
	/// Creates a deep clone of this file.
	/// </summary>
	/// <returns>A new instance with the same properties and cloned members.</returns>
	public override AstNode Clone()
	{
		SourceFile clone = new()
		{
			Name = Name,
			IsHeader = IsHeader,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (string line in HeaderComment)
		{
			clone.HeaderComment.Add(line);
		}

		foreach (string import in Imports)
		{
			clone.Imports.Add(import);
		}

		foreach (AstNode member in Members)
		{
			clone.Members.Add(member.Clone());
		}

		return clone;
	}
}
