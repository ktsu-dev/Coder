// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Runs what <see cref="PythonGenerator"/> writes, with a real interpreter.
/// </summary>
/// <remarks>
/// The other four targets that are checked by a toolchain are checked by compiling. Python has no
/// compiler, and the equivalent question is whether the module can be imported at all: a Python file
/// is a program that builds its own declarations, so a class statement runs when the module is loaded
/// and anything wrong with it is raised then. That is the whole class of error a test pinning the
/// text cannot see — the spelling looks right and the interpreter disagrees — and a base list naming
/// the class being declared is exactly one of them.
/// <para>
/// The driver is written the way a consumer would be: it supplies the names the generated module
/// expects to find — the interface it implements, and the type variable it is written over — and then
/// runs the module against them. <c>runpy</c> is how a module is run with names already in its
/// namespace; an import cannot, and the AST has no way to declare a <c>TypeVar</c> or a
/// <c>Generic</c> for the file to carry its own.
/// </para>
/// <para>
/// The test is inconclusive rather than failing where no interpreter is on the path, which is the
/// honest result: nothing was run.
/// </para>
/// </remarks>
[TestClass]
public class PythonGeneratedSourceImportsTests
{
	/// <summary>
	/// The consumer of the generated module, written the way a person would write one.
	/// </summary>
	/// <remarks>
	/// Every declaration the module makes is asked for afterwards, so a module that loads but declares
	/// nothing fails here rather than passing quietly. <c>Length</c> is the self-type idiom and
	/// <c>Node</c> the mutually-referential pair, which are the two shapes a base list can hold a name
	/// that does not exist yet.
	/// </remarks>
	private const string Driver = """
		import runpy
		from typing import Generic, TypeVar

		T = TypeVar("T")
		TSelf = TypeVar("TSelf")


		class IVector0(Generic[TSelf, T]):
		    pass


		class Visitor(Generic[TSelf]):
		    pass


		module = runpy.run_path(
		    "exemplar.py",
		    init_globals={"IVector0": IVector0, "Visitor": Visitor, "T": T},
		)

		assert module["Length"].__name__ == "Length"
		assert module["Node"].__name__ == "Node"

		""";

