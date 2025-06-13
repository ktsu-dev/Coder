// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

using ktsu.DeepClone;

/// <summary>
/// Represents a literal expression containing a constant value.
/// Examples: 42, "hello", true, 3.14
/// </summary>
/// <typeparam name="T">The type of the literal value.</typeparam>
public class LiteralExpression<T> : Expression
{
	/// <summary>
	/// Gets or sets the literal value.
	/// </summary>
	public T Value { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LiteralExpression{T}"/> class.
	/// </summary>
	/// <param name="value">The literal value.</param>
	public LiteralExpression(T value)
	{
		Value = value;
		ExpectedType = typeof(T).Name;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LiteralExpression{T}"/> class.
	/// Used for deserialization.
	/// </summary>
	public LiteralExpression()
	{
		Value = default!;
		ExpectedType = typeof(T).Name;
	}

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
		LiteralExpression<T> clone = new(Value?.DeepClone() ?? default!)
		{
			ExpectedType = ExpectedType
		};

		// Copy metadata
		foreach (System.Collections.Generic.KeyValuePair<string, object?> kvp in Metadata)
		{
			clone.Metadata[kvp.Key] = kvp.Value?.DeepClone();
		}

		return clone;
	}

	/// <summary>
	/// Returns a string representation of the literal value.
	/// </summary>
	/// <returns>String representation of the value.</returns>
	public override string ToString() => Value?.ToString() ?? "null";
}

/// <summary>
/// Helper class for creating common literal expressions.
/// </summary>
public static class Literal
{
	/// <summary>
	/// Creates a string literal expression.
	/// </summary>
	/// <param name="value">The string value.</param>
	/// <returns>A string literal expression.</returns>
	public static LiteralExpression<string> String(string value) => new(value);

	/// <summary>
	/// Creates an integer literal expression.
	/// </summary>
	/// <param name="value">The integer value.</param>
	/// <returns>An integer literal expression.</returns>
	public static LiteralExpression<int> Int(int value) => new(value);

	/// <summary>
	/// Creates a double literal expression.
	/// </summary>
	/// <param name="value">The double value.</param>
	/// <returns>A double literal expression.</returns>
	public static LiteralExpression<double> Double(double value) => new(value);

	/// <summary>
	/// Creates a boolean literal expression.
	/// </summary>
	/// <param name="value">The boolean value.</param>
	/// <returns>A boolean literal expression.</returns>
	public static LiteralExpression<bool> Bool(bool value) => new(value);

	/// <summary>
	/// Creates a float literal expression.
	/// </summary>
	/// <param name="value">The float value.</param>
	/// <returns>A float literal expression.</returns>
	public static LiteralExpression<float> Float(float value) => new(value);

	/// <summary>
	/// Creates a long literal expression.
	/// </summary>
	/// <param name="value">The long value.</param>
	/// <returns>A long literal expression.</returns>
	public static LiteralExpression<long> Long(long value) => new(value);
}
