// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compiles what <see cref="CSharpGenerator"/> writes, with <c>dotnet build</c>.
/// </summary>
/// <remarks>
/// Where C# can declare a member is a rule the text does not show: a field or function written
/// straight into a namespace reads like any other and fails with CS0116. A test that pinned the
/// spelling passed on exactly that output, which is why it was not noticed.
/// <para>
/// A driver in the same project uses every declaration, so a declaration that compiles on its own
/// but cannot be reached from outside fails here too. The test is inconclusive rather than failing
/// where no <c>dotnet</c> is on the path, which is the honest result: nothing was checked.
/// </para>
/// </remarks>
[TestClass]
public class CSharpGeneratedSourceCompilesTests
{
	/// <summary>
	/// The project the generated file is compiled as: a library, with warnings left as warnings,
	/// because what is under test is whether C# accepts the file rather than what it thinks of it.
	/// </summary>
	private const string Project = """
		<Project Sdk="Microsoft.NET.Sdk">
		  <PropertyGroup>
		    <TargetFramework>net10.0</TargetFramework>
		    <Nullable>disable</Nullable>
		    <ImplicitUsings>disable</ImplicitUsings>
		  </PropertyGroup>
		</Project>
		""";

	/// <summary>
	/// The consumer of the generated declarations, written the way a person would write one.
	/// </summary>
	private const string Driver = """
		public static class Driver
		{
			public static int Use()
			{
				global::Geometry.Point point = new global::Geometry.Point();
				global::Geometry.Geometry.count = point is null ? 0 : 1;
				return global::Geometry.Geometry.twice(global::Geometry.Geometry.Origins)
					+ global::Geometry.Geometry.count
					+ Globals.half(4)
					+ (new Tool() is null ? 0 : 1);
			}
		}
		""";

	/// <summary>
	/// Tests that a namespace and a file holding fields, functions and a variable beside their types
	/// compile, and that a second file can reach all of them.
	/// </summary>
	[TestMethod]
	public void ScopeMembers_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = new("Generated");
		file.Members.Add(CSharpScopeMemberTests.Geometry());

