// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// Where a function's behaviour comes from.
/// </summary>
/// <remarks>
/// A declaration with no statements is ambiguous without this: it could be a function that does
/// nothing, one whose behaviour the language supplies, or one that exists to be refused. The three
/// are different things and only the first is an empty body.
/// </remarks>
public enum FunctionDefinition
{
	/// <summary>The statements in <see cref="FunctionDeclaration.Body"/> are the behaviour.</summary>
	Provided,

	/// <summary>The language supplies the behaviour. C++ and C# spell this <c>= default</c>.</summary>
	Defaulted,

	/// <summary>
	/// Calling it is an error the compiler should catch. C++ and C# spell this <c>= delete</c>; a
	/// language with no such thing has to leave the call to fail at runtime, or not at all.
	/// </summary>
	Deleted,
}
