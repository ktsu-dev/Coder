// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that the declarations a generated header is made of are reachable through
/// <see cref="AstSchema"/>, so the editor can walk and rewire them.
/// </summary>
/// <remarks>
/// A node type the schema does not know about is one the graph cannot draw and the editor cannot
/// touch — it exists only for whoever builds one in code. Every slot is exercised the same way the
/// editor uses it: read what is in it, put something there, swap that for something else, and take it
/// out again.
/// </remarks>
[TestClass]
public class DeclarationSlotsTests
{
	/// <summary>
	/// A file, a namespace and a class each hold members, and each takes one.
	/// </summary>
	[TestMethod]
	public void Members_AreReachableOnEveryContainer()
	{
		SourceFile file = new("RigidBody.gen.hpp");
		NamespaceDeclaration components = new("holo::components");
		EnumDeclaration kind = new("BodyKind");

		Assert.IsTrue(AstSchema.TryAttach(file, AstSchema.SlotsOf(file)[0], components));
		Assert.IsTrue(AstSchema.TryAttach(components, AstSchema.SlotsOf(components)[0], new ClassDeclaration("RigidBody")));
		Assert.IsTrue(AstSchema.TryAttach(kind, AstSchema.SlotsOf(kind)[0], new EnumMember("Static")));

		Assert.HasCount(1, AstSchema.ChildrenOf(file, AstSchema.SlotsOf(file)[0]));
		Assert.HasCount(1, AstSchema.ChildrenOf(components, AstSchema.SlotsOf(components)[0]));
		Assert.HasCount(1, AstSchema.ChildrenOf(kind, AstSchema.SlotsOf(kind)[0]));
	}

	/// <summary>
	/// An enumeration takes only its own kind of member, which is what stops a class being dropped
	/// into one.
	/// </summary>
	[TestMethod]
	public void EnumMembers_AreTheOnlyThingAnEnumTakes()
	{
		EnumDeclaration kind = new("BodyKind");
		AstSlot members = AstSchema.SlotsOf(kind)[0];

		Assert.IsTrue(AstSchema.Accepts(members, new EnumMember("Static")));
		Assert.IsFalse(AstSchema.Accepts(members, new ClassDeclaration("RigidBody")));
		Assert.IsFalse(AstSchema.TryAttach(kind, members, new ClassDeclaration("RigidBody")));
		Assert.IsInstanceOfType<EnumMember>(AstSchema.CreateDefaultChild(members));
	}

	/// <summary>
	/// Attaching at a position inside a sequence replaces what is there rather than appending, which
	/// is what makes connecting to a filled pin swap it.
	/// </summary>
	[TestMethod]
	public void Members_AreReplacedInPlace()
	{
		EnumDeclaration kind = new("BodyKind");
		kind.Members.Add(new EnumMember("Static"));
		kind.Members.Add(new EnumMember("Dynamic"));
		AstSlot members = AstSchema.SlotsOf(kind)[0];

		Assert.IsTrue(AstSchema.TryAttachAt(kind, members, 0, new EnumMember("Kinematic")));

		Assert.AreEqual("Kinematic", kind.Members[0].Name);
		Assert.AreEqual("Dynamic", kind.Members[1].Name);

		Assert.IsTrue(AstSchema.TryDetachAt(kind, members, 0));
		Assert.HasCount(1, kind.Members);
	}

	/// <summary>
	/// The same for a file's and a namespace's members, which is where a whole subtree is rearranged.
	/// </summary>
	[TestMethod]
	public void Containers_ReplaceAndDetachTheirMembers()
	{
		SourceFile file = new("RigidBody.gen.hpp");
		file.Members.Add(new ClassDeclaration("First"));
		NamespaceDeclaration components = new("holo");
		components.Members.Add(new ClassDeclaration("First"));

		Assert.IsTrue(AstSchema.TryAttachAt(file, AstSchema.SlotsOf(file)[0], 0, new ClassDeclaration("Second")));
		Assert.IsTrue(AstSchema.TryAttachAt(components, AstSchema.SlotsOf(components)[0], 0, new ClassDeclaration("Second")));

		Assert.AreEqual("Second", ((ClassDeclaration)file.Members[0]).Name);
		Assert.AreEqual("Second", ((ClassDeclaration)components.Members[0]).Name);

		Assert.IsTrue(AstSchema.TryDetachAt(file, AstSchema.SlotsOf(file)[0], 0));
		Assert.IsTrue(AstSchema.TryDetachAt(components, AstSchema.SlotsOf(components)[0], 0));

		Assert.IsEmpty(file.Members);
		Assert.IsEmpty(components.Members);
	}

	/// <summary>
	/// A field's initialiser is a single-valued slot: filling it again replaces what was there.
	/// </summary>
	[TestMethod]
	public void FieldInitialiser_IsFilledAndEmptied()
	{
		FieldDeclaration mass = new("mass", "double");
		AstSlot initialValue = AstSchema.SlotsOf(mass)[0];

		Assert.IsEmpty(AstSchema.ChildrenOf(mass, initialValue));
		Assert.IsTrue(AstSchema.TryAttach(mass, initialValue, new LiteralExpression<double>(1.0)));
		Assert.HasCount(1, AstSchema.ChildrenOf(mass, initialValue));

		Assert.IsTrue(AstSchema.TryDetachAt(mass, initialValue, 0));
		Assert.IsNull(mass.InitialValue);

		// Emptying one that is already empty changes nothing, so it is not an edit.
		Assert.IsFalse(AstSchema.TryDetachAt(mass, initialValue, 0));
	}

