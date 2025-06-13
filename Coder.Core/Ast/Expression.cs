// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

/// <summary>
/// Represents an abstract base class for all expressions in the AST.
/// Expressions are constructs that can be evaluated to produce a value.
/// </summary>
public abstract class Expression : AstNode
{
	/// <summary>
	/// Gets the expected type of the expression result.
	/// This can be used for type checking and code generation.
	/// </summary>
	public string? ExpectedType { get; set; }

	/// <summary>
	/// Gets the node type name for this expression.
	/// </summary>
	/// <returns>The expression type name.</returns>
	public override string GetNodeTypeName() => GetType().Name;
}
