// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that undoing a conversion puts back every child the original held, including the ones that
/// stayed on it (ktsu-dev/Coder#438).
/// </summary>
/// <remarks>
/// Converting <c>o.m(a, b)</c> to a binary operator moves the receiver and the first argument into
/// the operands and leaves the call loose, still holding <c>b</c>. The undo used to run the
/// replacement in reverse, which adopted the operands into the call's arguments from position 0 and
/// wrote over <c>b</c>, so the document came back as <c>o.m(a)</c> with <c>b</c> left loose.
/// </remarks>
[TestClass]
public class ConvertUndoTests
{
	/// <summary>
	/// Gets every binary operator the palette offers, as MSTest data rows.
	/// </summary>
	/// <returns>One row per template label.</returns>
	public static IEnumerable<object[]> BinaryTemplates() =>
		AstNodeCatalog.Templates
			.Where(template => template.Create() is BinaryExpression)
			.Select(template => new object[] { template.Label });

	/// <summary>
	/// Tests that undoing a call-to-operator conversion gives back the original document with
	/// nothing loose.
	/// </summary>
	/// <param name="label">The palette label of the binary operator converted to.</param>
	[TestMethod]
	[DynamicData(nameof(BinaryTemplates))]
	public void Undo_RestoresEveryArgumentOfTheCall(string label)
	{
		(FunctionDeclaration function, CallExpression call) = MethodCall();
		AstGraphEditor editor = new(function);
		string before = Yaml(function);

		Assert.IsTrue(editor.Convert(call, Template(label).Create()));
		editor.Undo();

		Assert.AreEqual(before, Yaml(function));
		Assert.IsEmpty(editor.Graph.Detached);
	}

	/// <summary>
	/// Tests that undo, redo and undo again still returns the original document.
	/// </summary>
	/// <param name="label">The palette label of the binary operator converted to.</param>
	[TestMethod]
	[DynamicData(nameof(BinaryTemplates))]
	public void UndoRedoUndo_RestoresTheOriginal(string label)
	{
		(FunctionDeclaration function, CallExpression call) = MethodCall();
		AstGraphEditor editor = new(function);
		string before = Yaml(function);

		Assert.IsTrue(editor.Convert(call, Template(label).Create()));
		string converted = Yaml(function);
		editor.Undo();
		editor.Redo();

		Assert.AreEqual(converted, Yaml(function));

		editor.Undo();

		Assert.AreEqual(before, Yaml(function));
		Assert.IsEmpty(editor.Graph.Detached);
	}

	private static AstNodeTemplate Template(string label) =>
		AstNodeCatalog.Templates.First(template => template.Label == label && template.Create() is BinaryExpression);

	private static (FunctionDeclaration Function, CallExpression Call) MethodCall()
	{
		FunctionDeclaration function = new("f");
		function.Parameters.Add(new Parameter("o", "Counter"));
		function.Parameters.Add(new Parameter("a", "int"));
		function.Parameters.Add(new Parameter("b", "int"));

		CallExpression call = new(new VariableReference("o"), "m");
		call.Arguments.Add(new VariableReference("a"));
		call.Arguments.Add(new VariableReference("b"));
		function.Body.Add(new ExpressionStatement(call));

		return (function, call);
	}

	private static string Yaml(AstNode node) => new YamlSerializer().Serialize(node);
}
