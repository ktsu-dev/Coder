// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.ImGuiNodeEditor;

/// <summary>
/// A node-editor view of an AST, and the editing operations that keep the two in step.
/// </summary>
/// <remarks>
/// The AST is the document and the graph is a view of it. Every structural edit is applied to the
/// AST first and the engine graph is then rebuilt from it, so the two can never disagree — which is
/// the failure mode a two-way incremental sync invites. Rebuilding preserves each node's on-screen
/// position by AST identity, so a rebuild is invisible to the user.
/// <para>
/// Each AST node becomes one editor node with a single output pin — itself — and one input pin per
/// slot it can hold a child in. A link therefore reads "this node fills that slot of that parent",
/// which is exactly the parent/child edge of the tree, drawn.
/// </para>
/// </remarks>
public sealed class AstGraph
{
	/// <summary>
	/// How many unfilled pins a variadic slot offers beyond the children it already has.
	/// </summary>
	/// <remarks>
	/// One would do, but two lets a user connect twice without waiting for the rebuild between them.
	/// </remarks>
	private const int SpareVariadicPins = 2;

	/// <summary>
	/// The horizontal distance between one depth of the tree and the next, before physics runs.
	/// </summary>
	private const float DepthSpacing = 220f;

	/// <summary>
	/// The vertical distance between siblings, before physics runs.
	/// </summary>
	private const float SiblingSpacing = 110f;

	private readonly Dictionary<AstNode, Vector2> positions = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<int, AstNode> nodesById = [];
	private readonly Dictionary<AstNode, int> idsByNode = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<int, AstNode> ownerByOutputPin = [];
	private readonly Dictionary<int, SlotPin> slotByInputPin = [];
	private readonly Dictionary<SlotPin, int> inputPinBySlot = [];
	private readonly List<AstNode> detached = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="AstGraph"/> class over an AST.
	/// </summary>
	/// <param name="root">The root of the AST to view.</param>
	public AstGraph(AstNode root)
	{
		Ensure.NotNull(root);
		Root = root;
		Rebuild();
	}

	/// <summary>
	/// Gets the engine holding the nodes, links and force-directed layout.
	/// </summary>
	public NodeEditorEngine Engine { get; } = new();

	/// <summary>
	/// Gets the root of the AST this graph views.
	/// </summary>
	public AstNode Root { get; private set; }

	/// <summary>
	/// Gets the AST node each editor node stands for.
	/// </summary>
	public IReadOnlyDictionary<int, AstNode> Nodes => nodesById;

	/// <summary>
	/// Finds the AST node an editor node stands for.
	/// </summary>
	/// <param name="nodeId">The editor node's id.</param>
	/// <returns>The AST node, or null if the id is not in this graph.</returns>
	public AstNode? AstNodeFor(int nodeId) => nodesById.TryGetValue(nodeId, out AstNode? node) ? node : null;

	/// <summary>
	/// Rebuilds the engine graph from the current AST, preserving on-screen positions.
	/// </summary>
	/// <remarks>
	/// Called after every structural edit. Positions are keyed by AST node identity, so a node that
	/// survives the edit stays where the user put it and only genuinely new nodes are seeded.
	/// </remarks>
	public void Rebuild()
	{
		CapturePositions();

		Engine.Clear();
		nodesById.Clear();
		idsByNode.Clear();
		ownerByOutputPin.Clear();
		slotByInputPin.Clear();
		inputPinBySlot.Clear();

		RowCounter rows = new();
		foreach (AstNode subtree in Subtrees)
		{
			SeedMissingPositions(subtree, depth: 0, nextRow: rows);
		}

		foreach (AstNode subtree in Subtrees)
		{
			CreateNodes(subtree);
		}

		foreach (AstNode subtree in Subtrees)
		{
			CreateLinks(subtree);
		}
	}

	/// <summary>
	/// Gets the roots this graph draws: the document, then anything the user has created or
	/// disconnected but not yet attached.
	/// </summary>
	/// <remarks>
	/// A node dragged out of the palette has no parent yet, and one whose link was cut has stopped
	/// having one. Neither is reachable from <see cref="Root"/>, so both would vanish from a view
	/// built by walking the tree alone — which is not what a user who just created a node expects.
	/// </remarks>
	public IEnumerable<AstNode> Subtrees
	{
		get
		{
			yield return Root;
			foreach (AstNode node in detached)
			{
				yield return node;
			}
		}
	}

