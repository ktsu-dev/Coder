// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Languages;

using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Builds the interface from Holotype's <c>docs/generated-cpp-target.md</c> as an AST and checks that
/// the C++ generator emits the declaration that document specifies.
/// </summary>
/// <remarks>
/// The companion to <see cref="ExemplarHeaderTests"/>, which covers the component. This one covers
/// what an implementation is written against, and is where the four conventions that document holds
/// globally have to survive as C++: fallibility is a <c>Result</c> in the return type, a borrow is a
/// <c>Span</c>, a parameter's direction is <c>const</c>, and nothing is annotated for lifetime because
/// nothing owns anything.
/// <para>
/// Direction on a span describes the elements rather than the view, which is what makes
/// <c>integrate</c> — a system that reads one component and writes another — an ordinary signature
/// needing no special concept.
/// </para>
/// </remarks>
[TestClass]
public class ExemplarInterfaceTests
{
	/// <summary>The declaration the document specifies, with tabs expanded.</summary>
	private const string Expected =
		"""
		/// The simulation the physics system drives.
		class IPhysicsWorld
		{
		public:
		    IPhysicsWorld() = default;
		    IPhysicsWorld(const IPhysicsWorld&) = delete;
		    IPhysicsWorld& operator=(const IPhysicsWorld&) = delete;
		    virtual ~IPhysicsWorld() = default;

		    /// Advance the simulation by one step.
		    virtual void step(holo::Seconds dt) = 0;

		    /// Create a body. Fails if the world is at capacity.
		    [[nodiscard]] virtual holo::Result<BodyHandle> spawn(const holo::components::RigidBody& body) = 0;

		    /// Integrate velocities into positions.
		    virtual void integrate(std::span<const Velocity> velocities, std::span<Position> positions) = 0;

		    /// The body for an entity, if it has one.
		    [[nodiscard]] virtual std::optional<holo::components::RigidBody> find(EntityId entity) const = 0;
		};
		""";

	/// <summary>
	/// Builds the exemplar's interface as an AST.
	/// </summary>
	/// <returns>The declaration.</returns>
	private static ClassDeclaration PhysicsWorld()
	{
		ClassDeclaration world = new("IPhysicsWorld") { Kind = TypeDeclarationKind.Interface };
		world.Documentation.Add("The simulation the physics system drives.");

		world.Members.Add(new FunctionDeclaration("IPhysicsWorld")
		{
			Kind = FunctionKind.Constructor,
			Definition = FunctionDefinition.Defaulted,
		});

		// An unnamed parameter, which is what a deleted copy declaration wants: the parameter is
		// there to make the signature, and naming it would invite someone to look for its use.
		world.Members.Add(WithParameters(
			new FunctionDeclaration("IPhysicsWorld")
			{
				Kind = FunctionKind.Constructor,
				Definition = FunctionDefinition.Deleted,
			},
			Borrowed(string.Empty, "IPhysicsWorld")));

		world.Members.Add(WithParameters(
			new FunctionDeclaration("=")
			{
				Kind = FunctionKind.Operator,
				ReturnType = new TypeReference("IPhysicsWorld") { Indirection = TypeIndirection.Reference },
				Definition = FunctionDefinition.Deleted,
			},
			Borrowed(string.Empty, "IPhysicsWorld")));

		world.Members.Add(new FunctionDeclaration("IPhysicsWorld")
		{
			Kind = FunctionKind.Destructor,
			IsVirtual = true,
			Definition = FunctionDefinition.Defaulted,
		});

		world.Members.Add(Method(
			"step",
			"void",
			"Advance the simulation by one step.",
			[new Parameter("dt", "holo::Seconds")]));

		world.Members.Add(Method(
			"spawn",
			"holo::Result<BodyHandle>",
			"Create a body. Fails if the world is at capacity.",
			[Borrowed("body", "holo::components::RigidBody")],
			mustUseResult: true));

		world.Members.Add(Method(
			"integrate",
			"void",
			"Integrate velocities into positions.",
			[
				new Parameter("velocities")
				{
					Type = new TypeReference("std::span")
					{
						TypeArguments = { new TypeReference("Velocity") { IsReadOnly = true } },
					},
				},
				new Parameter("positions")
				{
					Type = new TypeReference("std::span") { TypeArguments = { new TypeReference("Position") } },
				},
			]));

		world.Members.Add(Method(
			"find",
			"std::optional<holo::components::RigidBody>",
			"The body for an entity, if it has one.",
			[new Parameter("entity", "EntityId")],
			mustUseResult: true,
			isReadOnly: true));

		return world;
	}

