// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Builds the semantic type from Holotype's <c>docs/generated-cpp-target.md</c> as an AST and checks
/// that the C++ generator emits the declaration that document specifies.
/// </summary>
/// <remarks>
/// The last of the three sections, and the one whose whole point is what it refuses: an entity id is
/// a number and so is a texture id, and the type exists so that adding one to the other stops
/// compiling. Every conversion across the underlying type is explicit in both directions, which is
/// what the declaration below says and what the assertions at the bottom check by construction.
/// <para>
/// One difference from the document is deliberate and was measured rather than assumed. It writes the
/// short accessor on a single line; the generator writes a braced block. Both are stable under
/// Holotype's own <c>.clang-format</c> — it reformats neither into the other — so this is the
/// generator's choice rather than something the formatter reconciles, and copying a hand-written
/// inline style would mean a generator guessing at when a body is short enough.
/// </para>
/// </remarks>
[TestClass]
public class ExemplarSemanticTypeTests
{
	/// <summary>
	/// The declaration the document specifies, with tabs expanded and one line changed: the document
	/// writes <c>value()</c> as <c>{ return value_; }</c> on the signature's line, and this is the
	/// braced form of the same body. That is the only edit — everything else below is the document's
	/// text.
	/// </summary>
	private const string Expected =
		"""
		/// A live entity. Distinct from every other identifier stored as a number.
		class EntityId
		{
		public:
		    using underlying = std::int64_t;

		    constexpr EntityId() noexcept = default;

		    /// Explicit: a bare number never becomes an EntityId by accident.
		    explicit constexpr EntityId(underlying value) noexcept
		        : value_(value)
		    {
		    }

		    /// Named, because getting the number back out is a decision too.
		    [[nodiscard]] constexpr underlying value() const noexcept
		    {
		        return value_;
		    }

		    [[nodiscard]] friend constexpr bool operator==(EntityId, EntityId) noexcept = default;
		    [[nodiscard]] friend constexpr auto operator<=>(EntityId, EntityId) noexcept = default;

		private:
		    underlying value_{};
		};

		static_assert(std::is_trivially_copyable_v<EntityId>,
		    "EntityId must be trivially copyable: it appears in components");
		static_assert(std::is_standard_layout_v<EntityId>,
		    "EntityId must be standard layout for its field offsets to be stable");
		""";

	/// <summary>
	/// Builds the exemplar's semantic type as an AST.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration EntityId()
	{
		ClassDeclaration entity = new("EntityId");
		entity.Documentation.Add("A live entity. Distinct from every other identifier stored as a number.");

		entity.Members.Add(new UsingAlias("underlying", "std::int64_t"));

		entity.Members.Add(new FunctionDeclaration("EntityId")
		{
			Kind = FunctionKind.Constructor,
			IsCompileTimeEvaluable = true,
			IsNoThrow = true,
			Definition = FunctionDefinition.Defaulted,
		});

		// Explicit is the whole point: a bare number never becomes an EntityId by accident.
		FunctionDeclaration fromUnderlying = new("EntityId")
		{
			Kind = FunctionKind.Constructor,
			IsExplicit = true,
			IsCompileTimeEvaluable = true,
			IsNoThrow = true,
		};
		fromUnderlying.Documentation.Add("Explicit: a bare number never becomes an EntityId by accident.");
		fromUnderlying.Parameters.Add(new Parameter("value", "underlying"));
		fromUnderlying.Initialisers.Add(new MemberInitialiser("value_", new VariableReference("value")));
		entity.Members.Add(fromUnderlying);

		FunctionDeclaration value = new("value")
		{
			ReturnType = "underlying",
			IsPure = true,
			IsCompileTimeEvaluable = true,
			IsReadOnly = true,
			IsNoThrow = true,
		};
		value.Documentation.Add("Named, because getting the number back out is a decision too.");
		value.Body.Add(new ReturnStatement(new VariableReference("value_")));
		entity.Members.Add(value);

		entity.Members.Add(Comparison("==", "bool"));
		entity.Members.Add(Comparison("<=>", "auto"));

		entity.Members.Add(new FieldDeclaration("value_", "underlying") { Visibility = Visibility.Private });

		return entity;
	}

