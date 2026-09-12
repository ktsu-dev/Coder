// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Graph;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Every concrete AST node type reaches the editor.
/// </summary>
/// <remarks>
/// <see cref="AstSchema"/> and <see cref="AstFields"/> are hand-written switches, deliberately:
/// a node's shape is part of the library's contract, and a reflective walk would start exposing
/// whatever property somebody added next, including the ones that are not children at all. The
/// cost of that choice is that both switches end in a silent default, so a node type omitted from
/// either produces no error, no warning and no failing test. It produces a node that draws an
/// empty inspector, which is how <see cref="MemberInitialiser"/> and
/// <see cref="ConstructionExpression"/> came to be missing from <see cref="AstFields"/> for a
/// while: they were added to the AST, the schema, the serializer and all six generators in one
/// run, and only the inspector was missed.
/// <para>
/// So the production code stays hand-written and the <em>test</em> is the reflective one. It walks
/// every concrete node type in the AST assembly and asks three things of each: that the schema has
/// a decision about its children rather than a default, that the inspector offers a field for
/// everything about it that is not a child, and that each of those fields reads back what it is
/// written. A node type that satisfies none of them fails here by name.
/// </para>
/// <para>
/// "A decision rather than a default" is the one thing reflection cannot see for itself — an empty
/// slot list is what a leaf should have and also what a forgotten node gets — so
/// <see cref="Childless"/> is where a node type says it has no children on purpose. It holds types
/// rather than names, so renaming one is a compile error here rather than a silently stale entry.
/// </para>
/// </remarks>
[TestClass]
public sealed class AstNodeCoverageTests
{
	/// <summary>
	/// The storage types the two generic node types are closed over.
	/// </summary>
	/// <remarks>
	/// The same four <see cref="AstFields"/> matches on, which is what makes them the four that
	/// exist as far as the editor is concerned. A fifth would have to be added in both places, and
	/// adding it only here fails the field-coverage test.
	/// </remarks>
	private static readonly Type[] StorageTypes = [typeof(string), typeof(int), typeof(double), typeof(bool)];

	/// <summary>
	/// The node types that hold no children, said rather than defaulted.
	/// </summary>
	/// <remarks>
	/// Each of these is a leaf in the AST's own terms: it names something, or it holds one value,
	/// and what it says about the program is said entirely by its own fields. A node that turns out
	/// to need a child later comes off this list at the same time as it gains its slot.
	/// </remarks>
	private static readonly Type[] Childless =
	[
		typeof(CompileTimeAssertion),
		typeof(EnumMember),
		typeof(Parameter),
		typeof(UsingAlias),
		typeof(VariableReference),
		typeof(AstLeafNode<>),
		typeof(LiteralExpression<>),
	];

	/// <summary>
	/// Gets every concrete node type, as MSTest data rows.
	/// </summary>
	/// <returns>One row per type, so a failure names the type rather than the loop.</returns>
	public static IEnumerable<object[]> NodeTypes() => Concrete().Select(type => new object[] { type });

	/// <summary>
	/// The schema has a decision about every node type's children.
	/// </summary>
	/// <param name="type">The node type under test.</param>
	[TestMethod]
	[DynamicData(nameof(NodeTypes))]
	public void EveryNodeTypeHasSlotsOrSaysItHasNone(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		bool declaredChildless = Childless.Contains(Definition(type));
		bool hasSlots = AstSchema.SlotsOf(New(type)).Count > 0;

		Assert.AreNotEqual(
			declaredChildless,
			hasSlots,
			declaredChildless
				? $"{Spell(type)} is listed as childless here but AstSchema.SlotsOf gives it slots. "
					+ "Take it off the Childless list."
				: $"{Spell(type)} gets no slots from AstSchema.SlotsOf, so the editor cannot connect "
					+ "anything to it. Add it to the switch, or list it in Childless here if it really "
					+ "holds nothing.");
	}

	/// <summary>
	/// The inspector offers a field for every node type that has something to edit.
	/// </summary>
	/// <param name="type">The node type under test.</param>
	/// <remarks>
	/// "Something to edit" is a public settable property holding a value rather than a child —
	/// text, a flag, a number, an enumeration or a <see cref="TypeReference"/>. A node with one of
	/// those and no fields is one whose inspector is blank, which is the failure this exists to
	/// catch. It does not check that <em>every</em> such property is offered: some are deliberately
	/// not, and a count would be a restatement of the switch rather than a check on it.
	/// </remarks>
	[TestMethod]
	[DynamicData(nameof(NodeTypes))]
	public void EveryNodeTypeWithSomethingToEditOffersAField(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		if (!Editable(type).Any())
		{
			return;
		}

		Assert.IsNotEmpty(
			AstFields.Of(New(type)),
			$"{Spell(type)} has editable properties ({string.Join(", ", Editable(type).Select(property => property.Name))}) "
				+ "but AstFields.Of returns nothing for it, so its inspector draws empty. Add it to the switch.");
	}

