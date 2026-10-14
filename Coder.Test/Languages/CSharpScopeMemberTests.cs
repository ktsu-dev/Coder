// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the fields, functions and variables of a namespace or a file are written into a static
/// class, which is the only place C# can declare them.
/// </summary>
/// <remarks>
/// C# has no member outside a type: one written straight into a namespace fails with CS0116, and one
/// at file scope with CS0106. <see cref="CSharpGeneratedSourceCompilesTests"/> builds the same shapes.
/// </remarks>
[TestClass]
public class CSharpScopeMemberTests
{
	/// <summary>
	/// Builds the namespace from the issue: a constant and a free function.
	/// </summary>
	/// <returns>The namespace.</returns>
	internal static NamespaceDeclaration Geometry()
	{
		NamespaceDeclaration geometry = new("Geometry");
		geometry.Members.Add(new FieldDeclaration("Origins", new TypeReference("int")) { IsConstant = true, InitialValue = Literal.Number(2) });

		FunctionDeclaration twice = new("twice") { ReturnType = "int" };
		twice.Parameters.Add(new Parameter("n", "int"));
		twice.Body.Add(new VariableDeclaration("doubled", "int", new BinaryExpression(new VariableReference("n"), BinaryOperator.Multiply, Literal.Number(2))));
		twice.Body.Add(new ReturnStatement(new VariableReference("doubled")));
		geometry.Members.Add(twice);

		geometry.Members.Add(new ClassDeclaration("Point"));
		geometry.Members.Add(new VariableDeclaration("count", "int", Literal.Number(0)));
		return geometry;
	}

	/// <summary>
	/// Tests that a namespace's fields, functions and variables are static members of a class named
	/// after it, and that its types stay where they are.
	/// </summary>
	[TestMethod]
	public void NamespaceMembers_AreHeldInAStaticClass()
	{
		string code = new CSharpGenerator().Generate(Geometry());

		Assert.Contains("public static class Geometry", code, StringComparison.Ordinal, code);
		Assert.Contains("public static readonly int Origins = 2;", code, StringComparison.Ordinal, code);
		Assert.Contains("public static int twice(int n)", code, StringComparison.Ordinal, code);
		Assert.Contains("public static int count = 0;", code, StringComparison.Ordinal, code);
		Assert.Contains("public class Point", code, StringComparison.Ordinal, code);

		// The holder is written once, where the first of its members was, and the type after it is
		// outside it rather than nested.
		Assert.AreEqual(1, code.Split("static class").Length - 1, code);
		Assert.IsLessThan(code.IndexOf("public class Point", StringComparison.Ordinal), code.IndexOf("static class Geometry", StringComparison.Ordinal), code);
		Assert.IsGreaterThan(code.IndexOf("public static int twice", StringComparison.Ordinal), code.IndexOf("public static int count", StringComparison.Ordinal), code);
	}

	/// <summary>
	/// Tests that a local inside a held function is not made static.
	/// </summary>
	[TestMethod]
	public void LocalsOfAHeldFunction_StayLocals()
	{
		string code = new CSharpGenerator().Generate(Geometry());

		Assert.Contains("int doubled = (n * 2);", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("static int doubled", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a held constant variable is <c>const</c> and not also <c>static</c> (CS0504).
	/// </summary>
	[TestMethod]
	public void HeldConstantVariable_IsConstWithoutStatic()
	{
		NamespaceDeclaration limits = new("Limits");
		limits.Members.Add(new VariableDeclaration("Max", "int", Literal.Number(9)) { IsConstant = true });

		string code = new CSharpGenerator().Generate(limits);

		Assert.Contains("public const int Max = 9;", code, StringComparison.Ordinal, code);
		Assert.DoesNotContain("static const", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that the holder takes another name when a type in the namespace already has its own.
	/// </summary>
	[TestMethod]
	public void HolderName_AvoidsATypeOfTheSameName()
	{
		NamespaceDeclaration geometry = new("Shapes.Geometry");
		geometry.Members.Add(new ClassDeclaration("Geometry"));
		geometry.Members.Add(new FieldDeclaration("Sides", new TypeReference("int")) { IsConstant = true, InitialValue = Literal.Number(4) });

		string code = new CSharpGenerator().Generate(geometry);

		Assert.Contains("public class Geometry", code, StringComparison.Ordinal, code);
		Assert.Contains("public static class GeometryGlobals", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a file's own fields and functions are held in a static class too.
	/// </summary>
	[TestMethod]
	public void FileMembers_AreHeldInAStaticClass()
	{
		SourceFile file = new("tools");
		FunctionDeclaration half = new("half") { ReturnType = "int" };
		half.Parameters.Add(new Parameter("n", "int"));
		half.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("n"), BinaryOperator.Divide, Literal.Number(2))));
		file.Members.Add(half);
		file.Members.Add(new ClassDeclaration("Tool"));

		string code = new CSharpGenerator().Generate(file);

		Assert.Contains("public static class Globals", code, StringComparison.Ordinal, code);
		Assert.Contains("public static int half(int n)", code, StringComparison.Ordinal, code);
		Assert.Contains("public class Tool", code, StringComparison.Ordinal, code);
	}

	/// <summary>
	/// Tests that a namespace holding only types gets no holder.
	/// </summary>
	[TestMethod]
	public void NamespaceOfTypes_HasNoHolder()
	{
		NamespaceDeclaration shapes = new("Shapes");
		shapes.Members.Add(new ClassDeclaration("Square"));

		string code = new CSharpGenerator().Generate(shapes);

		Assert.DoesNotContain("static class", code, StringComparison.Ordinal, code);
	}
}
