// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="EntryPoint"/>: what it holds, how each language spells the place a program
/// starts running, and that it survives a round trip through YAML.
/// </summary>
/// <remarks>
/// The whole reason the entry point is a node of its own is that no two languages spell it the same
/// way, so each generator is checked for its own spelling rather than for a shared one.
/// </remarks>
[TestClass]
public class EntryPointTests
{
	/// <summary>
	/// Builds an entry point that takes arguments, returns an exit code and has a statement in it.
	/// </summary>
	/// <returns>The entry point.</returns>
	private static EntryPoint SampleEntryPoint()
	{
		EntryPoint entryPoint = new() { AcceptsArguments = true, ReturnsExitCode = true };
		entryPoint.Body.Add(new VariableDeclaration("count", "int", Literal.Number(0)));
		entryPoint.Body.Add(new ReturnStatement(Literal.Number(0)));
		return entryPoint;
	}

	/// <summary>
	/// Tests that a clone copies the body rather than sharing it, so editing one program does not
	/// edit the other.
	/// </summary>
	[TestMethod]
	public void Clone_CopiesTheBodyRatherThanSharingIt()
	{
		EntryPoint original = SampleEntryPoint();

		EntryPoint clone = (EntryPoint)original.Clone();

		Assert.IsTrue(clone.AcceptsArguments);
		Assert.IsTrue(clone.ReturnsExitCode);
		Assert.AreEqual(original.Body.Count, clone.Body.Count);
		Assert.AreNotSame(original.Body[0], clone.Body[0]);

		((VariableDeclaration)clone.Body[0]).Name = "total";
		Assert.AreEqual("count", ((VariableDeclaration)original.Body[0]).Name);
	}

	/// <summary>
	/// Tests that the node reports the type name the serializer writes and reads it back under.
	/// </summary>
	[TestMethod]
	public void NodeTypeName_IsEntryPoint() => Assert.AreEqual("EntryPoint", new EntryPoint().GetNodeTypeName());

	/// <summary>
	/// Tests that the editor sees an entry point as one slot holding its statements.
	/// </summary>
	[TestMethod]
	public void Schema_ExposesTheBodyAsOneSlot()
	{
		EntryPoint entryPoint = SampleEntryPoint();
		AstSlot body = AstSchema.SlotsOf(entryPoint).Single();

		Assert.AreEqual("Body", body.Name);
		Assert.AreEqual(AstSlotCardinality.Many, body.Cardinality);
		Assert.AreEqual(2, AstSchema.ChildrenOf(entryPoint, body).Count);

		Assert.IsTrue(AstSchema.TryAttach(entryPoint, body, new ReturnStatement()));
		Assert.AreEqual(3, AstSchema.ChildrenOf(entryPoint, body).Count);

		Assert.IsTrue(AstSchema.TryDetachAt(entryPoint, body, 0));
		Assert.AreEqual(2, AstSchema.ChildrenOf(entryPoint, body).Count);
	}

	/// <summary>
	/// Tests that an entry point can be a class member — which is where C# puts <c>Main</c> — but not
	/// a statement inside a body, since a program does not start running part-way through a function.
	/// </summary>
	[TestMethod]
	public void Schema_TakesAnEntryPointAsAMemberButNotAsAStatement()
	{
		AstSlot members = AstSchema.SlotsOf(new ClassDeclaration("Program")).Single();
		AstSlot body = AstSchema.SlotsOf(new FunctionDeclaration("run")).Single(slot => slot.Name == "Body");

		Assert.IsTrue(AstSchema.Accepts(members, new EntryPoint()));
		Assert.IsFalse(AstSchema.Accepts(body, new EntryPoint()));
	}

	/// <summary>
	/// Tests that the editor has a caption for an entry point, since a node with no caption is a blank
	/// title bar on screen.
	/// </summary>
	[TestMethod]
	public void Schema_DescribesAnEntryPoint() => Assert.AreEqual("entry point", AstSchema.Describe(new EntryPoint()));

