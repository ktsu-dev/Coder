// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

/// <summary>
/// Represents a literal value in an expression.
/// Examples: 42, "hello", true, 3.14
/// </summary>
/// <typeparam name="T">The type of the literal value.</typeparam>
public class LiteralExpression<T> : Expression
{
	/// <summary>
	/// Gets or sets the literal value.
	/// </summary>
	public T? Value { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LiteralExpression{T}"/> class.
	/// </summary>
	/// <param name="value">The literal value.</param>
	public LiteralExpression(T? value) => Value = value;

	/// <summary>
	/// Initializes a new instance of the <see cref="LiteralExpression{T}"/> class.
	/// Used for deserialization.
	/// </summary>
	public LiteralExpression() => Value = default;

	/// <summary>
	/// Gets the node type name for this literal expression.
	/// </summary>
	/// <returns>The literal expression type name.</returns>
	public override string GetNodeTypeName() => $"Literal<{typeof(T).Name}>";

	/// <summary>
	/// Creates a deep clone of this literal expression.
	/// </summary>
	/// <returns>A new instance with the same property values.</returns>
	public override AstNode Clone()
	{
		LiteralExpression<T> clone = new(Value ?? default!)
		{
			ExpectedType = ExpectedType
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}

	/// <summary>
	/// Returns a string representation of the literal value.
	/// </summary>
	/// <returns>The string representation of the value.</returns>
	public override string ToString() => Value?.ToString() ?? "null";
}

/// <summary>
/// Helper class for creating literal expressions with common types.
/// </summary>
public static class Literal
{
	/// <summary>
	/// Creates a string literal expression.
	/// </summary>
	/// <param name="value">The string value.</param>
	/// <returns>A new string literal expression.</returns>
	public static LiteralExpression<string> Text(string value) => new(value);

	/// <summary>
	/// Creates an integer literal expression.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns>A new integer literal expression.</returns>
	public static LiteralExpression<int> Number(int value) => new(value);

	/// <summary>
	/// Creates a double-precision floating-point literal expression.
	/// </summary>
	/// <param name="value">The double value.</param>
	/// <returns>A new double literal expression.</returns>
	public static LiteralExpression<double> Decimal(double value) => new(value);

	/// <summary>
	/// Creates a boolean literal expression.
	/// </summary>
	/// <param name="value">The boolean value.</param>
	/// <returns>A new boolean literal expression.</returns>
	public static LiteralExpression<bool> Bool(bool value) => new(value);

	/// <summary>
	/// Creates a single-precision floating-point literal expression.
	/// </summary>
	/// <param name="value">The float value.</param>
	/// <returns>A new float literal expression.</returns>
	public static LiteralExpression<float> Single(float value) => new(value);

	/// <summary>
	/// Creates a long integer literal expression.
	/// </summary>
	/// <param name="value">The long value.</param>
	/// <returns>A new long literal expression.</returns>
	public static LiteralExpression<long> BigNumber(long value) => new(value);

	// Keep the old names for backwards compatibility
	/// <summary>Creates a string literal expression.</summary>
	public static LiteralExpression<string> String(string value) => Text(value);
	/// <summary>Creates an integer literal expression.</summary>
	public static LiteralExpression<int> Int(int value) => Number(value);
	/// <summary>Creates a double literal expression.</summary>
	public static LiteralExpression<double> Double(double value) => Decimal(value);
	/// <summary>Creates a float literal expression.</summary>
	public static LiteralExpression<float> Float(float value) => Single(value);
	/// <summary>Creates a long literal expression.</summary>
	public static LiteralExpression<long> Long(long value) => BigNumber(value);
}
