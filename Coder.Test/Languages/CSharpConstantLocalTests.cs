// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a constant local is written as a C# <c>const</c> only when C# can hold it as one.
/// </summary>
/// <remarks>
/// A C# <c>const</c> local must be a compile-time constant, so <c>const int doubled = (n * 2);</c> is
/// error CS0133. C# has no readonly local, so a constant computed at run time is an ordinary local,
/// the same as an inferred one already was.
/// </remarks>
[TestClass]
public class CSharpConstantLocalTests
{
	/// <summary>
	/// Tests that a constant computed from a parameter or a call is not written as <c>const</c>.
	/// </summary>
	[TestMethod]
	public void NonLiteralConstant_IsAPlainLocal()
	{
		FunctionDeclaration twice = new("Twice") { ReturnType = "int", IsStatic = true };
		twice.Parameters.Add(new Parameter("n", "int"));
		BinaryExpression product = new(new VariableReference("n"), BinaryOperator.Multiply, Literal.Number(2));
		twice.Body.Add(new VariableDeclaration("doubled", "int", product) { IsConstant = true });
		CallExpression call = new("Math.Abs");
		call.Arguments.Add(new VariableReference("doubled"));
		twice.Body.Add(new VariableDeclaration("size", "int", call) { IsConstant = true });
		twice.Body.Add(new ReturnStatement(new VariableReference("size")));

		string code = new CSharpGenerator().Generate(twice);

		Assert.Contains("int doubled = (n * 2);", code, StringComparison.Ordinal, code);
		Assert.Contains("int size = Math.Abs(doubled);", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("const", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a constant holding a literal is still written as <c>const</c>.
	/// </summary>
	[TestMethod]
	public void LiteralConstant_IsConst()
	{
		FunctionDeclaration limit = new("Limit") { ReturnType = "int", IsStatic = true };
		limit.Body.Add(new VariableDeclaration("max", "int", Literal.Number(7)) { IsConstant = true });
		limit.Body.Add(new ReturnStatement(new VariableReference("max")));

		string code = new CSharpGenerator().Generate(limit);

		Assert.Contains("const int max = 7;", code, StringComparison.Ordinal, code);
	}
}
