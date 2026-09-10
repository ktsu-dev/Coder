// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Ast;

using ktsu.Coder.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that cloning a declaration copies what it holds rather than sharing it.
/// </summary>
/// <remarks>
/// The editor clones to undo, and the graph clones to rebuild itself from the AST. A clone that
/// shares a collection with its original is the kind of defect that shows up as an edit landing in
/// two places at once, long after the clone was made and nowhere near it — so every node that holds
/// anything is checked here for the same property.
/// </remarks>
[TestClass]
public class DeclarationCloneTests
{
	/// <summary>
	/// An enumeration's members and documentation are copied, not shared.
	/// </summary>
	[TestMethod]
	public void EnumDeclaration_CopiesItsMembers()
	{
		EnumDeclaration original = new("BodyKind")
		{
			UnderlyingType = "std::uint8_t",
			Visibility = Visibility.Internal,
		};
		original.Documentation.Add("what a body does");
		original.Members.Add(new EnumMember("Static") { Value = "3" });

		EnumDeclaration clone = (EnumDeclaration)original.Clone();

		Assert.AreEqual("BodyKind", clone.Name);
		Assert.AreEqual("std::uint8_t", clone.UnderlyingType?.ToString());
		Assert.AreEqual(Visibility.Internal, clone.Visibility);
		Assert.AreSequenceEqual(original.Documentation, clone.Documentation);
		Assert.AreEqual("3", clone.Members[0].Value);
		Assert.AreNotSame(original.Members[0], clone.Members[0]);

		clone.Members[0].Name = "Dynamic";
		clone.Documentation.Add("added later");

		Assert.AreEqual("Static", original.Members[0].Name);
		Assert.HasCount(1, original.Documentation);
	}

	/// <summary>
	/// A member carries its metadata across, which is where a generator keeps anything the node's own
	/// properties cannot say.
	/// </summary>
	[TestMethod]
	public void EnumMember_CopiesItsMetadata()
	{
		EnumMember original = new("Static") { Value = "1" };
		original.Metadata["origin"] = "schema";

		EnumMember clone = (EnumMember)original.Clone();

		Assert.AreEqual("Static", clone.Name);
		Assert.AreEqual("1", clone.Value);
		Assert.AreEqual("schema", clone.Metadata["origin"]);
	}

	/// <summary>
	/// A field's type and initialiser are copied, so editing one instance's initialiser cannot reach
	/// the other's.
	/// </summary>
	[TestMethod]
	public void FieldDeclaration_CopiesItsTypeAndInitialiser()
	{
		FieldDeclaration original = new("mass", "double")
		{
			InitialValue = new LiteralExpression<double>(1.0),
			Visibility = Visibility.Private,
		};
		original.Documentation.Add("unit: kg");

		FieldDeclaration clone = (FieldDeclaration)original.Clone();

		Assert.AreEqual("double", clone.Type?.ToString());
		Assert.AreEqual(Visibility.Private, clone.Visibility);
		Assert.AreSequenceEqual(original.Documentation, clone.Documentation);
		Assert.AreNotSame(original.Type, clone.Type);
		Assert.AreNotSame(original.InitialValue, clone.InitialValue);
	}

	/// <summary>
	/// A namespace's members are cloned with it, which is what makes cloning one clone a whole
	/// subtree.
	/// </summary>
	[TestMethod]
	public void NamespaceDeclaration_CopiesItsMembers()
	{
		NamespaceDeclaration original = new("holo::components");
		original.Documentation.Add("the engine's components");
		original.Members.Add(new ClassDeclaration("RigidBody"));

		NamespaceDeclaration clone = (NamespaceDeclaration)original.Clone();

		Assert.AreEqual("holo::components", clone.Name);
		Assert.AreSequenceEqual(original.Documentation, clone.Documentation);
		Assert.AreNotSame(original.Members[0], clone.Members[0]);

		((ClassDeclaration)clone.Members[0]).Name = "Collider";

		Assert.AreEqual("RigidBody", ((ClassDeclaration)original.Members[0]).Name);
	}

