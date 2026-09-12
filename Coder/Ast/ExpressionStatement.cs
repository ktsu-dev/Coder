// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// An expression evaluated for its effect rather than its value.
/// Examples: <c>assert(ready);</c>, <c>items.clear();</c>
/// </summary>
/// <remarks>
/// The AST could already say what to do with a value — return it, assign it, pass it — but not that
/// a value is beside the point. That leaves a call that answers with nothing with nowhere to stand,
/// which is a hole rather than a simplification: a language where most of what a function body does
/// is call other functions cannot describe a body at all.
/// <para>
/// Every generator ends this the way it ends a return or an assignment, so Python's statement ends
/// at the newline and the other four end at a semicolon without either being a special case here.
/// </para>
/// </remarks>
public class ExpressionStatement : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExpressionStatement"/> class.
	/// Used for deserialization.
	/// </summary>
	public ExpressionStatement() => Expression = new LiteralExpression<string>(string.Empty);

	/// <summary>
	/// Initializes a new instance of the <see cref="ExpressionStatement"/> class.
	/// </summary>
	/// <param name="expression">The expression to evaluate.</param>
	public ExpressionStatement(Expression expression)
	{
		Ensure.NotNull(expression);
		Expression = expression;
	}

	/// <summary>
	/// Gets or sets the expression being evaluated.
	/// </summary>
	public Expression Expression { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "ExpressionStatement";

	/// <summary>
	/// Creates a deep clone of this statement.
	/// </summary>
	/// <returns>A new instance with a cloned expression.</returns>
	public override AstNode Clone()
	{
		ExpressionStatement clone = new()
		{
			Expression = (Expression)Expression.DeepClone(),
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}
