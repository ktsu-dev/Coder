// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="AstFields"/>, which is how the editor reads and writes everything about a
/// node that is not one of its children.
/// </summary>
/// <remarks>
/// These are the rules behind the inspector panel, so they are tested here rather than through the
/// UI: what a field is called, what it will accept and what it refuses is decided entirely by this
/// type.
/// </remarks>
[TestClass]
public class AstFieldsTests
{
	/// <summary>
	/// Tests that a literal's value is offered as a field of the right kind, so the editor draws a
	/// number box for a number and a tick box for a boolean.
	/// </summary>
	[TestMethod]
	public void Of_DescribesEachLiteralByItsKind()
	{
		Assert.AreEqual(AstFieldKind.Text, AstFields.Of(Literal.Text("hello"))[0].Kind);
		Assert.AreEqual(AstFieldKind.Number, AstFields.Of(Literal.Number(3))[0].Kind);
		Assert.AreEqual(AstFieldKind.Fraction, AstFields.Of(Literal.DecimalValue(1.5))[0].Kind);
		Assert.AreEqual(AstFieldKind.Flag, AstFields.Of(Literal.Bool(true))[0].Kind);
	}

	/// <summary>
	/// Tests that a literal's current value is what the field reports, which is what the inspector
	/// puts in the box.
	/// </summary>
	[TestMethod]
	public void Of_ReadsTheCurrentValue()
	{
		Assert.AreEqual("hello", AstFields.Read(Literal.Text("hello"), "Value"));
		Assert.AreEqual("3", AstFields.Read(Literal.Number(3), "Value"));
		Assert.AreEqual("1.5", AstFields.Read(Literal.DecimalValue(1.5), "Value"));
		Assert.AreEqual("true", AstFields.Read(Literal.Bool(true), "Value"));
	}

	/// <summary>
	/// Tests that writing a literal's value changes the document, which is the whole point of the
	/// inspector existing.
	/// </summary>
	[TestMethod]
	public void TryWrite_EditsALiteral()
	{
		LiteralExpression<int> literal = Literal.Number(1);

		Assert.IsTrue(AstFields.TryWrite(literal, "Value", "42"));
		Assert.AreEqual(42, literal.Value);
	}

	/// <summary>
	/// Tests that a value the field cannot hold is refused rather than written as a default, so a
	/// half-typed number leaves the document alone.
	/// </summary>
	[TestMethod]
	public void TryWrite_RefusesAValueThatDoesNotParse()
	{
		LiteralExpression<int> literal = Literal.Number(7);

		Assert.IsFalse(AstFields.TryWrite(literal, "Value", "seven"));
		Assert.AreEqual(7, literal.Value);
	}

	/// <summary>
	/// Tests that writing the value a field already holds reports no change, so an inspector that
	/// rewrites what it read does not fill the undo stack with steps that undo nothing.
	/// </summary>
	[TestMethod]
	public void TryWrite_IsANoOpForTheValueAlreadyHeld()
	{
		FunctionDeclaration function = new("total");

		Assert.IsFalse(AstFields.TryWrite(function, "Name", "total"));
		Assert.IsTrue(AstFields.TryWrite(function, "Name", "sum"));
		Assert.AreEqual("sum", function.Name);
	}

	/// <summary>
	/// Tests that a field a node does not have is refused rather than silently ignored.
	/// </summary>
	[TestMethod]
	public void TryWrite_RefusesAFieldTheNodeDoesNotHave()
	{
		ReturnStatement returnStmt = new();

		Assert.IsNull(AstFields.Read(returnStmt, "Name"));
		Assert.IsFalse(AstFields.TryWrite(returnStmt, "Name", "anything"));
		Assert.AreEqual(0, AstFields.Of(returnStmt).Count);
	}

	/// <summary>
	/// Tests that an operator is offered as a choice of every operator the AST has, since picking one
	/// is how a user says which expression they meant.
	/// </summary>
	[TestMethod]
	public void Of_OffersEveryOperatorAsAChoice()
	{
		BinaryExpression binary = new(Literal.Number(1), BinaryOperator.Add, Literal.Number(2));
		AstField field = AstFields.Of(binary).Single();

		Assert.AreEqual("Operator", field.Name);
		Assert.AreEqual(AstFieldKind.Choice, field.Kind);
		Assert.AreEqual(Enum.GetValues<BinaryOperator>().Length, field.Choices.Count);
		Assert.AreEqual("Add", field.Value);

		// The label carries the symbol, since that is what a user is looking for in the list.
		Assert.IsTrue(field.Choices.Any(choice => choice.Label.Contains('+', StringComparison.Ordinal)));
	}