	/// <summary>
	/// A file copies its banner, its imports and everything it declares.
	/// </summary>
	[TestMethod]
	public void SourceFile_CopiesEverythingItHolds()
	{
		SourceFile original = new("RigidBody.gen.hpp") { IsHeader = true };
		original.HeaderComment.Add("Generated. Do not edit.");
		original.Imports.Add("<cstdint>");
		original.Members.Add(new NamespaceDeclaration("holo"));

		SourceFile clone = (SourceFile)original.Clone();

		Assert.AreEqual("RigidBody.gen.hpp", clone.Name);
		Assert.IsTrue(clone.IsHeader);
		Assert.AreSequenceEqual(original.HeaderComment, clone.HeaderComment);
		Assert.AreSequenceEqual(original.Imports, clone.Imports);
		Assert.AreNotSame(original.Members[0], clone.Members[0]);

		clone.Imports.Add("<type_traits>");

		Assert.HasCount(1, original.Imports);
	}

	/// <summary>
	/// An alias copies the type it names.
	/// </summary>
	[TestMethod]
	public void UsingAlias_CopiesTheTypeItNames()
	{
		UsingAlias original = new("underlying", "std::int64_t") { Visibility = Visibility.Public };
		original.Documentation.Add("what an EntityId is stored as");

		UsingAlias clone = (UsingAlias)original.Clone();

		Assert.AreEqual("underlying", clone.Name);
		Assert.AreEqual("std::int64_t", clone.AliasedType?.ToString());
		Assert.AreEqual(Visibility.Public, clone.Visibility);
		Assert.AreSequenceEqual(original.Documentation, clone.Documentation);
		Assert.AreNotSame(original.AliasedType, clone.AliasedType);
	}

	/// <summary>
	/// An initialiser copies what the member starts at.
	/// </summary>
	[TestMethod]
	public void MemberInitialiser_CopiesItsValue()
	{
		MemberInitialiser original = new("value_", new VariableReference("value"));

		MemberInitialiser clone = (MemberInitialiser)original.Clone();

		Assert.AreEqual("value_", clone.Name);
		Assert.AreNotSame(original.Value, clone.Value);
		Assert.AreEqual("value", ((VariableReference)clone.Value!).Name);
	}

	/// <summary>
	/// A construction copies its type and its arguments.
	/// </summary>
	[TestMethod]
	public void ConstructionExpression_CopiesItsArguments()
	{
		ConstructionExpression original = new(new TypeReference("holo::Kilograms"));
		original.Arguments.Add(new LiteralExpression<double>(1.0));

		ConstructionExpression clone = (ConstructionExpression)original.Clone();

		Assert.AreEqual("holo::Kilograms", clone.Type?.ToString());
		Assert.AreNotSame(original.Arguments[0], clone.Arguments[0]);

		clone.Arguments.Add(new LiteralExpression<double>(2.0));

		Assert.HasCount(1, original.Arguments);
	}

	/// <summary>
	/// A function carries its shape and its initialisers across.
	/// </summary>
	[TestMethod]
	public void FunctionDeclaration_CopiesItsShape()
	{
		FunctionDeclaration original = new("EntityId")
		{
			Kind = FunctionKind.Constructor,
			Definition = FunctionDefinition.Defaulted,
			IsExplicit = true,
			IsCompileTimeEvaluable = true,
			IsNoThrow = true,
			IsFriend = true,
			IsVirtual = true,
			IsAbstract = true,
			IsReadOnly = true,
			MustUseResult = true,
		};
		original.Initialisers.Add(new MemberInitialiser("value_", new VariableReference("value")));

		FunctionDeclaration clone = (FunctionDeclaration)original.Clone();

		Assert.AreEqual(FunctionKind.Constructor, clone.Kind);
		Assert.AreEqual(FunctionDefinition.Defaulted, clone.Definition);
		Assert.IsTrue(clone.IsExplicit);
		Assert.IsTrue(clone.IsCompileTimeEvaluable);
		Assert.IsTrue(clone.IsNoThrow);
		Assert.IsTrue(clone.IsFriend);
		Assert.AreNotSame(original.Initialisers[0], clone.Initialisers[0]);
		Assert.AreEqual("value_", clone.Initialisers[0].Name);
	}
}
