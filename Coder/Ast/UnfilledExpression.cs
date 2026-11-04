// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

/// <summary>
/// Stands in an operand that has not been filled in yet.
/// </summary>
/// <remarks>
/// The parameterless constructors and the graph editor need something to put in a required operand
/// before the user has chosen what goes there. That used to be an empty string literal, which made a
/// deliberate <c>""</c> indistinguishable from the gap: a document holding <c>return ""</c> reported
/// "nothing in Expression" and would not generate. The placeholder is its own type so that the test
/// is what the node is rather than what it holds.
/// <para>
/// It is still a string literal with an empty value, so a generator handed an unfinished tree writes
/// what it always wrote rather than refusing it. It is serialized under its own key, so a YAML round
/// trip keeps it apart from a deliberate empty string too.
/// </para>
/// </remarks>
public sealed class UnfilledExpression : LiteralExpression<string>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UnfilledExpression"/> class.
	/// </summary>
	public UnfilledExpression()
		: base(string.Empty)
	{
	}

	/// <summary>
	/// Gets the placeholder's value, which is always empty.
	/// </summary>
	/// <remarks>
	/// Read-only here because a placeholder is filled by putting a node in its place, not by typing
	/// into it: a placeholder holding text would be neither a gap nor a literal the editor can show.
	/// </remarks>
	public new string? Value => base.Value;

	/// <summary>
	/// Gets the node type name for the placeholder.
	/// </summary>
	/// <returns>The placeholder's type name.</returns>
	public override string GetNodeTypeName() => "Unfilled";

	/// <summary>
	/// Creates a copy of this placeholder.
	/// </summary>
	/// <returns>A new placeholder carrying the same expected type and metadata.</returns>
	public override AstNode Clone()
	{
		UnfilledExpression clone = new()
		{
			ExpectedType = ExpectedType,
		};

		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}
}