	/// <summary>
	/// Tests that picking an operator writes it, for each of the three kinds of operator the AST has.
	/// </summary>
	[TestMethod]
	public void TryWrite_ChangesAnOperator()
	{
		BinaryExpression binary = new(Literal.Number(1), BinaryOperator.Add, Literal.Number(2));
		UnaryExpression unary = new(UnaryOperator.Negate, Literal.Number(1));
		AssignmentStatement assignment = new(new VariableReference("x"), Literal.Number(1));

		Assert.IsTrue(AstFields.TryWrite(binary, "Operator", nameof(BinaryOperator.Multiply)));
		Assert.IsTrue(AstFields.TryWrite(unary, "Operator", nameof(UnaryOperator.LogicalNot)));
		Assert.IsTrue(AstFields.TryWrite(assignment, "Operator", nameof(AssignmentOperator.AddAssign)));

		Assert.AreEqual(BinaryOperator.Multiply, binary.Operator);
		Assert.AreEqual(UnaryOperator.LogicalNot, unary.Operator);
		Assert.AreEqual(AssignmentOperator.AddAssign, assignment.Operator);
	}

	/// <summary>
	/// Tests that an operator name the enumeration does not have is refused, rather than parsed into
	/// whatever happens to be first.
	/// </summary>
	[TestMethod]
	public void TryWrite_RefusesAnUnknownOperator()
	{
		BinaryExpression binary = new(Literal.Number(1), BinaryOperator.Add, Literal.Number(2));

		Assert.IsFalse(AstFields.TryWrite(binary, "Operator", "Exponentiate"));
		Assert.AreEqual(BinaryOperator.Add, binary.Operator);
	}

	/// <summary>
	/// Tests that a declaration's name and type are editable, which is what naming a function or
	/// typing a parameter amounts to.
	/// </summary>
	[TestMethod]
	public void TryWrite_EditsDeclarations()
	{
		FunctionDeclaration function = new("newFunction") { ReturnType = "void" };
		Parameter parameter = new("value", "int");
		VariableDeclaration variable = new("total", "int");
		ClassDeclaration classDecl = new("NewClass");

		Assert.IsTrue(AstFields.TryWrite(function, "Name", "total"));
		Assert.IsTrue(AstFields.TryWrite(function, "ReturnType", "int"));
		Assert.IsTrue(AstFields.TryWrite(parameter, "Name", "count"));
		Assert.IsTrue(AstFields.TryWrite(parameter, "Type", "double"));
		Assert.IsTrue(AstFields.TryWrite(parameter, "Optional", "true"));
		Assert.IsTrue(AstFields.TryWrite(variable, "Constant", "true"));
		Assert.IsTrue(AstFields.TryWrite(classDecl, "BaseType", "Shape"));
		Assert.IsTrue(AstFields.TryWrite(classDecl, "Access", "internal"));

		Assert.AreEqual("total", function.Name);
		Assert.AreEqual("int", function.ReturnType);
		Assert.AreEqual("count", parameter.Name);
		Assert.AreEqual("double", parameter.Type);
		Assert.IsTrue(parameter.IsOptional);
		Assert.IsTrue(variable.IsConstant);
		Assert.AreEqual("Shape", classDecl.BaseType);
		Assert.AreEqual("internal", classDecl.AccessModifier);
	}

	/// <summary>
	/// Tests that clearing a box means "not set" rather than "set to empty", since that is what a
	/// user who empties a field is saying.
	/// </summary>
	[TestMethod]
	public void TryWrite_TreatsEmptyTextAsAbsent()
	{
		FunctionDeclaration function = new("total") { ReturnType = "int" };

		Assert.IsTrue(AstFields.TryWrite(function, "ReturnType", string.Empty));
		Assert.IsNull(function.ReturnType);
	}

	/// <summary>
	/// Tests that a name which must exist is not cleared, so emptying the box cannot leave a variable
	/// reference that refers to nothing.
	/// </summary>
	[TestMethod]
	public void TryWrite_RefusesToClearARequiredName()
	{
		VariableReference reference = new("value");

		Assert.IsFalse(AstFields.TryWrite(reference, "Name", string.Empty));
		Assert.AreEqual("value", reference.Name);
	}

	/// <summary>
	/// Tests that every node the palette offers can be inspected without faulting, so no entry in the
	/// menu produces something the inspector chokes on.
	/// </summary>
	[TestMethod]
	public void Of_HandlesEveryPaletteNode()
	{
		foreach (AstNodeTemplate template in AstNodeCatalog.Templates)
		{
			AstNode node = template.Create();

			foreach (AstField field in AstFields.Of(node))
			{
				Assert.IsFalse(string.IsNullOrWhiteSpace(field.Name), template.Label);
				Assert.AreEqual(field.Value, AstFields.Read(node, field.Name), template.Label);

				// A choice must offer the value it currently holds, or the inspector would show a
				// combo box with nothing selected.
				if (field.Kind == AstFieldKind.Choice)
				{
					Assert.IsTrue(
						field.Choices.Any(choice => string.Equals(choice.Value, field.Value, StringComparison.Ordinal)),
						$"{template.Label}: {field.Name} holds {field.Value}, which is not one of its choices");
				}
			}
		}
	}
}
