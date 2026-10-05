// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.CodeBlocker;
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

		Assert.AreEqual($"function doNothing() {{{CodeBlocker.DefaultNewLineString}}}{CodeBlocker.DefaultNewLineString}", code);
	}

	[TestMethod]
	public void CompleteImportItem_IsPassedThroughAndParses()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No Node.js runtime on the path, so nothing was parsed.");
			return;
		}

		SourceFile file = new("imports");
		file.Imports.Add("import { P } from \"./p.js\";");
		string generated = Generator.Generate(file);
		StringAssert.Contains(generated, "import { P } from \"./p.js\";", StringComparison.Ordinal);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string path = Path.Combine(directory, "imports.mjs");
			File.WriteAllText(path, generated);
			(int exitCode, string output) = ToolchainHarness.Run(node, $"--check {path}", directory);
			Assert.AreEqual(0, exitCode, $"Node.js rejected the generated source:{Environment.NewLine}{output}");
		});
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

		Assert.AreEqual($"const limit = 10;{CodeBlocker.DefaultNewLineString}", Generator.Generate(constant));
		Assert.AreEqual($"let count = 0;{CodeBlocker.DefaultNewLineString}", Generator.Generate(mutable));

		// `const` without an initializer is a syntax error, so an uninitialized constant has to be `let`.
		Assert.AreEqual($"let later;{CodeBlocker.DefaultNewLineString}", Generator.Generate(uninitialized));
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

		Assert.AreEqual($"total += 5;{CodeBlocker.DefaultNewLineString}", Generator.Generate(assignment));
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
	/// <summary>
	/// Builds the file from issue #104: a file-scope constant, and a class with a static field and a
	/// static constant.
	/// </summary>
	/// <returns>The source file.</returns>
	private static SourceFile StaticFieldFile()
	{
		SourceFile file = new("Sample");
		file.Members.Add(new FieldDeclaration("Limit", "int") { IsConstant = true, InitialValue = new LiteralExpression<int>(10) });
		ClassDeclaration counter = new("Counter");
		counter.Members.Add(new FieldDeclaration("created", "int") { IsStatic = true, InitialValue = new LiteralExpression<int>(0) });
		counter.Members.Add(new FieldDeclaration("MAX", "int") { IsStatic = true, IsConstant = true, InitialValue = new LiteralExpression<int>(99) });
		counter.Members.Add(new FieldDeclaration("count", "int") { InitialValue = new LiteralExpression<int>(1) });
		file.Members.Add(counter);
		return file;
	}

	/// <summary>
	/// Tests that static and constant class fields are written <c>static</c>, and an instance field is not.
	/// </summary>
	[TestMethod]
	public void ClassField_StaticOrConstant_IsWrittenStatic()
	{
		ClassDeclaration counter = new("Counter");
		counter.Members.Add(new FieldDeclaration("created", "int") { IsStatic = true, InitialValue = new LiteralExpression<int>(0) });
		counter.Members.Add(new FieldDeclaration("LIMIT", "int") { IsConstant = true, InitialValue = new LiteralExpression<int>(5) });
		counter.Members.Add(new FieldDeclaration("count", "int") { InitialValue = new LiteralExpression<int>(1) });

		string code = Generator.Generate(counter);

		StringAssert.Contains(code, "static created = 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "static LIMIT = 5;", StringComparison.Ordinal);
		StringAssert.Contains(code, "count = 1;", StringComparison.Ordinal);
		Assert.DoesNotContain("static count", code);
	}

	/// <summary>
	/// Tests that a field outside a class is declared, <c>const</c> when constant and <c>let</c> otherwise,
	/// rather than assigned to an undeclared name.
	/// </summary>
	[TestMethod]
	public void FileScopeField_IsDeclaredConstOrLet()
	{
		SourceFile file = new("Sample");
		file.Members.Add(new FieldDeclaration("Limit", "int") { IsConstant = true, InitialValue = new LiteralExpression<int>(10) });
		file.Members.Add(new FieldDeclaration("total", "int") { InitialValue = new LiteralExpression<int>(0) });
		file.Members.Add(new FieldDeclaration("Pending", "int") { IsConstant = true });

		string code = Generator.Generate(file);

		StringAssert.Contains(code, "const Limit = 10;", StringComparison.Ordinal);
		StringAssert.Contains(code, "let total = 0;", StringComparison.Ordinal);
		StringAssert.Contains(code, "let Pending;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the issue's file loads as an ES module under Node.js, the static constant is on the
	/// class, and the file-scope constant does not leak onto the global object.
	/// </summary>
	[TestMethod]
	public void StaticAndFileScopeFields_RunUnderNode()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No Node.js runtime on the path, so nothing was run.");
			return;
		}

		string generated = Generator.Generate(StaticFieldFile());

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string path = Path.Combine(directory, "sample.mjs");
			File.WriteAllText(path, generated + "console.log(Counter.MAX === 99, Counter.created, new Counter().MAX, Limit, globalThis.Limit);\n");
			(int exitCode, string output) = ToolchainHarness.Run(node, path, directory);
			Assert.AreEqual(0, exitCode, $"Node.js could not load the generated module:{Environment.NewLine}{output}{Environment.NewLine}{generated}");
			StringAssert.Contains(output, "true 0 undefined 10 undefined", StringComparison.Ordinal);
		});
	}
}
