// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a local's declared type decides how a shift is cast only inside the body that
/// declared it (#320).
/// </summary>
/// <remarks>
/// C# casts a shift's count to <c>int</c> unless its declared type is known to be <c>int</c>. A local
/// that outlived its body lent its type to a field of the same name, so <c>x &lt;&lt; s</c> over a
/// <c>long</c> field lost its cast and failed with CS0019 — in a later document as well, since a
/// generator is reused for every one.
/// </remarks>
[TestClass]
public class CSharpShiftCountScopeTests
{
	/// <summary>
	/// A class with a <c>long</c> field <c>s</c> and a method shifting by it.
	/// </summary>
	/// <returns>The class.</returns>
	private static ClassDeclaration Bits()
	{
		ClassDeclaration bits = new("Bits");
		bits.Members.Add(new FieldDeclaration("s", "long"));
		FunctionDeclaration shift = new("Shift") { ReturnType = "long" };
		shift.Parameters.Add(new Parameter("x", "long"));
		shift.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), BinaryOperator.LeftShift, new VariableReference("s"))));
		bits.Members.Add(shift);
		return bits;
	}

	/// <summary>
	/// An entry point whose body declares <c>int s</c>.
	/// </summary>
	/// <returns>The entry point.</returns>
	private static EntryPoint DeclaringAnIntS()
	{
		EntryPoint entryPoint = new();
		entryPoint.Body.Add(new VariableDeclaration("s", "int", Literal.Number(1)));
		return entryPoint;
	}

	/// <summary>
	/// Tests that a local declared in an entry point does not reach a later generation.
	/// </summary>
	[TestMethod]
	public void EntryPointLocal_DoesNotReachALaterDocument()
	{
		CSharpGenerator generator = new();
		generator.Generate(DeclaringAnIntS());

		string source = generator.Generate(Bits());

		Assert.Contains("(x << (int)(s))", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that a local declared in an entry point does not reach a class after it in the same file.
	/// </summary>
	[TestMethod]
	public void EntryPointLocal_DoesNotReachALaterClass()
	{
		SourceFile file = new("Generated");
		file.Members.Add(DeclaringAnIntS());
		file.Members.Add(Bits());

		string source = new CSharpGenerator().Generate(file);

		Assert.Contains("(x << (int)(s))", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that a local declared in a property accessor does not reach a method after it.
	/// </summary>
	[TestMethod]
	public void AccessorLocal_DoesNotReachALaterMethod()
	{
		ClassDeclaration holder = new("Holder");
		PropertyDeclaration property = new("Value", "int");
		property.GetterBody.Add(new VariableDeclaration("s", "int", Literal.Number(1)));
		property.GetterBody.Add(new ReturnStatement(new VariableReference("s")));
		holder.Members.Add(property);

		CSharpGenerator generator = new();
		generator.Generate(holder);

		string source = generator.Generate(Bits());

		Assert.Contains("(x << (int)(s))", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that a local generated on its own does not reach a later generation.
	/// </summary>
	[TestMethod]
	public void LocalGeneratedAlone_DoesNotReachALaterDocument()
	{
		CSharpGenerator generator = new();
		generator.Generate(new VariableDeclaration("s", "int", Literal.Number(1)));

		string source = generator.Generate(Bits());

		Assert.Contains("(x << (int)(s))", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that a local still decides the cast of a shift in its own body.
	/// </summary>
	[TestMethod]
	public void EntryPointLocal_StillDecidesItsOwnShift()
	{
		EntryPoint entryPoint = DeclaringAnIntS();
		entryPoint.Body.Add(new VariableDeclaration("y", "long", new BinaryExpression(new VariableReference("a"), BinaryOperator.LeftShift, new VariableReference("s"))));

		string source = new CSharpGenerator().Generate(entryPoint);

		Assert.Contains("(a << s)", source, StringComparison.Ordinal, source);
	}
}
