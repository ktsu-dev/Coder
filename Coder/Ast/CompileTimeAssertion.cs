// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// Something that must be true when the program is built rather than when it runs.
/// </summary>
/// <remarks>
/// What a generated type promises is often not something the type itself can say. A struct that
/// crosses a language boundary and the wire as raw bytes has to be trivially copyable and standard
/// layout, and the moment that stops being true is the moment a saved file starts being wrong — so
/// it is asserted where the type is declared, and the build fails rather than the save file.
/// <para>
/// <see cref="Condition"/> is text, and deliberately. A compile-time predicate is language-specific
/// in a way most of the AST is not: <c>std::is_trivially_copyable_v&lt;T&gt;</c> has no equivalent
/// anywhere else, so there is no shared idea underneath to model. It is carried the same way
/// <see cref="SourceFile.Imports"/> are, and for the same reason — an assertion, like an import, is
/// written for the language the file is for.
/// </para>
/// <para>
/// Only C++ and C have this — <c>static_assert</c> and <c>_Static_assert</c>, which differ in
/// spelling and in whether the message may be left out. Every other target here writes a comment
/// saying what was asserted, because a generated file that silently drops a guarantee looks like one
/// that still makes it.
/// </para>
/// </remarks>
public class CompileTimeAssertion : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CompileTimeAssertion"/> class.
	/// </summary>
	public CompileTimeAssertion()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CompileTimeAssertion"/> class.
	/// </summary>
	/// <param name="condition">The predicate that must hold, as the target language writes it.</param>
	/// <param name="message">What to say when it does not.</param>
	public CompileTimeAssertion(string condition, string? message = null)
	{
		Condition = condition;
		Message = message;
	}

	/// <summary>
	/// Gets or sets the predicate that must hold, as the target language writes it.
	/// </summary>
	public string? Condition { get; set; }

	/// <summary>
	/// Gets or sets what to say when the predicate does not hold.
	/// </summary>
	/// <remarks>
	/// Worth writing rather than leaving to the compiler: the predicate says what is false and the
	/// message says why anyone cared, and only the second tells whoever hits it what to do.
	/// </remarks>
	public string? Message { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "CompileTimeAssertion";

	/// <summary>
	/// Creates a deep clone of this assertion.
	/// </summary>
	/// <returns>A new instance with the same properties.</returns>
	public override AstNode Clone()
	{
		CompileTimeAssertion clone = new()
		{
			Condition = Condition,
			Message = Message,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}