	/// <summary>
	/// Tests that C# emits the static <c>Main</c> it looks for, with the argument array and the exit
	/// code the node asked for.
	/// </summary>
	[TestMethod]
	public void CSharp_GeneratesMain()
	{
		string code = new CSharpGenerator().Generate(SampleEntryPoint());

		StringAssert.Contains(code, "public static int Main(string[] args)", StringComparison.Ordinal);
		StringAssert.Contains(code, "int count = 0;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an entry point that takes nothing and returns nothing is emitted as a bare void
	/// <c>Main</c>, rather than one with an unused parameter.
	/// </summary>
	[TestMethod]
	public void CSharp_GeneratesABareMainWhenNothingIsAskedFor()
	{
		string code = new CSharpGenerator().Generate(new EntryPoint());

		StringAssert.Contains(code, "public static void Main()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that C++ emits the free <c>int main</c> the standard names, with <c>argc</c> and
	/// <c>argv</c> when the program reads its arguments.
	/// </summary>
	[TestMethod]
	public void Cpp_GeneratesMain()
	{
		StringAssert.Contains(new CppGenerator().Generate(SampleEntryPoint()), "int main(int argc, char* argv[])", StringComparison.Ordinal);

		// C++'s main returns int whether or not the program hands back an exit code.
		StringAssert.Contains(new CppGenerator().Generate(new EntryPoint()), "int main()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that Python emits the function along with the <c>__main__</c> guard that runs it, and the
	/// import the arguments and exit code need.
	/// </summary>
	[TestMethod]
	public void Python_GeneratesMainAndItsGuard()
	{
		string code = new PythonGenerator().Generate(SampleEntryPoint());

		StringAssert.Contains(code, "import sys", StringComparison.Ordinal);
		StringAssert.Contains(code, "def main(args):", StringComparison.Ordinal);
		StringAssert.Contains(code, "if __name__ == \"__main__\":", StringComparison.Ordinal);
		StringAssert.Contains(code, "sys.exit(main(sys.argv[1:]))", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that an entry point needing neither arguments nor an exit code imports nothing, since an
	/// unused import is something a linter will complain about.
	/// </summary>
	[TestMethod]
	public void Python_ImportsNothingWhenSysIsNotNeeded()
	{
		string code = new PythonGenerator().Generate(new EntryPoint());

		Assert.IsFalse(code.Contains("import sys", StringComparison.Ordinal));
		StringAssert.Contains(code, "def main():", StringComparison.Ordinal);
		StringAssert.Contains(code, "pass", StringComparison.Ordinal);
		StringAssert.Contains(code, "main()", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that JavaScript emits the function along with the call that runs it, since a module that
	/// only defines <c>main</c> does nothing when it is run.
	/// </summary>
	[TestMethod]
	public void JavaScript_GeneratesMainAndCallsIt()
	{
		string code = new JavaScriptGenerator().Generate(SampleEntryPoint());

		StringAssert.Contains(code, "function main(args) {", StringComparison.Ordinal);
		StringAssert.Contains(code, "process.exit(main(process.argv.slice(2)));", StringComparison.Ordinal);

		StringAssert.Contains(new JavaScriptGenerator().Generate(new EntryPoint()), "main();", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that every generator accepts an entry point, so a document rooted at one can be generated
	/// rather than refused.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_AcceptsAnEntryPoint()
	{
		foreach (ILanguageGenerator generator in new ILanguageGenerator[]
			{ new CSharpGenerator(), new CppGenerator(), new PythonGenerator(), new JavaScriptGenerator() })
		{
			Assert.IsTrue(generator.CanGenerate(new EntryPoint()), generator.DisplayName);
		}
	}

	/// <summary>
	/// Tests that an entry point survives a round trip through YAML, which is how the editor saves.
	/// </summary>
	[TestMethod]
	public void Yaml_RoundTripsAnEntryPoint()
	{
		EntryPoint original = SampleEntryPoint();

		string yaml = new YamlSerializer().Serialize(original);
		EntryPoint restored = (EntryPoint)new YamlDeserializer().Deserialize(yaml)!;

		Assert.IsTrue(restored.AcceptsArguments);
		Assert.IsTrue(restored.ReturnsExitCode);
		Assert.AreEqual(2, restored.Body.Count);
		Assert.AreEqual(new CSharpGenerator().Generate(original), new CSharpGenerator().Generate(restored));
	}

	/// <summary>
	/// Tests that the inspector edits the two things that vary about an entry point.
	/// </summary>
	[TestMethod]
	public void Fields_EditArgumentsAndExitCode()
	{
		EntryPoint entryPoint = new();

		Assert.IsTrue(AstFields.TryWrite(entryPoint, "Arguments", "true"));
		Assert.IsTrue(AstFields.TryWrite(entryPoint, "ExitCode", "true"));

		Assert.IsTrue(entryPoint.AcceptsArguments);
		Assert.IsTrue(entryPoint.ReturnsExitCode);

		// A value that is not a flag leaves the document alone.
		Assert.IsFalse(AstFields.TryWrite(entryPoint, "Arguments", "maybe"));
		Assert.IsTrue(entryPoint.AcceptsArguments);
	}

	/// <summary>
	/// Tests that the palette offers an entry point, since a program with no way to start is not one
	/// a user can build from the menu.
	/// </summary>
	[TestMethod]
	public void Catalog_OffersAnEntryPoint() =>
		Assert.IsTrue(AstNodeCatalog.Templates.Any(template => template.Create() is EntryPoint));
}
