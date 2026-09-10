// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ktsu.Coder.Ast;

/// <summary>
/// A uniform view of the AST's parent/child structure, so the editor can walk any node without a
/// switch over its type.
/// </summary>
/// <remarks>
/// The AST keeps children in three different places depending on the node: typed properties, typed
/// collections, and <see cref="AstCompositeNode"/>'s keyed dictionary. Every one of those is reached
/// through <see cref="SlotsOf"/>, <see cref="ChildrenOf"/> and <see cref="TryAttach"/> here, which is
/// what lets <see cref="AstGraph"/> convert in both directions with no per-type code of its own.
/// <para>
/// The mapping is deliberately hand-written rather than reflective: an AST node's shape is part of
/// the library's contract, and a reflective walk would silently start exposing any property someone
/// added later, including ones that are not children at all.
/// </para>
/// </remarks>
public static class AstSchema
{
	private static readonly AstSlot ExpressionSlot = new("Expression", AstSlotCardinality.One, AstSlotKind.Expression);
	private static readonly AstSlot LeftSlot = new("Left", AstSlotCardinality.One, AstSlotKind.Expression);
	private static readonly AstSlot RightSlot = new("Right", AstSlotCardinality.One, AstSlotKind.Expression);
	private static readonly AstSlot OperandSlot = new("Operand", AstSlotCardinality.One, AstSlotKind.Expression);
	private static readonly AstSlot InitialValueSlot = new("InitialValue", AstSlotCardinality.One, AstSlotKind.Expression);
	private static readonly AstSlot TargetSlot = new("Target", AstSlotCardinality.One, AstSlotKind.Expression);
	private static readonly AstSlot ValueSlot = new("Value", AstSlotCardinality.One, AstSlotKind.Expression);
	private static readonly AstSlot ParametersSlot = new("Parameters", AstSlotCardinality.Many, AstSlotKind.Parameter);
	private static readonly AstSlot BodySlot = new("Body", AstSlotCardinality.Many, AstSlotKind.Statement);
	private static readonly AstSlot MembersSlot = new("Members", AstSlotCardinality.Many, AstSlotKind.Member);
	/// <summary>The name of the slot an expression's arguments sit in.</summary>
	private const string ArgumentsSlotName = "Arguments";

	private static readonly AstSlot ArgumentsSlot = new(ArgumentsSlotName, AstSlotCardinality.Many, AstSlotKind.Expression);
	private static readonly AstSlot EnumMembersSlot = new("Members", AstSlotCardinality.Many, AstSlotKind.EnumMember);

	/// <summary>
	/// Lists the slots a node exposes, in the order the editor should draw them.
	/// </summary>
	/// <param name="node">The node to describe.</param>
	/// <returns>The node's slots, empty for a leaf.</returns>
	public static IReadOnlyList<AstSlot> SlotsOf(AstNode node) => node switch
	{
		SourceFile => [MembersSlot],
		NamespaceDeclaration => [MembersSlot],
		ClassDeclaration => [MembersSlot],
		EnumDeclaration => [EnumMembersSlot],
		FieldDeclaration => [InitialValueSlot],
		MemberInitialiser => [ValueSlot],
		ConstructionExpression => [ArgumentsSlot],
		FunctionDeclaration => [ParametersSlot, BodySlot],
		EntryPoint => [BodySlot],
		ReturnStatement => [ExpressionSlot],
		BinaryExpression => [LeftSlot, RightSlot],
		UnaryExpression => [OperandSlot],
		VariableDeclaration => [InitialValueSlot],
		AssignmentStatement => [TargetSlot, ValueSlot],
		_ => [],
	};

