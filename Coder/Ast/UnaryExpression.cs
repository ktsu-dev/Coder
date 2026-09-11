// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// Represents a unary expression: one operator applied to one operand.
/// Examples: -x, !ready, ~mask
/// </summary>
/// <remarks>
/// Only prefix operators are modelled. Increment and decrement are deliberately absent: Python has
/// no spelling for them, and <see cref="AssignmentStatement"/> with
/// <see cref="AssignmentOperator.AddAssign"/> already expresses the same effect in every target
/// language.
/// </remarks>
public class UnaryExpression : Expression
{
	/// <summary>
	/// Gets or sets the unary operator.
	/// </summary>
	public UnaryOperator Operator { get; set; }

	/// <summary>
	/// Gets or sets the operand the operator is applied to.
	/// </summary>
	public Expression Operand { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UnaryExpression"/> class.
	/// </summary>
	/// <param name="operator">The unary operator.</param>
	/// <param name="operand">The operand.</param>
	public UnaryExpression(UnaryOperator @operator, Expression operand)
	{
		Ensure.NotNull(operand);
		Operator = @operator;
		Operand = operand;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UnaryExpression"/> class.
	/// Used for deserialization.
	/// </summary>
	public UnaryExpression()
	{
		Operator = UnaryOperator.Negate;
		Operand = new LiteralExpression<string>("");
	}

	/// <summary>
	/// Creates a deep clone of this unary expression.
	/// </summary>
	/// <returns>A new instance with the same property values.</returns>
	public override AstNode Clone()
	{
		UnaryExpression clone = new()
		{
			Operator = Operator,
			Operand = (Expression)Operand.DeepClone(),
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
/// Defines the types of unary operators supported.
/// </summary>
/// <remarks>
/// Every operator here exists in all five target languages, so no AST using them is untranslatable.
/// Only the spelling of <see cref="LogicalNot"/> differs, in Python.
/// </remarks>
public enum UnaryOperator
{
	/// <summary>Arithmetic negation (-)</summary>
	Negate,

	/// <summary>Unary plus (+)</summary>
	Plus,

	/// <summary>Logical negation (!), spelled <c>not</c> in Python</summary>
	LogicalNot,

	/// <summary>Bitwise complement (~)</summary>
	BitwiseNot,
}
