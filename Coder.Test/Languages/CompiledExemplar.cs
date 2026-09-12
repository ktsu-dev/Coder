// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;

/// <summary>
/// The declarations every generator that is checked by compiling its output is checked against.
/// </summary>
/// <remarks>
/// One AST, three real compilers. Building a parallel one per language would say less than this
/// does: what is being claimed is that the *same* declarations come out as valid source in each
/// target, which is the whole premise of a language-agnostic AST and is not something three
/// similar-looking fixtures can demonstrate.
/// <para>
/// There are two groups, and the line between them is the receiver. Everything down to
/// <see cref="OriginTable"/> is what all three targets are checked against, and none of it contains a
/// body that reads a member — a body that does says <c>self-&gt;x</c> in C and <c>self.x</c> in the
/// other two, which is a difference no shared declaration can hold. Everything after it reads a
/// member, so it is shared by the two that spell that the same way and is not offered to C.
/// </para>
/// <para>
/// What each test still adds for itself is what only that language has to answer — C's struct of
/// function pointers reached through a driver, Rust's trait whose constant an implementation
/// supplies, Go's destructor and the package it compiles as.
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
	/// Gets a member that reads the instance without modifying it.
	/// </summary>
	/// <param name="name">What the target's own conventions call it.</param>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// The name is the caller's because it is the one thing about these that is not shared: Rust asks
	/// for <c>sum</c> and Go exports <c>Sum</c>, and neither generator recases a name it was given.
	/// </remarks>
	public static FunctionDeclaration Sum(string name)
	{
		FunctionDeclaration sum = new(name) { ReturnType = "int", IsReadOnly = true, IsPure = true };
		sum.Body.Add(new ReturnStatement(new BinaryExpression(
			new VariableReference("self.x"), BinaryOperator.Add, new VariableReference("self.y"))));
		return sum;
	}

	/// <summary>
	/// Gets a member that modifies the instance, which is what earns it the other receiver.
	/// </summary>
	/// <param name="name">What the target's own conventions call it.</param>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// The one a compiler is needed for. Both targets would compile the read-only receiver here too,
	/// and would modify a copy nobody ever looks at again.
	/// </remarks>
	public static FunctionDeclaration Shift(string name)
	{
		FunctionDeclaration shift = new(name);
		shift.Parameters.Add(new Parameter("dx", "int"));
		shift.Body.Add(new AssignmentStatement(
			new VariableReference("self.x"), new VariableReference("dx"), AssignmentOperator.AddAssign));
		return shift;
	}

	/// <summary>
	/// Gets the binary operator, which one target spells as a trait and the other has to name.
	/// </summary>
	/// <returns>The declaration.</returns>
	public static FunctionDeclaration Plus()
	{
		FunctionDeclaration plus = new("+")
		{
			Kind = FunctionKind.Operator,
			ReturnType = "Point",
			IsReadOnly = true,
		};
		plus.Parameters.Add(new Parameter("rhs", "Point"));
		plus.Body.Add(new ReturnStatement(Combined("rhs")));
		return plus;
	}

	/// <summary>
	/// Gets the unary operator that shares its symbol with the binary one.
	/// </summary>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// Declared beside <see cref="Plus"/> on purpose. A target that decides which operator a symbol
	/// means without looking at how many operands the declaration takes writes the same name twice
	/// here, which is a spelling a test can pin and only a compiler refuses.
	/// </remarks>
	public static FunctionDeclaration Negate()
	{
		FunctionDeclaration negate = new("-")
		{
			Kind = FunctionKind.Operator,
			ReturnType = "Point",
			IsReadOnly = true,
		};
		negate.Body.Add(new ReturnStatement(Combined(null)));
		return negate;
	}

	/// <summary>
	/// Gets a member that calls another for its effect and then chooses between two values.
	/// </summary>
	/// <param name="name">What the target's own conventions call it.</param>
	/// <param name="callee">What the member it calls is called there.</param>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// The two nodes neither target can take on trust. An <see cref="ExpressionStatement"/> holding a
	/// call is the only way to say "do this and discard what it answers with", and a
	/// <see cref="ConditionalExpression"/> has no ternary operator to fall back on in either: the
	/// inherited <c>?:</c> would not be a different spelling, it would not parse. Go is the further
	/// of the two from it, since <c>if</c> there is not an expression at all.
	/// </remarks>
	public static FunctionDeclaration Pick(string name, string callee)
	{
		// Not read-only, so the receiver is the one that may call the member that shifts it.
		FunctionDeclaration pick = new(name) { ReturnType = "int" };

		pick.Body.Add(new ExpressionStatement(
			new CallExpression(new VariableReference("self"), callee)
			{
				Arguments = { new LiteralExpression<int>(1) },
			}));

		pick.Body.Add(new ReturnStatement(new ConditionalExpression(
			new BinaryExpression(
				new VariableReference("self.x"), BinaryOperator.GreaterThan, new VariableReference("self.y")),
			new VariableReference("self.x"),
			new VariableReference("self.y"))));

		return pick;
	}

	/// <summary>
	/// Gets the conversion, which neither target declares as one.
	/// </summary>
	/// <param name="answer">What it answers with, spelled as the target spells it.</param>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// The expression is the caller's because the two targets bind the value being converted to
	/// different names: Rust's <c>From</c> takes a <c>value</c> and Go's method takes the receiver.
	/// </remarks>
	public static FunctionDeclaration ToDouble(string answer)
	{
		FunctionDeclaration conversion = new("ignored")
		{
			Kind = FunctionKind.ConversionOperator,
			ReturnType = "double",
			IsReadOnly = true,
		};
		conversion.Body.Add(new ReturnStatement(new VariableReference(answer)));
		return conversion;
	}

	/// <summary>
	/// Gets a free function taking a borrowed string and a sequence, and declaring two locals.
	/// </summary>
	/// <returns>The declaration.</returns>
	/// <remarks>
	/// Both locals are read, which one target only warns about and the other refuses outright.
	/// </remarks>
	public static FunctionDeclaration Measure()
	{
		FunctionDeclaration measure = new("measure") { ReturnType = "int" };
		measure.Parameters.Add(new Parameter("label")
		{
			Type = new TypeReference("str") { Indirection = TypeIndirection.Reference, IsReadOnly = true },
		});
		measure.Parameters.Add(new Parameter("sizes")
		{
			Type = new TypeReference("list") { TypeArguments = { new TypeReference("int") } },
		});

		measure.Body.Add(new VariableDeclaration("total", "int", new LiteralExpression<int>(0)));
		measure.Body.Add(new VariableDeclaration("guessed", null, new LiteralExpression<int>(1)) { IsTypeInferred = true });
		measure.Body.Add(new AssignmentStatement(
			new VariableReference("total"), new VariableReference("guessed"), AssignmentOperator.AddAssign));
		measure.Body.Add(new ReturnStatement(new VariableReference("total")));

		return measure;
	}

	/// <summary>
	/// Builds the value an operator answers with.
	/// </summary>
	/// <param name="operand">The operand to combine each member with, or null to negate it.</param>
	/// <returns>The expression.</returns>
	private static ConstructionExpression Combined(string? operand)
	{
		ConstructionExpression built = new(new TypeReference("Point"));

		foreach (string member in new[] { "x", "y" })
		{
			Expression value = operand is null
				? new UnaryExpression(UnaryOperator.Negate, new VariableReference($"self.{member}"))
				: new BinaryExpression(
					new VariableReference($"self.{member}"),
					BinaryOperator.Add,
					new VariableReference($"{operand}.{member}"));

			built.Arguments.Add(new MemberInitialiser(member) { Value = value });
		}

		return built;
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