	/// <summary>
	/// The semantic type together with what is asserted about it.
	/// </summary>
	/// <returns>The file.</returns>
	/// <remarks>
	/// A file with no banner and no imports, because the document's section is the declaration and
	/// the assertions beside it rather than a whole header. What the assertions say is the reason the
	/// type can appear in a component at all.
	/// </remarks>
	private static SourceFile EntityIdWithAssertions()
	{
		SourceFile file = new("EntityId.gen.hpp");
		file.Members.Add(EntityId());
		file.Members.Add(new CompileTimeAssertion(
			"std::is_trivially_copyable_v<EntityId>",
			"EntityId must be trivially copyable: it appears in components"));
		file.Members.Add(new CompileTimeAssertion(
			"std::is_standard_layout_v<EntityId>",
			"EntityId must be standard layout for its field offsets to be stable"));

		return file;
	}

	/// <summary>
	/// Builds one of the comparison operators, which are symmetric and so belong beside the type
	/// rather than to either operand.
	/// </summary>
	/// <param name="symbol">The operator's symbol.</param>
	/// <param name="returnType">What it returns.</param>
	/// <returns>The declaration.</returns>
	private static FunctionDeclaration Comparison(string symbol, string returnType)
	{
		FunctionDeclaration comparison = new(symbol)
		{
			Kind = FunctionKind.Operator,
			ReturnType = returnType,
			IsPure = true,
			IsFriend = true,
			IsCompileTimeEvaluable = true,
			IsNoThrow = true,
			Definition = FunctionDefinition.Defaulted,
		};

		comparison.Parameters.Add(new Parameter(string.Empty, "EntityId"));
		comparison.Parameters.Add(new Parameter(string.Empty, "EntityId"));
		return comparison;
	}

	/// <summary>
	/// The generated declaration is the one the document specifies.
	/// </summary>
	[TestMethod]
	public void Cpp_GeneratesTheSemanticTypeTheDocumentSpecifies() =>
		Assert.AreEqual(
			Expected.ReplaceLineEndings("\n").TrimEnd(),
			new CppGenerator().Generate(EntityIdWithAssertions()).ReplaceLineEndings("\n").TrimEnd());

	/// <summary>
	/// A member is initialised rather than assigned, which is the only way to start one that cannot be
	/// assigned at all.
	/// </summary>
	[TestMethod]
	public void Cpp_InitialisesTheMemberRatherThanAssigningToIt()
	{
		string code = new CppGenerator().Generate(EntityId()).ReplaceLineEndings("\n");

		Assert.Contains("explicit constexpr EntityId(underlying value) noexcept\n        : value_(value)", code, StringComparison.Ordinal);
		Assert.DoesNotContain("value_ =", code, StringComparison.Ordinal);
	}

	/// <summary>
	/// A language with no initialiser list assigns instead, at the top of the constructor and in the
	/// order declared, which is what the initialiser means there.
	/// </summary>
	[TestMethod]
	public void OtherLanguages_AssignWhereCppInitialises()
	{
		Assert.Contains("this.value_ = value", new CSharpGenerator().Generate(EntityId()), StringComparison.Ordinal);
		Assert.Contains("self.value_ = value", new PythonGenerator().Generate(EntityId()), StringComparison.Ordinal);
	}

	/// <summary>
	/// A group of members that say nothing about themselves stays together, and the access label that
	/// divides them gets air above it.
	/// </summary>
	[TestMethod]
	public void Cpp_SeparatesTheGroupsAndNotTheirMembers()
	{
		string code = new CppGenerator().Generate(EntityId()).ReplaceLineEndings("\n");

		// The two comparison operators are the same kind of thing and neither is documented, so they
		// read as one pair.
		Assert.Contains("operator==(EntityId, EntityId) noexcept = default;\n    [[nodiscard]] friend", code, StringComparison.Ordinal);

		// The access changes, which is a divide whatever sits either side of it.
		Assert.Contains("= default;\n\nprivate:", code, StringComparison.Ordinal);
	}
}
