// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents the point a program starts running at: <c>main</c>, or whatever the target language
/// calls it.
/// </summary>
/// <remarks>
/// A node of its own rather than a function named "main", because every language spells its entry
/// point differently and only some of them spell it as a function at all. C# wants a static
/// <c>Main</c> returning <c>void</c> or <c>int</c>, C++ wants a free <c>int main</c> taking
/// <c>argc</c> and <c>argv</c>, Python wants a function plus the <c>__main__</c> guard that calls
/// it, and JavaScript wants a function plus the call. Naming the intent lets each generator write
/// its own spelling; a <see cref="FunctionDeclaration"/> called "main" would only be right for
/// whichever language it was written for.
/// <para>
/// <see cref="AcceptsArguments"/> and <see cref="ReturnsExitCode"/> are the only two things that
/// vary about an entry point across those languages, so they are what the node carries. The name,
/// the parameter spelling and the wiring that runs it are the generator's business.
/// </para>
/// </remarks>
public class EntryPoint : AstCompositeNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EntryPoint"/> class.
	/// </summary>
	public EntryPoint()
	{
	}

	/// <summary>
	/// Gets or sets a value indicating whether the program reads the command line arguments.
	/// </summary>
	public bool AcceptsArguments { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the program returns an exit code to its caller.
	/// </summary>
	public bool ReturnsExitCode { get; set; }

	/// <summary>
	/// Gets the statements the program runs, in order.
	/// </summary>
	public Collection<AstNode> Body { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "EntryPoint";

	/// <summary>
	/// Creates a deep clone of this entry point.
	/// </summary>
	/// <returns>A new instance with the same properties and a cloned body.</returns>
	public override AstNode Clone()
	{
		EntryPoint clone = new()
		{
			AcceptsArguments = AcceptsArguments,
			ReturnsExitCode = ReturnsExitCode
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (AstNode statement in Body)
		{
			clone.Body.Add(statement.Clone());
		}

		foreach ((string key, AstNode child) in Children)
		{
			clone.Children[key] = child.Clone();
		}

		return clone;
	}
}
