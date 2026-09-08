// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ktsu.Coder.Ast;

/// <summary>
/// The kind of value a field holds, which is what decides the widget the editor draws for it.
/// </summary>
public enum AstFieldKind
{
	/// <summary>Free text: a name, a type, a string literal.</summary>
	Text,

	/// <summary>A whole number.</summary>
	Number,

	/// <summary>A number with a fractional part.</summary>
	Fraction,

	/// <summary>True or false.</summary>
	Flag,

	/// <summary>One of a fixed set of values, listed in <see cref="AstField.Choices"/>.</summary>
	Choice,
}

/// <summary>
/// One option a <see cref="AstFieldKind.Choice"/> field offers.
/// </summary>
/// <param name="Value">What <see cref="AstFields.TryWrite"/> is given to select this option.</param>
/// <param name="Label">How the option reads on screen.</param>
/// <remarks>
/// The two differ because an operator's stored spelling and its readable one are not the same
/// thing: the field is written with <c>Add</c> and the user picks <c>+ (Add)</c>.
/// </remarks>
public sealed record AstFieldChoice(string Value, string Label);

/// <summary>
/// One editable property of an AST node, as the editor's inspector sees it.
/// </summary>
/// <param name="Name">The property's name, which is also how <see cref="AstFields.TryWrite"/> addresses it.</param>
/// <param name="Kind">The kind of value it holds.</param>
/// <param name="Value">Its current value, as text.</param>
/// <param name="Choices">The options a <see cref="AstFieldKind.Choice"/> field offers; empty otherwise.</param>
/// <remarks>
/// Every value is carried as text, whatever its kind. That is what lets one undoable command record
/// "this field was that, now it is this" for a name, a number, a flag and an operator alike, rather
/// than needing a command type per property.
/// </remarks>
public sealed record AstField(string Name, AstFieldKind Kind, string Value, IReadOnlyList<AstFieldChoice> Choices)
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AstField"/> record for a field with no choices.
	/// </summary>
	/// <param name="name">The property's name.</param>
	/// <param name="kind">The kind of value it holds.</param>
	/// <param name="value">Its current value, as text.</param>
	public AstField(string name, AstFieldKind kind, string value)
		: this(name, kind, value, [])
	{
	}
}

/// <summary>
/// Reads and writes the properties of an AST node that are values rather than children.
/// </summary>
/// <remarks>
/// A node's children are edited by dragging links; everything else about it — a function's name, a
/// literal's value, a binary expression's operator — is edited here. Presenting all of them as
/// named text fields keeps the rules in one testable place and leaves the editor drawing a widget
/// per <see cref="AstFieldKind"/> rather than a panel per node type.
/// <para>
/// <see cref="TryWrite"/> refuses a value it cannot parse rather than throwing or silently writing a
/// default, so a half-typed number in the inspector leaves the document alone until it is a number.
/// </para>
/// </remarks>
public static class AstFields
{
	/// <summary>
	/// The visibilities a declaration offers.
	/// </summary>
	/// <remarks>
	/// Built from the enumeration so a visibility the AST gains appears in the inspector without
	/// anyone remembering to list it here. <see cref="Visibility.Unspecified"/> is labelled for what
	/// it means rather than by its name: it is not a fifth modifier, it is the absence of one.
	/// </remarks>
	private static readonly IReadOnlyList<AstFieldChoice> Visibilities =
	[
		.. Enum.GetValues<Visibility>().Select(visibility => new AstFieldChoice(
			visibility.ToString(),
			visibility == Visibility.Unspecified ? "(language default)" : visibility.ToString().ToLowerInvariant())),
	];

