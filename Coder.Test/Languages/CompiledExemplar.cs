// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;

/// <summary>
/// The declarations every generator that is checked by compiling its output is checked against.
/// </summary>
/// <remarks>
/// One AST, two real compilers. Building a parallel one per language would say less than this does:
/// what is being claimed is that the *same* declarations come out as valid source in each target,
/// which is the whole premise of a language-agnostic AST and is not something two similar-looking
/// fixtures can demonstrate.
/// <para>
/// What each test adds for itself is what only that language has to answer — C's struct of function
/// pointers reached through a driver, Rust's operator traits and its implementation for a type. What
/// is here is what both have to answer, and nothing whose spelling depends on the receiver: a body
/// that reads a member says <c>self-&gt;x</c> in one language and <c>self.x</c> in the other, so
/// those belong to whichever test is about that language.
/// </para>
/// </remarks>
internal static class CompiledExemplar
{
	/// <summary>
	/// Gets the enumeration, which fixes its representation and gives one member a value.
	/// </summary>
	/// <returns>The declaration.</returns>
	public static EnumDeclaration Colour()
	{
		EnumDeclaration colour = new("Colour") { UnderlyingType = "int" };
		colour.Documentation.Add("What something is coloured.");
		colour.Members.Add(new EnumMember("Red") { Value = "1" });
		colour.Members.Add(new EnumMember("Green"));
		return colour;
	}

	/// <summary>
	/// Gets the type both languages build a value of: two fields, a constructor and a static.
	/// </summary>
	/// <returns>The declaration, which a caller may add its own members to.</returns>
	public static ClassDeclaration Point()
	{
		ClassDeclaration point = new("Point") { Kind = TypeDeclarationKind.Struct };
		point.Documentation.Add("Somewhere on a surface.");
		point.Members.Add(new VariableDeclaration("x", "int"));
		point.Members.Add(new VariableDeclaration("y", "int", new LiteralExpression<int>(1)));

		FunctionDeclaration create = new("Point") { Kind = FunctionKind.Constructor };
		create.Parameters.Add(new Parameter("x", "int"));
		create.Parameters.Add(new Parameter("y", "int") { IsOptional = true, DefaultValue = "0" });
		create.Initialisers.Add(new MemberInitialiser("x") { Value = new VariableReference("x") });
		create.Initialisers.Add(new MemberInitialiser("y") { Value = new VariableReference("y") });
		point.Members.Add(create);

		FunctionDeclaration zero = new("zero") { ReturnType = "Point", IsStatic = true, IsPure = true };
		zero.Body.Add(new ReturnStatement(Origin(0)));
		point.Members.Add(zero);

		return point;
	}

	/// <summary>
	/// Gets the set of members an implementation supplies.
	/// </summary>
	/// <returns>The declaration.</returns>
	public static ClassDeclaration Shape()
	{
		ClassDeclaration shape = new("Shape") { Kind = TypeDeclarationKind.Interface };
		shape.Documentation.Add("What every shape can do.");

		FunctionDeclaration draw = new("draw") { IsAbstract = true };
		draw.Parameters.Add(new Parameter("scale", "double"));
		shape.Members.Add(draw);

		shape.Members.Add(new FunctionDeclaration("area")
		{
			ReturnType = "double",
			IsReadOnly = true,
			IsAbstract = true,
		});

		return shape;
	}

	/// <summary>
	/// Gets a second name for the type.
	/// </summary>
	/// <returns>The alias.</returns>
	public static UsingAlias OriginAlias() => new() { Name = "Origin", AliasedType = "Point" };

	/// <summary>
	/// Gets the table: a constant array of rows that each name the member every value is for.
	/// </summary>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// This is the one that has caught the most. A table with no length has to be spelled as
	/// something that can be a constant, and what that something is differs by language.
	/// </remarks>
	public static FieldDeclaration OriginTable()
	{
		ConstructionExpression table = new(type: null);
		table.Arguments.Add(Origin(0));
		table.Arguments.Add(Origin(1));

		FieldDeclaration origins = new()
		{
			Name = "ORIGINS",
			Type = new TypeReference("Point") { IsArray = true, IsReadOnly = true },
			IsConstant = true,
			InitialValue = table,
		};
		origins.Documentation.Add("Where each shape starts.");

		return origins;
	}

	/// <summary>
	/// Builds one row of the table, or the value the static answers.
	/// </summary>
	/// <param name="offset">What both members hold.</param>
	/// <returns>The expression.</returns>
	private static ConstructionExpression Origin(int offset)
	{
		ConstructionExpression row = new(new TypeReference("Point"));
		row.Arguments.Add(new MemberInitialiser("x") { Value = new LiteralExpression<int>(offset) });
		row.Arguments.Add(new MemberInitialiser("y") { Value = new LiteralExpression<int>(offset) });
		return row;
	}
}
