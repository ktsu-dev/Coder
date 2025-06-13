// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

/// <summary>
/// Represents a binary expression with a left operand, operator, and right operand.
/// Examples: a + b, x * y, result == expected
/// </summary>
public class BinaryExpression : Expression
{
	/// <summary>
	/// Gets or sets the left operand of the binary expression.
	/// </summary>
	public Expression Left { get; set; }

	/// <summary>
	/// Gets or sets the binary operator.
	/// </summary>
	public BinaryOperator Operator { get; set; }

	/// <summary>
	/// Gets or sets the right operand of the binary expression.
	/// </summary>
	public Expression Right { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="BinaryExpression"/> class.
	/// </summary>
	/// <param name="left">The left operand.</param>
	/// <param name="operator">The binary operator.</param>
	/// <param name="right">The right operand.</param>
	public BinaryExpression(Expression left, BinaryOperator @operator, Expression right)
	{
		Left = left ?? throw new ArgumentNullException(nameof(left));
		Operator = @operator;
		Right = right ?? throw new ArgumentNullException(nameof(right));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BinaryExpression"/> class.
	/// Used for deserialization.
	/// </summary>
	public BinaryExpression()
	{
		Left = new LiteralExpression<string>("");
		Operator = BinaryOperator.Add;
		Right = new LiteralExpression<string>("");
	}

	/// <summary>
	/// Creates a deep clone of this binary expression.
	/// </summary>
	/// <returns>A new instance with the same property values.</returns>
	public override AstNode Clone()
	{
		BinaryExpression clone = new()
		{
			Left = (Expression)Left.DeepClone(),
			Operator = Operator,
			Right = (Expression)Right.DeepClone(),
			ExpectedType = ExpectedType
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}

/// <summary>
/// Defines the types of binary operators supported.
/// </summary>
public enum BinaryOperator
{
	/// <summary>Addition operator (+)</summary>
	Add,

	/// <summary>Subtraction operator (-)</summary>
	Subtract,

	/// <summary>Multiplication operator (*)</summary>
	Multiply,

	/// <summary>Division operator (/)</summary>
	Divide,

	/// <summary>Modulo operator (%)</summary>
	Modulo,

	/// <summary>Equality operator (==)</summary>
	Equal,

	/// <summary>Inequality operator (!=)</summary>
	NotEqual,

	/// <summary>Less than operator (&lt;)</summary>
	LessThan,

	/// <summary>Less than or equal operator (&lt;=)</summary>
	LessThanOrEqual,

	/// <summary>Greater than operator (&gt;)</summary>
	GreaterThan,

	/// <summary>Greater than or equal operator (&gt;=)</summary>
	GreaterThanOrEqual,

	/// <summary>Logical AND operator (&amp;&amp;)</summary>
	LogicalAnd,

	/// <summary>Logical OR operator (||)</summary>
	LogicalOr,

	/// <summary>Bitwise AND operator (&amp;)</summary>
	BitwiseAnd,

	/// <summary>Bitwise OR operator (|)</summary>
	BitwiseOr,

	/// <summary>Bitwise XOR operator (^)</summary>
	BitwiseXor,

	/// <summary>Left shift operator (&lt;&lt;)</summary>
	LeftShift,

	/// <summary>Right shift operator (&gt;&gt;)</summary>
	RightShift,
}