	/// <summary>
	/// Builds one of the interface's methods: virtual, with no definition of its own.
	/// </summary>
	/// <param name="name">The method's name.</param>
	/// <param name="returnType">What it returns.</param>
	/// <param name="documentation">The line above it.</param>
	/// <param name="parameters">Its parameters, in order.</param>
	/// <param name="mustUseResult">Whether ignoring the result is a mistake.</param>
	/// <param name="isReadOnly">Whether calling it leaves the receiver unchanged.</param>
	/// <returns>The method.</returns>
	private static FunctionDeclaration Method(
		string name,
		string returnType,
		string documentation,
		Parameter[] parameters,
		bool mustUseResult = false,
		bool isReadOnly = false)
	{
		FunctionDeclaration method = new(name)
		{
			ReturnType = returnType,
			IsAbstract = true,
			MustUseResult = mustUseResult,
			IsReadOnly = isReadOnly,
		};

		method.Documentation.Add(documentation);
		return WithParameters(method, parameters);
	}

	/// <summary>
	/// Adds parameters to a declaration.
	/// </summary>
	/// <param name="declaration">The declaration to add to.</param>
	/// <param name="parameters">The parameters, in order.</param>
	/// <returns>The same declaration.</returns>
	private static FunctionDeclaration WithParameters(FunctionDeclaration declaration, params Parameter[] parameters)
	{
		foreach (Parameter parameter in parameters)
		{
			declaration.Parameters.Add(parameter);
		}

		return declaration;
	}

	/// <summary>
	/// Builds a parameter the callee may read for the length of the call and may not keep.
	/// </summary>
	/// <param name="name">The parameter's name, empty for one deliberately unnamed.</param>
	/// <param name="type">The type borrowed.</param>
	/// <returns>The parameter.</returns>
	private static Parameter Borrowed(string name, string type) => new(name)
	{
		Type = new TypeReference(type) { IsReadOnly = true, Indirection = TypeIndirection.Reference },
	};

	/// <summary>
	/// The generated declaration is the one the document specifies.
	/// </summary>
	[TestMethod]
	public void Cpp_GeneratesTheInterfaceTheDocumentSpecifies() =>
		Assert.AreEqual(
			Expected.ReplaceLineEndings("\n").TrimEnd(),
			new CppGenerator().Generate(PhysicsWorld()).ReplaceLineEndings("\n").TrimEnd());

	/// <summary>
	/// The declarations that say nothing about themselves stay together, and a documented one gets
	/// air above it.
	/// </summary>
	/// <remarks>
	/// A run of defaulted and deleted declarations reads as one group rather than as four paragraphs,
	/// which is the whole reason the rule looks at both members rather than only the one about to be
	/// written.
	/// </remarks>
	[TestMethod]
	public void Cpp_GroupsTheDeclarationsThatSayNothing()
	{
		string code = new CppGenerator().Generate(PhysicsWorld()).ReplaceLineEndings("\n");

		Assert.Contains(
			"IPhysicsWorld() = default;\n    IPhysicsWorld(const IPhysicsWorld&) = delete;",
			code,
			StringComparison.Ordinal);
		Assert.Contains(
			"virtual ~IPhysicsWorld() = default;\n\n    /// Advance",
			code,
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Direction on a span describes the elements, not the view, so a system that reads one component
	/// and writes another is an ordinary signature.
	/// </summary>
	[TestMethod]
	public void Cpp_BorrowsElementsReadOnlyWithoutBorrowingTheViewReadOnly() =>
		Assert.Contains(
			"integrate(std::span<const Velocity> velocities, std::span<Position> positions)",
			new CppGenerator().Generate(PhysicsWorld()),
			StringComparison.Ordinal);
}
