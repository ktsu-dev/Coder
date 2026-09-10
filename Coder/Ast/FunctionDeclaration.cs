// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Represents a function declaration in the abstract syntax tree.
/// </summary>
public class FunctionDeclaration : AstCompositeNode, IHasVisibility, IHasDocumentation
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
	/// Gets or sets what this declares.
	/// </summary>
	public FunctionKind Kind { get; set; }

	/// <summary>
	/// Gets or sets where the behaviour comes from.
	/// </summary>
	/// <remarks>
	/// Named apart from <see cref="Body"/>, which holds the statements: this says whether those
	/// statements are the behaviour at all.
	/// </remarks>
	public FunctionDefinition Definition { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether a derived type may replace this.
	/// </summary>
	public bool IsVirtual { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this has no implementation of its own and a derived
	/// type must supply one.
	/// </summary>
	/// <remarks>
	/// C++ spells this <c>= 0</c> and calls it pure virtual, which is a different thing from
	/// <see cref="IsPure"/> — one says a declaration has no definition, the other says a call has no
	/// effect. An abstract declaration is virtual whether or not <see cref="IsVirtual"/> says so,
	/// since there is nothing else it could be.
	/// </remarks>
	public bool IsAbstract { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether calling this leaves the receiver unchanged.
	/// </summary>
	/// <remarks>
	/// C++ spells this as a trailing <c>const</c> and C# as <c>readonly</c> on a member of a struct.
	/// Weaker than <see cref="IsPure"/>, which says a call has no effect at all rather than no effect
	/// on the one object.
	/// </remarks>
	public bool IsReadOnly { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether ignoring the result is a mistake.
	/// </summary>
	/// <remarks>
	/// C++ spells this <c>[[nodiscard]]</c>, which is also what <see cref="IsPure"/> earns — purity
	/// implies it, since a call that does nothing else and whose result is thrown away did nothing at
	/// all. This says it for a call that does something too: one returning a result that may be a
	/// failure has to be looked at.
	/// </remarks>
	public bool MustUseResult { get; set; }

	/// <inheritdoc/>
	public Collection<string> Documentation { get; init; } = [];

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
			Kind = Kind,
			Definition = Definition,
			IsStatic = IsStatic,
			IsPure = IsPure,
			IsVirtual = IsVirtual,
			IsAbstract = IsAbstract,
			IsReadOnly = IsReadOnly,
			MustUseResult = MustUseResult
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (string line in Documentation)
		{
			clone.Documentation.Add(line);
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