	/// <summary>
	/// Reads the children currently sitting in a slot.
	/// </summary>
	/// <param name="node">The parent node.</param>
	/// <param name="slot">The slot to read.</param>
	/// <returns>The children, in order. Empty when the slot is unfilled.</returns>
	/// <exception cref="ArgumentException"><paramref name="slot"/> is not one of <paramref name="node"/>'s.</exception>
	public static IReadOnlyList<AstNode> ChildrenOf(AstNode node, AstSlot slot)
	{
		Ensure.NotNull(node);
		Ensure.NotNull(slot);

		AstNode? single = (node, slot.Name) switch
		{
			(ReturnStatement returnStmt, "Expression") => returnStmt.Expression,
			(BinaryExpression binary, "Left") => binary.Left,
			(BinaryExpression binary, "Right") => binary.Right,
			(UnaryExpression unary, "Operand") => unary.Operand,
			(VariableDeclaration varDecl, "InitialValue") => varDecl.InitialValue,
			(FieldDeclaration field, "InitialValue") => field.InitialValue,
			(MemberInitialiser initialiser, "Value") => initialiser.Value,
			(AssignmentStatement assignment, "Target") => assignment.Target,
			(AssignmentStatement assignment, "Value") => assignment.Value,
			_ => null,
		};

		if (single is not null)
		{
			return [single];
		}

		return (node, slot.Name) switch
		{
			(SourceFile file, "Members") => [.. file.Members],
			(NamespaceDeclaration namespaceDecl, "Members") => [.. namespaceDecl.Members],
			(ClassDeclaration classDecl, "Members") => [.. classDecl.Members],
			(EnumDeclaration enumDecl, "Members") => [.. enumDecl.Members],
			(ConstructionExpression construction, ArgumentsSlotName) => [.. construction.Arguments],
			(FunctionDeclaration function, "Parameters") => [.. function.Parameters],
			(FunctionDeclaration function, "Body") => [.. function.Body],
			(EntryPoint entryPoint, "Body") => [.. entryPoint.Body],
			_ when SlotsOf(node).Contains(slot) => [],
			_ => throw new ArgumentException($"{node.GetNodeTypeName()} has no slot named '{slot.Name}'.", nameof(slot)),
		};
	}

	/// <summary>
	/// Attaches a child to a slot, at a position within it.
	/// </summary>
	/// <param name="parent">The parent node.</param>
	/// <param name="slot">The slot to fill.</param>
	/// <param name="index">The position within the slot; ignored for a single-valued one.</param>
	/// <param name="child">The node to attach.</param>
	/// <returns>True if the child was attached; false if the slot will not take it.</returns>
	/// <remarks>
	/// An index inside an existing sequence replaces that entry; one at or past the end appends. That
	/// is what makes connecting to a slot's free pin add a child and connecting to a filled one swap
	/// it, which is how a node editor is expected to behave.
	/// </remarks>
	public static bool TryAttachAt(AstNode parent, AstSlot slot, int index, AstNode child)
	{
		Ensure.NotNull(parent);
		Ensure.NotNull(slot);
		Ensure.NotNull(child);

		if (!Accepts(slot, child))
		{
			return false;
		}

		if (slot.Cardinality == AstSlotCardinality.Many && index < ChildrenOf(parent, slot).Count)
		{
			// Swapping an occupant out, which has to happen in place: removing and appending would
			// silently move the new child to the end of the sequence and reorder a function body.
			return TryReplaceAt(parent, slot, index, child);
		}

		switch (parent, slot.Name)
		{
			case (ReturnStatement returnStmt, "Expression"):
				returnStmt.SetExpression(child);
				return true;

			case (BinaryExpression binary, "Left") when child is Expression leftExpr:
				binary.Left = leftExpr;
				return true;

			case (BinaryExpression binary, "Right") when child is Expression rightExpr:
				binary.Right = rightExpr;
				return true;

			case (UnaryExpression unary, "Operand") when child is Expression operandExpr:
				unary.Operand = operandExpr;
				return true;

			case (VariableDeclaration varDecl, "InitialValue") when child is Expression initialExpr:
				varDecl.InitialValue = initialExpr;
				return true;

			case (FieldDeclaration field, "InitialValue") when child is Expression fieldExpr:
				field.InitialValue = fieldExpr;
				return true;

			case (MemberInitialiser initialiser, "Value") when child is Expression initialiserExpr:
				initialiser.Value = initialiserExpr;
				return true;

			case (ConstructionExpression construction, ArgumentsSlotName):
				construction.Arguments.Add(child);
				return true;

			case (AssignmentStatement assignment, "Target") when child is Expression targetExpr:
				assignment.Target = targetExpr;
				return true;

			case (AssignmentStatement assignment, "Value") when child is Expression valueExpr:
				assignment.Value = valueExpr;
				return true;

			case (FunctionDeclaration function, "Parameters") when child is Parameter parameter:
				function.Parameters.Add(parameter);
				return true;

			case (FunctionDeclaration function, "Body"):
				function.Body.Add(child);
				return true;

			case (EntryPoint entryPoint, "Body"):
				entryPoint.Body.Add(child);
				return true;

			case (ClassDeclaration classDecl, "Members"):
				classDecl.Members.Add(child);
				return true;

			case (SourceFile file, "Members"):
				file.Members.Add(child);
				return true;

			case (NamespaceDeclaration namespaceDecl, "Members"):
				namespaceDecl.Members.Add(child);
				return true;

			case (EnumDeclaration enumDecl, "Members") when child is EnumMember enumMember:
				enumDecl.Members.Add(enumMember);
				return true;

			default:
				return false;
		}
	}