	/// <summary>
	/// Gets the nodes the user has created or disconnected that are not part of the document.
	/// </summary>
	public IReadOnlyList<AstNode> Detached => detached;

	/// <summary>
	/// Replaces the document, discarding what was there.
	/// </summary>
	/// <param name="root">The document to show.</param>
	/// <remarks>
	/// Replaces the document wholesale, which is what opening a file does. Positions are keyed to
	/// node identity, so the new document is laid out afresh rather than inheriting the arrangement
	/// of the nodes it replaces.
	/// </remarks>
	public void Load(AstNode root)
	{
		Ensure.NotNull(root);

		Root = root;
		detached.Clear();
		positions.Clear();
		Rebuild();
	}

	/// <summary>
	/// Adds a node to the graph without attaching it to anything.
	/// </summary>
	/// <param name="node">The node to add.</param>
	/// <param name="position">Where to place it.</param>
	/// <returns>The new editor node's id.</returns>
	public int AddDetached(AstNode node, Vector2 position)
	{
		Ensure.NotNull(node);

		positions[node] = position;
		detached.Add(node);
		Rebuild();
		return idsByNode[node];
	}

	/// <summary>
	/// Reports where a node currently sits.
	/// </summary>
	/// <param name="node">The node to locate.</param>
	/// <returns>Its parent, slot and position, or <see cref="AstLocation.Detached"/> if it has no parent.</returns>
	public AstLocation LocationOf(AstNode node)
	{
		Ensure.NotNull(node);

		foreach (AstNode subtree in Subtrees)
		{
			if (TryFindParent(subtree, node, out AstNode? parent, out AstSlot? slot, out int index))
			{
				return new AstLocation(parent, slot, index);
			}
		}

		return AstLocation.Detached;
	}

	/// <summary>
	/// Puts a node where a location says it belongs, taking it out of wherever it is now.
	/// </summary>
	/// <param name="node">The node to move.</param>
	/// <param name="location">Where to put it. <see cref="AstLocation.Detached"/> leaves it parentless.</param>
	/// <returns>True if the node was moved.</returns>
	/// <remarks>
	/// This is the single operation every edit is expressed in terms of, and therefore the single
	/// operation every undo is expressed in terms of too — connecting, disconnecting and reparenting
	/// are all "put this node there", and each one's inverse is "put it back where it was". Undo
	/// having one implementation rather than one per edit is what keeps it correct as edits are added.
	/// <para>
	/// It refers to nodes rather than pins deliberately: pin identifiers are reassigned by every
	/// <see cref="Rebuild"/>, so a command holding one would be reapplying an edit to whatever
	/// happened to inherit that number.
	/// </para>
	/// </remarks>
	public bool MoveTo(AstNode node, AstLocation location)
	{
		Ensure.NotNull(node);

		if (ReferenceEquals(node, Root))
		{
			return false;
		}

		DetachFromParent(node);
		detached.Remove(node);

		if (location.Parent is null)
		{
			detached.Add(node);
		}
		else if (!AstSchema.TryAttachAt(location.Parent, location.Slot!, location.Index, node))
		{
			// The slot will not take it any more, so the node stays in the graph rather than vanishing.
			detached.Add(node);
			Rebuild();
			return false;
		}

		Rebuild();
		return true;
	}

	/// <summary>
	/// Takes a node out of the graph entirely, along with everything under it.
	/// </summary>
	/// <param name="node">The node to remove.</param>
	/// <returns>True if the node was removed.</returns>
	/// <remarks>
	/// Addresses the node itself rather than its editor identifier, so an undo recorded against it
	/// still means the same node after the rebuild that reassigned the identifiers.
	/// </remarks>
	public bool RemoveNode(AstNode node)
	{
		Ensure.NotNull(node);

		if (ReferenceEquals(node, Root) || !idsByNode.ContainsKey(node))
		{
			return false;
		}

		DetachFromParent(node);
		detached.Remove(node);
		ForgetPositions(node);
		Rebuild();
		return true;
	}