	/// <summary>
	/// Tests that a module whose classes are written over themselves can be loaded.
	/// </summary>
	[TestMethod]
	public void GeneratedSource_Imports()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "driver.py"), Driver);
			File.WriteAllText(
				Path.Combine(directory, "exemplar.py"),
				new PythonGenerator().Generate(Exemplar()));

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(
				0,
				exitCode,
				$"Python could not load the generated module:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a static property with bodies is reachable from the class, rather than answering
	/// the property object a <c>@property</c> read from the class gives (issue #180).
	/// </summary>
	[TestMethod]
	public void StaticProperty_IsReachableFromTheClass()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		ClassDeclaration counter = new("Counter");
		counter.Members.Add(new PropertyDeclaration("Limit", "int")
		{
			IsStatic = true,
			HasSetter = true,
			GetterBody = { new ReturnStatement(new LiteralExpression<int>(10)) },
		});
		SourceFile file = new("counter");
		file.Members.Add(counter);

		string generated = new PythonGenerator().Generate(file);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "counter.py"), generated);
			File.WriteAllText(
				Path.Combine(directory, "driver.py"),
				string.Join(
					Environment.NewLine,
					"from counter import Counter",
					"assert Counter.limit() == 10, Counter.limit",
					"assert Counter().limit() == 10",
					"assert Counter.set_limit(3) is None",
					string.Empty));

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(0, exitCode, $"The static property was not reachable from the class:{Environment.NewLine}{output}{Environment.NewLine}{generated}");
		});
	}

	[TestMethod]
	public void MutableFields_ArePerInstanceAndEnumsImport()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		SourceFile file = new("mutable");
		ClassDeclaration bag = new("Bag");
		bag.Members.Add(new FieldDeclaration("items", TypeReference.Parse("list<int>"))
		{
			InitialValue = new ConstructionExpression(),
		});
		bag.Members.Add(new FieldDeclaration("bare"));
		file.Members.Add(bag);
		file.Members.Add(new EnumDeclaration("State"));

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "mutable.py"), new PythonGenerator().Generate(file));
			File.WriteAllText(
				Path.Combine(directory, "driver.py"),
				"""
				import mutable

				first = mutable.Bag()
				second = mutable.Bag()
				first.items.append(1)
				assert second.items == []
				assert first.bare is None
				assert mutable.State
				""");

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(0, exitCode, $"Python rejected the generated module:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a subclass declaring no constructor still accepts its base's arguments: one with
	/// nothing to initialise inherits the base's constructor, and one with an instance-initialised
	/// field forwards to it (#149).
	/// </summary>
	[TestMethod]
	public void SubclassWithoutConstructor_AcceptsTheBaseConstructorsArguments()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		SourceFile file = new("inherited");

		// Base(x): self.x = x
		ClassDeclaration baseClass = new("Base");
		FunctionDeclaration constructor = new("Base") { Kind = FunctionKind.Constructor };
		constructor.Parameters.Add(new Parameter("x", "int"));
		constructor.Body.Add(new AssignmentStatement(new VariableReference("self.x"), new VariableReference("x")));
		baseClass.Members.Add(constructor);
		file.Members.Add(baseClass);

		file.Members.Add(new ClassDeclaration("Derived") { BaseType = "Base" });

		ClassDeclaration tagged = new("Tagged") { BaseType = "Base" };
		tagged.Members.Add(new FieldDeclaration("tags", TypeReference.Parse("list<int>"))
		{
			InitialValue = new ConstructionExpression(),
		});
		file.Members.Add(tagged);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string generated = new PythonGenerator().Generate(file);
			File.WriteAllText(Path.Combine(directory, "inherited.py"), generated);
			File.WriteAllText(
				Path.Combine(directory, "driver.py"),
				"""
				import inherited

				derived = inherited.Derived(5)
				assert derived.x == 5, derived.x

				first = inherited.Tagged(6)
				second = inherited.Tagged(x=7)
				assert first.x == 6, first.x
				assert second.x == 7, second.x
				first.tags.append(1)
				assert second.tags == [], second.tags
				""");

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(0, exitCode, $"Python rejected the generated module:{Environment.NewLine}{output}{Environment.NewLine}{generated}");
		});
	}

	/// <summary>
	/// Tests that a module whose annotations name a class that does not exist yet can be loaded.
	/// </summary>
	/// <remarks>
	/// Python before 3.14 evaluates an annotation as the <c>def</c> or the class body runs, so a
	/// factory answering its own class, a method taking another instance, a field holding one, and a
	/// parameter naming a class declared further down are each a <c>NameError</c> on import unless the
	/// annotations are deferred.
	/// </remarks>
	[TestMethod]
	public void AnnotationsNamingUndeclaredClasses_Import()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		SourceFile file = new("shapes");
		file.HeaderComment.Add("Generated by Coder. Do not edit.");

		ClassDeclaration point = new("Point");
		point.Members.Add(new FieldDeclaration("x", "int"));

		FunctionDeclaration zero = new("zero") { ReturnType = "Point", IsStatic = true };
		zero.Body.Add(new ReturnStatement(new ConstructionExpression("Point")));
		point.Members.Add(zero);

		FunctionDeclaration distance = new("distance") { ReturnType = "int" };
		distance.Parameters.Add(new Parameter("other", "Point"));
		distance.Body.Add(new ReturnStatement(0));
		point.Members.Add(distance);

		FunctionDeclaration attach = new("attach");
		attach.Parameters.Add(new Parameter("node", "Node"));
		point.Members.Add(attach);

		ClassDeclaration node = new("Node");
		node.Members.Add(new FieldDeclaration("next", "Node"));

		file.Members.Add(point);
		file.Members.Add(node);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "shapes.py"), new PythonGenerator().Generate(file));
			File.WriteAllText(
				Path.Combine(directory, "driver.py"),
				"""
				import shapes

				assert isinstance(shapes.Point.zero(), shapes.Point)
				assert shapes.Node.__name__ == "Node"
				""");

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(0, exitCode, $"Python could not load the generated module:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Tests that a class with a property that can be written but not read can be loaded and set.
	/// </summary>
	/// <remarks>
	/// <c>@level.setter</c> names the property a getter made, so a property with no getter needs
	/// another spelling or the class statement raises <c>NameError</c>.
	/// </remarks>
	[TestMethod]
	public void WriteOnlyProperty_ImportsAndSets()
	{
		string? python = ToolchainHarness.FindOnPath("--version", "python3", "python");
		if (python is null)
		{
			Assert.Inconclusive("No Python interpreter on the path, so nothing was run.");
			return;
		}

		ClassDeclaration sink = new("Sink");
		PropertyDeclaration level = new("level", "int") { HasGetter = false, HasSetter = true };
		CallExpression record = new("record");
		record.Arguments.Add(new VariableReference("value"));
		level.SetterBody.Add(new ExpressionStatement(record));
		sink.Members.Add(level);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "sink.py"), new PythonGenerator().Generate(sink));
			File.WriteAllText(
				Path.Combine(directory, "driver.py"),
				"""
				import runpy

				recorded = []
				module = runpy.run_path("sink.py", init_globals={"record": recorded.append})

				instance = module["Sink"]()
				instance.level = 3
				assert recorded == [3], recorded

				try:
				    instance.level
				except AttributeError:
				    pass
				else:
				    raise AssertionError("a write-only property could be read")
				""");

			(int exitCode, string output) = ToolchainHarness.Run(python, "driver.py", directory);
			Assert.AreEqual(0, exitCode, $"Python rejected the generated module:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// Builds a file whose declarations are each written over themselves.
	/// </summary>
	/// <returns>The file to generate.</returns>
	private static SourceFile Exemplar()
	{
		SourceFile file = new("exemplar");
		file.HeaderComment.Add("Generated by Coder. Do not edit.");

		// The shape every generated ktsu.Semantics quantity has: an interface written over the type
		// implementing it, so that the method it declares answers that type rather than the interface.
		ClassDeclaration length = new("Length");
		length.Interfaces.Add(TypeReference.Parse("IVector0<Length<T>, T>"));
		length.Members.Add(new VariableDeclaration("Value", "T"));

		// The same knot without generics of its own: a type and the thing that visits it, each named
		// in the other's declaration.
		ClassDeclaration node = new("Node");
		node.Interfaces.Add(TypeReference.Parse("Visitor<Node>"));

		file.Members.Add(length);
		file.Members.Add(node);

		return file;
	}
}
