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
	/// The kinds of type a declaration can be, offered as a menu rather than typed.
	/// </summary>
	/// <summary>The name of the field every declaration that has a visibility exposes.</summary>
	private const string VisibilityField = "Visibility";

	/// <summary>The name of the field every node holding one value exposes.</summary>
	private const string ValueField = "Value";

	/// <summary>The name of the field a call exposes its callee through.</summary>
	private const string CalleeField = "Callee";

	/// <summary>The name of the field a node holding one type exposes.</summary>
	private const string TypeField = "Type";

	/// <summary>The name of the field a declaration promising not to modify something exposes.</summary>
	/// <remarks>
	/// One name for two different promises, which is why it is shared rather than repeated: on a
	/// function it says the call does not modify the receiver, and on a type that none of its
	/// members does. A reader of the inspector sees the same word for the same idea.
	/// </remarks>
	private const string ReadOnlyField = "ReadOnly";

	private static readonly IReadOnlyList<AstFieldChoice> FunctionKinds =
	[
		.. Enum.GetValues<FunctionKind>().Select(kind => new AstFieldChoice(kind.ToString(), kind.ToString())),
	];

	/// <summary>
	/// Where a function's behaviour comes from, offered as a menu rather than typed.
	/// </summary>
	private static readonly IReadOnlyList<AstFieldChoice> FunctionDefinitions =
	[
		.. Enum.GetValues<FunctionDefinition>().Select(definition => new AstFieldChoice(
			definition.ToString(),
			definition.ToString().ToLowerInvariant())),
	];

	/// <summary>
	/// The kinds of type a declaration can be, offered as a menu rather than typed.
	/// </summary>
	private static readonly IReadOnlyList<AstFieldChoice> TypeKinds =
	[
		.. Enum.GetValues<TypeDeclarationKind>().Select(kind => new AstFieldChoice(
			kind.ToString(),
			kind.ToString().ToLowerInvariant())),
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
			SourceFile file =>
			[
				new("Name", AstFieldKind.Text, file.Name ?? string.Empty),
				new("Header", AstFieldKind.Flag, Spell(file.IsHeader)),
			],

			NamespaceDeclaration namespaceDecl =>
			[
				new("Name", AstFieldKind.Text, namespaceDecl.Name ?? string.Empty),
			],

			EnumDeclaration enumDecl =>
			[
				new("Name", AstFieldKind.Text, enumDecl.Name ?? string.Empty),
				new("UnderlyingType", AstFieldKind.Text, enumDecl.UnderlyingType?.ToString() ?? string.Empty),
				new(VisibilityField, AstFieldKind.Choice, enumDecl.Visibility.ToString(), Visibilities),
			],

			EnumMember enumMember =>
			[
				new("Name", AstFieldKind.Text, enumMember.Name ?? string.Empty),
				new(ValueField, AstFieldKind.Text, enumMember.Value ?? string.Empty),
			],

			MemberInitialiser initialiser =>
			[
				new("Name", AstFieldKind.Text, initialiser.Name ?? string.Empty),
			],

			CompileTimeAssertion assertion =>
			[
				new("Condition", AstFieldKind.Text, assertion.Condition ?? string.Empty),
				new("Message", AstFieldKind.Text, assertion.Message ?? string.Empty),
			],

			UsingAlias usingAlias =>
			[
				new("Name", AstFieldKind.Text, usingAlias.Name ?? string.Empty),
				new("AliasedType", AstFieldKind.Text, usingAlias.AliasedType?.ToString() ?? string.Empty),
				new(VisibilityField, AstFieldKind.Choice, usingAlias.Visibility.ToString(), Visibilities),
			],

			FieldDeclaration fieldDecl =>
			[
				new("Name", AstFieldKind.Text, fieldDecl.Name ?? string.Empty),
				new("Type", AstFieldKind.Text, fieldDecl.Type?.ToString() ?? string.Empty),
				new(VisibilityField, AstFieldKind.Choice, fieldDecl.Visibility.ToString(), Visibilities),
				new("Static", AstFieldKind.Flag, Spell(fieldDecl.IsStatic)),
				new("Constant", AstFieldKind.Flag, Spell(fieldDecl.IsConstant)),
			],

			PropertyDeclaration property =>
			[
				new("Name", AstFieldKind.Text, property.Name ?? string.Empty),
				new(TypeField, AstFieldKind.Text, property.Type?.ToString() ?? string.Empty),
				new(VisibilityField, AstFieldKind.Choice, property.Visibility.ToString(), Visibilities),
				new("Static", AstFieldKind.Flag, Spell(property.IsStatic)),
				new("Readable", AstFieldKind.Flag, Spell(property.HasGetter)),
				new("Writable", AstFieldKind.Flag, Spell(property.HasSetter)),
				new("InitOnly", AstFieldKind.Flag, Spell(property.SetterIsInitOnly)),
			],

			ClassDeclaration classDecl =>
			[
				new("Name", AstFieldKind.Text, classDecl.Name ?? string.Empty),
				new("Kind", AstFieldKind.Choice, classDecl.Kind.ToString(), TypeKinds),
				new("BaseType", AstFieldKind.Text, classDecl.BaseType?.ToString() ?? string.Empty),
				new(VisibilityField, AstFieldKind.Choice, classDecl.Visibility.ToString(), Visibilities),
				new("Record", AstFieldKind.Flag, Spell(classDecl.IsRecord)),
				new("Partial", AstFieldKind.Flag, Spell(classDecl.IsPartial)),
				new(ReadOnlyField, AstFieldKind.Flag, Spell(classDecl.IsReadOnly)),
			],

			_ => OfCallable(node),
		};
	}

	/// <summary>
	/// Lists the properties of a function, a parameter or a variable the inspector can edit.
	/// </summary>
	/// <param name="node">The node to describe.</param>
	/// <returns>The fields, in the order the inspector should draw them.</returns>
	/// <remarks>
	/// Split from the type declarations only because one switch over every node the AST has is more
	/// branches than the analyzer accepts.
	/// </remarks>
	private static IReadOnlyList<AstField> OfCallable(AstNode node)
	{
		return node switch
		{
			FunctionDeclaration function =>
			[
				new("Name", AstFieldKind.Text, function.Name ?? string.Empty),
				new("ReturnType", AstFieldKind.Text, function.ReturnType?.ToString() ?? string.Empty),
				new(VisibilityField, AstFieldKind.Choice, function.Visibility.ToString(), Visibilities),
				new("Static", AstFieldKind.Flag, Spell(function.IsStatic)),
				new("Pure", AstFieldKind.Flag, Spell(function.IsPure)),
				new("Kind", AstFieldKind.Choice, function.Kind.ToString(), FunctionKinds),
				new("Definition", AstFieldKind.Choice, function.Definition.ToString(), FunctionDefinitions),
				new("Virtual", AstFieldKind.Flag, Spell(function.IsVirtual)),
				new("Abstract", AstFieldKind.Flag, Spell(function.IsAbstract)),
				new(ReadOnlyField, AstFieldKind.Flag, Spell(function.IsReadOnly)),
				new("MustUseResult", AstFieldKind.Flag, Spell(function.MustUseResult)),
			],

			EntryPoint entryPoint =>
			[
				new("Arguments", AstFieldKind.Flag, Spell(entryPoint.AcceptsArguments)),
				new("ExitCode", AstFieldKind.Flag, Spell(entryPoint.ReturnsExitCode)),
			],

			Parameter parameter =>
			[
				new("Name", AstFieldKind.Text, parameter.Name ?? string.Empty),
				new("Type", AstFieldKind.Text, parameter.Type?.ToString() ?? string.Empty),
				new("Optional", AstFieldKind.Flag, Spell(parameter.IsOptional)),
				new("Default", AstFieldKind.Text, parameter.DefaultValue ?? string.Empty),
			],

			VariableDeclaration varDecl =>
			[
				new("Name", AstFieldKind.Text, varDecl.Name),
				new("Type", AstFieldKind.Text, varDecl.Type?.ToString() ?? string.Empty),
				new("Constant", AstFieldKind.Flag, Spell(varDecl.IsConstant)),
				new("Inferred", AstFieldKind.Flag, Spell(varDecl.IsTypeInferred)),
				new(VisibilityField, AstFieldKind.Choice, varDecl.Visibility.ToString(), Visibilities),
			],

			_ => OfExpression(node),
		};
	}

	/// <summary>
	/// Lists the properties of an expression or a leaf the inspector can edit.
	/// </summary>
	/// <param name="node">The node to describe.</param>
	/// <returns>The fields, in the order the inspector should draw them.</returns>
	/// <remarks>
	/// Split from the declarations only because one switch over every node the AST has is more
	/// branches than the analyzer accepts. The line is the same one <see cref="TryWrite"/> draws.
	/// </remarks>
	private static IReadOnlyList<AstField> OfExpression(AstNode node)
	{
		return node switch
		{
			VariableReference varRef =>
			[
				new("Name", AstFieldKind.Text, varRef.Name),
			],

			CallExpression callExpr =>
			[
				new(CalleeField, AstFieldKind.Text, callExpr.Callee),
			],

			// The one expression that names a type rather than a name, which is why it could not
			// exist before TypeReference did. With no type it is a braced list, so the field is
			// allowed to be empty.
			ConstructionExpression construction =>
			[
				new(TypeField, AstFieldKind.Text, construction.Type?.ToString() ?? string.Empty),
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

			LiteralExpression<string> literal => [new(ValueField, AstFieldKind.Text, literal.Value ?? string.Empty)],
			LiteralExpression<int> literal => [new(ValueField, AstFieldKind.Number, Spell(literal.Value))],
			LiteralExpression<double> literal => [new(ValueField, AstFieldKind.Fraction, Spell(literal.Value))],
			LiteralExpression<bool> literal => [new(ValueField, AstFieldKind.Flag, Spell(literal.Value))],

			AstLeafNode<string> leaf => [new(ValueField, AstFieldKind.Text, leaf.Value ?? string.Empty)],
			AstLeafNode<int> leaf => [new(ValueField, AstFieldKind.Number, Spell(leaf.Value))],
			AstLeafNode<double> leaf => [new(ValueField, AstFieldKind.Fraction, Spell(leaf.Value))],
			AstLeafNode<bool> leaf => [new(ValueField, AstFieldKind.Flag, Spell(leaf.Value))],

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

		return TryWriteDeclaration(node, fieldName, value) || TryWriteExpression(node, fieldName, value);
	}

	/// <summary>
	/// Writes a field of a declaration.
	/// </summary>
	/// <param name="node">The node to write to.</param>
	/// <param name="fieldName">The field to write.</param>
	/// <param name="value">The value, as text.</param>
	/// <returns>True if the node was changed.</returns>
	/// <remarks>
	/// Split from <see cref="TryWriteExpression"/> only because one switch over every node the AST
	/// has is more branches than any analyzer will accept. The line between them is the same one the
	/// AST already draws: something that declares a name, or something that computes a value.
	/// </remarks>
	private static bool TryWriteDeclaration(AstNode node, string fieldName, string value)
	{
		return (node, fieldName) switch
		{
			(SourceFile file, "Name") => Assign(() => file.Name = OrNull(value)),
			(SourceFile file, "Header") => AssignFlag(value, isHeader => file.IsHeader = isHeader),

			(NamespaceDeclaration namespaceDecl, "Name") => Assign(() => namespaceDecl.Name = OrNull(value)),

			(EnumDeclaration enumDecl, "Name") => Assign(() => enumDecl.Name = OrNull(value)),
			(EnumDeclaration enumDecl, "UnderlyingType") => Assign(() => enumDecl.UnderlyingType = OrNull(value)),
			(EnumDeclaration enumDecl, VisibilityField) =>
				AssignMember<Visibility>(value, enumVisibility => enumDecl.Visibility = enumVisibility),

			(EnumMember enumMember, "Name") => Assign(() => enumMember.Name = OrNull(value)),
			(EnumMember enumMember, ValueField) => Assign(() => enumMember.Value = OrNull(value)),

			(MemberInitialiser initialiser, "Name") => Assign(() => initialiser.Name = OrNull(value)),

			(CompileTimeAssertion assertion, "Condition") => Assign(() => assertion.Condition = OrNull(value)),
			(CompileTimeAssertion assertion, "Message") => Assign(() => assertion.Message = OrNull(value)),

			(UsingAlias usingAlias, "Name") => Assign(() => usingAlias.Name = OrNull(value)),
			(UsingAlias usingAlias, "AliasedType") => Assign(() => usingAlias.AliasedType = OrNull(value)),
			(UsingAlias usingAlias, VisibilityField) =>
				AssignMember<Visibility>(value, aliasVisibility => usingAlias.Visibility = aliasVisibility),

			(FieldDeclaration fieldDecl, "Name") => Assign(() => fieldDecl.Name = OrNull(value)),
			(FieldDeclaration fieldDecl, "Type") => Assign(() => fieldDecl.Type = OrNull(value)),
			(FieldDeclaration fieldDecl, VisibilityField) =>
				AssignMember<Visibility>(value, fieldVisibility => fieldDecl.Visibility = fieldVisibility),
			(FieldDeclaration fieldDecl, "Static") =>
				AssignFlag(value, fieldIsStatic => fieldDecl.IsStatic = fieldIsStatic),
			(FieldDeclaration fieldDecl, "Constant") =>
				AssignFlag(value, fieldIsConstant => fieldDecl.IsConstant = fieldIsConstant),

			(PropertyDeclaration property, "Name") => Assign(() => property.Name = OrNull(value)),
			(PropertyDeclaration property, TypeField) => Assign(() => property.Type = OrNull(value)),
			(PropertyDeclaration property, VisibilityField) =>
				AssignMember<Visibility>(value, propertyVisibility => property.Visibility = propertyVisibility),
			(PropertyDeclaration property, "Static") =>
				AssignFlag(value, propertyIsStatic => property.IsStatic = propertyIsStatic),
			(PropertyDeclaration property, "Readable") =>
				AssignFlag(value, readable => property.HasGetter = readable),
			(PropertyDeclaration property, "Writable") =>
				AssignFlag(value, writable => property.HasSetter = writable),
			(PropertyDeclaration property, "InitOnly") =>
				AssignFlag(value, initOnly => property.SetterIsInitOnly = initOnly),

			(ClassDeclaration classDecl, "Name") => Assign(() => classDecl.Name = OrNull(value)),
			(ClassDeclaration classDecl, "Kind") =>
				AssignMember<TypeDeclarationKind>(value, typeKind => classDecl.Kind = typeKind),
			(ClassDeclaration classDecl, "BaseType") => Assign(() => classDecl.BaseType = OrNull(value)),
			(ClassDeclaration classDecl, VisibilityField) =>
				AssignMember<Visibility>(value, classVisibility => classDecl.Visibility = classVisibility),
			(ClassDeclaration classDecl, "Record") =>
				AssignFlag(value, isRecord => classDecl.IsRecord = isRecord),
			(ClassDeclaration classDecl, "Partial") =>
				AssignFlag(value, isPartial => classDecl.IsPartial = isPartial),
			(ClassDeclaration classDecl, ReadOnlyField) =>
				AssignFlag(value, isClassReadOnly => classDecl.IsReadOnly = isClassReadOnly),

			(FunctionDeclaration function, "Name") => Assign(() => function.Name = OrNull(value)),
			(FunctionDeclaration function, "ReturnType") => Assign(() => function.ReturnType = OrNull(value)),
			(FunctionDeclaration function, VisibilityField) =>
				AssignMember<Visibility>(value, functionVisibility => function.Visibility = functionVisibility),
			(FunctionDeclaration function, "Static") =>
				AssignFlag(value, isStatic => function.IsStatic = isStatic),
			(FunctionDeclaration function, "Pure") =>
				AssignFlag(value, isPure => function.IsPure = isPure),
			_ => TryWriteFunctionShape(node, fieldName, value),
		};
	}

	/// <summary>
	/// Writes a field describing what a function declares and how.
	/// </summary>
	/// <param name="node">The node to write to.</param>
	/// <param name="fieldName">The field to write.</param>
	/// <param name="value">The value, as text.</param>
	/// <returns>True if the node was changed.</returns>
	private static bool TryWriteFunctionShape(AstNode node, string fieldName, string value)
	{
		return (node, fieldName) switch
		{
			(FunctionDeclaration function, "Kind") =>
				AssignMember<FunctionKind>(value, kind => function.Kind = kind),
			(FunctionDeclaration function, "Definition") =>
				AssignMember<FunctionDefinition>(value, definition => function.Definition = definition),
			(FunctionDeclaration function, "Virtual") =>
				AssignFlag(value, isVirtual => function.IsVirtual = isVirtual),
			(FunctionDeclaration function, "Abstract") =>
				AssignFlag(value, isAbstract => function.IsAbstract = isAbstract),
			(FunctionDeclaration function, ReadOnlyField) =>
				AssignFlag(value, isReadOnly => function.IsReadOnly = isReadOnly),
			(FunctionDeclaration function, "MustUseResult") =>
				AssignFlag(value, mustUse => function.MustUseResult = mustUse),

			(EntryPoint entryPoint, "Arguments") =>
				AssignFlag(value, acceptsArguments => entryPoint.AcceptsArguments = acceptsArguments),
			(EntryPoint entryPoint, "ExitCode") =>
				AssignFlag(value, returnsExitCode => entryPoint.ReturnsExitCode = returnsExitCode),

			(Parameter parameter, "Name") => Assign(() => parameter.Name = OrNull(value)),
			(Parameter parameter, "Type") => Assign(() => parameter.Type = OrNull(value)),
			(Parameter parameter, "Optional") => AssignFlag(value, optional => parameter.IsOptional = optional),
			(Parameter parameter, "Default") => Assign(() => parameter.DefaultValue = OrNull(value)),

			(VariableDeclaration varDecl, "Name") => value.Length > 0 && Assign(() => varDecl.Name = value),
			(VariableDeclaration varDecl, "Type") => Assign(() => varDecl.Type = OrNull(value)),
			(VariableDeclaration varDecl, "Constant") => AssignFlag(value, constant => varDecl.IsConstant = constant),
			(VariableDeclaration varDecl, "Inferred") => AssignFlag(value, inferred => varDecl.IsTypeInferred = inferred),
			(VariableDeclaration varDecl, VisibilityField) =>
				AssignMember<Visibility>(value, varVisibility => varDecl.Visibility = varVisibility),

			_ => false,
		};
	}

	/// <summary>
	/// Writes a field of an expression or a leaf.
	/// </summary>
	/// <param name="node">The node to write to.</param>
	/// <param name="fieldName">The field to write.</param>
	/// <param name="value">The value, as text.</param>
	/// <returns>True if the node was changed.</returns>
	private static bool TryWriteExpression(AstNode node, string fieldName, string value)
	{
		return (node, fieldName) switch
		{
			(VariableReference varRef, "Name") => value.Length > 0 && Assign(() => varRef.Name = value),

			(CallExpression callExpr, CalleeField) => value.Length > 0 && Assign(() => callExpr.Callee = value),

			(ConstructionExpression construction, TypeField) => Assign(() => construction.Type = OrNull(value)),

			(BinaryExpression binary, "Operator") =>
				AssignMember<BinaryOperator>(value, binaryOp => binary.Operator = binaryOp),
			(UnaryExpression unary, "Operator") =>
				AssignMember<UnaryOperator>(value, unaryOp => unary.Operator = unaryOp),
			(AssignmentStatement assignment, "Operator") =>
				AssignMember<AssignmentOperator>(value, assignOp => assignment.Operator = assignOp),

			(LiteralExpression<string> literal, ValueField) => Assign(() => literal.Value = value),
			(LiteralExpression<int> literal, ValueField) => AssignInteger(value, number => literal.Value = number),
			(LiteralExpression<double> literal, ValueField) => AssignNumber(value, number => literal.Value = number),
			(LiteralExpression<bool> literal, ValueField) => AssignFlag(value, flag => literal.Value = flag),

			(AstLeafNode<string> leaf, ValueField) => Assign(() => leaf.Value = value),
			(AstLeafNode<int> leaf, ValueField) => AssignInteger(value, number => leaf.Value = number),
			(AstLeafNode<double> leaf, ValueField) => AssignNumber(value, number => leaf.Value = number),
			(AstLeafNode<bool> leaf, ValueField) => AssignFlag(value, flag => leaf.Value = flag),

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

	/// <summary>
	/// Parses text into a flag and writes it, reporting whether the node changed.
	/// </summary>
	/// <param name="value">The text the user left in the box.</param>
	/// <param name="assign">The assignment to perform once the text parses.</param>
	/// <returns>True if the text parsed and the assignment was performed.</returns>
	/// <remarks>
	/// Parsing and writing are one call rather than a <c>TryParse(…) &amp;&amp; Assign(…)</c> pair
	/// because the switches over the AST are long enough that forty such pairs put one of them past
	/// what an analyzer will accept for one method. Each kind of field has its own name rather than
	/// an overload, so the lambda's parameter type is inferred from the one candidate.
	/// </remarks>
	private static bool AssignFlag(string value, Action<bool> assign)
	{
		if (!bool.TryParse(value, out bool parsed))
		{
			return false;
		}

		assign(parsed);
		return true;
	}

	/// <summary>
	/// Parses text into a whole number and writes it, reporting whether the node changed.
	/// </summary>
	/// <param name="value">The text the user left in the box.</param>
	/// <param name="assign">The assignment to perform once the text parses.</param>
	/// <returns>True if the text parsed and the assignment was performed.</returns>
	/// <remarks>Culture-invariant: this is a source value, not text shown in the user's locale.</remarks>
	private static bool AssignInteger(string value, Action<int> assign)
	{
		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
		{
			return false;
		}

		assign(parsed);
		return true;
	}

	/// <summary>
	/// Parses text into a number and writes it, reporting whether the node changed.
	/// </summary>
	/// <param name="value">The text the user left in the box.</param>
	/// <param name="assign">The assignment to perform once the text parses.</param>
	/// <returns>True if the text parsed and the assignment was performed.</returns>
	/// <remarks>Culture-invariant: this is a source value, not text shown in the user's locale.</remarks>
	private static bool AssignNumber(string value, Action<double> assign)
	{
		if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
		{
			return false;
		}

		assign(parsed);
		return true;
	}

	/// <summary>
	/// Parses text into a member of an enumeration and writes it, reporting whether the node changed.
	/// </summary>
	/// <typeparam name="TEnum">The enumeration the field holds.</typeparam>
	/// <param name="value">The text the user left in the box.</param>
	/// <param name="assign">The assignment to perform once the text parses.</param>
	/// <returns>True if the text named a member and the assignment was performed.</returns>
	/// <remarks>
	/// Case-insensitively, so a document hand-edited with "public" reads back the same as the
	/// inspector's own "Public". That was already how visibility was read; it is now how every
	/// enumeration is, there being no reason for one of them to be the exception.
	/// </remarks>
	private static bool AssignMember<TEnum>(string value, Action<TEnum> assign)
		where TEnum : struct, Enum
	{
		if (!Enum.TryParse(value, ignoreCase: true, out TEnum parsed))
		{
			return false;
		}

		assign(parsed);
		return true;
	}

	private static string Spell(bool value) => value ? "true" : "false";

	private static string Spell(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string Spell(double value) => value.ToString(CultureInfo.InvariantCulture);
}
