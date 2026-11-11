// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Serialization;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;

/// <summary>
/// Tests that a construction keeps its <see cref="Expression.ExpectedType"/> through
/// <see cref="AstNode.Clone"/> and a YAML round trip.
/// </summary>
/// <remarks>
/// A braced list with no type of its own reads its type from <c>ExpectedType</c>, so losing it turned
/// a C local typed <c>Point</c> into <c>void*</c> after a save and reload or a clone
/// (ktsu-dev/Coder#237).
/// </remarks>
[TestClass]
public class ConstructionExpectedTypeTests
{
	private const string ExpectedType = "Point";

	[TestMethod]
	public void Clone_Construction_KeepsExpectedType()
	{
		ConstructionExpression construction = TypelessPoint();

		ConstructionExpression clone = Assert.IsInstanceOfType<ConstructionExpression>(construction.Clone());

		Assert.AreEqual(ExpectedType, clone.ExpectedType);
	}

	[TestMethod]
	public void RoundTrip_Construction_KeepsExpectedType()
	{
		ConstructionExpression construction = TypelessPoint();

		ConstructionExpression restored = Assert.IsInstanceOfType<ConstructionExpression>(RoundTrip(construction));

		Assert.AreEqual(ExpectedType, restored.ExpectedType);
	}

	[TestMethod]
	public void RoundTrip_Construction_WithoutExpectedType_StaysWithout()
	{
		ConstructionExpression construction = new(null);

		string yaml = new YamlSerializer().Serialize(construction);
		ConstructionExpression restored = Assert.IsInstanceOfType<ConstructionExpression>(RoundTrip(construction));

		Assert.DoesNotContain("expectedType", yaml);
		Assert.IsNull(restored.ExpectedType);
	}

	[TestMethod]
	public void CGenerator_TypedLocal_IsTheSameAfterCloneAndRoundTrip()
	{
		FunctionDeclaration function = Function();
		string original = new CGenerator().Generate(function);

		Assert.Contains("Point p = ", original);
		Assert.AreEqual(original, new CGenerator().Generate(function.Clone()));
		Assert.AreEqual(original, new CGenerator().Generate(RoundTrip(function)));
	}

	private static ConstructionExpression TypelessPoint()
	{
		ConstructionExpression construction = new(null) { ExpectedType = ExpectedType };
		construction.Arguments.Add(new MemberInitialiser("x", new LiteralExpression<int>(1)));
		return construction;
	}

	private static FunctionDeclaration Function()
	{
		FunctionDeclaration function = new("make") { ReturnType = "void" };
		function.Body.Add(new VariableDeclaration("p", null, TypelessPoint()));
		return function;
	}

	private static AstNode RoundTrip(AstNode node)
	{
		string yaml = new YamlSerializer().Serialize(node);
		AstNode? restored = new YamlDeserializer().Deserialize(yaml);
		Assert.IsNotNull(restored, yaml);
		return restored;
	}
}