	/// <summary>
	/// Swaps the child at one position of a sequence for another, keeping the order around it.
	/// </summary>
	/// <param name="parent">The parent node.</param>
	/// <param name="slot">The slot to write into.</param>
	/// <param name="index">The position to overwrite.</param>
	/// <param name="child">The node to put there.</param>
	/// <returns>True if the child was written.</returns>
	private static bool TryReplaceAt(AstNode parent, AstSlot slot, int index, AstNode child)
	{
		switch (parent, slot.Name)
		{
			case (FunctionDeclaration function, "Parameters") when child is Parameter parameter:
				function.Parameters[index] = parameter;
				return true;

			case (FunctionDeclaration function, "Body"):
				function.Body[index] = child;
				return true;

			case (EntryPoint entryPoint, "Body"):
				entryPoint.Body[index] = child;
				return true;

			case (ClassDeclaration classDecl, "Members"):
				classDecl.Members[index] = child;
				return true;

			case (SourceFile file, "Members"):
				file.Members[index] = child;
				return true;

			case (NamespaceDeclaration namespaceDecl, "Members"):
				namespaceDecl.Members[index] = child;
				return true;

			case (EnumDeclaration enumDecl, "Members") when child is EnumMember enumMember:
				enumDecl.Members[index] = enumMember;
				return true;

			case (ConstructionExpression construction, ArgumentsSlotName):
				construction.Arguments[index] = child;
				return true;

			default:
				return false;
		}
	}

	/// <summary>
	/// Attaches a child to a slot, replacing a single-valued one and appending to a sequence.
	/// </summary>
	/// <param name="parent">The parent node.</param>
	/// <param name="slot">The slot to fill.</param>
	/// <param name="child">The node to attach.</param>
	/// <returns>True if the child was attached; false if the slot will not take it.</returns>
	public static bool TryAttach(AstNode parent, AstSlot slot, AstNode child) =>
		TryAttachAt(parent, slot, int.MaxValue, child);

