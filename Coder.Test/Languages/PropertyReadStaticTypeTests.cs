// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using System.Text;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a property read, <c>self.total</c>, <c>this.total</c> or <c>p.total</c>,
/// is known to be of the type its declaration says, the way a field read is (#433), so
/// integer arithmetic on it is lowered the same way (#466).
/// </summary>
/// <remarks>
/// With <c>total</c> an <c>int</c> of <c>-7</c>, <c>total / 2</c> is <c>-3</c> and <c>total % 2</c> is
/// <c>-1</c> in the C family. Without the property's type Python's and JavaScript's <c>/</c> are true
/// division and give <c>-3.5</c>, and Python's <c>%</c> floors to <c>1</c>, all of which still run, so
/// the generated code is run rather than read. Rust adds an <c>i32</c> to an <c>i64</c> only once the
/// <c>i32</c> is widened, which it is only when its type is known.
/// </remarks>
[TestClass]
public class PropertyReadStaticTypeTests
{
	/// <summary>
	/// A class <c>Acc</c> with an automatic <c>int total</c> property, methods that divide it through
	/// the receiver word, and free functions that divide it through a parameter of type <c>Acc</c>.
	/// </summary>
	/// <param name="receiver">The word the target reaches its own instance by.</param>
	private static SourceFile File(string receiver)
	{
		SourceFile file = new("acc");
		ClassDeclaration acc = new("Acc");
		acc.Members.Add(new PropertyDeclaration("total", new TypeReference("int")) { HasSetter = true });

		foreach ((string name, BinaryOperator op) in new[]
		{
			("Half", BinaryOperator.Divide),
			("Rest", BinaryOperator.Modulo),
		})
		{
			FunctionDeclaration method = new($"self{name}") { ReturnType = new TypeReference("int") };
			method.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference($"{receiver}.total"), op, Literal.Number(2))));
			acc.Members.Add(method);

			FunctionDeclaration through = new($"param{name}") { ReturnType = new TypeReference("int") };
			through.Parameters.Add(new Parameter("p", "Acc"));
			through.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("p.total"), op, Literal.Number(2))));
			file.Members.Add(through);
		}

		file.Members.Insert(0, acc);
		return file;
	}

	private static readonly (string Call, int Expected)[] Expectations =
	[
		("selfHalf", -3),
		("selfRest", -1),
		("paramHalf", -3),
		("paramRest", -1),
	];

	/// <summary>
	/// Tests that Python truncates a division and a remainder of an automatic <c>int</c> property read
	/// through <c>self</c> or a typed parameter.
	/// </summary>
	[TestMethod]
	public void Python_PropertyThroughAReceiver_MatchesTheCFamily()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new PythonGenerator().Generate(File("self")));
		module.AppendLine();
		module.AppendLine("a = Acc()");
		module.AppendLine("a.total = -7");
		foreach ((string call, int expected) in Expectations)
		{
			string expression = call.StartsWith("self", StringComparison.Ordinal) ? $"a.{call}()" : $"{call}(a)";
			module.AppendLine($"r = {expression}");
			module.AppendLine($"assert type(r) is int and r == {expected}, f\"{call} = {{r!r}}\"");
		}

		Run(python, "check.py", module.ToString());
	}

	/// <summary>
	/// Tests that JavaScript truncates a division and a remainder of an automatic <c>int</c> property
	/// read through <c>this</c> or a typed parameter.
	/// </summary>
	[TestMethod]
	public void JavaScript_PropertyThroughAReceiver_MatchesTheCFamily()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		StringBuilder module = new(new JavaScriptGenerator().Generate(File("this")));
		module.AppendLine();
		module.AppendLine("const a = new Acc();");
		module.AppendLine("a.total = -7;");
		foreach ((string call, int expected) in Expectations)
		{
			string expression = call.StartsWith("self", StringComparison.Ordinal) ? $"a.{call}()" : $"{call}(a)";
			module.AppendLine($"{{ const r = {expression}; if (r !== {expected}) {{ throw new Error(`{call} = ${{r}}`); }} }}");
		}

		Run(node, "check.js", module.ToString());
	}

	/// <summary>
	/// Tests that Rust widens an automatic <c>int</c> property read through <c>self</c> before adding a
	/// <c>long</c> to it, so the method compiles.
	/// </summary>
	[TestMethod]
	public void Rust_PropertyPlusLong_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "rustc") is null)
		{
			Assert.Inconclusive("No Rust compiler on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = new("acc");
		ClassDeclaration acc = new("Acc");
		acc.Members.Add(new PropertyDeclaration("total", new TypeReference("int")) { HasSetter = true });
		FunctionDeclaration wide = new("wide") { ReturnType = new TypeReference("long") };
		wide.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("self.total"), BinaryOperator.Add, new LiteralExpression<long>(1))));
		acc.Members.Add(wide);
		file.Members.Add(acc);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string code = $"#![allow(unused_variables, unused_mut, dead_code)]{Environment.NewLine}{new RustGenerator().Generate(file)}";
			System.IO.File.WriteAllText(Path.Combine(directory, "acc.rs"), code);
			(int exitCode, string output) = ToolchainHarness.Run("rustc", "--crate-type lib --edition 2021 -o acc.rlib acc.rs", directory);
			Assert.AreEqual(0, exitCode, $"rustc rejected the generated source:{Environment.NewLine}{code}{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a class with a field and a property of the same name leaves the read untyped, the
	/// rule a name two fields share already keeps, rather than guessing which one is meant.
	/// </summary>
	[TestMethod]
	public void FieldAndPropertyOfTheSameName_LeavesTheReadUntyped()
	{
		SourceFile file = new("acc");
		ClassDeclaration acc = new("Acc");
		acc.Members.Add(new FieldDeclaration("total", new TypeReference("int")));
		acc.Members.Add(new PropertyDeclaration("total", new TypeReference("double")) { HasSetter = true });
		file.Members.Add(acc);

		FunctionDeclaration through = new("paramHalf") { ReturnType = new TypeReference("double") };
		through.Parameters.Add(new Parameter("p", "Acc"));
		through.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("p.total"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(through);

		string python = new PythonGenerator().Generate(file);
		Assert.Contains("(p.total / 2)", python, StringComparison.Ordinal, python);
	}

	private static void Run(string runtime, string fileName, string source) =>
		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			System.IO.File.WriteAllText(Path.Combine(directory, fileName), source);
			(int exitCode, string output) = ToolchainHarness.Run(runtime, fileName, directory);
			Assert.AreEqual(0, exitCode, $"{output}{Environment.NewLine}{source}");
		});
}