		FunctionDeclaration half = new("half") { ReturnType = "int" };
		half.Parameters.Add(new Parameter("n", "int"));
		half.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("n"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(half);
		file.Members.Add(new ClassDeclaration("Tool"));

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), Driver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), new CSharpGenerator().Generate(file));

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}");
		});
	}

	/// <summary>
	/// The consumer of the generated lists, which reads each one back.
	/// </summary>
	private const string ListDriver = """
		public static class ListDriver
		{
			public static int Use()
			{
				Bag bag = new Bag();
				bag.reset();
				return bag.items.Count + bag.make().Count + bag.table.Length + bag.grid[1][0] + Bag.empty().Count;
			}
		}
		""";

	/// <summary>
	/// Tests that a typeless list compiles wherever C# can meet one — initialising a
	/// <c>List&lt;T&gt;</c> field or local, an array and a table of arrays, and as a return value and
	/// an assigned value — rather than only as an array's initialiser (#418).
	/// </summary>
	[TestMethod]
	public void TypelessLists_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		static ConstructionExpression List(params Expression[] elements)
		{
			ConstructionExpression list = new();
			foreach (Expression element in elements)
			{
				list.Arguments.Add(element);
			}

			return list;
		}

		ClassDeclaration bag = new("Bag");
		bag.Members.Add(new FieldDeclaration("items", TypeReference.Parse("list<int>")) { Visibility = Visibility.Public, InitialValue = List(Literal.Number(1), Literal.Number(2)) });
		bag.Members.Add(new FieldDeclaration("table", new TypeReference("int") { IsArray = true }) { Visibility = Visibility.Public, InitialValue = List(Literal.Number(3), Literal.Number(4)) });
		bag.Members.Add(new FieldDeclaration("grid", new TypeReference("int[]") { IsArray = true })
		{
			Visibility = Visibility.Public,
			InitialValue = List(List(Literal.Number(1), Literal.Number(2)), List(Literal.Number(3), Literal.Number(4))),
		});

		FunctionDeclaration make = new("make") { ReturnType = "list<int>", Visibility = Visibility.Public };
		make.Body.Add(new VariableDeclaration("xs", "list<int>", List(Literal.Number(1), Literal.Number(2))));
		make.Body.Add(new ReturnStatement(List(Literal.Number(5), Literal.Number(6))));
		bag.Members.Add(make);

		FunctionDeclaration reset = new("reset") { ReturnType = "void", Visibility = Visibility.Public };
		reset.Body.Add(new AssignmentStatement(new VariableReference("items"), List(Literal.Number(7))));
		bag.Members.Add(reset);

		FunctionDeclaration empty = new("empty") { ReturnType = "list<int>", Visibility = Visibility.Public, IsStatic = true };
		empty.Body.Add(new ReturnStatement(List()));
		bag.Members.Add(empty);

		// The using is the file's to declare; that a list<T> parameter does not bring one is #334.
		SourceFile file = new("Generated");
		file.Imports.Add("System.Collections.Generic");
		file.Members.Add(bag);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "ListDriver.cs"), ListDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// The consumer of the generated operators, which uses each the way C# code would.
	/// </summary>
	/// <remarks>
	/// <c>a + b</c> is what catches a binary operator written as a unary one: the declaration
	/// compiles either way, and only a call site with two operands refuses it.
	/// </remarks>
	private const string OperatorDriver = """
		public static class OperatorDriver
		{
			public static double Use()
			{
				Point a = new Point(1, 2);
				Point b = new Point(3);
				Point sum = a + b;
				Point negated = -a;
				double converted = a;
				return converted + (double)sum + (double)negated;
			}
		}
		""";

	/// <summary>
	/// Tests that a type's binary, unary and conversion operators compile, and that a caller can use
	/// each with the operands the declaration describes.
	/// </summary>
	[TestMethod]
	public void Operators_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		ClassDeclaration point = CompiledExemplar.Point();
		point.Members.Add(CompiledExemplar.Plus());
		point.Members.Add(CompiledExemplar.Negate());
		point.Members.Add(CompiledExemplar.Times());
		point.Members.Add(CompiledExemplar.ToDouble("self.x"));

		SourceFile file = new("Generated");
		file.Members.Add(point);
		file.Members.Add(CompiledExemplar.Widths());

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), OperatorDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that operators declared private, protected or virtual, which C++ allows, still compile
	/// as the public static operators C# requires, with what was dropped written down (#260).
	/// </summary>
	[TestMethod]
	public void OperatorsDeclaredPrivateOrVirtual_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		FunctionDeclaration plus = CompiledExemplar.Plus();
		plus.Visibility = Visibility.Private;
		FunctionDeclaration negate = CompiledExemplar.Negate();
		negate.IsVirtual = true;
		FunctionDeclaration toDouble = CompiledExemplar.ToDouble("self.x");
		toDouble.Visibility = Visibility.Protected;

		ClassDeclaration point = CompiledExemplar.Point();
		point.Members.Add(plus);
		point.Members.Add(negate);
		point.Members.Add(toDouble);

		SourceFile file = new("Generated");
		file.Members.Add(point);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("// private: operator + is public, as every C# operator is", source, StringComparison.Ordinal, source);
			Assert.Contains("// virtual: operator - is static", source, StringComparison.Ordinal, source);
			Assert.Contains("// protected: ", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), OperatorDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// The consumer of the generated shifts, which passes a <c>long</c> count to each.
	/// </summary>
	private const string ShiftDriver = """
		public static class ShiftDriver
		{
			public static long Use() => Globals.sh(1L, 3L) + Globals.shAssign(2L) + Globals.shLiteral(1L);
		}
		""";

	/// <summary>
	/// Tests that a shift and a shift-assignment by a <c>long</c> count compile, which C#'s
	/// <c>int</c>-count shift operators reject unless the count is cast, and that an <c>int</c>
	/// literal count is still written bare (#271).
	/// </summary>
	[TestMethod]
	public void ShiftsByALongCount_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		FunctionDeclaration sh = new("sh") { ReturnType = "long" };
		sh.Parameters.Add(new Parameter("x", "long"));
		sh.Parameters.Add(new Parameter("n", "long"));
		sh.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), BinaryOperator.LeftShift, new VariableReference("n"))));

		FunctionDeclaration shAssign = new("shAssign") { ReturnType = "long" };
		shAssign.Parameters.Add(new Parameter("n", "long"));
		shAssign.Body.Add(new VariableDeclaration("k", "long", Literal.BigNumberValue(3)));
		shAssign.Body.Add(new AssignmentStatement(new VariableReference("k"), new VariableReference("n"), AssignmentOperator.LeftShiftAssign));
		shAssign.Body.Add(new AssignmentStatement(new VariableReference("k"), new VariableReference("n"), AssignmentOperator.RightShiftAssign));
		shAssign.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("k"), BinaryOperator.RightShift, new VariableReference("n"))));

		FunctionDeclaration shLiteral = new("shLiteral") { ReturnType = "long" };
		shLiteral.Parameters.Add(new Parameter("x", "long"));
		shLiteral.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), BinaryOperator.LeftShift, Literal.Number(3))));

		SourceFile file = new("Generated");
		file.Members.Add(sh);
		file.Members.Add(shAssign);
		file.Members.Add(shLiteral);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("(x << 3)", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), ShiftDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// The consumer of the string comparisons, which orders a pair each way.
	/// </summary>
	private const string StringOrderingDriver = """
		public static class StringOrderingDriver
		{
			public static bool Use() =>
				Globals.before("a", "b")
				&& Globals.atLeast("b", "a")
				&& Globals.early("a")
				&& Globals.lateOrLater("z")
				&& Globals.literalFirst("z");
		}
		""";

	/// <summary>
	/// Tests that <c>&lt;</c> and <c>&gt;=</c> between two strings, and between a string and a literal
	/// on either side, compile: C# defines no ordering operator on <c>string</c>, so each is written
	/// as an ordinal comparison rather than as the CS0019 the bare operator is (#372).
	/// </summary>
	[TestMethod]
	public void StringOrderingComparisons_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = StringOrderingFixture.Build();

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("(string.CompareOrdinal(a, b) < 0)", source, StringComparison.Ordinal, source);
			Assert.Contains("(string.CompareOrdinal(a, \"m\") >= 0)", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), StringOrderingDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// The consumer of a type that declares its own shift, which shifts one value by another.
	/// </summary>
	private const string BitsDriver = """
		public static class BitsDriver
		{
			public static Bits Use(Bits a, Bits b) => Globals.Join(a, b);
		}
		""";

	/// <summary>
	/// Tests that a shift and a shift-assignment between values of a type declaring its own
	/// <c>&lt;&lt;</c> compile, which the <c>int</c> cast #271 put on every shift count turned into
	/// CS0030, while a <c>long</c> count beside it is still cast (#305).
	/// </summary>
	[TestMethod]
	public void ShiftsByAUserDeclaredOperator_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		ClassDeclaration bits = new("Bits") { Kind = TypeDeclarationKind.Struct };
		bits.Members.Add(new VariableDeclaration("value", "int") { Visibility = Visibility.Public });
		FunctionDeclaration shift = new("<<") { Kind = FunctionKind.Operator, ReturnType = "Bits" };
		shift.Parameters.Add(new Parameter("count", "Bits"));
		shift.Body.Add(new ReturnStatement(new VariableReference("count")));
		bits.Members.Add(shift);

		FunctionDeclaration join = new("Join") { ReturnType = "Bits" };
		join.Parameters.Add(new Parameter("a", "Bits"));
		join.Parameters.Add(new Parameter("b", "Bits"));
		join.Body.Add(new VariableDeclaration("k", "Bits", new VariableReference("a")));
		join.Body.Add(new AssignmentStatement(new VariableReference("k"), new VariableReference("b"), AssignmentOperator.LeftShiftAssign));
		join.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("k"), BinaryOperator.LeftShift, new VariableReference("b"))));

		FunctionDeclaration wide = new("Wide") { ReturnType = "long" };
		wide.Parameters.Add(new Parameter("x", "long"));
		wide.Parameters.Add(new Parameter("n", "long"));
		wide.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), BinaryOperator.LeftShift, new VariableReference("n"))));

		SourceFile file = new("Generated");
		file.Members.Add(bits);
		file.Members.Add(join);
		file.Members.Add(wide);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("(k << b)", source, StringComparison.Ordinal, source);
			Assert.Contains("(x << (int)(n))", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), BitsDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a class method calling a function, or reading a constant field, declared beside it by
	/// its bare name compiles, both at file scope and inside nested namespaces, and when a type beside
	/// them has already claimed the holder's name (#279).
	/// </summary>
	[TestMethod]
	public void ClassMethodsReachingTheirScopesMembers_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = new("Generated");
		AddMembersAndAUser(file.Members, "helper", "Box");
		file.Members.Add(new ClassDeclaration("Globals"));

		NamespaceDeclaration outer = new("Outer");
		NamespaceDeclaration geometry = new("Inner.Geometry");
		AddMembersAndAUser(geometry.Members, "twice", "Shape");
		outer.Members.Add(geometry);
		file.Members.Add(outer);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("using static global::GlobalsGlobals;", source, StringComparison.Ordinal, source);
			Assert.Contains("using static global::Outer.Inner.Geometry.Geometry;", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});

		static void AddMembersAndAUser(ICollection<AstNode> members, string function, string type)
		{
			FunctionDeclaration free = new(function) { ReturnType = "int" };
			free.Body.Add(new ReturnStatement(Literal.Number(3)));
			members.Add(free);
			members.Add(new FieldDeclaration("Limit", "int") { IsConstant = true, InitialValue = Literal.Number(4) });

			ClassDeclaration user = new(type);
			FunctionDeclaration size = new("size") { ReturnType = "int" };
			size.Body.Add(new ReturnStatement(new BinaryExpression(new CallExpression(function), BinaryOperator.Add, new VariableReference("Limit"))));
			user.Members.Add(size);
			members.Add(user);
		}
	}

	/// <summary>
	/// Tests that a struct whose fields start at a value compiles, which C# allows only beside a
	/// declared constructor (CS8983), and that <c>new</c> starts the fields at those values (#270).
	/// </summary>
	[TestMethod]
	public void StructsWithInitialisedFields_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		SourceFile file = new("Generated");
		ClassDeclaration counter = new("Counter") { Kind = TypeDeclarationKind.Struct };
		counter.Members.Add(new FieldDeclaration("count", "int") { Visibility = Visibility.Public, InitialValue = Literal.Number(7) });
		file.Members.Add(counter);

		ClassDeclaration limits = new("Limits") { Kind = TypeDeclarationKind.Struct, IsReadOnly = true };
		limits.Members.Add(new FieldDeclaration("most", "int") { Visibility = Visibility.Public, InitialValue = Literal.Number(9) });
		file.Members.Add(limits);

		const string StructDriver = """
			public static class Driver
			{
				public static int Use() => new Counter().count + new Limits().most;
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), StructDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a local with no initialiser compiles whether or not it says a type, which C# can
	/// only infer from an initialiser (CS0818), and that one with an initialiser is still <c>var</c> (#355).
	/// </summary>
	[TestMethod]
	public void LocalsWithoutAnInitialiser_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		FunctionDeclaration locals = new("locals") { ReturnType = "int" };
		locals.Body.Add(new VariableDeclaration("untyped"));
		locals.Body.Add(new VariableDeclaration("typed", "int") { IsTypeInferred = true });
		locals.Body.Add(new VariableDeclaration("started", "int", Literal.Number(2)) { IsTypeInferred = true });
		locals.Body.Add(new AssignmentStatement(new VariableReference("untyped"), Literal.Number(1)));
		locals.Body.Add(new AssignmentStatement(new VariableReference("typed"), Literal.Number(3)));
		locals.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("typed"), BinaryOperator.Add, new VariableReference("started"))));

		SourceFile file = new("Generated");
		file.Members.Add(locals);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("object untyped;", source, StringComparison.Ordinal, source);
			Assert.Contains("untyped: C# infers a type only from an initialiser", source, StringComparison.Ordinal, source);
			Assert.Contains("int typed;", source, StringComparison.Ordinal, source);
			Assert.Contains("var started = 2;", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that virtual and abstract methods that are private, belong to a struct, or are static,
	/// which C++ allows and C# rejects (CS0621, CS0106, CS0112), compile with what was changed or
	/// dropped written down (#412).
	/// </summary>
	[TestMethod]
	public void MethodsCSharpCannotOverride_Compile()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		ClassDeclaration widget = new("Widget");
		FunctionDeclaration doDraw = new("DoDraw") { ReturnType = "int", IsVirtual = true, Visibility = Visibility.Private };
		doDraw.Body.Add(new ReturnStatement(Literal.Number(1)));
		widget.Members.Add(doDraw);
		widget.Members.Add(new FunctionDeclaration("Area") { ReturnType = "int", IsAbstract = true, Visibility = Visibility.Private });

		ClassDeclaration factory = new("Factory");
		FunctionDeclaration make = new("Make") { ReturnType = "int", IsStatic = true, IsVirtual = true };
		make.Body.Add(new ReturnStatement(Literal.Number(2)));
		factory.Members.Add(make);
		factory.Members.Add(new FunctionDeclaration("Build") { ReturnType = "int", IsStatic = true, IsAbstract = true });

		ClassDeclaration span = new("Span") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration len = new("Len") { ReturnType = "int", IsVirtual = true };
		len.Body.Add(new ReturnStatement(Literal.Number(3)));
		span.Members.Add(len);
		span.Members.Add(new FunctionDeclaration("Area") { ReturnType = "int", IsAbstract = true });

		SourceFile file = new("Generated");
		file.Members.Add(widget);
		file.Members.Add(factory);
		file.Members.Add(span);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("protected virtual int DoDraw()", source, StringComparison.Ordinal, source);
			Assert.Contains("protected abstract int Area();", source, StringComparison.Ordinal, source);
			Assert.Contains("// private: DoDraw is protected, because C# cannot override a private member", source, StringComparison.Ordinal, source);
			Assert.Contains("public static int Make()", source, StringComparison.Ordinal, source);
			Assert.Contains("// virtual: Make is static, which C# cannot override outside an interface", source, StringComparison.Ordinal, source);
			Assert.Contains("// Build is abstract and static", source, StringComparison.Ordinal, source);
			Assert.Contains("public class Factory", source, StringComparison.Ordinal, source);
			Assert.Contains("public int Len()", source, StringComparison.Ordinal, source);
			Assert.Contains("// virtual: Len belongs to a struct, which C# cannot derive from", source, StringComparison.Ordinal, source);
			Assert.Contains("// Area is abstract, which a C# struct cannot declare", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// The consumer of the narrow-arithmetic declarations.
	/// </summary>
	private const string NarrowDriver = """
		public static class NarrowDriver
		{
			public static short Use(short a, short b, byte u)
			{
				Packet packet = new();
				packet.Bump();
				return (short)(Globals.F(a, b, u) + packet.Count + packet.Doubled + packet.Half + Globals.G(a, b, true) + new Pair(a, b).lo);
			}
		}
		""";

	/// <summary>
	/// Tests that arithmetic, bitwise and unary results on <c>short</c> and <c>byte</c> written back
	/// into a local, a field, an assignment and a return compile, which C#'s promotion to <c>int</c>
	/// rejects with CS0266 unless the result is cast (#411), and that a result into <c>int</c> is not
	/// cast. So do the same results passed as a call's argument, given to a member in an object
	/// initialiser or a constructor's initialisers, and a non-constant conditional of two
	/// <c>int</c> literals into a <c>short</c> (#448).
	/// </summary>
	[TestMethod]
	public void NarrowArithmetic_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		// short F(short a, short b, byte u) { short c = a + b; byte d = u & 15; short e = -a; c = c * 2; return c - e; }
		FunctionDeclaration f = new("F") { ReturnType = "short" };
		f.Parameters.Add(new Parameter("a", "short"));
		f.Parameters.Add(new Parameter("b", "short"));
		f.Parameters.Add(new Parameter("u", "byte"));
		f.Body.Add(new VariableDeclaration("c", "short", new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));
		f.Body.Add(new VariableDeclaration("d", "byte", new BinaryExpression(new VariableReference("u"), BinaryOperator.BitwiseAnd, Literal.Number(15))));
		f.Body.Add(new VariableDeclaration("e", "short", new UnaryExpression(UnaryOperator.Negate, new VariableReference("a"))));
		f.Body.Add(new VariableDeclaration("w", "int", new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("d"))));
		f.Body.Add(new AssignmentStatement(new VariableReference("c"), new BinaryExpression(new VariableReference("c"), BinaryOperator.Multiply, Literal.Number(2))));
		f.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("c"), BinaryOperator.Subtract, new VariableReference("e"))));

		ClassDeclaration packet = new("Packet");
		packet.Members.Add(new FieldDeclaration("Base", new TypeReference("short")) { IsStatic = true, Visibility = Visibility.Public, InitialValue = Literal.Number(3) });
		packet.Members.Add(new FieldDeclaration("Doubled", new TypeReference("short"))
		{
			Visibility = Visibility.Public,
			InitialValue = new BinaryExpression(new VariableReference("Base"), BinaryOperator.Multiply, Literal.Number(2)),
		});
		packet.Members.Add(new FieldDeclaration("Count", new TypeReference("sbyte")) { Visibility = Visibility.Public });
		FunctionDeclaration bump = new("Bump") { Visibility = Visibility.Public };
		bump.Body.Add(new AssignmentStatement(new VariableReference("Count"), new BinaryExpression(new VariableReference("Count"), BinaryOperator.Add, Literal.Number(1))));
		packet.Members.Add(bump);
		packet.Members.Add(new PropertyDeclaration("Half", "ushort")
		{
			Visibility = Visibility.Public,
			GetterBody = { new ReturnStatement(new BinaryExpression(new VariableReference("Base"), BinaryOperator.RightShift, Literal.Number(1))) },
		});

		// struct Pair { public short lo; public Pair(short a, short b) : lo(a + b) {} }
		ClassDeclaration pair = new("Pair") { Kind = TypeDeclarationKind.Struct };
		pair.Members.Add(new FieldDeclaration("lo", new TypeReference("short")) { Visibility = Visibility.Public });
		FunctionDeclaration pairConstructor = new("Pair") { Kind = FunctionKind.Constructor, Visibility = Visibility.Public };
		pairConstructor.Parameters.Add(new Parameter("a", "short"));
		pairConstructor.Parameters.Add(new Parameter("b", "short"));
		pairConstructor.Initialisers.Add(new MemberInitialiser("lo", new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));
		pair.Members.Add(pairConstructor);

		// short take(short v) { return v; }
		FunctionDeclaration take = new("take") { ReturnType = "short" };
		take.Parameters.Add(new Parameter("v", "short"));
		take.Body.Add(new ReturnStatement(new VariableReference("v")));

		// short G(short a, short b, bool flag) { short t = take(a + b); Pair pr = new Pair { lo = a + b }; short s = flag ? 1 : 2; return s; }
		FunctionDeclaration g = new("G") { ReturnType = "short" };
		g.Parameters.Add(new Parameter("a", "short"));
		g.Parameters.Add(new Parameter("b", "short"));
		g.Parameters.Add(new Parameter("flag", "bool"));
		CallExpression takeCall = new("take");
		takeCall.Arguments.Add(new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b")));
		g.Body.Add(new VariableDeclaration("t", "short", takeCall));
		ConstructionExpression pairInit = new(new TypeReference("Pair"));
		pairInit.Arguments.Add(new MemberInitialiser("lo", new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b"))));
		g.Body.Add(new VariableDeclaration("pr", "Pair", pairInit));
		g.Body.Add(new VariableDeclaration("s", "short", new ConditionalExpression(new VariableReference("flag"), Literal.Number(1), Literal.Number(2))));
		g.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("s"), BinaryOperator.Add, new VariableReference("t"))));

		SourceFile file = new("Generated");
		file.Members.Add(f);
		file.Members.Add(take);
		file.Members.Add(g);
		file.Members.Add(packet);
		file.Members.Add(pair);

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			Assert.Contains("int w = (a + d);", source, StringComparison.Ordinal, source);

			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), NarrowDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that the class holding a scope's functions does not take the name of a nested namespace
	/// beside it, at namespace and at file scope, nor fall back to a name a sibling already has (#429).
	/// </summary>
	[TestMethod]
	public void ScopeHolderBesideASameNamedNamespace_Compiles()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		static FunctionDeclaration Returning(string name)
		{
			FunctionDeclaration function = new(name) { ReturnType = "int" };
			function.Body.Add(new ReturnStatement(Literal.Number(2)));
			return function;
		}

		// namespace Physics { namespace Physics { class Body } int gravity(); class World }
		NamespaceDeclaration physics = new("Physics");
		NamespaceDeclaration inner = new("Physics");
		inner.Members.Add(new ClassDeclaration("Body"));
		physics.Members.Add(inner);
		physics.Members.Add(Returning("gravity"));
		physics.Members.Add(new ClassDeclaration("World"));

		// At file scope the holder wants Globals, which a namespace has, and then GlobalsGlobals,
		// which a class has.
		NamespaceDeclaration globals = new("Globals.Inner");
		globals.Members.Add(new ClassDeclaration("Marker"));

		SourceFile file = new("Generated");
		file.Members.Add(physics);
		file.Members.Add(globals);
		file.Members.Add(new ClassDeclaration("GlobalsGlobals"));
		file.Members.Add(Returning("half"));

		const string HolderDriver = """
			public static class Driver
			{
				public static int Use() =>
					global::Physics.PhysicsGlobals.gravity()
					+ global::GlobalsGlobals2.half()
					+ (new global::Physics.Physics.Body() is null ? 0 : 1)
					+ (new global::Physics.World() is null ? 0 : 1)
					+ (new global::Globals.Inner.Marker() is null ? 0 : 1)
					+ (new global::GlobalsGlobals() is null ? 0 : 1);
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), HolderDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}

	/// <summary>
	/// Tests that a member and a local named <c>field</c> keep their meaning inside a property
	/// accessor, where C# 14 reads a bare <c>field</c> as the synthesized backing field (#461): the
	/// build gives no CS9258 or CS9273, and the getter and setter reach the declared member.
	/// </summary>
	[TestMethod]
	public void AMemberOrLocalNamedField_KeepsItsMeaningInsideAnAccessor()
	{
		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		// class Box { private int field = 42; int Field { get => field; set => field = value; } int Twice { get { int field = 2; return field; } } }
		ClassDeclaration box = new("Box");
		box.Members.Add(new FieldDeclaration("field", "int") { Visibility = Visibility.Private, InitialValue = Literal.Number(42) });
		box.Members.Add(new PropertyDeclaration("Field", "int")
		{
			HasSetter = true,
			GetterBody = { new ReturnStatement(new VariableReference("field")) },
			SetterBody = { new AssignmentStatement(new VariableReference("field"), new VariableReference("value")) },
		});
		box.Members.Add(new PropertyDeclaration("Twice", "int")
		{
			GetterBody =
			{
				new VariableDeclaration("field", "int", Literal.Number(2)),
				new ReturnStatement(new VariableReference("field")),
			},
		});

		SourceFile file = new("Generated");
		file.Members.Add(box);

		const string ProgramProject = """
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <OutputType>Exe</OutputType>
			    <TargetFramework>net10.0</TargetFramework>
			    <Nullable>disable</Nullable>
			    <ImplicitUsings>disable</ImplicitUsings>
			    <WarningsAsErrors>CS9258;CS9273</WarningsAsErrors>
			  </PropertyGroup>
			</Project>
			""";

		const string FieldDriver = """
			public static class Driver
			{
				public static void Main()
				{
					Box box = new Box();
					int before = box.Field;
					box.Field = 7;
					System.Console.Write($"{before} {box.Field} {box.Twice}");
				}
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			string source = new CSharpGenerator().Generate(file);
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), ProgramProject);
			File.WriteAllText(Path.Combine(directory, "Driver.cs"), FieldDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "run -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
			StringAssert.EndsWith(output.TrimEnd(), "42 7 2", source);
		});
	}

	/// <summary>
	/// Builds a struct <c>Point { x, y }</c>, a struct <c>Frame</c> whose <c>origin</c> field starts
	/// at a typeless list naming Point's members, and a function <c>sum</c> whose local is
	/// initialised the same way (#473).
	/// </summary>
	/// <returns>The source file.</returns>
	private static SourceFile DesignatedLists()
	{
		static ConstructionExpression Designated(int x, int y)
		{
			ConstructionExpression list = new();
			list.Arguments.Add(new MemberInitialiser("x", Literal.Number(x)));
			list.Arguments.Add(new MemberInitialiser("y", Literal.Number(y)));
			return list;
		}

		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct, Visibility = Visibility.Public };
		point.Members.Add(new FieldDeclaration("x", "int") { Visibility = Visibility.Public });
		point.Members.Add(new FieldDeclaration("y", "int") { Visibility = Visibility.Public });

		ClassDeclaration frame = new("Frame") { Kind = TypeDeclarationKind.Struct, Visibility = Visibility.Public };
		frame.Members.Add(new FieldDeclaration("origin", "Point") { Visibility = Visibility.Public, InitialValue = Designated(3, 4) });
		frame.Members.Add(new FunctionDeclaration("Frame") { Kind = FunctionKind.Constructor, Visibility = Visibility.Public });

		FunctionDeclaration sum = new("sum") { ReturnType = "int", Visibility = Visibility.Public, IsStatic = true };
		sum.Body.Add(new VariableDeclaration("p", "Point", Designated(1, 2)));
		sum.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("p.x"), BinaryOperator.Add, new VariableReference("p.y"))));
		frame.Members.Add(sum);

		SourceFile file = new("designated");
		file.Members.Add(point);
		file.Members.Add(frame);
		return file;
	}

	/// <summary>
	/// Tests that a typeless list naming its members, initialising a local and a field, compiles as
	/// a target-typed <c>new() { … }</c> rather than bare braces, which C# takes only for an array
	/// (CS0622), and that the values arrive (#473).
	/// </summary>
	[TestMethod]
	public void TypelessDesignatedLists_Compile()
	{
		string source = new CSharpGenerator().Generate(DesignatedLists());
		Assert.Contains("Point p = new() { x = 1, y = 2 };", source, StringComparison.Ordinal, source);
		Assert.Contains("Point origin = new() { x = 3, y = 4 };", source, StringComparison.Ordinal, source);

		if (ToolchainHarness.FindOnPath("--version", "dotnet") is null)
		{
			Assert.Inconclusive("No dotnet on the path, so nothing was compiled.");
			return;
		}

		const string DesignatedDriver = """
			public static class DesignatedDriver
			{
				public static int Use() => Frame.sum() + new Frame().origin.x + new Frame().origin.y;
			}
			""";

		ToolchainHarness.InTemporaryDirectory(directory =>
		{
			File.WriteAllText(Path.Combine(directory, "Generated.csproj"), Project);
			File.WriteAllText(Path.Combine(directory, "DesignatedDriver.cs"), DesignatedDriver);
			File.WriteAllText(Path.Combine(directory, "Generated.cs"), source);

			(int exitCode, string output) = ToolchainHarness.Run("dotnet", "build -nologo", directory);
			Assert.AreEqual(0, exitCode, $"C# rejected the generated source:{Environment.NewLine}{output}{Environment.NewLine}{source}");
		});
	}
}