	/// <summary>
	/// Lists the properties of a node the inspector can edit.
	/// </summary>
	/// <param name="node">The node to inspect.</param>
	/// <returns>Its fields, in the order the inspector should draw them. Empty for a node with none.</returns>
	public static IReadOnlyList<AstField> Of(AstNode node)
	{
		Ensure.NotNull(node);

		return node switch
		{
			ClassDeclaration classDecl =>
			[
				new("Name", AstFieldKind.Text, classDecl.Name ?? string.Empty),
				new("BaseType", AstFieldKind.Text, classDecl.BaseType ?? string.Empty),
				new("Visibility", AstFieldKind.Choice, classDecl.Visibility.ToString(), Visibilities),
			],

			FunctionDeclaration function =>
			[
				new("Name", AstFieldKind.Text, function.Name ?? string.Empty),
				new("ReturnType", AstFieldKind.Text, function.ReturnType ?? string.Empty),
				new("Visibility", AstFieldKind.Choice, function.Visibility.ToString(), Visibilities),
			],

			EntryPoint entryPoint =>
			[
				new("Arguments", AstFieldKind.Flag, Spell(entryPoint.AcceptsArguments)),
				new("ExitCode", AstFieldKind.Flag, Spell(entryPoint.ReturnsExitCode)),
			],

			Parameter parameter =>
			[
				new("Name", AstFieldKind.Text, parameter.Name ?? string.Empty),
				new("Type", AstFieldKind.Text, parameter.Type ?? string.Empty),
				new("Optional", AstFieldKind.Flag, Spell(parameter.IsOptional)),
				new("Default", AstFieldKind.Text, parameter.DefaultValue ?? string.Empty),
			],

			VariableDeclaration varDecl =>
			[
				new("Name", AstFieldKind.Text, varDecl.Name),
				new("Type", AstFieldKind.Text, varDecl.Type ?? string.Empty),
				new("Constant", AstFieldKind.Flag, Spell(varDecl.IsConstant)),
				new("Inferred", AstFieldKind.Flag, Spell(varDecl.IsTypeInferred)),
				new("Visibility", AstFieldKind.Choice, varDecl.Visibility.ToString(), Visibilities),
			],

			VariableReference varRef =>
			[
				new("Name", AstFieldKind.Text, varRef.Name),
			],

			BinaryExpression binary =>
			[
				new("Operator", AstFieldKind.Choice, binary.Operator.ToString(), OperatorChoices<BinaryOperator>()),
			],

			UnaryExpression unary =>
			[
				new("Operator", AstFieldKind.Choice, unary.Operator.ToString(), OperatorChoices<UnaryOperator>()),
			],

			AssignmentStatement assignment =>
			[
				new("Operator", AstFieldKind.Choice, assignment.Operator.ToString(), OperatorChoices<AssignmentOperator>()),
			],

			LiteralExpression<string> literal => [new("Value", AstFieldKind.Text, literal.Value ?? string.Empty)],
			LiteralExpression<int> literal => [new("Value", AstFieldKind.Number, Spell(literal.Value))],
			LiteralExpression<double> literal => [new("Value", AstFieldKind.Fraction, Spell(literal.Value))],
			LiteralExpression<bool> literal => [new("Value", AstFieldKind.Flag, Spell(literal.Value))],

			AstLeafNode<string> leaf => [new("Value", AstFieldKind.Text, leaf.Value ?? string.Empty)],
			AstLeafNode<int> leaf => [new("Value", AstFieldKind.Number, Spell(leaf.Value))],
			AstLeafNode<double> leaf => [new("Value", AstFieldKind.Fraction, Spell(leaf.Value))],
			AstLeafNode<bool> leaf => [new("Value", AstFieldKind.Flag, Spell(leaf.Value))],

			_ => [],
		};
	}

