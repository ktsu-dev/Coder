// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System;

/// <summary>
/// What a type parameter is required to be.
/// </summary>
/// <remarks>
/// Named intents rather than the text of a constraint, for the reason <see cref="Visibility"/> is
/// an enumeration rather than a modifier's spelling: each target writes them differently, and three
/// of them write most of these not at all. A generator handed <c>struct, INumber&lt;T&gt;</c> as a
/// string could only paste it, which would put C# in every file the AST produced.
/// <para>
/// Four, and deliberately not more. These are the ones with an idea underneath that survives the
/// trip between languages — a value, a reference, something constructible, something that is a
/// named other thing. A C++ concept is a predicate over a type and can say anything at all
/// (<c>requires (T a) { a.begin(); }</c>), which is the same reason
/// <see cref="CompileTimeAssertion.Condition"/> is text: there is no shared idea to model, only a
/// language's own way of asking a question.
/// </para>
/// </remarks>
public enum TypeConstraintKind
{
	/// <summary>The argument is, or derives from, a named type.</summary>
	Implements,

	/// <summary>The argument is copied rather than referenced, and is never absent.</summary>
	ValueType,

	/// <summary>The argument is referenced rather than copied, and may be absent.</summary>
	ReferenceType,

	/// <summary>The argument can be made with no arguments of its own.</summary>
	Constructible,
}

/// <summary>
/// One requirement on a type parameter.
/// </summary>
/// <remarks>
/// A value rather than an <see cref="AstNode"/>, and for the same reason
/// <see cref="ClassDeclaration.SpecialisationArguments"/> holds values: a constraint is part of the
/// thing being declared rather than a member of it, so there is nothing in the editor for it to be
/// a node of. <see cref="Parse(string)"/> and <see cref="ToString"/> are inverses, which is what
/// lets a document carry a whole parameter as one readable line.
/// </remarks>
public sealed class TypeConstraint : IEquatable<TypeConstraint>
{
	/// <summary>What a constraint asking for a value type is written as.</summary>
	private const string ValueTypeKeyword = "struct";

	/// <summary>What a constraint asking for a reference type is written as.</summary>
	private const string ReferenceTypeKeyword = "class";

	/// <summary>What a constraint asking for a no-argument constructor is written as.</summary>
	private const string ConstructibleKeyword = "new()";

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeConstraint"/> class.
	/// </summary>
	public TypeConstraint()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeConstraint"/> class for a named type.
	/// </summary>
	/// <param name="type">The type the argument has to be.</param>
	public TypeConstraint(TypeReference? type)
	{
		Kind = TypeConstraintKind.Implements;
		Type = type;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TypeConstraint"/> class of a given kind.
	/// </summary>
	/// <param name="kind">What the argument is required to be.</param>
	public TypeConstraint(TypeConstraintKind kind) => Kind = kind;

	/// <summary>
	/// Gets or sets what the argument is required to be.
	/// </summary>
	public TypeConstraintKind Kind { get; set; }

	/// <summary>
	/// Gets or sets the type the argument has to be, when <see cref="Kind"/> is
	/// <see cref="TypeConstraintKind.Implements"/>; null otherwise.
	/// </summary>
	public TypeReference? Type { get; set; }

	/// <summary>
	/// Reads a written constraint.
	/// </summary>
	/// <param name="text">The constraint as it is written.</param>
	/// <returns>The constraint.</returns>
	/// <remarks>
	/// The three keywords are spelled the way C# spells them, which is a choice rather than a
	/// discovery — some spelling had to be the written one, and <see cref="TypeReference"/> already
	/// writes a generic argument the way the C family does. Anything that is not one of the three
	/// is a type, so a spelling this does not recognise becomes a requirement to be that named
	/// thing rather than an error.
	/// </remarks>
	public static TypeConstraint Parse(string text)
	{
		string written = (text ?? string.Empty).Trim();

		return written switch
		{
			ValueTypeKeyword => new TypeConstraint(TypeConstraintKind.ValueType),
			ReferenceTypeKeyword => new TypeConstraint(TypeConstraintKind.ReferenceType),
			ConstructibleKeyword => new TypeConstraint(TypeConstraintKind.Constructible),
			_ => new TypeConstraint(TypeReference.Parse(written)),
		};
	}

	/// <inheritdoc />
	public override string ToString() => Kind switch
	{
		TypeConstraintKind.ValueType => ValueTypeKeyword,
		TypeConstraintKind.ReferenceType => ReferenceTypeKeyword,
		TypeConstraintKind.Constructible => ConstructibleKeyword,
		_ => Type?.ToString() ?? string.Empty,
	};

	/// <summary>
	/// Creates a copy.
	/// </summary>
	/// <returns>The copy.</returns>
	public TypeConstraint Clone() => new() { Kind = Kind, Type = Type?.Clone() };

	/// <inheritdoc />
	public bool Equals(TypeConstraint? other) =>
		other is not null && Kind == other.Kind && Equals(Type, other.Type);

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as TypeConstraint);

	/// <inheritdoc />
	public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);
}
