// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// What a <see cref="FunctionDeclaration"/> declares.
/// </summary>
/// <remarks>
/// One node rather than five, for the reason <see cref="TypeDeclarationKind"/> is one: all five have
/// a name, parameters, a body and the same modifiers, and differ only in how the language spells the
/// declaration. Splitting them would duplicate the schema, the inspector, the serializer and four
/// generators to say the same thing five times.
/// </remarks>
public enum FunctionKind
{
	/// <summary>An ordinary function or method.</summary>
	Method,

	/// <summary>Builds an instance of the type it belongs to.</summary>
	Constructor,

	/// <summary>Runs when an instance of the type it belongs to is finished with.</summary>
	Destructor,

	/// <summary>
	/// Gives an operator a meaning for the type. <see cref="FunctionDeclaration.Name"/> is the
	/// operator's symbol.
	/// </summary>
	Operator,

	/// <summary>
	/// Converts the type to another. <see cref="FunctionDeclaration.ReturnType"/> is what it converts
	/// to, and the declaration has no name of its own.
	/// </summary>
	ConversionOperator,
}
