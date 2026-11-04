// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that an operator declared as a friend is written with the operands it declares, and not
/// with the instance added in front of them (#231).
/// </summary>
/// <remarks>
/// A friend is declared beside the type rather than as a member of it, so it already names every
/// operand. Each target below would otherwise take the instance as well, which turns a binary
/// operator into one with three operands. The compile tests prove each spelling is accepted; these
/// pin which spelling it is.
/// </remarks>
[TestClass]
public class FriendOperatorTests
{
	/// <summary>
	/// Tests that C# writes a friend as a static operator over its two declared operands.
	/// </summary>
	[TestMethod]
	public void CSharpTakesOnlyTheDeclaredOperands()
	{
		string generated = new CSharpGenerator().Generate(PointWithFriend());

		StringAssert.Contains(generated, "public static Point operator *(Point a, Point b)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that Rust takes the first operand as self and rebinds it to its declared name.
	/// </summary>
	[TestMethod]
	public void RustTakesTheFirstOperandAsSelf()
	{
		string generated = new RustGenerator().Generate(PointWithFriend());

		StringAssert.Contains(generated, "fn mul(self, b: Point) -> Point", StringComparison.Ordinal);
		StringAssert.Contains(generated, "let a = self;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that Go writes a friend as a function of the type's name rather than a method.
	/// </summary>
	[TestMethod]
	public void GoWritesAFunctionRatherThanAMethod()
	{
		string generated = new GoGenerator().Generate(PointWithFriend());

		StringAssert.Contains(generated, "func PointMultiply(a Point, b Point) Point", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that C gives a friend no instance parameter.
	/// </summary>
	[TestMethod]
	public void CTakesNoInstance()
	{
		string generated = new CGenerator().Generate(PointWithFriend());

		StringAssert.Contains(generated, "(Point a, Point b)", StringComparison.Ordinal);
		Assert.IsFalse(generated.Contains("self, Point a", StringComparison.Ordinal), generated);
	}

	private static SourceFile PointWithFriend()
	{
		ClassDeclaration point = CompiledExemplar.Point();
		point.Members.Add(CompiledExemplar.Times());

		SourceFile file = new("point");
		file.Members.Add(point);
		return file;
	}
}
