// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="CallExpression"/> and its generation in every target language.
/// </summary>
[TestClass]
public class CallExpressionTests
{
	/// <summary>
	/// Tests that a free function keeps its callee and arguments and has no receiver.
	/// </summary>
	[TestMethod]
	public void CallExpression_ShouldCreateCorrectStructure()
	{
		CallExpression call = new("sqrt")
		{
			Arguments = { new VariableReference("x") },
		};

		Assert.AreEqual("sqrt", call.Callee);
		Assert.IsNull(call.Receiver);
		Assert.HasCount(1, call.Arguments);
	}

	/// <summary>
	/// Tests that a member call keeps the receiver it was constructed with.
	/// </summary>
	[TestMethod]
	public void CallExpression_KeepsItsReceiver()
	{
		VariableReference receiver = new("point");

		CallExpression call = new(receiver, "translate");

		Assert.AreSame(receiver, call.Receiver);
		Assert.AreEqual("translate", call.Callee);
	}

	/// <summary>
	/// Tests that a null callee or receiver is refused, since every emitter writes both unconditionally.
	/// </summary>
	[TestMethod]
	public void CallExpression_RejectsNullArguments()
	{
		Assert.ThrowsExactly<ArgumentNullException>(() => new CallExpression(null!));
		Assert.ThrowsExactly<ArgumentNullException>(() => new CallExpression(null!, "translate"));
		Assert.ThrowsExactly<ArgumentNullException>(() => new CallExpression(new VariableReference("point"), null!));
	}

	/// <summary>
	/// Tests that cloning copies the callee and metadata and deep-copies the receiver and arguments.
	/// </summary>
	[TestMethod]
	public void CallExpression_Clone_IsDeep()
	{
		CallExpression original = new(new VariableReference("point"), "translate")
		{
			ExpectedType = "void",
			Arguments = { new VariableReference("dx"), Literal.Number(2) },
		};
		original.Metadata["origin"] = "test";

		CallExpression clone = (CallExpression)original.Clone();

		Assert.AreEqual("translate", clone.Callee);
		Assert.AreEqual("void", clone.ExpectedType);
		Assert.AreEqual("test", clone.Metadata["origin"]);
		Assert.IsNotNull(clone.Receiver);
		Assert.AreNotSame(original.Receiver, clone.Receiver);
		Assert.AreEqual("point", ((VariableReference)clone.Receiver).Name);
		Assert.HasCount(2, clone.Arguments);
		Assert.AreNotSame(original.Arguments[0], clone.Arguments[0]);
	}

	/// <summary>
	/// Tests that a free function is spelled identically by every generator, C included: with no
	/// receiver there is nothing for the languages to disagree about.
	/// </summary>
	[TestMethod]
	public void FreeFunctionCall_IsSpelledIdenticallyByEveryGenerator()
	{
		CallExpression call = new("sqrt")
		{
			Arguments = { new VariableReference("x") },
		};

		Assert.AreEqual("sqrt(x)", new CSharpGenerator().Generate(call), "C#");
		Assert.AreEqual("sqrt(x)", new CppGenerator().Generate(call), "C++");
		Assert.AreEqual("sqrt(x)", new CGenerator().Generate(call), "C");
		Assert.AreEqual("sqrt(x)", new PythonGenerator().Generate(call), "Python");
		Assert.AreEqual("sqrt(x)", new JavaScriptGenerator().Generate(call), "JavaScript");
		Assert.AreEqual("sqrt(x)", new RustGenerator().Generate(call), "Rust");
	}

	/// <summary>
	/// Tests that a call with no arguments still writes its parentheses, which is what distinguishes
	/// calling something from naming it.
	/// </summary>
	[TestMethod]
	public void CallWithNoArguments_StillWritesItsParentheses() =>
		Assert.AreEqual("reset()", new CSharpGenerator().Generate(new CallExpression("reset")));

	/// <summary>
	/// Tests that arguments are separated by a comma and a space, in the order they were added.
	/// </summary>
	[TestMethod]
	public void Arguments_AreWrittenInOrder()
	{
		CallExpression call = new("clamp")
		{
			Arguments = { new VariableReference("value"), Literal.Number(0), Literal.Number(1) },
		};

		Assert.AreEqual("clamp(value, 0, 1)", new CppGenerator().Generate(call));
	}

