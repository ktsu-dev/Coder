// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Implemented by a declaration that can carry documentation, so a generator can emit it without
/// switching on which kind of declaration it is.
/// </summary>
/// <remarks>
/// Lines rather than one string, because most of what ends up here is not prose: a schema knows a
/// member's unit, its range and how it is quantised on the wire, and a generated type can hold none
/// of that, so the facts are written where whoever reads the generated code will see them. Each line
/// is emitted as one comment.
/// </remarks>
public interface IHasDocumentation
{
	/// <summary>
	/// Gets the documentation lines, in the order they should be emitted.
	/// </summary>
	public Collection<string> Documentation { get; }
}
