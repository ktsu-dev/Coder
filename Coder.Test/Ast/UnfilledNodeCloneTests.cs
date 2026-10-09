// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using System.Reflection;
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a node nobody has filled in yet can still be cloned.
/// </summary>
/// <remarks>
/// A node dropped in the editor, or read from YAML with a key missing, holds whatever its
/// parameterless constructor gave it. The generators clone whole types before they write them, so a
/// clone that validates what it copies turns a half-built AST into an exception out of generation.
/// </remarks>
[TestClass]
public class UnfilledNodeCloneTests
{
	/// <summary>
	/// Every node type built with its parameterless constructor clones without throwing.
	/// </summary>
	[TestMethod]
	public void EveryNodeType_ClonesFromItsParameterlessConstructor()
	{
		IEnumerable<Type> nodeTypes = typeof(AstNode).Assembly.GetTypes()
			.Where(t => typeof(AstNode).IsAssignableFrom(t) && !t.IsAbstract && !t.ContainsGenericParameters)
			.Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null);

		List<string> failures = [];
		foreach (Type nodeType in nodeTypes)
		{
			AstNode node = (AstNode)Activator.CreateInstance(nodeType)!;
			try
			{
				_ = node.Clone();
			}
			catch (ArgumentException ex)
			{
				failures.Add($"{nodeType.Name}: {ex.Message}");
			}
		}

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
	}

	/// <summary>
	/// A variable reference with no name yet clones to another with no name.
	/// </summary>
	[TestMethod]
	public void VariableReference_WithNoName_ClonesToNoName()
	{
		VariableReference original = new() { ExpectedType = "int" };

		VariableReference clone = (VariableReference)original.Clone();

		Assert.AreEqual(string.Empty, clone.Name);
		Assert.AreEqual("int", clone.ExpectedType?.ToString());
	}

	/// <summary>
	/// A class with a property and an assignment that has no target yet still generates in the
	/// languages that clone the class before writing it.
	/// </summary>
	/// <param name="languageId">The language to generate.</param>
	[TestMethod]
	[DataRow("c")]
	[DataRow("cpp")]
	[DataRow("rust")]
	[DataRow("go")]
	public void ClassWithAnUnfilledAssignment_Generates(string languageId)
	{
		ClassDeclaration box = new("Box");
		box.Members.Add(new PropertyDeclaration("Size", "int"));
		FunctionDeclaration reset = new("reset");
		reset.Body.Add(new AssignmentStatement());
		box.Members.Add(reset);

		ILanguageGenerator generator = languageId switch
		{
			"c" => new CGenerator(),
			"cpp" => new CppGenerator(),
			"rust" => new RustGenerator(),
			_ => new GoGenerator(),
		};

		string output = generator.Generate(box);

		Assert.Contains("Box", output);
	}
}