	/// <summary>
	/// Empties one position of a slot.
	/// </summary>
	/// <param name="parent">The parent node.</param>
	/// <param name="slot">The slot to empty.</param>
	/// <param name="index">The position within the slot; ignored for a single-valued one.</param>
	/// <returns>True if something was detached.</returns>
	/// <remarks>
	/// <see cref="BinaryExpression.Left"/> and the other non-nullable operands cannot simply be
	/// cleared, so they are set to the same empty string literal the AST's own parameterless
	/// constructors use. That is the AST's existing spelling of "not filled in yet", so detaching in
	/// the editor leaves a node the rest of the library already understands rather than a null it
	/// does not.
	/// </remarks>
	public static bool TryDetachAt(AstNode parent, AstSlot slot, int index)
	{
		Ensure.NotNull(parent);
		Ensure.NotNull(slot);

		switch (parent, slot.Name)
		{
			case (ReturnStatement returnStmt, "Expression"):
				return returnStmt.RemoveChild("Expression");

			case (VariableDeclaration varDecl, "InitialValue"):
				bool hadValue = varDecl.InitialValue is not null;
				varDecl.InitialValue = null;
				return hadValue;

			case (FieldDeclaration field, "InitialValue"):
				bool hadFieldValue = field.InitialValue is not null;
				field.InitialValue = null;
				return hadFieldValue;

			case (MemberInitialiser initialiser, "Value"):
				bool hadInitialiserValue = initialiser.Value is not null;
				initialiser.Value = null;
				return hadInitialiserValue;

			case (BinaryExpression binary, "Left"):
				binary.Left = Unfilled();
				return true;

			case (BinaryExpression binary, "Right"):
				binary.Right = Unfilled();
				return true;

			case (UnaryExpression unary, "Operand"):
				unary.Operand = Unfilled();
				return true;

			case (AssignmentStatement assignment, "Target"):
				assignment.Target = Unfilled();
				return true;

			case (AssignmentStatement assignment, "Value"):
				assignment.Value = Unfilled();
				return true;

			case (FunctionDeclaration function, "Parameters") when index < function.Parameters.Count:
				function.Parameters.RemoveAt(index);
				return true;

			case (FunctionDeclaration function, "Body") when index < function.Body.Count:
				function.Body.RemoveAt(index);
				return true;

			case (EntryPoint entryPoint, "Body") when index < entryPoint.Body.Count:
				entryPoint.Body.RemoveAt(index);
				return true;

			case (ClassDeclaration classDecl, "Members") when index < classDecl.Members.Count:
				classDecl.Members.RemoveAt(index);
				return true;

			case (SourceFile file, "Members") when index < file.Members.Count:
				file.Members.RemoveAt(index);
				return true;

			case (NamespaceDeclaration namespaceDecl, "Members") when index < namespaceDecl.Members.Count:
				namespaceDecl.Members.RemoveAt(index);
				return true;

			case (EnumDeclaration enumDecl, "Members") when index < enumDecl.Members.Count:
				enumDecl.Members.RemoveAt(index);
				return true;

			case (ConstructionExpression construction, ArgumentsSlotName) when index < construction.Arguments.Count:
				construction.Arguments.RemoveAt(index);
				return true;

			default:
				return false;
		}
	}

	/// <summary>
	/// Builds the node to put in a slot when the user asks for one more child in it.
	/// </summary>
	/// <param name="slot">The slot the child is for.</param>
	/// <returns>A node the slot will take.</returns>
	/// <remarks>
	/// This is what makes "one more parameter" a single click rather than creating a node from the
	/// palette and dragging it into a pin. Each kind's default is the emptiest thing that is still
	/// valid there, so the user fills it in rather than clearing it out first.
	/// </remarks>
	public static AstNode CreateDefaultChild(AstSlot slot)
	{
		Ensure.NotNull(slot);

		return slot.Accepts switch
		{
			AstSlotKind.Parameter => new Parameter("value", "int"),
			AstSlotKind.Statement => new ReturnStatement(),
			AstSlotKind.Member => new FunctionDeclaration("newMethod") { ReturnType = "void" },
			AstSlotKind.EnumMember => new EnumMember("NewValue"),
			_ => Unfilled(),
		};
	}

	/// <summary>
	/// Builds the node the AST uses to mean "nothing here yet".
	/// </summary>
	/// <returns>An empty string literal, matching what the AST's parameterless constructors install.</returns>
	public static Expression Unfilled() => new LiteralExpression<string>(string.Empty);

	/// <summary>
	/// Reports whether a node is the placeholder <see cref="Unfilled"/> produces, so the editor can
	/// show an operand as outstanding rather than as a deliberate empty string.
	/// </summary>
	/// <param name="node">The node to test.</param>
	/// <returns>True if the node is an empty string literal.</returns>
	public static bool IsUnfilled(AstNode node) =>
		node is LiteralExpression<string> { Value: "" } or AstLeafNode<string> { Value: "" };

