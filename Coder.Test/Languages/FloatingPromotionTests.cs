// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that an integer type outside <c>int</c> and <c>long</c> mixed with a floating type is typed as
/// the floating type whichever side it is on (#292).
/// </summary>
/// <remarks>
/// <c>s * 1.5</c> with <c>short s</c> is a <c>double</c> in every C-family language. Typed as the
/// <c>short</c>, Python and JavaScript truncated <c>(s * 1.5) / 2</c> as integer division and C
/// declared an inferred local holding it as a <c>short</c>, dropping the fraction.
/// </remarks>
[TestClass]
public class FloatingPromotionTests
{
	/// <summary>
	/// Tests that a division of an integer times a floating value is true division in Python and JavaScript.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	/// <param name="integerType">The integer operand's type.</param>
	/// <param name="floatingType">The floating operand's type.</param>
	[TestMethod]
	[DataRow("python", "short", "double")]
	[DataRow("python", "byte", "double")]
	[DataRow("python", "uint", "double")]
	[DataRow("python", "short", "float")]
	[DataRow("python", "byte", "float")]
	[DataRow("python", "uint", "float")]
	[DataRow("javascript", "short", "double")]
	[DataRow("javascript", "byte", "float")]
	[DataRow("javascript", "uint", "float")]
	public void IntegerOnTheLeftOfAFloatingProduct_DividesTruly(string language, string integerType, string floatingType)
	{
		ILanguageGenerator generator = language == "python" ? new PythonGenerator() : new JavaScriptGenerator();

		string code = generator.Generate(Dividing(integerType, floatingType, integerType));

		StringAssert.Contains(code, "((s * f) / 2)", StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a division of two integers stays integer division when one of them is a <c>short</c>.
	/// </summary>
	/// <param name="language">The generator to ask.</param>
	[TestMethod]
	[DataRow("python")]
	[DataRow("javascript")]
	public void ShortTimesInt_StillDividesAsIntegers(string language)
	{
		ILanguageGenerator generator = language == "python" ? new PythonGenerator() : new JavaScriptGenerator();

		string code = generator.Generate(Dividing("short", "int", "int"));

		Assert.IsFalse(code.Contains("return ((s * f) / 2)", StringComparison.Ordinal), code);
	}

	/// <summary>
	/// Tests that a C inferred local holding an integer times a floating value is declared floating.
	/// </summary>
	/// <param name="integerType">The integer operand's type.</param>
	/// <param name="floatingType">The floating operand's type.</param>
	[TestMethod]
	[DataRow("short", "double")]
	[DataRow("uint", "float")]
	public void C_InferredLocalOfAFloatingProduct_IsFloating(string integerType, string floatingType)
	{
		FunctionDeclaration function = new("scaled") { ReturnType = new TypeReference(floatingType) };
		function.Parameters.Add(new Parameter("s", integerType));
		function.Parameters.Add(new Parameter("f", floatingType));
		function.Body.Add(new VariableDeclaration("t", null, Product()) { IsTypeInferred = true });
		function.Body.Add(new ReturnStatement(new VariableReference("t")));

		string code = new CGenerator().Generate(function);

		StringAssert.Contains(code, $"{floatingType} t = ", StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a conditional choosing between an integer and a floating value is floating whichever
	/// branch holds the integer.
	/// </summary>
	[TestMethod]
	public void C_InferredLocalOfAMixedConditional_IsFloating()
	{
		FunctionDeclaration function = new("pick") { ReturnType = new TypeReference("double") };
		function.Parameters.Add(new Parameter("ready", "bool"));
		function.Parameters.Add(new Parameter("s", "short"));
		function.Body.Add(new VariableDeclaration(
			"t",
			null,
			new ConditionalExpression(new VariableReference("ready"), new VariableReference("s"), Literal.DecimalValue(2.5)))
		{ IsTypeInferred = true });
		function.Body.Add(new ReturnStatement(new VariableReference("t")));

		string code = new CGenerator().Generate(function);

		StringAssert.Contains(code, "double t = ", StringComparison.Ordinal, code);
	}

	private static BinaryExpression Product() =>
		new(new VariableReference("s"), BinaryOperator.Multiply, new VariableReference("f"));

	/// <summary>
	/// Builds <c>return (s * f) / 2;</c> over parameters of the given types.
	/// </summary>
	private static FunctionDeclaration Dividing(string integerType, string otherType, string returnType)
	{
		FunctionDeclaration function = new("half") { ReturnType = new TypeReference(returnType) };
		function.Parameters.Add(new Parameter("s", integerType));
		function.Parameters.Add(new Parameter("f", otherType));
		function.Body.Add(new ReturnStatement(new BinaryExpression(Product(), BinaryOperator.Divide, Literal.Number(2))));
		return function;
	}
}
