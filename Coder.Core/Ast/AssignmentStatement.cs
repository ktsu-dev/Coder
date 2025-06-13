// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

/// <summary>
/// Represents an assignment statement.
/// Examples: x = 5; result = a + b; items[0] = "hello";
/// </summary>
public class AssignmentStatement : AstNode
{
	/// <summary>
	/// Gets or sets the target of the assignment (variable name, property access, etc.).
	/// </summary>
	public Expression Target { get; set; }

	/// <summary>
	/// Gets or sets the value to assign.
	/// </summary>
	public Expression Value { get; set; }

	/// <summary>
	/// Gets or sets the assignment operator type.
	/// </summary>
	public AssignmentOperator Operator { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="AssignmentStatement"/> class.
	/// </summary>
	/// <param name="target">The assignment target.</param>
	/// <param name="value">The value to assign.</param>
	/// <param name="operator">The assignment operator (defaults to simple assignment).</param>
	public AssignmentStatement(Expression target, Expression value, AssignmentOperator @operator = AssignmentOperator.Assign)
	{
		Target = target ?? throw new ArgumentNullException(nameof(target));
		Value = value ?? throw new ArgumentNullException(nameof(value));
		Operator = @operator;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AssignmentStatement"/> class.
	/// Used for deserialization.
	/// </summary>
	public AssignmentStatement()
	{
		Target = new VariableReference("");
		Value = new LiteralExpression<string>("");
		Operator = AssignmentOperator.Assign;
	}

	/// <summary>
	/// Gets the node type name for this assignment statement.
	/// </summary>
	/// <returns>The node type name.</returns>
	public override string GetNodeTypeName() => "AssignmentStatement";

	/// <summary>
	/// Creates a deep clone of this assignment statement.
	/// </summary>
	/// <returns>A new instance with the same property values.</returns>
	public override AstNode Clone()
	{
		AssignmentStatement clone = new()
		{
			Target = (Expression)Target.DeepClone(),
			Value = (Expression)Value.DeepClone(),
			Operator = Operator
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}

	/// <summary>
	/// Returns a string representation of the assignment statement.
	/// </summary>
	/// <returns>String representation.</returns>
	public override string ToString()
	{
		string operatorSymbol = Operator switch
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
			_ => "="
		};

		return $"{Target} {operatorSymbol} {Value}";
	}
}

/// <summary>
/// Defines the types of assignment operators supported.
/// </summary>
public enum AssignmentOperator
{
	/// <summary>Simple assignment (=)</summary>
	Assign,

	/// <summary>Addition assignment (+=)</summary>
	AddAssign,

	/// <summary>Subtraction assignment (-=)</summary>
	SubtractAssign,

	/// <summary>Multiplication assignment (*=)</summary>
	MultiplyAssign,

	/// <summary>Division assignment (/=)</summary>
	DivideAssign,

	/// <summary>Modulo assignment (%=)</summary>
	ModuloAssign,

	/// <summary>Bitwise AND assignment (&amp;=)</summary>
	BitwiseAndAssign,

	/// <summary>Bitwise OR assignment (|=)</summary>
	BitwiseOrAssign,

	/// <summary>Bitwise XOR assignment (^=)</summary>
	BitwiseXorAssign,

	/// <summary>Left shift assignment (&lt;&lt;=)</summary>
	LeftShiftAssign,

	/// <summary>Right shift assignment (&gt;&gt;=)</summary>
	RightShiftAssign,
}
