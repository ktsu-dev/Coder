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
}
