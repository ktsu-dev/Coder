// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="CppGenerator"/>.
/// </summary>
[TestClass]
public class CppGeneratorTests
{
	private CppGenerator Generator { get; } = new();

	/// <summary>
	/// Tests that the generator reports the identity the registry and file writer rely on.
	/// </summary>
	[TestMethod]
	public void Identity_IsCpp()
	{
		Assert.AreEqual("cpp", Generator.LanguageId);
		Assert.AreEqual("C++", Generator.DisplayName);
		Assert.AreEqual("cpp", Generator.FileExtension);
	}

	/// <summary>
	/// Tests that a function with no return type declared becomes void, with the brace on its own line.
	/// </summary>
	[TestMethod]
	public void EmptyFunction_DefaultsToVoid()
	{
		FunctionDeclaration function = new("doNothing");

		string code = Generator.Generate(function);

		string nl = Environment.NewLine;
		Assert.AreEqual($"void doNothing(){nl}{{{nl}}}{nl}", code);
	}

	/// <summary>
	/// Tests that the AST's language-neutral type names are mapped to C++ spellings, in both the
	/// return position and the parameter list.
	/// </summary>
	[TestMethod]
	public void Types_AreMappedToCppSpellings()
	{
		FunctionDeclaration function = new("greet") { ReturnType = "str" };
		function.Parameters.Add(new Parameter("name", "str"));
		function.Parameters.Add(new Parameter("times", "int"));

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "std::string greet(std::string name, int times)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an unrecognized type name is passed through, so a caller can name a real C++ type.
	/// </summary>
	[TestMethod]
	public void UnknownType_IsPassedThrough()
	{
		FunctionDeclaration function = new("make") { ReturnType = "MyWidget" };

		StringAssert.Contains(Generator.Generate(function), "MyWidget make()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a return statement recurses into its expression and terminates with a semicolon,
	/// indented one level inside the function body.
	/// </summary>
	[TestMethod]
	public void ReturnStatement_GeneratesExpressionAndSemicolon()
	{
		FunctionDeclaration function = new("add") { ReturnType = "int" };
		function.Parameters.Add(new Parameter("a", "int"));
		function.Parameters.Add(new Parameter("b", "int"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "    return (a + b);", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests the three shapes a variable declaration can take: a mapped type, an inferred type with
	/// an initializer, and an inferred type without one.
	/// </summary>
	[TestMethod]
	public void VariableDeclaration_HandlesTypedAndInferred()
	{
		VariableDeclaration typed = new("name", "str", Literal.Text("ktsu"));
		VariableDeclaration inferred = new("count", initialValue: Literal.Number(1)) { IsTypeInferred = true };
		VariableDeclaration inferredEmpty = new("later") { IsTypeInferred = true };

		string nl = Environment.NewLine;
		Assert.AreEqual($"std::string name = \"ktsu\";{nl}", Generator.Generate(typed));
		Assert.AreEqual($"auto count = 1;{nl}", Generator.Generate(inferred));

		// `auto` without an initializer does not compile, so the fallback has to name a type.
		Assert.AreEqual($"std::any later;{nl}", Generator.Generate(inferredEmpty));
	}

	/// <summary>
	/// Tests that a constant declaration carries the const qualifier.
	/// </summary>
	[TestMethod]
	public void ConstantDeclaration_IsQualified()
	{
		VariableDeclaration constant = new("limit", "int", Literal.Number(10)) { IsConstant = true };

		Assert.AreEqual($"const int limit = 10;{Environment.NewLine}", Generator.Generate(constant));
	}

	/// <summary>
	/// Tests that logical operators use the C++ symbolic spellings rather than Python's words.
	/// </summary>
	[TestMethod]
	public void LogicalOperators_UseSymbols()
	{
		BinaryExpression and = new(new VariableReference("a"), BinaryOperator.LogicalAnd, new VariableReference("b"));

		Assert.AreEqual("(a && b)", Generator.Generate(and));
	}

	/// <summary>
	/// Tests that booleans use the C++ lowercase keywords, not Python's capitalized ones.
	/// </summary>
	[TestMethod]
	public void BooleanLiteral_IsLowercase()
	{
		Assert.AreEqual("true", Generator.Generate(Literal.Bool(true)));
		Assert.AreEqual("false", Generator.Generate(Literal.Bool(false)));
	}

	/// <summary>
	/// Tests that a node the generator does not handle is refused rather than silently mis-generated.
	/// </summary>
	[TestMethod]
	public void CanGenerate_RejectsUnknownNode()
	{
		Assert.IsFalse(Generator.CanGenerate(new AstLeafNode<double>(1.0)));
		Assert.ThrowsExactly<NotSupportedException>(() => Generator.Generate(new AstLeafNode<double>(1.0)));
	}
}
