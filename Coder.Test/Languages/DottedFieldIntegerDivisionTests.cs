// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a field reached through a receiver, <c>self.total</c>, <c>this.total</c> or
/// <c>p.total</c>, is known to be of the type its declaration says (#433), so Python and JavaScript
/// divide it the way the C family does.
/// </summary>
/// <remarks>
/// With <c>total</c> an <c>int</c> of <c>-7</c>, <c>total / 2</c> is <c>-3</c> and <c>total % 2</c> is
/// <c>-1</c> in C, C++, C#, Go and Rust. Without the field's type Python's and JavaScript's <c>/</c>
/// are true division and give <c>-3.5</c>, and Python's <c>%</c> floors to <c>1</c>, all of which still
/// run, so the generated code is run rather than read.
/// </remarks>
[TestClass]
public class DottedFieldIntegerDivisionTests
{
	/// <summary>
	/// A class <c>Acc</c> with an <c>int total = -7</c> field, methods that divide it through the
	/// receiver word, and free functions that divide it through a parameter of type <c>Acc</c>.
	/// </summary>
	/// <param name="receiver">The word the target reaches its own instance by.</param>
	private static SourceFile File(string receiver)
	{
		SourceFile file = new("acc");
		ClassDeclaration acc = new("Acc");
		acc.Members.Add(new FieldDeclaration("total", new TypeReference("int")) { InitialValue = Literal.Number(-7) });

		foreach ((string name, BinaryOperator op, AssignmentOperator assign) in new[]
		{
			("Half", BinaryOperator.Divide, AssignmentOperator.DivideAssign),
			("Rest", BinaryOperator.Modulo, AssignmentOperator.ModuloAssign),
		})
		{
			string field = $"{receiver}.total";

			FunctionDeclaration method = new($"self{name}") { ReturnType = new TypeReference("int") };
			method.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference(field), op, Literal.Number(2))));
			acc.Members.Add(method);

			FunctionDeclaration inPlace = new($"self{name}InPlace") { ReturnType = new TypeReference("int") };
			inPlace.Body.Add(new AssignmentStatement(new VariableReference(field), Literal.Number(2), assign));
			inPlace.Body.Add(new ReturnStatement(new VariableReference(field)));
			acc.Members.Add(inPlace);

			FunctionDeclaration through = new($"param{name}") { ReturnType = new TypeReference("int") };
			through.Parameters.Add(new Parameter("p", "Acc"));
			through.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("p.total"), op, Literal.Number(2))));
			file.Members.Add(through);

			FunctionDeclaration throughInPlace = new($"param{name}InPlace") { ReturnType = new TypeReference("int") };
			throughInPlace.Parameters.Add(new Parameter("p", "Acc"));
			throughInPlace.Body.Add(new AssignmentStatement(new VariableReference("p.total"), Literal.Number(2), assign));
			throughInPlace.Body.Add(new ReturnStatement(new VariableReference("p.total")));
			file.Members.Add(throughInPlace);
		}

		file.Members.Insert(0, acc);
		return file;
	}

	private static readonly (string Call, int Expected)[] Expectations =
	[
		("selfHalf", -3),
		("selfRest", -1),
		("selfHalfInPlace", -3),
		("selfRestInPlace", -1),
		("paramHalf", -3),
		("paramRest", -1),
		("paramHalfInPlace", -3),
		("paramRestInPlace", -1),
	];

	/// <summary>
	/// Tests that Python truncates a division and a remainder of an <c>int</c> field read through
	/// <c>self</c> or a typed parameter, and through <c>/=</c> and <c>%=</c>.
	/// </summary>
	[TestMethod]
	public void Python_FieldThroughAReceiver_MatchesTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new PythonGenerator().Generate(File("self")));
		module.AppendLine();
		foreach ((string call, int expected) in Expectations)
		{
			string expression = call.StartsWith("self", StringComparison.Ordinal) ? $"Acc().{call}()" : $"{call}(Acc())";
			module.AppendLine($"r = {expression}");
			module.AppendLine($"assert type(r) is int and r == {expected}, f\"{call} = {{r!r}}\"");
		}

		Run(python, "check.py", module.ToString());
	}

	/// <summary>
	/// Tests that JavaScript truncates a division and a remainder of an <c>int</c> field read through
	/// <c>this</c> or a typed parameter, and through <c>/=</c> and <c>%=</c>.
	/// </summary>
	[TestMethod]
	public void JavaScript_FieldThroughAReceiver_MatchesTheCFamily()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new JavaScriptGenerator().Generate(File("this")));
		module.AppendLine();
		foreach ((string call, int expected) in Expectations)
		{
			string expression = call.StartsWith("self", StringComparison.Ordinal) ? $"new Acc().{call}()" : $"{call}(new Acc())";
			module.AppendLine($"{{ const r = {expression}; if (r !== {expected}) {{ throw new Error(`{call} = ${{r}}`); }} }}");
		}

		Run(node, "check.js", module.ToString());
	}

	/// <summary>
	/// Tests that a field reached through a receiver whose class declares no such field, or through a
	/// name no scope declares, keeps the unknown type it had before.
	/// </summary>
	[TestMethod]
	public void UnknownReceiverOrField_KeepsTheTypeItHadBefore()
	{
		SourceFile file = new("acc");
		ClassDeclaration acc = new("Acc");
		acc.Members.Add(new FieldDeclaration("total", new TypeReference("int")));
		file.Members.Add(acc);

		FunctionDeclaration missingField = new("missingField") { ReturnType = new TypeReference("double") };
		missingField.Parameters.Add(new Parameter("p", "Acc"));
		missingField.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("p.scale"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(missingField);

		FunctionDeclaration undeclared = new("undeclared") { ReturnType = new TypeReference("double") };
		undeclared.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("q.total"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(undeclared);

		string python = new PythonGenerator().Generate(file);
		Assert.Contains("(p.scale / 2)", python, StringComparison.Ordinal, python);
		Assert.Contains("(q.total / 2)", python, StringComparison.Ordinal, python);
	}

	private static void Run(string runtime, string fileName, string source) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, fileName), source);
			(int exitCode, string output) = ToolchainHarness.Run(runtime, fileName, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
}
