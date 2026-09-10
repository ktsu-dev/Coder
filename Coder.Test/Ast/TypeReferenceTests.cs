// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="TypeReference"/>: what it reads, what it writes back, and that each
/// generator spells the same type its own language's way.
/// </summary>
/// <remarks>
/// A type used to be a bare string, which every generator could only paste. These are the tests that
/// say it is no longer one — the last of them is the case that motivated the change, a parameter no
/// string-shaped property could describe without the caller writing C++ by hand.
/// </remarks>
[TestClass]
public class TypeReferenceTests
{
	/// <summary>
	/// A name with nothing around it is a name.
	/// </summary>
	[TestMethod]
	public void Parse_ReadsAPlainName()
	{
		TypeReference type = TypeReference.Parse("int");

		Assert.AreEqual("int", type.Name);
		Assert.IsEmpty(type.TypeArguments);
		Assert.IsFalse(type.IsReadOnly);
		Assert.AreEqual(TypeIndirection.None, type.Indirection);
	}

	/// <summary>
	/// A qualified name is one name. Which separator a language uses is the generator's business, so
	/// the AST does not split it into parts it would only have to join again.
	/// </summary>
	[TestMethod]
	public void Parse_KeepsAQualifiedNameWhole()
	{
		Assert.AreEqual("std::vector", TypeReference.Parse("std::vector").Name);
		Assert.AreEqual("System.Collections.Generic.List", TypeReference.Parse("System.Collections.Generic.List").Name);
	}

	/// <summary>
	/// The argument list is a list, which is the whole difference from a string.
	/// </summary>
	[TestMethod]
	public void Parse_ReadsArgumentsInOrder()
	{
		TypeReference type = TypeReference.Parse("Dictionary<string, List<int>>");

		Assert.AreEqual("Dictionary", type.Name);
		Assert.HasCount(2, type.TypeArguments);
		Assert.AreEqual("string", type.TypeArguments[0].Name);
		Assert.AreEqual("List", type.TypeArguments[1].Name);
		Assert.AreEqual("int", type.TypeArguments[1].TypeArguments[0].Name);
	}

	/// <summary>
	/// Read-only-ness and indirection are properties of the type, including of a type nested inside
	/// another one's argument list.
	/// </summary>
	[TestMethod]
	public void Parse_ReadsQualifiersAtEveryDepth()
	{
		TypeReference outer = TypeReference.Parse("const RigidBody&");

		Assert.IsTrue(outer.IsReadOnly);
		Assert.AreEqual(TypeIndirection.Reference, outer.Indirection);

		TypeReference element = TypeReference.Parse("std::span<const Velocity>").TypeArguments[0];

		Assert.IsTrue(element.IsReadOnly);
		Assert.AreEqual("Velocity", element.Name);
	}

	/// <summary>
	/// <c>readonly</c> is accepted for the same thing, because the AST names the property for what it
	/// means rather than for C++'s spelling of it.
	/// </summary>
	[TestMethod]
	public void Parse_AcceptsEitherQualifierKeyword()
	{
		Assert.AreEqual(TypeReference.Parse("const Body"), TypeReference.Parse("readonly Body"));
	}

	/// <summary>
	/// A name that merely starts with a keyword is a name.
	/// </summary>
	[TestMethod]
	public void Parse_DoesNotMistakeANameForAQualifier()
	{
		TypeReference type = TypeReference.Parse("constant");

		Assert.AreEqual("constant", type.Name);
		Assert.IsFalse(type.IsReadOnly);
	}

	/// <summary>
	/// Whatever the grammar can read, it writes back the same.
	/// </summary>
	[TestMethod]
	[DataRow("int")]
	[DataRow("std::vector")]
	[DataRow("List<int>")]
	[DataRow("Dictionary<string, List<int>>")]
	[DataRow("const RigidBody&")]
	[DataRow("std::span<const Velocity>")]
	[DataRow("Body*")]
	public void ToString_IsTheInverseOfParse(string text) =>
		Assert.AreEqual(text, TypeReference.Parse(text).ToString());

	/// <summary>
	/// Text the grammar cannot read survives intact rather than being dropped or half-read.
	/// </summary>
	/// <remarks>
	/// This is what lets the string-shaped API keep working: a type nobody anticipated is wrong-but
	/// -lossless, and a half-read one would be neither.
	/// </remarks>
	[TestMethod]
	[DataRow("List<int")]
	[DataRow("<>")]
	[DataRow("a b c")]
	public void Parse_KeepsTextItCannotRead(string text)
	{
		TypeReference type = TypeReference.Parse(text);

		Assert.AreEqual(text, type.Name);
		Assert.AreEqual(text, type.ToString());
	}