	/// <summary>
	/// Reports whether a slot will take a node, which is what makes a link legal.
	/// </summary>
	/// <param name="slot">The slot being connected to.</param>
	/// <param name="candidate">The node being connected.</param>
	/// <returns>True if the connection is legal.</returns>
	/// <remarks>
	/// The legacy <see cref="AstLeafNode{T}"/> shapes are not <see cref="Ast.Expression"/> subclasses
	/// but every generator emits them wherever an expression is expected, so they count as one here.
	/// </remarks>
	public static bool Accepts(AstSlot slot, AstNode candidate)
	{
		Ensure.NotNull(slot);
		Ensure.NotNull(candidate);

		return slot.Accepts switch
		{
			AstSlotKind.Parameter => candidate is Parameter,
			AstSlotKind.Expression => IsExpression(candidate),
			// A parameter is not a statement, and neither is an entry point: a program starts
			// running at one, so it belongs to a class or to the document rather than inside a body.
			AstSlotKind.Statement => candidate is not (Parameter or EntryPoint),
			AstSlotKind.Member => candidate is FunctionDeclaration or VariableDeclaration or FieldDeclaration
				or ClassDeclaration or EnumDeclaration or NamespaceDeclaration or UsingAlias or EntryPoint,
			AstSlotKind.EnumMember => candidate is EnumMember,
			_ => false,
		};
	}

	/// <summary>
	/// Reports whether a node can stand where an expression is expected.
	/// </summary>
	/// <param name="node">The node to test.</param>
	/// <returns>True if the node evaluates to a value.</returns>
	public static bool IsExpression(AstNode node) =>
		node is Expression
			or AstLeafNode<string>
			or AstLeafNode<int>
			or AstLeafNode<bool>
			or AstLeafNode<double>;

	/// <summary>
	/// Builds the caption the editor draws on a node's title bar.
	/// </summary>
	/// <param name="node">The node to describe.</param>
	/// <returns>A short label naming the node and its most identifying detail.</returns>
	/// <remarks>
	/// The type name alone is not enough to tell two variable references apart on screen, so the
	/// detail that distinguishes an instance — a name, an operator, a literal's value — is included.
	/// </remarks>
	public static string Describe(AstNode node)
	{
		Ensure.NotNull(node);

		return node switch
		{
			ClassDeclaration classDecl => $"class {classDecl.Name ?? "<unnamed>"}",
			FunctionDeclaration function => $"function {function.Name ?? "<unnamed>"}",
			EntryPoint => "entry point",
			Parameter parameter => $"param {parameter.Name ?? "<unnamed>"}",
			ReturnStatement => "return",
			BinaryExpression binary => $"binary {SpellOrName(binary.Operator)}",
			UnaryExpression unary => $"unary {SpellOrName(unary.Operator)}",
			AssignmentStatement assignment => $"assign {SpellOrName(assignment.Operator)}",
			VariableDeclaration varDecl => $"var {varDecl.Name}",
			VariableReference varRef => varRef.Name,
			LiteralExpression<string> literal => Quote(literal.Value),
			LiteralExpression<int> literal => literal.Value.ToString(CultureInfo.InvariantCulture),
			LiteralExpression<double> literal => literal.Value.ToString(CultureInfo.InvariantCulture),
			LiteralExpression<bool> literal => literal.Value ? "true" : "false",
			AstLeafNode<string> leaf => Quote(leaf.Value),
			AstLeafNode<int> leaf => leaf.Value.ToString(CultureInfo.InvariantCulture),
			AstLeafNode<bool> leaf => leaf.Value ? "true" : "false",
			_ => node.GetNodeTypeName(),
		};
	}

	private static string Quote(string? value) => $"\"{value ?? string.Empty}\"";

	// A caption must never throw on an out-of-range operator, so an unspelled one falls back to its name.
	private static string SpellOrName(BinaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) && symbol is not null ? symbol : op.ToString();

	private static string SpellOrName(UnaryOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) && symbol is not null ? symbol : op.ToString();

	private static string SpellOrName(AssignmentOperator op) =>
		OperatorSymbols.TryGetSymbol(op, out string? symbol) && symbol is not null ? symbol : op.ToString();
}
