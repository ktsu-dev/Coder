// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests the operator and escaping logic shared by every generator, driven through each generator's
/// public surface. The four generators used to carry their own copy of these tables; this pins the
/// spellings so a future language can defer to the shared set without re-deriving them.
/// </summary>
[TestClass]
public class LanguageGeneratorBaseTests
{
	private static readonly PythonGenerator Python = new();
	private static readonly CSharpGenerator CSharp = new();
	private static readonly JavaScriptGenerator JavaScript = new();
	private static readonly CppGenerator Cpp = new();

	/// <summary>
	/// Every binary operator in the C-family spelling, which C# and C++ use unchanged.
	/// </summary>
	/// <returns>Operator and expected spelling pairs.</returns>
	public static IEnumerable<object[]> CFamilyBinaryOperators =>
	[
		[BinaryOperator.Add, "+"],
		[BinaryOperator.Subtract, "-"],
		[BinaryOperator.Multiply, "*"],
		[BinaryOperator.Divide, "/"],
		[BinaryOperator.Modulo, "%"],
		[BinaryOperator.Equal, "=="],
		[BinaryOperator.NotEqual, "!="],
		[BinaryOperator.LessThan, "<"],
		[BinaryOperator.LessThanOrEqual, "<="],
		[BinaryOperator.GreaterThan, ">"],
		[BinaryOperator.GreaterThanOrEqual, ">="],
		[BinaryOperator.LogicalAnd, "&&"],
		[BinaryOperator.LogicalOr, "||"],
		[BinaryOperator.BitwiseAnd, "&"],
		[BinaryOperator.BitwiseOr, "|"],
		[BinaryOperator.BitwiseXor, "^"],
		[BinaryOperator.LeftShift, "<<"],
		[BinaryOperator.RightShift, ">>"],
	];

	/// <summary>
	/// Every assignment operator, spelled identically in all four target languages.
	/// </summary>
	/// <returns>Operator and expected spelling pairs.</returns>
	public static IEnumerable<object[]> AssignmentOperators =>
	[
		[AssignmentOperator.Assign, "="],
		[AssignmentOperator.AddAssign, "+="],
		[AssignmentOperator.SubtractAssign, "-="],
		[AssignmentOperator.MultiplyAssign, "*="],
		[AssignmentOperator.DivideAssign, "/="],
		[AssignmentOperator.ModuloAssign, "%="],
		[AssignmentOperator.BitwiseAndAssign, "&="],
		[AssignmentOperator.BitwiseOrAssign, "|="],
		[AssignmentOperator.BitwiseXorAssign, "^="],
		[AssignmentOperator.LeftShiftAssign, "<<="],
		[AssignmentOperator.RightShiftAssign, ">>="],
	];

	private static string Binary(ILanguageGenerator generator, BinaryOperator op) =>
		generator.Generate(new BinaryExpression(new VariableReference("a"), op, new VariableReference("b")));

	private static string Assign(ILanguageGenerator generator, AssignmentOperator op) =>
		generator.Generate(new AssignmentStatement(new VariableReference("x"), Literal.Number(1), op)).Trim();

	/// <summary>
	/// Tests that C# and C++ spell every binary operator the C-family way.
	/// </summary>
	/// <param name="op">The operator under test.</param>
	/// <param name="expected">Its expected spelling.</param>
	[TestMethod]
	[DynamicData(nameof(CFamilyBinaryOperators))]
	public void CFamilyGenerators_SpellEveryBinaryOperator(BinaryOperator op, string expected)
	{
		Assert.AreEqual($"(a {expected} b)", Binary(CSharp, op), $"C# should spell {op} as {expected}");
		Assert.AreEqual($"(a {expected} b)", Binary(Cpp, op), $"C++ should spell {op} as {expected}");
	}

	/// <summary>
	/// Tests that JavaScript matches the C-family set apart from equality, which it strictens.
	/// </summary>
	/// <param name="op">The operator under test.</param>
	/// <param name="expected">The C-family spelling.</param>
	[TestMethod]
	[DynamicData(nameof(CFamilyBinaryOperators))]
	public void JavaScript_StrictensOnlyEquality(BinaryOperator op, string expected)
	{
		string javascript = op switch
		{
			BinaryOperator.Equal => "===",
			BinaryOperator.NotEqual => "!==",
			_ => expected,
		};

		Assert.AreEqual($"(a {javascript} b)", Binary(JavaScript, op), $"JavaScript should spell {op} as {javascript}");
	}

	/// <summary>
	/// Tests that Python matches the C-family set apart from the logical operators, which it words.
	/// </summary>
	/// <param name="op">The operator under test.</param>
	/// <param name="expected">The C-family spelling.</param>
	[TestMethod]
	[DynamicData(nameof(CFamilyBinaryOperators))]
	public void Python_WordsOnlyTheLogicalOperators(BinaryOperator op, string expected)
	{
		string python = op switch
		{
			BinaryOperator.LogicalAnd => "and",
			BinaryOperator.LogicalOr => "or",
			_ => expected,
		};

		Assert.AreEqual($"(a {python} b)", Binary(Python, op), $"Python should spell {op} as {python}");
	}

