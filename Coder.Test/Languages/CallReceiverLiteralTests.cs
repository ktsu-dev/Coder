// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Linq;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a number literal used as a call's receiver keeps its grouping.
/// </summary>
/// <remarks>
/// <c>-2.5.abs()</c> is <c>-(2.5.abs())</c> in every target, because unary minus binds looser than
/// member access, and <c>5.toString()</c> does not parse in JavaScript or Python.
/// </remarks>
[TestClass]
public class CallReceiverLiteralTests
{
	/// <summary>
	/// Every generator that writes a receiver in front of the callee. C passes it as an argument instead.
	/// </summary>
	private static readonly ILanguageGenerator[] Generators =
	[
		new CSharpGenerator(),
		new CppGenerator(),
		new GoGenerator(),
		new RustGenerator(),
		new PythonGenerator(),
		new JavaScriptGenerator(),
	];

	/// <summary>
	/// Tests that a negative literal receiver is parenthesised, so the call applies to the negative value.
	/// </summary>
	[TestMethod]
	public void ANegativeLiteralReceiverIsParenthesised()
	{
		CallExpression call = new(Literal.DecimalValue(-2.5), "abs");

		foreach (ILanguageGenerator generator in Generators)
		{
			string code = generator.Generate(call).Trim();

			Assert.StartsWith("(-2.5", code, StringComparison.Ordinal, $"{generator.GetType().Name} wrote {code}");
			Assert.EndsWith(").abs()", code, StringComparison.Ordinal, $"{generator.GetType().Name} wrote {code}");
		}
	}

	/// <summary>
	/// Tests that an integer literal receiver is parenthesised, so the dot is not read as a decimal point.
	/// </summary>
	[TestMethod]
	public void AnIntegerLiteralReceiverIsParenthesised()
	{
		CallExpression call = new(Literal.Number(5), "toString");

		foreach (ILanguageGenerator generator in Generators.Where(g => g is JavaScriptGenerator or PythonGenerator))
		{
			Assert.AreEqual("(5).toString()", generator.Generate(call).Trim(), generator.GetType().Name);
		}
	}

	/// <summary>
	/// Tests that a receiver that is not a literal is still written bare.
	/// </summary>
	[TestMethod]
	public void ANameReceiverIsWrittenBare()
	{
		CallExpression call = new(new VariableReference("value"), "abs");

		foreach (ILanguageGenerator generator in Generators)
		{
			Assert.AreEqual("value.abs()", generator.Generate(call).Trim(), generator.GetType().Name);
		}
	}
}
