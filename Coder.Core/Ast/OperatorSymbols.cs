// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System;

/// <summary>
/// The source spellings of the AST's operators.
/// </summary>
/// <remarks>
/// The spellings live with the operators rather than with any one consumer, because both the AST's
/// own <c>ToString</c> and every language generator need them. Every language the generators target
/// spells the assignment operators identically, and C# and C++ spell the binary operators the
/// C-family way; a language that differs on one operator handles that operator and defers the rest
/// here.
/// </remarks>
public static class OperatorSymbols
{
	/// <summary>
	/// Gets the source spelling of an assignment operator.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException"><paramref name="op"/> has no spelling.</exception>
	public static string GetSymbol(AssignmentOperator op) =>
		TryGetSymbol(op, out string? symbol) && symbol is not null
			? symbol
			: throw new NotSupportedException($"Unsupported assignment operator: {op}");

	/// <summary>
	/// Gets the C-family source spelling of a binary operator.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException"><paramref name="op"/> has no spelling.</exception>
	public static string GetSymbol(BinaryOperator op) =>
		TryGetSymbol(op, out string? symbol) && symbol is not null
			? symbol
			: throw new NotSupportedException($"Unsupported binary operator: {op}");

	/// <summary>
	/// Gets the C-family source spelling of a unary operator.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <returns>The operator's source spelling.</returns>
	/// <exception cref="NotSupportedException"><paramref name="op"/> has no spelling.</exception>
	public static string GetSymbol(UnaryOperator op) =>
		TryGetSymbol(op, out string? symbol) && symbol is not null
			? symbol
			: throw new NotSupportedException($"Unsupported unary operator: {op}");

	/// <summary>
	/// Tries to get the source spelling of an assignment operator.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <param name="symbol">The operator's source spelling, or null.</param>
	/// <returns>True if the operator has a spelling; otherwise, false.</returns>
	/// <remarks>For callers that must not throw on an out-of-range value, such as <c>ToString</c>.</remarks>
	public static bool TryGetSymbol(AssignmentOperator op, out string? symbol)
	{
		symbol = op switch
		{
			AssignmentOperator.Assign => "=",
			AssignmentOperator.AddAssign => "+=",
			AssignmentOperator.SubtractAssign => "-=",
			AssignmentOperator.MultiplyAssign => "*=",
			AssignmentOperator.DivideAssign => "/=",
			AssignmentOperator.ModuloAssign => "%=",
			AssignmentOperator.BitwiseAndAssign => "&=",
			AssignmentOperator.BitwiseOrAssign => "|=",
			AssignmentOperator.BitwiseXorAssign => "^=",
			AssignmentOperator.LeftShiftAssign => "<<=",
			AssignmentOperator.RightShiftAssign => ">>=",
			_ => null
		};

		return symbol is not null;
	}

	/// <summary>
	/// Tries to get the C-family source spelling of a binary operator.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <param name="symbol">The operator's source spelling, or null.</param>
	/// <returns>True if the operator has a spelling; otherwise, false.</returns>
	/// <remarks>For callers that must not throw on an out-of-range value, such as <c>ToString</c>.</remarks>
	public static bool TryGetSymbol(BinaryOperator op, out string? symbol)
	{
		symbol = op switch
		{
			BinaryOperator.Add => "+",
			BinaryOperator.Subtract => "-",
			BinaryOperator.Multiply => "*",
			BinaryOperator.Divide => "/",
			BinaryOperator.Modulo => "%",
			BinaryOperator.Equal => "==",
			BinaryOperator.NotEqual => "!=",
			BinaryOperator.LessThan => "<",
			BinaryOperator.LessThanOrEqual => "<=",
			BinaryOperator.GreaterThan => ">",
			BinaryOperator.GreaterThanOrEqual => ">=",
			BinaryOperator.LogicalAnd => "&&",
			BinaryOperator.LogicalOr => "||",
			BinaryOperator.BitwiseAnd => "&",
			BinaryOperator.BitwiseOr => "|",
			BinaryOperator.BitwiseXor => "^",
			BinaryOperator.LeftShift => "<<",
			BinaryOperator.RightShift => ">>",
			_ => null
		};

		return symbol is not null;
	}

	/// <summary>
	/// Tries to get the C-family source spelling of a unary operator.
	/// </summary>
	/// <param name="op">The operator to spell.</param>
	/// <param name="symbol">The operator's source spelling, or null.</param>
	/// <returns>True if the operator has a spelling; otherwise, false.</returns>
	/// <remarks>For callers that must not throw on an out-of-range value, such as <c>ToString</c>.</remarks>
	public static bool TryGetSymbol(UnaryOperator op, out string? symbol)
	{
		symbol = op switch
		{
			UnaryOperator.Negate => "-",
			UnaryOperator.Plus => "+",
			UnaryOperator.LogicalNot => "!",
			UnaryOperator.BitwiseNot => "~",
			_ => null
		};

		return symbol is not null;
	}
}
