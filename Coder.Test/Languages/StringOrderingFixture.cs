// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;

/// <summary>
/// Functions ordering strings with <c>&lt;</c> and <c>&gt;=</c>: between two parameters, against a
/// literal on the right, and against one on the left (#372).
/// </summary>
/// <remarks>
/// The operators read the same in every target, but C# defines none of them on <c>string</c>, and
/// Rust defines them between a <c>String</c> and a <c>&amp;str</c> in neither order, so the text
/// alone does not show which of these compile.
/// </remarks>
internal static class StringOrderingFixture
{
	/// <summary>
	/// Builds the document.
	/// </summary>
	/// <returns>A file holding <c>before</c>, <c>atLeast</c>, <c>early</c>, <c>lateOrLater</c> and
	/// <c>literalFirst</c>.</returns>
	public static SourceFile Build()
	{
		SourceFile file = new("Generated");
		file.Members.Add(Compare("before", BinaryOperator.LessThan, new VariableReference("a"), new VariableReference("b"), "a", "b"));
		file.Members.Add(Compare("atLeast", BinaryOperator.GreaterThanOrEqual, new VariableReference("a"), new VariableReference("b"), "a", "b"));
		file.Members.Add(Compare("early", BinaryOperator.LessThan, new VariableReference("a"), Literal.Text("m"), "a"));
		file.Members.Add(Compare("lateOrLater", BinaryOperator.GreaterThanOrEqual, new VariableReference("a"), Literal.Text("m"), "a"));
		file.Members.Add(Compare("literalFirst", BinaryOperator.LessThan, Literal.Text("m"), new VariableReference("a"), "a"));
		return file;
	}

	private static FunctionDeclaration Compare(string name, BinaryOperator op, Expression left, Expression right, params string[] parameters)
	{
		FunctionDeclaration function = new(name) { ReturnType = "bool" };
		foreach (string parameter in parameters)
		{
			function.Parameters.Add(new Parameter(parameter, "string"));
		}

		function.Body.Add(new ReturnStatement(new BinaryExpression(left, op, right)));
		return function;
	}
}