	/// <summary>
	/// Two types describing the same thing are equal, and equal types hash together.
	/// </summary>
	[TestMethod]
	public void Equality_ComparesTheWholeType()
	{
		TypeReference span = TypeReference.Parse("std::span<const Velocity>");

		Assert.AreEqual(span, TypeReference.Parse("std::span<const Velocity>"));
		Assert.AreEqual(span.GetHashCode(), TypeReference.Parse("std::span<const Velocity>").GetHashCode());
		Assert.AreNotEqual(span, TypeReference.Parse("std::span<Velocity>"));
		Assert.AreNotEqual(span, TypeReference.Parse("std::span<const Position>"));
	}

	/// <summary>
	/// A clone shares no argument with its original, so editing one does not edit the other.
	/// </summary>
	[TestMethod]
	public void Clone_CopiesTheArgumentsRatherThanSharingThem()
	{
		TypeReference original = TypeReference.Parse("List<int>");
		TypeReference clone = original.Clone();

		Assert.AreEqual(original, clone);
		Assert.AreNotSame(original.TypeArguments[0], clone.TypeArguments[0]);

		clone.TypeArguments[0].Name = "double";

		Assert.AreEqual("int", original.TypeArguments[0].Name);
	}

	/// <summary>
	/// A name may be written where a type is wanted, which is what keeps the common case short.
	/// </summary>
	[TestMethod]
	public void StringConversion_ReadsTheText()
	{
		Parameter parameter = new("scale") { Type = "List<int>" };

		Assert.AreEqual("List", parameter.Type?.Name);
		Assert.HasCount(1, parameter.Type!.TypeArguments);

		parameter.Type = null;

		Assert.IsNull(parameter.Type);
	}

	/// <summary>
	/// A node keeps its own copy of a type, so cloning it does not share one.
	/// </summary>
	[TestMethod]
	public void Clone_OfANodeCopiesItsType()
	{
		FunctionDeclaration original = new("count") { ReturnType = "List<int>" };
		FunctionDeclaration clone = (FunctionDeclaration)original.Clone();

		Assert.AreEqual(original.ReturnType, clone.ReturnType);
		Assert.AreNotSame(original.ReturnType, clone.ReturnType);
	}

	/// <summary>
	/// The case the change exists for: a borrowed sequence of read-only elements, which no bare
	/// string could describe without the caller spelling C++ themselves.
	/// </summary>
	[TestMethod]
	public void Cpp_SpellsABorrowedSequenceOfReadOnlyElements()
	{
		FunctionDeclaration integrate = new("integrate") { ReturnType = "void" };
		integrate.Parameters.Add(new Parameter("velocities")
		{
			Type = new TypeReference("std::span")
			{
				TypeArguments = { new TypeReference("Velocity") { IsReadOnly = true } },
			},
		});
		integrate.Parameters.Add(new Parameter("positions")
		{
			Type = new TypeReference("std::span") { TypeArguments = { new TypeReference("Position") } },
		});

		string code = new CppGenerator().Generate(integrate);

		Assert.Contains("std::span<const Velocity> velocities", code, StringComparison.Ordinal);
		Assert.Contains("std::span<Position> positions", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// Every language spells the same type its own way, which is the point of holding it as structure
	/// rather than as text.
	/// </summary>
	/// <remarks>
	/// C++ maps the container's name and keeps the argument, which is what a name-to-name mapping
	/// buys: the table used to hold whole types, so the name and its arguments could not both be
	/// honoured and a <c>list&lt;int&gt;</c> came out as neither.
	/// </remarks>
	[TestMethod]
	public void EveryGenerator_SpellsAParameterisedTypeItsOwnWay()
	{
		FunctionDeclaration function = new("items") { ReturnType = "List<int>" };

		Assert.Contains("List<int> items", new CSharpGenerator().Generate(function), StringComparison.Ordinal);
		Assert.Contains("std::vector<int> items", new CppGenerator().Generate(function), StringComparison.Ordinal);
		Assert.Contains("-> List[int]", new PythonGenerator().Generate(function), StringComparison.Ordinal);
	}
}
