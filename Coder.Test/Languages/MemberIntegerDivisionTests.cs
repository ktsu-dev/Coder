// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a name declared as a member of a file, namespace or type rather than in a function is
/// known to be of the type its declaration says (#304), so Python and JavaScript divide it the way the
/// C family does and C types a local copied from it.
/// </summary>
/// <remarks>
/// <c>LIMIT / 2</c> with <c>LIMIT</c> a file-scope <c>int</c> of <c>-7</c> is <c>-3</c> in C, C++, C#,
/// Go and Rust, and <c>LIMIT % 2</c> is <c>-1</c>. Without the member's type, Python's and JavaScript's
/// <c>/</c> are true division and give <c>-3.5</c>, and Python's <c>%</c> floors to <c>1</c>, both of
/// which still run, so the generated code is run rather than read.
/// </remarks>
[TestClass]
public class MemberIntegerDivisionTests
{
	/// <summary>
	/// A file holding a constant <c>int LIMIT = -7</c> and two functions dividing it by two.
	/// </summary>
	private static SourceFile File()
	{
		SourceFile file = new("limits");
		file.Members.Add(new VariableDeclaration("LIMIT", "int", Literal.Number(-7)) { IsConstant = true });

		foreach ((string name, BinaryOperator op) in new[] { ("half", BinaryOperator.Divide), ("rest", BinaryOperator.Modulo) })
		{
			FunctionDeclaration function = new(name) { ReturnType = new TypeReference("int") };
			function.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("LIMIT"), op, Literal.Number(2))));
			file.Members.Add(function);
		}

		return file;
	}

	/// <summary>
	/// Tests that Python truncates a division and a remainder of a file-scope <c>int</c> constant.
	/// </summary>
	[TestMethod]
	public void Python_FileScopeIntConstant_MatchesTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new PythonGenerator().Generate(File()));
		module.AppendLine();
		module.AppendLine("assert type(half()) is int and half() == -3, f\"half() = {half()!r}\"");
		module.AppendLine("assert type(rest()) is int and rest() == -1, f\"rest() = {rest()!r}\"");

		Run(python, "check.py", module.ToString());
	}

	/// <summary>
	/// Tests that JavaScript truncates a division and a remainder of a file-scope <c>int</c> constant.
	/// </summary>
	[TestMethod]
	public void JavaScript_FileScopeIntConstant_MatchesTheCFamily()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new JavaScriptGenerator().Generate(File()));
		module.AppendLine();
		module.AppendLine("if (half() !== -3) { throw new Error(`half() = ${half()}`); }");
		module.AppendLine("if (rest() !== -1) { throw new Error(`rest() = ${rest()}`); }");

		Run(node, "check.js", module.ToString());
	}

	/// <summary>
	/// Tests that a field declared at file scope is read for its type too, which is where a constant
	/// table that a function indexes lives.
	/// </summary>
	[TestMethod]
	public void FileScopeField_IsTypedByItsDeclaration()
	{
		SourceFile file = new("limits");
		file.Members.Add(new FieldDeclaration("most", new TypeReference("int")) { IsStatic = true, IsConstant = true, InitialValue = Literal.Number(9) });

		FunctionDeclaration half = new("half") { ReturnType = new TypeReference("int") };
		half.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("most"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(half);

		string javascript = new JavaScriptGenerator().Generate(file);
		Assert.Contains("Math.trunc", javascript, StringComparison.Ordinal, javascript);
	}

	/// <summary>
	/// Tests that C types a local inferred from a file-scope <c>int</c> constant as <c>int</c> rather than
	/// as a pointer of no type.
	/// </summary>
	[TestMethod]
	public void C_LocalInferredFromAFileScopeConstant_IsAnInt()
	{
		SourceFile file = new("limits");
		file.Members.Add(new VariableDeclaration("LIMIT", "int", Literal.Number(-7)) { IsConstant = true });

		FunctionDeclaration copy = new("copy") { ReturnType = new TypeReference("int") };
		copy.Body.Add(new VariableDeclaration("k", null, new VariableReference("LIMIT")));
		copy.Body.Add(new ReturnStatement(new VariableReference("k")));
		file.Members.Add(copy);

		string c = new CGenerator().Generate(file);
		Assert.Contains("int k = LIMIT;", c, StringComparison.Ordinal, c);
	}

	/// <summary>
	/// Tests that a name two members share, or one a parameter of the function shadows, is not given a
	/// member's type: the first is ambiguous and the second is the parameter's.
	/// </summary>
	[TestMethod]
	public void AmbiguousOrShadowedName_KeepsTheTypeItHadBefore()
	{
		SourceFile file = new("limits");
		file.Members.Add(new VariableDeclaration("LIMIT", "int", Literal.Number(-7)) { IsConstant = true });
		ClassDeclaration other = new("Other");
		other.Members.Add(new FieldDeclaration("LIMIT", new TypeReference("int")));
		other.Members.Add(new FieldDeclaration("scale", new TypeReference("int")));
		file.Members.Add(other);

		FunctionDeclaration ambiguous = new("ambiguous") { ReturnType = new TypeReference("double") };
		ambiguous.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("LIMIT"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(ambiguous);

		FunctionDeclaration shadowed = new("shadowed") { ReturnType = new TypeReference("double") };
		shadowed.Parameters.Add(new Parameter("scale", "double"));
		shadowed.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("scale"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(shadowed);

		string python = new PythonGenerator().Generate(file);
		Assert.Contains("(LIMIT / 2)", python, StringComparison.Ordinal, python);
		Assert.Contains("(scale / 2)", python, StringComparison.Ordinal, python);
	}

	private static void Run(string runtime, string fileName, string source) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, fileName), source);
			(int exitCode, string output) = ToolchainHarness.Run(runtime, fileName, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
}