	/// <summary>
	/// Works out what connecting two pins would mean, and whether it is allowed, without doing it.
	/// </summary>
	/// <param name="outputPinId">The pin of the node being attached.</param>
	/// <param name="inputPinId">The slot pin it should fill.</param>
	/// <param name="child">The node being attached, when the connection is legal.</param>
	/// <param name="target">Where it would end up, when the connection is legal.</param>
	/// <returns>Whether the connection is allowed, and why not when it is not.</returns>
	/// <remarks>
	/// Separate from <see cref="Connect"/> so a caller can record an undo step only for an edit that
	/// is going to happen. Recording first and discovering the refusal afterwards would leave a
	/// no-op on the undo stack.
	/// </remarks>
	public AstConnectResult ValidateConnection(int outputPinId, int inputPinId, out AstNode? child, out AstLocation target)
	{
		child = null;
		target = AstLocation.Detached;

		if (!ownerByOutputPin.TryGetValue(outputPinId, out AstNode? candidate))
		{
			return new AstConnectResult(false, "That output pin is not in this graph.");
		}

		if (!slotByInputPin.TryGetValue(inputPinId, out SlotPin? slotPin))
		{
			return new AstConnectResult(false, "That input pin is not in this graph.");
		}

		if (ReferenceEquals(candidate, slotPin.Parent))
		{
			return new AstConnectResult(false, "A node cannot be its own child.");
		}

		if (Contains(candidate, slotPin.Parent))
		{
			return new AstConnectResult(false, "That would put the node inside itself.");
		}

		if (!AstSchema.Accepts(slotPin.Slot, candidate))
		{
			return new AstConnectResult(false, $"{AstSchema.Describe(candidate)} cannot fill {slotPin.Slot.Name}.");
		}

		child = candidate;
		target = new AstLocation(slotPin.Parent, slotPin.Slot, slotPin.Index);
		return new AstConnectResult(true, $"Connect {AstSchema.Describe(candidate)} to {slotPin.Slot.Name} of {AstSchema.Describe(slotPin.Parent)}");
	}

	/// <summary>
	/// Connects a node's output pin to a slot pin on another node.
	/// </summary>
	/// <param name="outputPinId">The pin of the node being attached.</param>
	/// <param name="inputPinId">The slot pin it should fill.</param>
	/// <returns>What happened, and why if it was refused.</returns>
	/// <remarks>
	/// A node has exactly one parent, so connecting one that already has a parent moves it rather
	/// than sharing it. Connecting a node into its own subtree is refused: the AST is a tree, and a
	/// cycle would make every walk over it non-terminating.
	/// </remarks>
	public AstConnectResult Connect(int outputPinId, int inputPinId)
	{
		AstConnectResult check = ValidateConnection(outputPinId, inputPinId, out AstNode? child, out AstLocation target);
		if (!check.Success || child is null)
		{
			return check;
		}

		return MoveTo(child, target)
			? new AstConnectResult(true, "Connected.")
			: new AstConnectResult(false, $"{target.Slot!.Name} would not take {AstSchema.Describe(child)}.");
	}

	/// <summary>
	/// Reports which node a link attaches, without cutting it.
	/// </summary>
	/// <param name="linkId">The link to look at.</param>
	/// <returns>The child the link attaches, or null if there is no such link.</returns>
	/// <remarks>
	/// Separate from <see cref="Disconnect"/> so a caller can record an undo step against the node
	/// itself. A command holding the link identifier would be meaningless after the next
	/// <see cref="Rebuild"/>, which reassigns them.
	/// </remarks>
	public AstNode? ChildOfLink(int linkId)
	{
		Link? link = Engine.Links.FirstOrDefault(l => l.Id == linkId);
		return link is not null && ownerByOutputPin.TryGetValue(link.OutputPinId, out AstNode? child)
			? child
			: null;
	}

	/// <summary>
	/// Cuts a link, leaving the child in the graph but no longer part of the document.
	/// </summary>
	/// <param name="linkId">The link to cut.</param>
	/// <returns>True if the link was found and cut.</returns>
	public bool Disconnect(int linkId)
	{
		AstNode? child = ChildOfLink(linkId);
		return child is not null && MoveTo(child, AstLocation.Detached);
	}

	/// <summary>
	/// Removes a node and everything under it.
	/// </summary>
	/// <param name="nodeId">The editor node to remove.</param>
	/// <returns>True if the node was removed.</returns>
	/// <remarks>The document's root is never removed: a graph with no root has nothing to generate from.</remarks>
	public bool Remove(int nodeId) =>
		nodesById.TryGetValue(nodeId, out AstNode? node) && RemoveNode(node);

