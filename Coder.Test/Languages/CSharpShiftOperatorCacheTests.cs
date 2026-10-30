// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that whether a document declares a shift operator is worked out again for every
/// generation, not remembered from an earlier one of the same tree (#333).
/// </summary>
/// <remarks>
/// A shift count whose type C# cannot read is cast to <c>int</c> unless the document declares a
/// <c>&lt;&lt;</c> or <c>&gt;&gt;</c> of its own. The editor edits its tree in place and regenerates
/// it on the same generator, so an answer remembered by the root alone went stale: removing the
/// operator kept the cast off (CS0019 on a <c>long</c> count), and adding one kept it on (CS0030 on a
/// count of the operator's type).
/// </remarks>
[TestClass]
public class CSharpShiftOperatorCacheTests
{
	/// <summary>
	/// A file holding a class that shifts by a <c>long</c> field and a struct declaring
	/// <c>operator &lt;&lt;</c>.
	/// </summary>
	/// <param name="other">The struct, so a test can take it out of the file again.</param>
	/// <returns>The file.</returns>
	private static SourceFile File(out ClassDeclaration other)
	{
		ClassDeclaration bits = new("Bits");
		bits.Members.Add(new FieldDeclaration("s", "long"));
		FunctionDeclaration shift = new("Shift") { ReturnType = "long" };
		shift.Parameters.Add(new Parameter("x", "long"));
		shift.Body.Add(new ReturnStatement(new BinaryExpression(new VariableReference("x"), BinaryOperator.LeftShift, new VariableReference("s"))));
		bits.Members.Add(shift);

		other = new ClassDeclaration("Other") { Kind = TypeDeclarationKind.Struct };
		FunctionDeclaration shiftOther = new("<<") { Kind = FunctionKind.Operator, ReturnType = "Other", IsStatic = true };
		shiftOther.Parameters.Add(new Parameter("value", "Other"));
		shiftOther.Parameters.Add(new Parameter("count", "Other"));
		shiftOther.Body.Add(new ReturnStatement(new VariableReference("value")));
		other.Members.Add(shiftOther);

		SourceFile file = new("Generated");
		file.Members.Add(bits);
		return file;
	}

	/// <summary>
	/// Tests that removing the shift operator from a tree already generated puts the cast back.
	/// </summary>
	[TestMethod]
	public void RemovingTheOperator_PutsTheCastBack()
	{
		SourceFile file = File(out ClassDeclaration other);
		file.Members.Add(other);

		CSharpGenerator generator = new();
		generator.Generate(file);
		file.Members.Remove(other);

		string source = generator.Generate(file);

		Assert.AreEqual(new CSharpGenerator().Generate(file), source);
		Assert.Contains("(x << (int)(s))", source, StringComparison.Ordinal, source);
	}

	/// <summary>
	/// Tests that adding a shift operator to a tree already generated takes the cast off.
	/// </summary>
	[TestMethod]
	public void AddingTheOperator_TakesTheCastOff()
	{
		SourceFile file = File(out ClassDeclaration other);

		CSharpGenerator generator = new();
		generator.Generate(file);
		file.Members.Add(other);

		string source = generator.Generate(file);

		Assert.AreEqual(new CSharpGenerator().Generate(file), source);
		Assert.Contains("(x << s)", source, StringComparison.Ordinal, source);
	}
}