	/// <summary>
	/// A member initialiser holds one value, the same way a field does.
	/// </summary>
	[TestMethod]
	public void MemberInitialiserValue_IsFilledAndEmptied()
	{
		MemberInitialiser initialiser = new("value_");
		AstSlot value = AstSchema.SlotsOf(initialiser)[0];

		Assert.IsTrue(AstSchema.TryAttach(initialiser, value, new VariableReference("value")));
		Assert.HasCount(1, AstSchema.ChildrenOf(initialiser, value));

		Assert.IsTrue(AstSchema.TryDetachAt(initialiser, value, 0));
		Assert.IsNull(initialiser.Value);
	}

	/// <summary>
	/// A construction takes a member initialiser among its arguments, which is what a designated
	/// initialiser is.
	/// </summary>
	/// <remarks>
	/// The AST, the serializer and all seven generators handle one in this position — it is a
	/// designated initialiser in C++, an object initialiser in C#, a keyword argument in Python and
	/// an object literal in JavaScript. The graph is the uniform view of the AST, so a shape every
	/// other projection can express has to be one it can express too.
	/// </remarks>
	[TestMethod]
	public void ConstructionArguments_TakeAMemberInitialiser()
	{
		ConstructionExpression construction = new(new TypeReference("holo::Kilograms"));
		AstSlot arguments = AstSchema.SlotsOf(construction)[0];

		Assert.IsTrue(AstSchema.Accepts(arguments, new MemberInitialiser("value_")));
		Assert.IsTrue(AstSchema.TryAttach(
			construction,
			arguments,
			new MemberInitialiser("value_") { Value = new LiteralExpression<double>(1.0) }));

		Assert.HasCount(1, AstSchema.ChildrenOf(construction, arguments));
		Assert.IsInstanceOfType<MemberInitialiser>(construction.Arguments[0]);

		// And it comes out again, so one loaded from a document can be rewired rather than stranded.
		Assert.IsTrue(AstSchema.TryDetachAt(construction, arguments, 0));
		Assert.IsEmpty(construction.Arguments);
	}

	/// <summary>
	/// A call does not take one, because no generator has a spelling for it there.
	/// </summary>
	/// <remarks>
	/// The two share a slot name and a collection type, and the difference is the whole reason the
	/// kinds are separate: every generator reads <c>construction.Arguments</c> for a member
	/// initialiser and none reads a call's, so one attached to a call would be written as whatever
	/// fell out rather than as a named argument.
	/// </remarks>
	[TestMethod]
	public void CallArguments_DoNotTakeAMemberInitialiser()
	{
		CallExpression call = new("std::sqrt");
		AstSlot arguments = AstSchema.SlotsOf(call).Single(slot => slot.Name == "Arguments");

		Assert.IsFalse(AstSchema.Accepts(arguments, new MemberInitialiser("value_")));
		Assert.IsFalse(AstSchema.TryAttach(call, arguments, new MemberInitialiser("value_")));
		Assert.IsEmpty(call.Arguments);
	}

	/// <summary>
	/// A construction's arguments are a sequence, so they are added, swapped and removed in order.
	/// </summary>
	[TestMethod]
	public void ConstructionArguments_AreASequence()
	{
		ConstructionExpression construction = new(new TypeReference("holo::Kilograms"));
		AstSlot arguments = AstSchema.SlotsOf(construction)[0];

		Assert.IsTrue(AstSchema.TryAttach(construction, arguments, new LiteralExpression<double>(1.0)));
		Assert.IsTrue(AstSchema.TryAttach(construction, arguments, new LiteralExpression<double>(2.0)));
		Assert.HasCount(2, AstSchema.ChildrenOf(construction, arguments));

		Assert.IsTrue(AstSchema.TryAttachAt(construction, arguments, 0, new LiteralExpression<double>(3.0)));
		Assert.AreEqual(3.0, ((LiteralExpression<double>)construction.Arguments[0]).Value);

		Assert.IsTrue(AstSchema.TryDetachAt(construction, arguments, 0));
		Assert.HasCount(1, construction.Arguments);
	}

	/// <summary>
	/// A class takes every kind of declaration a generated type is made of, which is what lets one be
	/// assembled in the editor rather than only in code.
	/// </summary>
	[TestMethod]
	public void Members_AcceptEveryKindOfDeclaration()
	{
		ClassDeclaration body = new("RigidBody");
		AstSlot members = AstSchema.SlotsOf(body)[0];

		Assert.IsTrue(AstSchema.Accepts(members, new FieldDeclaration("mass", "double")));
		Assert.IsTrue(AstSchema.Accepts(members, new EnumDeclaration("BodyKind")));
		Assert.IsTrue(AstSchema.Accepts(members, new UsingAlias("underlying", "long")));
		Assert.IsTrue(AstSchema.Accepts(members, new NamespaceDeclaration("holo")));
		Assert.IsFalse(AstSchema.Accepts(members, new EnumMember("Static")));
	}
}