	/// <summary>
	/// Lists the places the document is not yet complete.
	/// </summary>
	/// <returns>One problem per outstanding operand or stranded node, empty when the document is whole.</returns>
	/// <remarks>
	/// A graph is legitimately incomplete while it is being edited, so this reports rather than
	/// throws. It is what an editor shows in a problems panel, and what a caller should consult
	/// before handing <see cref="Root"/> to a generator.
	/// </remarks>
	public IReadOnlyList<AstGraphProblem> Validate()
	{
		List<AstGraphProblem> problems = [];
		Inspect(Root, problems);

		foreach (AstNode node in detached)
		{
			problems.Add(new AstGraphProblem(node, $"{AstSchema.Describe(node)} is not connected to anything."));
			Inspect(node, problems);
		}

		return problems;
	}

	private static void Inspect(AstNode node, List<AstGraphProblem> problems)
	{
		foreach (AstSlot slot in AstSchema.SlotsOf(node))
		{
			IReadOnlyList<AstNode> children = AstSchema.ChildrenOf(node, slot);

			if (slot.Cardinality == AstSlotCardinality.One
				&& (children.Count == 0 || AstSchema.IsUnfilled(children[0])))
			{
				problems.Add(new AstGraphProblem(node, $"{AstSchema.Describe(node)} has nothing in {slot.Name}."));
			}

			foreach (AstNode child in children)
			{
				Inspect(child, problems);
			}
		}
	}

	/// <summary>
	/// Reports whether one node sits anywhere inside another's subtree.
	/// </summary>
	/// <param name="subtree">The node whose subtree to search.</param>
	/// <param name="candidate">The node to look for.</param>
	/// <returns>True if <paramref name="candidate"/> is <paramref name="subtree"/> or below it.</returns>
	public static bool Contains(AstNode subtree, AstNode candidate)
	{
		Ensure.NotNull(subtree);
		Ensure.NotNull(candidate);

		return ReferenceEquals(subtree, candidate)
			|| ChildrenInOrder(subtree).Any(child => Contains(child, candidate));
	}

	private void DetachFromParent(AstNode node)
	{
		foreach (AstNode subtree in Subtrees)
		{
			if (TryFindParent(subtree, node, out AstNode? parent, out AstSlot? slot, out int index))
			{
				AstSchema.TryDetachAt(parent!, slot!, index);
				return;
			}
		}
	}

	private static bool TryFindParent(AstNode search, AstNode target, out AstNode? parent, out AstSlot? slot, out int index)
	{
		foreach (AstSlot candidateSlot in AstSchema.SlotsOf(search))
		{
			IReadOnlyList<AstNode> children = AstSchema.ChildrenOf(search, candidateSlot);
			for (int i = 0; i < children.Count; i++)
			{
				if (ReferenceEquals(children[i], target))
				{
					parent = search;
					slot = candidateSlot;
					index = i;
					return true;
				}

				if (TryFindParent(children[i], target, out parent, out slot, out index))
				{
					return true;
				}
			}
		}

		parent = null;
		slot = null;
		index = 0;
		return false;
	}

	private void ForgetPositions(AstNode node)
	{
		positions.Remove(node);
		foreach (AstNode child in ChildrenInOrder(node))
		{
			ForgetPositions(child);
		}
	}

	/// <summary>
	/// Reads the on-screen positions back out of the engine, so an edit does not discard where the
	/// user or the physics put each node.
	/// </summary>
	private void CapturePositions()
	{
		foreach (Node node in Engine.Nodes)
		{
			if (nodesById.TryGetValue(node.Id, out AstNode? astNode))
			{
				positions[astNode] = node.Position;
			}
		}
	}

	/// <summary>
	/// Gives every node without a remembered position one, laid out by depth and sibling order.
	/// </summary>
	/// <param name="node">The subtree to seed.</param>
	/// <param name="depth">The node's depth from the root.</param>
	/// <param name="nextRow">The running row allocator, shared across the walk.</param>
	/// <remarks>
	/// Only a starting arrangement: the force-directed layout takes over from here. Seeding by depth
	/// rather than at the origin matters because a force-directed layout started from coincident
	/// points has no gradient to work with.
	/// </remarks>
	private void SeedMissingPositions(AstNode node, int depth, RowCounter nextRow)
	{
		if (!positions.ContainsKey(node))
		{
			positions[node] = new Vector2(depth * DepthSpacing, nextRow.Take() * SiblingSpacing);
		}

		foreach (AstNode child in ChildrenInOrder(node))
		{
			SeedMissingPositions(child, depth + 1, nextRow);
		}
	}