	/// <summary>
	/// Every field the inspector offers reads back what it is written.
	/// </summary>
	/// <param name="type">The node type under test.</param>
	/// <remarks>
	/// <see cref="AstFields.Of"/> and <see cref="AstFields.TryWrite"/> are two switches over the
	/// same set of fields, so a field added to one and not the other reads but does not write — it
	/// looks editable and silently is not. Writing a value the field does not already hold is the
	/// only way to tell: <see cref="AstFields.TryWrite"/> answers false for an edit that changes
	/// nothing, deliberately, so that one does not reach the undo stack.
	/// </remarks>
	[TestMethod]
	[DynamicData(nameof(NodeTypes))]
	public void EveryFieldTheInspectorOffersCanBeWritten(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		AstNode node = New(type);

		foreach (AstField field in AstFields.Of(node))
		{
			string changed = Change(field);

			Assert.IsTrue(
				AstFields.TryWrite(node, field.Name, changed),
				$"{Spell(type)}.{field.Name} is offered by AstFields.Of but AstFields.TryWrite refused "
					+ $"'{changed}'. The two switches disagree about it.");

			Assert.AreEqual(
				changed,
				AstFields.Read(node, field.Name),
				$"{Spell(type)}.{field.Name} did not read back what it was written.");
		}
	}

	/// <summary>
	/// A value the field does not already hold, of a kind the field can take.
	/// </summary>
	/// <param name="field">The field to change.</param>
	/// <returns>The new value, as text.</returns>
	/// <remarks>
	/// A choice picks any option other than the current one, so the test says nothing about which
	/// option is which. The rest are the smallest change that is still well formed for the kind.
	/// </remarks>
	private static string Change(AstField field) => field.Kind switch
	{
		// Lower case, because that is how the inspector spells a flag and the assertion is that
		// the field reads back exactly what it was written rather than something that parses the
		// same way.
		AstFieldKind.Flag => bool.TryParse(field.Value, out bool flag)
			? (!flag).ToString().ToLowerInvariant()
			: throw new InvalidOperationException($"'{field.Value}' is not a flag."),

		AstFieldKind.Number => (Parse(field.Value) + 1).ToString(CultureInfo.InvariantCulture),

		AstFieldKind.Fraction => (Parse(field.Value) + 0.5).ToString(CultureInfo.InvariantCulture),

		AstFieldKind.Choice => field.Choices
			.Select(choice => choice.Value)
			.FirstOrDefault(choice => !string.Equals(choice, field.Value, StringComparison.Ordinal))
			?? throw new InvalidOperationException($"{field.Name} offers no option other than '{field.Value}'."),

		_ => field.Value + "Changed",
	};

	private static double Parse(string value) =>
		double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
			? parsed
			: throw new InvalidOperationException($"'{value}' is not a number.");

	/// <summary>
	/// Every concrete node type in the AST assembly, with the generic ones closed.
	/// </summary>
	/// <returns>The types, in a stable order.</returns>
	/// <remarks>
	/// Read off <see cref="AstNode"/>'s own assembly rather than from a list, which is the whole
	/// point: a node type that exists is in here whether or not anybody remembered it.
	/// </remarks>
	private static IEnumerable<Type> Concrete() =>
		typeof(AstNode).Assembly
			.GetTypes()
			.Where(type => type.IsClass && !type.IsAbstract && type.IsPublic && typeof(AstNode).IsAssignableFrom(type))
			.SelectMany(Closed)
			.OrderBy(Spell, StringComparer.Ordinal);

	/// <summary>
	/// One node type, or its closures when it is generic.
	/// </summary>
	/// <param name="type">The type to close.</param>
	/// <returns>The constructible types it stands for.</returns>
	private static IEnumerable<Type> Closed(Type type) =>
		type.IsGenericTypeDefinition
			? StorageTypes.Select(storage => type.MakeGenericType(storage))
			: [type];

	private static Type Definition(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

	/// <summary>
	/// Builds one, through the parameterless constructor every node type has.
	/// </summary>
	/// <param name="type">The type to build.</param>
	/// <returns>A new node.</returns>
	/// <remarks>
	/// That every node type has one is itself part of the contract: the YAML deserializer builds a
	/// node before it has read any of its properties, so a node type without one could not be read
	/// back at all. A type that loses it fails here rather than at the first document that uses it.
	/// </remarks>
	private static AstNode New(Type type) =>
		Activator.CreateInstance(type) as AstNode
			?? throw new InvalidOperationException($"{Spell(type)} has no parameterless constructor.");

	/// <summary>
	/// The public settable properties that hold a value rather than a child.
	/// </summary>
	/// <param name="type">The type to look at.</param>
	/// <returns>The properties, which may be none.</returns>
	/// <remarks>
	/// <see cref="Expression.ExpectedType"/> is excluded, and it is the only exclusion. It is a
	/// hint for type checking that every expression carries and the inspector offers on none of
	/// them; counting it would make this test demand a field on every expression in the AST, which
	/// is a change to the editor's surface rather than the gap this exists to catch. Excluding the
	/// property rather than the two node types that have nothing else is what keeps that on record:
	/// the reason is one property shared by every expression, not two nodes that happen to be bare.
	/// </remarks>
	private static IEnumerable<PropertyInfo> Editable(Type type) =>
		type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(property => property.SetMethod is { IsPublic: true })
			.Where(property => property.DeclaringType != typeof(Expression))
			.Where(property => IsValue(property.PropertyType));

	private static bool IsValue(Type type)
	{
		Type bare = Nullable.GetUnderlyingType(type) ?? type;

		return bare.IsEnum
			|| bare == typeof(string)
			|| bare == typeof(bool)
			|| bare == typeof(int)
			|| bare == typeof(double)
			|| bare == typeof(TypeReference);
	}

	/// <summary>
	/// A node type's name, with its type argument where it has one.
	/// </summary>
	/// <param name="type">The type to name.</param>
	/// <returns>A name a failure message can be read from, such as <c>LiteralExpression&lt;int&gt;</c>.</returns>
	private static string Spell(Type type) =>
		type.IsGenericType
			? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name))}>"
			: type.Name;
}
