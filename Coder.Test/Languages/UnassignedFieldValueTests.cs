// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Runs what Python and JavaScript write for fields and automatic properties with no initialiser,
/// and checks that a fresh instance holds each one's zero (#345).
/// </summary>
/// <remarks>
/// A field with no initialiser is value-initialised, so a default-constructed instance is the one
/// the declaration described. C++, C#, Go and Rust start such a field at zero. A bare Python
/// annotation creates no attribute at all, so <c>counter.count += 1</c> raised <c>AttributeError</c>,
/// and a bare JavaScript field is <c>undefined</c>, so the same line made <c>NaN</c>. Neither shows in
/// the text, so the tests run the output.
/// </remarks>
[TestClass]
public class UnassignedFieldValueTests
{
	/// <summary>
	/// Tests that a Python instance starts each unassigned field and automatic property at its type's
	/// zero, and at <c>None</c> where the type has none.
	/// </summary>
	[TestMethod]
	public void Python_UnassignedFieldsStartAtZero()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		string generated = new PythonGenerator().Generate(Counter());

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "counter.py"), generated);
			File.WriteAllText(
				Path.Combine(directory, "driver.py"),
				"""
				import counter

				c = counter.Counter()
				c.count += c.step
				assert c.count == 1, c.count
				assert c.total == 0 and c.ratio == 0.0 and c.flag is False and c.name == ""
				assert c.origin is None
				assert c.Size == 0
				c.Size += 2
				assert c.Size == 2
				assert counter.Counter().count == 0
				""");

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(0, exitCode, $"A fresh instance's fields were not zero:{Environment.NewLine}{output}{Environment.NewLine}{generated}");
		});
	}

	/// <summary>
	/// Tests that a JavaScript instance starts each unassigned field and automatic property at its
	/// type's zero rather than <c>undefined</c>.
	/// </summary>
	[TestMethod]
	public void JavaScript_UnassignedFieldsStartAtZero()
	{
		string? node = ToolchainHarness.FindOnPath("--version", "node");
		if (node is null)
		{
			Assert.Inconclusive("No JavaScript runtime on the path, so nothing was run.");
			return;
		}

		string generated = new JavaScriptGenerator().Generate(Counter());
		string driver = """

			const assert = (condition, message) => { if (!condition) { throw new Error(message); } };
			const c = new Counter();
			c.count += c.step;
			assert(c.count === 1, `count is ${c.count}`);
			assert(c.total === 0 && c.ratio === 0 && c.flag === false && c.name === "", "a zero was not written");
			c.Size += 2;
			assert(c.Size === 2, `Size is ${c.Size}`);
			assert(new Counter().count === 0, "a second instance shares the first's count");
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "counter.js"), generated + driver);

			(int exitCode, string output) = ToolchainHarness.Run(node, "counter.js", directory);
			Assert.AreEqual(0, exitCode, $"A fresh instance's fields were not zero:{Environment.NewLine}{output}{Environment.NewLine}{generated}");
		});
	}

	/// <summary>
	/// Builds a class with one unassigned field of each type that has a zero, one of a type that has
	/// none, an initialised field, and an unassigned automatic property.
	/// </summary>
	/// <returns>A file holding the class.</returns>
	private static SourceFile Counter()
	{
		ClassDeclaration counter = new("Counter");
		counter.Members.Add(new FieldDeclaration("count", "int"));
		counter.Members.Add(new FieldDeclaration("total", "long"));
		counter.Members.Add(new FieldDeclaration("ratio", "double"));
		counter.Members.Add(new FieldDeclaration("flag", "bool"));
		counter.Members.Add(new FieldDeclaration("name", "string"));
		counter.Members.Add(new FieldDeclaration("origin", "Point"));
		counter.Members.Add(new FieldDeclaration("step", "int") { InitialValue = Literal.Number(1) });
		counter.Members.Add(new PropertyDeclaration("Size", "int") { HasSetter = true });

		SourceFile file = new("counter");
		file.Members.Add(counter);
		return file;
	}
}
