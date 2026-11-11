// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Runs what the JavaScript generator writes for names that are JavaScript keywords. A binding — a
/// parameter, local, function, class or top-level name — has no escape, so it has to come out with an
/// underscore wherever it is declared and used. A member keeps its name, because JavaScript reads a
/// keyword after a <c>.</c> and as a field, accessor or method name.
/// </summary>
/// <remarks>
/// The run is inconclusive rather than failing where no node is on the path, which is the honest
/// result: nothing was checked.
/// </remarks>
[TestClass]
public class JavaScriptReservedWordIdentifierTests
{
	/// <summary>
	/// The JavaScript driver. A member keeps its keyword name, which JavaScript reads after a
	/// <c>.</c>, and a top-level name, which no keyword can be, has had an underscore appended.
	/// </summary>
	private const string JavaScriptDriver = """


		const holder = new Holder();
		if (holder.class !== 3 || holder.default !== 4) { process.exit(1); }
		if (holder.delete !== 1) { process.exit(2); }
		if (holder.total(5, 6) !== 14) { process.exit(3); }
		if (holder.twice() !== 3) { process.exit(4); }
		if (Flag.null !== 0 || Flag.All !== 1) { process.exit(5); }
		if (return_(7) !== 7 || relay() !== 5) { process.exit(6); }
		if (var_ !== 7) { process.exit(7); }
		if (!(new Child() instanceof import_)) { process.exit(8); }

		""";

	/// <summary>
	/// Tests that JavaScript appends an underscore to a keyword-named parameter, local, function,
	/// class and top-level name, at their declarations and where they are used, and leaves a
	/// keyword-named field, accessor, method and enumeration member as declared, which is how
	/// JavaScript reads a keyword after a <c>.</c>.
	/// </summary>
	[TestMethod]
	public void JavaScript_KeywordNamesRunAndResolve()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No node on the path, so nothing was run.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new JavaScriptGenerator().Generate(JavaScriptTypes()) + JavaScriptDriver;
			File.WriteAllText(Path.Combine(directory, "holder.js"), source);

			(int ran, string output) = ToolchainHarness.Run(node, "holder.js", directory);
			Assert.AreEqual(0, ran, $"the generated script failed, or a keyword-named name read back wrongly (driver check {ran}):{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a JavaScript reference escapes the binding it leads with but not the member after a
	/// <c>.</c>, and keeps <c>this</c> and the literals as written.
	/// </summary>
	[TestMethod]
	public void JavaScript_ReferenceEscapesTheBindingButNotTheMember()
	{
		string source = new JavaScriptGenerator().Generate(JavaScriptTypes());

		Assert.Contains("this.class", source, StringComparison.Ordinal, source);
		Assert.Contains("this.switch()", source, StringComparison.Ordinal, source);
		Assert.Contains("get delete()", source, StringComparison.Ordinal, source);
		Assert.Contains("total(in_, typeof_)", source, StringComparison.Ordinal, source);
		Assert.Contains("return_(5)", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("this_", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("class_", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("switch_", source, StringComparison.Ordinal, source);
		Assert.DoesNotContain("null_", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Builds classes, an enumeration, functions and a top-level name whose names are JavaScript
	/// keywords: members, which keep their names, and bindings, which cannot.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile JavaScriptTypes()
	{
		ClassDeclaration holder = new("Holder");
		holder.Members.Add(new VariableDeclaration("class", "int", new LiteralExpression<int>(3)));
		holder.Members.Add(new VariableDeclaration("default", "int", new LiteralExpression<int>(4)));
		holder.Members.Add(new PropertyDeclaration("delete", "int")
		{
			GetterBody = { new ReturnStatement(new LiteralExpression<int>(1)) },
		});

		FunctionDeclaration keywordMethod = new("switch") { ReturnType = "int" };
		keywordMethod.Body.Add(new ReturnStatement(new LiteralExpression<int>(2)));
		holder.Members.Add(keywordMethod);

		FunctionDeclaration twice = new("twice") { ReturnType = "int" };
		twice.Body.Add(new ReturnStatement(new BinaryExpression(
			new CallExpression(new VariableReference("this"), "switch"),
			BinaryOperator.Add,
			new LiteralExpression<int>(1))));
		holder.Members.Add(twice);

		FunctionDeclaration total = new("total") { ReturnType = "int" };
		total.Parameters.Add(new Parameter("in", "int"));
		total.Parameters.Add(new Parameter("typeof", "int"));
		total.Body.Add(new VariableDeclaration(
			"let",
			"int",
			new BinaryExpression(new VariableReference("in"), BinaryOperator.Add, new VariableReference("typeof"))));
		total.Body.Add(new VariableDeclaration(
			"new",
			"int",
			new BinaryExpression(new VariableReference("let"), BinaryOperator.Add, new VariableReference("this.class"))));
		total.Body.Add(new ReturnStatement(new VariableReference("new")));
		holder.Members.Add(total);

		EnumDeclaration flag = new("Flag");
		flag.Members.Add(new EnumMember("null"));
		flag.Members.Add(new EnumMember("All"));

		FunctionDeclaration identity = new("return") { ReturnType = "int" };
		identity.Parameters.Add(new Parameter("with", "int"));
		identity.Body.Add(new ReturnStatement(new VariableReference("with")));

		CallExpression callIdentity = new("return");
		callIdentity.Arguments.Add(new LiteralExpression<int>(5));
		FunctionDeclaration relay = new("relay") { ReturnType = "int" };
		relay.Body.Add(new ReturnStatement(callIdentity));

		ClassDeclaration keywordBase = new("import");
		ClassDeclaration child = new("Child") { BaseType = new TypeReference("import") };

		SourceFile file = new("holder");
		file.Members.Add(holder);
		file.Members.Add(flag);
		file.Members.Add(identity);
		file.Members.Add(relay);
		file.Members.Add(new VariableDeclaration("var", "int", new LiteralExpression<int>(7)));
		file.Members.Add(keywordBase);
		file.Members.Add(child);
		return file;
	}
}