	/// <summary>
	/// Tests that every generator spells every assignment operator the same way.
	/// </summary>
	/// <param name="op">The operator under test.</param>
	/// <param name="expected">Its expected spelling.</param>
	[TestMethod]
	[DynamicData(nameof(AssignmentOperators))]
	public void EveryGenerator_SpellsEveryAssignmentOperatorAlike(AssignmentOperator op, string expected)
	{
		Assert.AreEqual($"x {expected} 1", Assign(Python, op), $"Python should spell {op} as {expected}");
		Assert.AreEqual($"x {expected} 1;", Assign(CSharp, op), $"C# should spell {op} as {expected}");
		Assert.AreEqual($"x {expected} 1;", Assign(JavaScript, op), $"JavaScript should spell {op} as {expected}");
		Assert.AreEqual($"x {expected} 1;", Assign(Cpp, op), $"C++ should spell {op} as {expected}");
	}

	/// <summary>
	/// Tests that an operator value outside the enum is refused rather than emitted as a number.
	/// </summary>
	[TestMethod]
	public void UnmappedOperators_AreRefused()
	{
		BinaryOperator binary = (BinaryOperator)9999;
		AssignmentOperator assignment = (AssignmentOperator)9999;

		Assert.ThrowsExactly<NotSupportedException>(() => Binary(Cpp, binary));
		Assert.ThrowsExactly<NotSupportedException>(() => Binary(JavaScript, binary));
		Assert.ThrowsExactly<NotSupportedException>(() => Binary(Python, binary));
		Assert.ThrowsExactly<NotSupportedException>(() => Assign(Cpp, assignment));
		Assert.ThrowsExactly<NotSupportedException>(() => Assign(JavaScript, assignment));
	}

	/// <summary>
	/// Tests that the shared escaper handles every sequence it claims to, in every generator.
	/// </summary>
	[TestMethod]
	public void StringLiterals_AreEscapedIdenticallyEverywhere()
	{
		LiteralExpression<string> literal = Literal.Text("a\\b\"c\nd\re\tf");
		const string expected = "\"a\\\\b\\\"c\\nd\\re\\tf\"";

		Assert.AreEqual(expected, Python.Generate(literal));
		Assert.AreEqual(expected, CSharp.Generate(literal));
		Assert.AreEqual(expected, JavaScript.Generate(literal));
		Assert.AreEqual(expected, Cpp.Generate(literal));
	}

	/// <summary>
	/// Tests the legacy <see cref="AstLeafNode{T}"/> path that the generators still accept, so the
	/// older serialized shape keeps working.
	/// </summary>
	[TestMethod]
	public void LegacyLeafNodes_AreStillGenerated()
	{
		Assert.AreEqual("\"hi\"", JavaScript.Generate(new AstLeafNode<string>("hi")));
		Assert.AreEqual("7", JavaScript.Generate(new AstLeafNode<int>(7)));
		Assert.AreEqual("true", JavaScript.Generate(new AstLeafNode<bool>(true)));

		Assert.AreEqual("\"hi\"", Cpp.Generate(new AstLeafNode<string>("hi")));
		Assert.AreEqual("7", Cpp.Generate(new AstLeafNode<int>(7)));
		Assert.AreEqual("false", Cpp.Generate(new AstLeafNode<bool>(false)));

		Assert.AreEqual("True", Python.Generate(new AstLeafNode<bool>(true)));
	}

	/// <summary>
	/// Tests that a parameter generated on its own carries the language's own conventions, which is
	/// the path a caller takes when composing a signature by hand.
	/// </summary>
	[TestMethod]
	public void StandaloneParameter_UsesTheLanguageConventions()
	{
		Parameter parameter = new("count", "int");

		Assert.AreEqual("count", JavaScript.Generate(parameter), "JavaScript parameters carry no type");
		Assert.AreEqual("int count", Cpp.Generate(parameter));
		Assert.AreEqual("int count", CSharp.Generate(parameter));
		Assert.AreEqual("count: int", Python.Generate(parameter), "Python spells the type as a hint after the name");
	}

	/// <summary>
	/// Tests that a double literal is spelled with an invariant decimal point, so a machine with a
	/// comma decimal separator does not emit code that will not parse.
	/// </summary>
	[TestMethod]
	public void DoubleLiterals_UseAnInvariantDecimalPoint()
	{
		LiteralExpression<double> literal = Literal.DecimalValue(1.5);

		Assert.AreEqual("1.5", JavaScript.Generate(literal));
		Assert.AreEqual("1.5", Cpp.Generate(literal));
	}
}
