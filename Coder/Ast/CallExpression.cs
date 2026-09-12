// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System.Collections.ObjectModel;

/// <summary>
/// Calls something and evaluates to what it answers with.
/// Examples: <c>sqrt(x)</c>, <c>point.translate(dx, dy)</c>
/// </summary>
/// <remarks>
/// <see cref="ConstructionExpression"/> builds a value of a type; this reaches one that already
/// exists. Without it an expression beyond an operator applied to operands cannot be said at all,
/// so a caller with a call to make has to hand a <see cref="VariableReference"/> the whole thing as
/// text — which passes through every generator unchanged, and is therefore right in at most one
/// language.
/// <para>
/// <see cref="Receiver"/> is modelled rather than folded into <see cref="Callee"/> because that is
/// the part the languages disagree about: <c>a.b(c)</c> in C#, C++, Python and JavaScript, and
/// <c>b(&amp;a, c)</c> in C, which has no member functions and lowers one to a free function taking
/// the instance. That is the same transformation <see cref="Languages.CGenerator"/> already performs
/// on the declaration side, so modelling the receiver is what lets the call site follow the
/// declaration.
/// </para>
/// <para>
/// <see cref="Callee"/> is text and is written verbatim, which is a decision rather than an
/// oversight. A square root is <c>std::sqrt</c>, <c>Math.Sqrt</c>, <c>math.sqrt</c> and
/// <c>Math.sqrt</c> in the five targets, and there is no shared idea underneath those four spellings
/// for the AST to hold — the way there is underneath a type, which is why
/// <see cref="TypeReference"/> is structure. Choosing the name is the caller's, exactly as
/// <see cref="SourceFile.Imports"/> and <see cref="CompileTimeAssertion.Condition"/> are the
/// caller's. What the AST does carry is the <em>shape</em> of the call, which is what the
/// generators need in order to disagree about it.
/// </para>
/// </remarks>
public class CallExpression : Expression
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CallExpression"/> class.
	/// Used for deserialization.
	/// </summary>
	public CallExpression() => Callee = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="CallExpression"/> class for a free function.
	/// </summary>
	/// <param name="callee">What is being called, spelled as the target language spells it.</param>
	public CallExpression(string callee)
	{
		Ensure.NotNull(callee);
		Callee = callee;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CallExpression"/> class for a member call.
	/// </summary>
	/// <param name="receiver">The instance the call is made on.</param>
	/// <param name="callee">What is being called, spelled as the target language spells it.</param>
	public CallExpression(Expression receiver, string callee)
	{
		Ensure.NotNull(receiver);
		Ensure.NotNull(callee);
		Receiver = receiver;
		Callee = callee;
	}

	/// <summary>
	/// Gets or sets what is being called, written verbatim.
	/// </summary>
	public string Callee { get; set; }

	/// <summary>
	/// Gets or sets the instance the call is made on, or null for a free function.
	/// </summary>
	public Expression? Receiver { get; set; }

	/// <summary>
	/// Gets the arguments, in order.
	/// </summary>
	/// <remarks>
	/// <see cref="AstNode"/> rather than <see cref="Expression"/>, matching
	/// <see cref="ConstructionExpression.Arguments"/>: the legacy <see cref="AstLeafNode{T}"/> shapes
	/// are not expressions but every generator emits them where one is expected.
	/// </remarks>
	public Collection<AstNode> Arguments { get; init; } = [];

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "CallExpression";

	/// <summary>
	/// Creates a deep clone of this call.
	/// </summary>
	/// <returns>A new instance with the same callee and cloned receiver and arguments.</returns>
	public override AstNode Clone()
	{
		CallExpression clone = new()
		{
			Callee = Callee,
			Receiver = (Expression?)Receiver?.DeepClone(),
			ExpectedType = ExpectedType,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		foreach (AstNode argument in Arguments)
		{
			clone.Arguments.Add(argument.Clone());
		}

		return clone;
	}
}
