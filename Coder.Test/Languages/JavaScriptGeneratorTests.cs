// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="JavaScriptGenerator"/>.
/// </summary>
[TestClass]
public class JavaScriptGeneratorTests
{
	private JavaScriptGenerator Generator { get; } = new();

	/// <summary>
	/// Tests that the generator reports the identity the registry and file writer rely on.
	/// </summary>
	[TestMethod]
	public void Identity_IsJavaScript()
	{
		Assert.AreEqual("javascript", Generator.LanguageId);
		Assert.AreEqual("JavaScript", Generator.DisplayName);
		Assert.AreEqual("js", Generator.FileExtension);
	}

	/// <summary>
	/// Tests that a function with no body still produces a well-formed declaration.
	/// </summary>
	[TestMethod]
	public void EmptyFunction_ProducesBraces()
	{
		FunctionDeclaration function = new("doNothing");

		string code = Generator.Generate(function);

		Assert.AreEqual($"function doNothing() {{{Environment.NewLine}}}{Environment.NewLine}", code);
	}

	/// <summary>
	/// Tests that parameter types are dropped, since JavaScript has nowhere to put them, and that
	/// an optional parameter becomes a default argument.
	/// </summary>
	[TestMethod]
	public void Parameters_DropTypesAndKeepDefaults()
	{
		FunctionDeclaration function = new("greet");
		function.Parameters.Add(new Parameter("name", "str"));
		function.Parameters.Add(new Parameter("loud", "bool") { IsOptional = true, DefaultValue = "false" });

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "function greet(name, loud = false) {", StringComparison.Ordinal);
		Assert.IsFalse(code.Contains("str", StringComparison.Ordinal), "JavaScript output should carry no type annotations");
	}

	/// <summary>
	/// Tests that a return statement recurses into its expression rather than stringifying the node,
	/// and terminates with a semicolon.
	/// </summary>
	[TestMethod]
	public void ReturnStatement_GeneratesExpressionAndSemicolon()
	{
		FunctionDeclaration function = new("add");
		function.Parameters.Add(new Parameter("a"));
		function.Parameters.Add(new Parameter("b"));
		function.Body.Add(new ReturnStatement(
			new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));

		string code = Generator.Generate(function);

		StringAssert.Contains(code, "    return (a + b);", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that equality maps to the strict operators rather than the coercing ones.
	/// </summary>
	[TestMethod]
	public void Equality_UsesStrictOperators()
	{
		BinaryExpression equal = new(new VariableReference("a"), BinaryOperator.Equal, new VariableReference("b"));
		BinaryExpression notEqual = new(new VariableReference("a"), BinaryOperator.NotEqual, new VariableReference("b"));

		Assert.AreEqual("(a === b)", Generator.Generate(equal));
		Assert.AreEqual("(a !== b)", Generator.Generate(notEqual));
	}

	/// <summary>
	/// Tests that an initialized constant becomes `const` while everything else becomes `let`.
	/// </summary>
	[TestMethod]
	public void VariableDeclaration_ChoosesConstOrLet()
	{
		VariableDeclaration constant = new("limit", "int", Literal.Number(10)) { IsConstant = true };
		VariableDeclaration mutable = new("count", "int", Literal.Number(0));
		VariableDeclaration uninitialized = new("later") { IsConstant = true };

		Assert.AreEqual($"const limit = 10;{Environment.NewLine}", Generator.Generate(constant));
		Assert.AreEqual($"let count = 0;{Environment.NewLine}", Generator.Generate(mutable));

		// `const` without an initializer is a syntax error, so an uninitialized constant has to be `let`.
		Assert.AreEqual($"let later;{Environment.NewLine}", Generator.Generate(uninitialized));
	}

	/// <summary>
	/// Tests that string literals are escaped rather than emitted raw.
	/// </summary>
	[TestMethod]
	public void StringLiteral_IsEscaped()
	{
		string code = Generator.Generate(Literal.Text("say \"hi\"\n"));

		Assert.AreEqual("\"say \\\"hi\\\"\\n\"", code);
	}

	/// <summary>
	/// Tests that an assignment uses the mapped compound operator and terminates the statement.
	/// </summary>
	[TestMethod]
	public void Assignment_UsesCompoundOperator()
	{
		AssignmentStatement assignment = new(
			new VariableReference("total"), Literal.Number(5), AssignmentOperator.AddAssign);

		Assert.AreEqual($"total += 5;{Environment.NewLine}", Generator.Generate(assignment));
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