	/// <summary>
	/// Tests that five of the six targets write a member call with a dot.
	/// </summary>
	[TestMethod]
	public void MemberCall_IsWrittenWithADotByEveryLanguageThatHasMembers()
	{
		CallExpression call = new(new VariableReference("point"), "translate")
		{
			Arguments = { new VariableReference("dx") },
		};

		Assert.AreEqual("point.translate(dx)", new CSharpGenerator().Generate(call), "C#");
		Assert.AreEqual("point.translate(dx)", new CppGenerator().Generate(call), "C++");
		Assert.AreEqual("point.translate(dx)", new PythonGenerator().Generate(call), "Python");
		Assert.AreEqual("point.translate(dx)", new JavaScriptGenerator().Generate(call), "JavaScript");
		Assert.AreEqual("point.translate(dx)", new RustGenerator().Generate(call), "Rust");
	}

	/// <summary>
	/// Tests that C turns the receiver into the first argument, matching the lowering it already
	/// performs on a member function's declaration.
	/// </summary>
	/// <remarks>
	/// This is the case the node exists for: the same tree comes out as two different shapes, which
	/// is not something a caller passing the call as text could get.
	/// </remarks>
	[TestMethod]
	public void C_PassesTheReceiverAsTheFirstArgument()
	{
		CallExpression call = new(new VariableReference("point"), "Point_translate")
		{
			Arguments = { new VariableReference("dx"), new VariableReference("dy") },
		};

		Assert.AreEqual("Point_translate(&point, dx, dy)", new CGenerator().Generate(call));
	}

	/// <summary>
	/// Tests that C writes no trailing comma when the receiver is the only argument.
	/// </summary>
	[TestMethod]
	public void C_WritesNoSeparatorWhenTheReceiverIsTheOnlyArgument()
	{
		CallExpression call = new(new VariableReference("point"), "Point_reset");

		Assert.AreEqual("Point_reset(&point)", new CGenerator().Generate(call));
	}

	/// <summary>
	/// Tests that the receiver and the arguments are recursed into rather than stringified, so a
	/// call can be made on the result of another one.
	/// </summary>
	[TestMethod]
	public void CallExpression_RecursesIntoCompoundOperands()
	{
		CallExpression inner = new("origin");
		CallExpression outer = new(inner, "translate")
		{
			Arguments =
			{
				new BinaryExpression(new VariableReference("a"), BinaryOperator.Add, new VariableReference("b")),
			},
		};

		Assert.AreEqual("origin().translate((a + b))", new CSharpGenerator().Generate(outer));
	}

	/// <summary>
	/// Tests that a call survives a round trip through YAML with its callee, receiver and arguments.
	/// </summary>
	[TestMethod]
	public void CallExpression_RoundTripsThroughYaml()
	{
		CallExpression original = new(new VariableReference("point"), "translate")
		{
			ExpectedType = "void",
			Arguments = { new VariableReference("dx"), Literal.Number(3) },
		};

		string yaml = new YamlSerializer().Serialize(original);
		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		Assert.IsInstanceOfType<CallExpression>(deserialized);

		CallExpression roundTripped = (CallExpression)deserialized;
		Assert.AreEqual("translate", roundTripped.Callee);
		Assert.AreEqual("void", roundTripped.ExpectedType);
		Assert.IsInstanceOfType<VariableReference>(roundTripped.Receiver);
		Assert.AreEqual("point", ((VariableReference)roundTripped.Receiver!).Name);
		Assert.HasCount(2, roundTripped.Arguments);
		Assert.AreEqual("point.translate(dx, 3)", new CSharpGenerator().Generate(roundTripped));
	}

	/// <summary>
	/// Tests that a free function round-trips without gaining a receiver, so the absence of one is
	/// carried rather than defaulted.
	/// </summary>
	[TestMethod]
	public void CallExpression_WithNoReceiver_RoundTripsWithoutGainingOne()
	{
		string yaml = new YamlSerializer().Serialize(new CallExpression("sqrt")
		{
			Arguments = { new VariableReference("x") },
		});

		AstNode? deserialized = new YamlDeserializer().Deserialize(yaml);

		Assert.IsInstanceOfType<CallExpression>(deserialized);
		Assert.IsNull(((CallExpression)deserialized).Receiver);
	}

	/// <summary>
	/// Tests that every generator accepts a call, so none of them can be handed one it would refuse.
	/// </summary>
	[TestMethod]
	public void EveryGenerator_AcceptsACall()
	{
		CallExpression call = new("sqrt");

		foreach (ILanguageGenerator generator in
			new ILanguageGenerator[] { new CSharpGenerator(), new CppGenerator(), new CGenerator(), new PythonGenerator(), new JavaScriptGenerator(), new RustGenerator() })
		{
			Assert.IsTrue(generator.CanGenerate(call), $"{generator.DisplayName} should accept a call");
		}
	}
}
