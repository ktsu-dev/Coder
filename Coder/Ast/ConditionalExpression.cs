// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// Chooses between two values on a condition.
/// Examples: <c>ready ? go : wait</c>, and in Python <c>go if ready else wait</c>
/// </summary>
/// <remarks>
/// A choice between two values is not a choice between two statements: four of the five targets
/// spell this as an expression and would need a temporary and a branch to say it any other way, and
/// the fifth — Python — reorders the operands rather than lacking it. That reordering is precisely
/// the kind of difference the AST exists to absorb, and it is invisible to a caller that hands a
/// generator this node instead of the text.
/// </remarks>
public class ConditionalExpression : Expression
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConditionalExpression"/> class.
	/// Used for deserialization.
	/// </summary>
	public ConditionalExpression()
	{
		Condition = new LiteralExpression<string>(string.Empty);
		WhenTrue = new LiteralExpression<string>(string.Empty);
		WhenFalse = new LiteralExpression<string>(string.Empty);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConditionalExpression"/> class.
	/// </summary>
	/// <param name="condition">What decides which value the expression takes.</param>
	/// <param name="whenTrue">The value taken when the condition holds.</param>
	/// <param name="whenFalse">The value taken when it does not.</param>
	public ConditionalExpression(Expression condition, Expression whenTrue, Expression whenFalse)
	{
		Ensure.NotNull(condition);
		Ensure.NotNull(whenTrue);
		Ensure.NotNull(whenFalse);
		Condition = condition;
		WhenTrue = whenTrue;
		WhenFalse = whenFalse;
	}

	/// <summary>
	/// Gets or sets what decides which value the expression takes.
	/// </summary>
	public Expression Condition { get; set; }

	/// <summary>
	/// Gets or sets the value taken when the condition holds.
	/// </summary>
	public Expression WhenTrue { get; set; }

	/// <summary>
	/// Gets or sets the value taken when the condition does not hold.
	/// </summary>
	public Expression WhenFalse { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "ConditionalExpression";

	/// <summary>
	/// Creates a deep clone of this expression.
	/// </summary>
	/// <returns>A new instance with all three operands cloned.</returns>
	public override AstNode Clone()
	{
		ConditionalExpression clone = new()
		{
			Condition = (Expression)Condition.DeepClone(),
			WhenTrue = (Expression)WhenTrue.DeepClone(),
			WhenFalse = (Expression)WhenFalse.DeepClone(),
			ExpectedType = ExpectedType,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}
