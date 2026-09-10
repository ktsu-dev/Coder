// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a function declaration in the abstract syntax tree.
/// </summary>
public class FunctionDeclaration : AstCompositeNode, IHasVisibility
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FunctionDeclaration"/> class.
	/// </summary>
	public FunctionDeclaration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FunctionDeclaration"/> class with a name.
	/// </summary>
	/// <param name="name">The name of the function.</param>
	public FunctionDeclaration(string name) => Name = name;

	/// <summary>
	/// Gets or sets the name of the function.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the return type of the function.
	/// </summary>
	public TypeReference? ReturnType { get; set; }

	/// <summary>
	/// Gets or sets how widely the function is visible.
	/// </summary>
	public Visibility Visibility { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the function is called without a receiver.
	/// </summary>
	/// <remarks>
	/// C++ and C# spell this <c>static</c>, JavaScript spells it <c>static</c> on a class member and
	/// nothing at module scope, and Python spells it <c>@staticmethod</c> — which also decides whether
	/// the method takes <c>self</c>, so this is the one modifier that changes a signature rather than
	/// only decorating it.
	/// </remarks>
	public bool IsStatic { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the function's result depends only on its arguments
	/// and calling it changes nothing.
	/// </summary>
	/// <remarks>
	/// Not to be confused with C++'s <em>pure virtual</em>, which means a declaration has no
	/// definition. That is a different property and the AST does not model it yet.
	/// <para>
	/// Only two languages can say anything: C++ gets <c>[[nodiscard]]</c> and C# gets
	/// <c>[Pure]</c>. Both are the same observation — discarding the result of a call that does
	/// nothing else is always a mistake — rather than a promise to the optimiser, which is what
	/// <c>__attribute__((pure))</c> would be and which the AST is in no position to make.
	/// </para>
	/// </remarks>
	public bool IsPure { get; set; }

	/// <summary>
	/// Gets or sets a list of parameters for the function.
	/// </summary>
	public Collection<Parameter> Parameters { get; init; } = [];

	/// <summary>
	/// Gets or sets a list of statements that make up the function body.
	/// </summary>
	public Collection<AstNode> Body { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "FunctionDeclaration";

	/// <summary>
	/// Creates a deep clone of this function declaration.
	/// </summary>
	/// <returns>A new instance of the function declaration with the same properties and cloned children.</returns>
	public override AstNode Clone()
	{
		FunctionDeclaration clone = new()
		{
			Name = Name,
			ReturnType = ReturnType?.Clone(),
			Visibility = Visibility,
			IsStatic = IsStatic,
			IsPure = IsPure
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		// Clone parameters
		foreach (Parameter parameter in Parameters)
		{
			clone.Parameters.Add((Parameter)parameter.Clone());
		}

		// Clone body statements
		foreach (AstNode statement in Body)
		{
			clone.Body.Add(statement.Clone());
		}

		// Clone the children dictionary
		foreach ((string key, AstNode child) in Children)
		{
			clone.Children[key] = child.Clone();
		}

		return clone;
	}
}