	/// <summary>
	/// Reads one field's current value.
	/// </summary>
	/// <param name="node">The node to read from.</param>
	/// <param name="fieldName">The field to read.</param>
	/// <returns>The value as text, or null if the node has no such field.</returns>
	/// <remarks>
	/// This is what an undoable edit records as the value to put back, so it has to be read before
	/// the write rather than reconstructed afterwards.
	/// </remarks>
	public static string? Read(AstNode node, string fieldName) =>
		Of(node).FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.Ordinal))?.Value;

	/// <summary>
	/// Writes one field, if the value is one the field can hold.
	/// </summary>
	/// <param name="node">The node to write to.</param>
	/// <param name="fieldName">The field to write.</param>
	/// <param name="value">The value, as text.</param>
	/// <returns>True if the node was changed; false if there is no such field or the value does not parse.</returns>
	/// <remarks>
	/// Writing the value a field already holds returns false: an edit that changes nothing should not
	/// reach the undo stack.
	/// </remarks>
	public static bool TryWrite(AstNode node, string fieldName, string value)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(fieldName);
		Ensure.NotNull(value);

		string? current = Read(node, fieldName);
		if (current is null || string.Equals(current, value, StringComparison.Ordinal))
		{
			return false;
		}

		return (node, fieldName) switch
		{
			(ClassDeclaration classDecl, "Name") => Assign(() => classDecl.Name = OrNull(value)),
			(ClassDeclaration classDecl, "BaseType") => Assign(() => classDecl.BaseType = OrNull(value)),
			(ClassDeclaration classDecl, "Visibility") =>
				TryParseVisibility(value, out Visibility classVisibility) && Assign(() => classDecl.Visibility = classVisibility),

			(FunctionDeclaration function, "Name") => Assign(() => function.Name = OrNull(value)),
			(FunctionDeclaration function, "ReturnType") => Assign(() => function.ReturnType = OrNull(value)),
			(FunctionDeclaration function, "Visibility") =>
				TryParseVisibility(value, out Visibility functionVisibility) && Assign(() => function.Visibility = functionVisibility),

			(EntryPoint entryPoint, "Arguments") =>
				TryParseBool(value, out bool acceptsArguments) && Assign(() => entryPoint.AcceptsArguments = acceptsArguments),
			(EntryPoint entryPoint, "ExitCode") =>
				TryParseBool(value, out bool returnsExitCode) && Assign(() => entryPoint.ReturnsExitCode = returnsExitCode),

			(Parameter parameter, "Name") => Assign(() => parameter.Name = OrNull(value)),
			(Parameter parameter, "Type") => Assign(() => parameter.Type = OrNull(value)),
			(Parameter parameter, "Optional") => TryParseBool(value, out bool optional) && Assign(() => parameter.IsOptional = optional),
			(Parameter parameter, "Default") => Assign(() => parameter.DefaultValue = OrNull(value)),

			(VariableDeclaration varDecl, "Name") => value.Length > 0 && Assign(() => varDecl.Name = value),
			(VariableDeclaration varDecl, "Type") => Assign(() => varDecl.Type = OrNull(value)),
			(VariableDeclaration varDecl, "Constant") => TryParseBool(value, out bool constant) && Assign(() => varDecl.IsConstant = constant),
			(VariableDeclaration varDecl, "Inferred") => TryParseBool(value, out bool inferred) && Assign(() => varDecl.IsTypeInferred = inferred),
			(VariableDeclaration varDecl, "Visibility") =>
				TryParseVisibility(value, out Visibility varVisibility) && Assign(() => varDecl.Visibility = varVisibility),

			(VariableReference varRef, "Name") => value.Length > 0 && Assign(() => varRef.Name = value),

			(BinaryExpression binary, "Operator") =>
				Enum.TryParse(value, out BinaryOperator binaryOp) && Assign(() => binary.Operator = binaryOp),
			(UnaryExpression unary, "Operator") =>
				Enum.TryParse(value, out UnaryOperator unaryOp) && Assign(() => unary.Operator = unaryOp),
			(AssignmentStatement assignment, "Operator") =>
				Enum.TryParse(value, out AssignmentOperator assignOp) && Assign(() => assignment.Operator = assignOp),

			(LiteralExpression<string> literal, "Value") => Assign(() => literal.Value = value),
			(LiteralExpression<int> literal, "Value") => TryParseInt(value, out int number) && Assign(() => literal.Value = number),
			(LiteralExpression<double> literal, "Value") => TryParseDouble(value, out double number) && Assign(() => literal.Value = number),
			(LiteralExpression<bool> literal, "Value") => TryParseBool(value, out bool flag) && Assign(() => literal.Value = flag),

			(AstLeafNode<string> leaf, "Value") => Assign(() => leaf.Value = value),
			(AstLeafNode<int> leaf, "Value") => TryParseInt(value, out int number) && Assign(() => leaf.Value = number),
			(AstLeafNode<double> leaf, "Value") => TryParseDouble(value, out double number) && Assign(() => leaf.Value = number),
			(AstLeafNode<bool> leaf, "Value") => TryParseBool(value, out bool flag) && Assign(() => leaf.Value = flag),

			_ => false,
		};
	}

	/// <summary>
	/// Lists an operator enumeration's members as choices, labelled with the symbol they spell.
	/// </summary>
	/// <typeparam name="TOperator">The operator enumeration.</typeparam>
	/// <returns>One choice per member, in declaration order.</returns>
	/// <remarks>
	/// Built from the enumeration rather than written out, so an operator added to the AST appears in
	/// the editor without anyone having to remember to list it here.
	/// </remarks>
	public static IReadOnlyList<AstFieldChoice> OperatorChoices<TOperator>()
		where TOperator : struct, Enum =>
		[.. Enum.GetValues<TOperator>().Select(op => new AstFieldChoice(op.ToString(), LabelFor(op)))];

	/// <summary>
	/// Labels an operator with its symbol and its name, so a menu of them reads as source rather than
	/// as an enumeration.
	/// </summary>
	/// <typeparam name="TOperator">The operator enumeration.</typeparam>
	/// <param name="op">The operator to label.</param>
	/// <returns>The label, which is the name alone when the operator has no symbol.</returns>
	private static string LabelFor<TOperator>(TOperator op)
		where TOperator : struct, Enum
	{
		string? symbol = op switch
		{
			BinaryOperator binary => Symbol(binary),
			UnaryOperator unary => Symbol(unary),
			AssignmentOperator assignment => Symbol(assignment),
			_ => null,
		};

		return symbol is null ? op.ToString() : $"{symbol}  {op}";
	}

	private static string? Symbol(BinaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) ? symbol : null;

	private static string? Symbol(UnaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) ? symbol : null;

	private static string? Symbol(AssignmentOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) ? symbol : null;

	/// <summary>
	/// Performs an assignment and reports that the node changed.
	/// </summary>
	/// <param name="assign">The assignment to perform.</param>
	/// <returns>Always true, so the caller's expression reads as "parse, then write".</returns>
	private static bool Assign(Action assign)
	{
		assign();
		return true;
	}

	/// <summary>
	/// Treats empty text as an absent value, since a cleared inspector box means "not set".
	/// </summary>
	/// <param name="value">The text the user left in the box.</param>
	/// <returns>The text, or null when it is empty.</returns>
	private static string? OrNull(string value) => value.Length == 0 ? null : value;

	// Culture-invariant throughout: these are source values, not text shown in the user's locale.
	private static bool TryParseInt(string value, out int result) =>
		int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

	private static bool TryParseDouble(string value, out double result) =>
		double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

	private static bool TryParseBool(string value, out bool result) => bool.TryParse(value, out result);

	// Case-insensitively, so a document hand-edited with "public" reads back the same as the
	// inspector's own "Public".
	private static bool TryParseVisibility(string value, out Visibility result) =>
		Enum.TryParse(value, ignoreCase: true, out result);

	private static string Spell(bool value) => value ? "true" : "false";

	private static string Spell(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string Spell(double value) => value.ToString(CultureInfo.InvariantCulture);
}