	/// <summary>
	/// Creates an editor node for every AST node in the tree, recording the pin mapping.
	/// </summary>
	/// <param name="node">The subtree to create nodes for.</param>
	private void CreateNodes(AstNode node)
	{
		List<string> inputPinNames = [];
		List<SlotPin> inputSlots = [];

		foreach (AstSlot slot in AstSchema.SlotsOf(node))
		{
			int filled = AstSchema.ChildrenOf(node, slot).Count;
			int pinCount = slot.Cardinality == AstSlotCardinality.One ? 1 : filled + SpareVariadicPins;

			for (int index = 0; index < pinCount; index++)
			{
				inputPinNames.Add(PinLabel(slot, index));
				inputSlots.Add(new SlotPin(node, slot, index));
			}
		}

		Node created = Engine.CreateNode(positions[node], AstSchema.Describe(node), inputPinNames, ["out"]);

		nodesById[created.Id] = node;
		idsByNode[node] = created.Id;
		ownerByOutputPin[created.OutputPins[0].Id] = node;

		for (int i = 0; i < created.InputPins.Count; i++)
		{
			slotByInputPin[created.InputPins[i].Id] = inputSlots[i];
			inputPinBySlot[inputSlots[i]] = created.InputPins[i].Id;
		}

		foreach (AstNode child in ChildrenInOrder(node))
		{
			CreateNodes(child);
		}
	}

	/// <summary>
	/// Links every child's output pin to the parent slot pin it fills.
	/// </summary>
	/// <param name="node">The subtree to link.</param>
	private void CreateLinks(AstNode node)
	{
		foreach (AstSlot slot in AstSchema.SlotsOf(node))
		{
			IReadOnlyList<AstNode> children = AstSchema.ChildrenOf(node, slot);
			for (int index = 0; index < children.Count; index++)
			{
				int childOutputPin = OutputPinOf(children[index]);
				int parentInputPin = InputPinOf(node, slot, index);
				Engine.TryCreateLink(childOutputPin, parentInputPin);
			}
		}

		foreach (AstNode child in ChildrenInOrder(node))
		{
			CreateLinks(child);
		}
	}

	/// <summary>
	/// Enumerates a node's children across all its slots, in slot then index order.
	/// </summary>
	/// <param name="node">The node whose children to enumerate.</param>
	/// <returns>The children, in the order the editor draws their pins.</returns>
	public static IEnumerable<AstNode> ChildrenInOrder(AstNode node)
	{
		Ensure.NotNull(node);

		foreach (AstSlot slot in AstSchema.SlotsOf(node))
		{
			foreach (AstNode child in AstSchema.ChildrenOf(node, slot))
			{
				yield return child;
			}
		}
	}

	/// <summary>
	/// Names a slot's pin, numbering it only where the slot can hold more than one child.
	/// </summary>
	/// <param name="slot">The slot the pin belongs to.</param>
	/// <param name="index">The pin's index within the slot.</param>
	/// <returns>The pin's label.</returns>
	/// <remarks>
	/// This is the pin's <see cref="Pin.DisplayName"/>, not its <see cref="Pin.Name"/>: the engine
	/// assigns every pin a positional name of its own ("In 1", "In 2") and keeps the caller's label
	/// as the display name, so a pin is matched to a slot by the former.
	/// </remarks>
	public static string PinLabel(AstSlot slot, int index)
	{
		Ensure.NotNull(slot);

		return slot.Cardinality == AstSlotCardinality.One
			? slot.Name
			: string.Create(CultureInfo.InvariantCulture, $"{slot.Name}[{index}]");
	}

	private int OutputPinOf(AstNode node) =>
		Engine.Nodes.First(n => n.Id == idsByNode[node]).OutputPins[0].Id;

	private int InputPinOf(AstNode parent, AstSlot slot, int index) =>
		inputPinBySlot[new SlotPin(parent, slot, index)];

	/// <summary>
	/// One input pin's meaning: the slot of the parent it fills, and at which position.
	/// </summary>
	/// <param name="Parent">The AST node owning the slot.</param>
	/// <param name="Slot">The slot the pin fills.</param>
	/// <param name="Index">The pin's position within a variadic slot; always zero for a single one.</param>
	private sealed record SlotPin(AstNode Parent, AstSlot Slot, int Index);

	/// <summary>
	/// Hands out successive row numbers across one seeding walk.
	/// </summary>
	private sealed class RowCounter
	{
		private int next;

		/// <summary>
		/// Takes the next row.
		/// </summary>
		/// <returns>The row index.</returns>
		public int Take() => next++;
	}
}
