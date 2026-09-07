// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using ktsu.Coder.Ast;
using ktsu.ImGuiNodeEditor;
using ktsu.UndoRedo;
using ktsu.UndoRedo.Contracts;
using ktsu.UndoRedo.Core.Services;

/// <summary>
/// Draws an <see cref="AstGraph"/> and turns the gestures over it into edits.
/// </summary>
/// <remarks>
/// Everything that decides what an edit means lives in <see cref="AstGraph"/> and
/// <see cref="AstSchema"/>, which is why those can be tested headlessly. This type only reads what
/// the user did and asks the graph to do it, so the part that cannot be tested without a GPU holds
/// no rules of its own.
/// <para>
/// Undo is <c>ktsu.UndoRedo</c>'s, recorded here rather than inside <see cref="AstGraph"/> because
/// only the caller knows where one user-visible edit begins and ends. Reparenting is expressed as
/// <see cref="AstGraph.MoveTo"/>, whose inverse is the same call with the location the node came
/// from, so connecting, disconnecting and dragging a node between slots share one implementation of
/// undo rather than needing one each. The stack therefore holds a description of what each step did
/// rather than an opaque snapshot, and it knows where the document was last saved.
/// </para>
/// </remarks>
/// <param name="root">The AST to edit.</param>
public sealed class AstGraphEditor(AstNode root)
{
	private readonly NodeEditorRenderer renderer = new();

	private string statusMessage = string.Empty;

	private string fieldBuffer = string.Empty;

	private string? editingField;

	/// <summary>
	/// Gets the graph being edited.
	/// </summary>
	public AstGraph Graph { get; } = new AstGraph(root);

	/// <summary>
	/// Gets the undo stack, which also tracks whether the document has changed since it was saved.
	/// </summary>
	/// <remarks>
	/// Exposed rather than wrapped: a caller that wants to list what has been done, mark the document
	/// saved, or ask whether it still has unsaved work is asking the stack, and re-declaring each of
	/// those here would only be a second name for the same thing.
	/// </remarks>
	public IUndoRedoService History { get; } =
		new UndoRedoService(new StackManager(), new SaveBoundaryManager(), new CommandMerger());

	/// <summary>
	/// Gets or sets a value indicating whether the force-directed layout is running.
	/// </summary>
	/// <remarks>
	/// On by default, since arranging a tree by hand is the work this library exists to avoid. A user
	/// who has arranged things deliberately turns it off rather than fighting it.
	/// </remarks>
	public bool LayoutRunning { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the physics debug overlays are drawn.
	/// </summary>
	public bool ShowDebugOverlays { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the inspector panel is drawn.
	/// </summary>
	public bool ShowInspector { get; set; } = true;

	/// <summary>
	/// Gets the problems the document currently has, refreshed each frame it is drawn.
	/// </summary>
	public IReadOnlyList<AstGraphProblem> Problems { get; private set; } = [];

	/// <summary>
	/// Gets the node the inspector is editing, or null when nothing is selected.
	/// </summary>
	/// <remarks>
	/// Held as the AST node rather than the editor's identifier for it: every structural edit rebuilds
	/// the engine graph and reassigns those identifiers, so a selection remembered by number would
	/// follow whichever node inherited it.
	/// </remarks>
	public AstNode? SelectedNode { get; private set; }

	/// <summary>
	/// Draws the editor and applies whatever the user did this frame.
	/// </summary>
	/// <param name="size">The area to draw the graph in.</param>
	/// <param name="deltaTime">Seconds since the last frame, for the layout simulation.</param>
	public void Draw(Vector2 size, float deltaTime)
	{
		DrawToolbar();

		// The inspector's room is taken out of the graph's rather than added below it: the caller gave
		// this editor a fixed area, and a panel drawn past the bottom of it is one the user has to
		// scroll a node editor to reach.
		//
		// The canvas gets a child window of its own because that is the only thing the node editor
		// will size itself to: it fills whatever window it is drawn in, whatever size it is passed.
		float reserved = ShowInspector ? InspectorHeight : 0f;
		Vector2 graphSize = new(size.X, Math.Max(size.Y - reserved, MinimumGraphHeight));

		Vector2 origin = ImGui.GetCursorScreenPos();
		ImGui.BeginChild("ast-graph-canvas", graphSize, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

		renderer.Render(Graph.Engine, graphSize);

		ApplyNodeMovement();
		ApplyLinkChanges();
		TrackSelection();
		ApplyDeletions();
		DrawPalette();

		if (ShowDebugOverlays)
		{
			renderer.RenderDebugOverlays(Graph.Engine, origin, graphSize, showDebug: true);
		}

		ImGui.EndChild();

		DrawInspector();

		if (LayoutRunning)
		{
			// The layout's gravity pulls everything towards the world origin, and node positions are
			// canvas-relative, so an origin left at zero pulls the whole graph into the top-left
			// corner and off the edge of it. Aiming it at the middle of the canvas is what keeps an
			// arrangement the user has not touched inside the view, and it follows the window as it
			// is resized.
			Graph.Engine.WorldOrigin = graphSize * 0.5f;
			Graph.Engine.UpdatePhysics(deltaTime);
		}

		Problems = Graph.Validate();
	}

	/// <summary>
	/// The height the inspector panel is given at the bottom of the editor's area.
	/// </summary>
	private const float InspectorHeight = 210f;

	/// <summary>
	/// The least room the graph keeps, however little the editor was given.
	/// </summary>
	private const float MinimumGraphHeight = 120f;

	/// <summary>
	/// Draws the row of controls above the graph.
	/// </summary>
	private void DrawToolbar()
	{
		bool layoutRunning = LayoutRunning;
		if (ImGui.Checkbox("Auto layout", ref layoutRunning))
		{
			LayoutRunning = layoutRunning;
		}

		ImGui.SameLine();
		bool showDebug = ShowDebugOverlays;
		if (ImGui.Checkbox("Debug overlays", ref showDebug))
		{
			ShowDebugOverlays = showDebug;
		}

		ImGui.SameLine();
		ImGui.BeginDisabled(!History.CanUndo);
		if (ImGui.Button("Undo"))
		{
			Undo();
		}

		ImGui.EndDisabled();

		ImGui.SameLine();
		ImGui.BeginDisabled(!History.CanRedo);
		if (ImGui.Button("Redo"))
		{
			Redo();
		}

		ImGui.EndDisabled();

		ImGui.SameLine();
		if (ImGui.Button("Fit"))
		{
			FitView();
		}

		ImGui.SameLine();
		bool showInspector = ShowInspector;
		if (ImGui.Checkbox("Inspector", ref showInspector))
		{
			ShowInspector = showInspector;
		}

		ImGui.SameLine();
		ImGui.TextUnformatted(Problems.Count == 0
			? statusMessage
			: $"{Problems.Count} outstanding");
	}

	/// <summary>
	/// Remembers which node the user has selected, so the inspector has something to edit.
	/// </summary>
	/// <remarks>
	/// An empty selection is not treated as a deselection: clicking the canvas to pan, or opening the
	/// palette, clears ImNodes' selection, and an inspector that emptied itself every time the user
	/// moved the view would be unusable. The selection therefore changes only when a different node
	/// is picked, or when the selected node leaves the graph.
	/// </remarks>
	private void TrackSelection()
	{
		int selectedCount = ImNodes.NumSelectedNodes();
		if (selectedCount > 0)
		{
			int[] selected = new int[selectedCount];
			ImNodes.GetSelectedNodes(ref selected[0]);
			SelectedNode = Graph.AstNodeFor(selected[0]);
			return;
		}

		if (SelectedNode is not null && !Graph.Nodes.Values.Any(node => ReferenceEquals(node, SelectedNode)))
		{
			SelectedNode = null;
		}
	}

	/// <summary>
	/// Draws the panel that edits the selected node's own properties: its name, its type, its
	/// operator, the value of a literal, and how many children its variadic slots hold.
	/// </summary>
	/// <remarks>
	/// This is the half of editing that links cannot express. A node's children are its structure and
	/// are edited by dragging; everything else about it is a value, and a value needs somewhere to be
	/// typed.
	/// </remarks>
	private void DrawInspector()
	{
		if (!ShowInspector)
		{
			return;
		}

		ImGui.Separator();
		ImGui.BeginChild("ast-inspector", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);

		AstNode? node = SelectedNode;
		if (node is null)
		{
			ImGui.TextUnformatted("Select a node to edit it.");
			ImGui.EndChild();
			return;
		}

		ImGui.TextUnformatted(AstSchema.Describe(node));

		foreach (AstField field in AstFields.Of(node))
		{
			DrawField(node, field);
		}

		foreach (AstSlot slot in AstSchema.SlotsOf(node))
		{
			if (slot.Cardinality == AstSlotCardinality.Many)
			{
				DrawSlotCount(node, slot);
			}
		}

		DrawConversions(node);
		ImGui.EndChild();
	}

	/// <summary>
	/// Draws the menu of kinds the selected node could be turned into.
	/// </summary>
	/// <param name="node">The node being inspected.</param>
	/// <remarks>
	/// Grouped the same way the palette is, so the entry a user is looking for is where they last saw
	/// it — and because eighteen binary operators do not belong in a flat list either.
	/// </remarks>
	private void DrawConversions(AstNode node)
	{
		List<AstNodeTemplate> conversions = [.. ConversionsFor(node)];
		if (conversions.Count == 0)
		{
			return;
		}

		if (!ImGui.BeginMenu("Convert to"))
		{
			return;
		}

		foreach (string category in conversions.Select(template => template.Category).Distinct())
		{
			if (!ImGui.BeginMenu(category))
			{
				continue;
			}

			foreach (AstNodeTemplate template in conversions.Where(entry => string.Equals(entry.Category, category, StringComparison.Ordinal)))
			{
				if (ImGui.MenuItem(template.Group is null ? template.Label : $"{template.Group}: {template.Label}"))
				{
					Convert(node, template.Create());
				}
			}

			ImGui.EndMenu();
		}

		ImGui.EndMenu();
	}

	/// <summary>
	/// Draws one editable property, using the widget its kind calls for.
	/// </summary>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	/// <remarks>
	/// Text is committed when the box is left or the user presses enter rather than on every
	/// keystroke, so typing a name puts one step on the undo stack instead of one per character.
	/// </remarks>
	private void DrawField(AstNode node, AstField field)
	{
		ImGui.SetNextItemWidth(180f);

		switch (field.Kind)
		{
			case AstFieldKind.Flag:
				bool flag = string.Equals(field.Value, "true", StringComparison.Ordinal);
				if (ImGui.Checkbox(field.Name, ref flag))
				{
					SetField(node, field.Name, flag ? "true" : "false");
				}

				break;

			case AstFieldKind.Choice:
				DrawChoiceField(node, field);
				break;

			default:
				DrawTextField(node, field);
				break;
		}
	}

	/// <summary>
	/// Draws a property the user types into.
	/// </summary>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	/// <remarks>
	/// One buffer is shared across every field, keyed by which one is being typed into: only one box
	/// can have the keyboard at a time, so a buffer per field would be state to keep in step with the
	/// document for no gain. A field that is not being typed into shows the document's value, so an
	/// undo while the box is open is reflected rather than overwritten.
	/// </remarks>
	private void DrawTextField(AstNode node, AstField field)
	{
		bool editing = string.Equals(editingField, field.Name, StringComparison.Ordinal);
		string buffer = editing ? fieldBuffer : field.Value;

		if (ImGui.InputText(field.Name, ref buffer, 256))
		{
			editingField = field.Name;
			fieldBuffer = buffer;
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			SetField(node, field.Name, buffer);
			editingField = null;
		}
	}

	/// <summary>
	/// Draws a property the user picks from a fixed set, which is how an operator is chosen.
	/// </summary>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	private void DrawChoiceField(AstNode node, AstField field)
	{
		AstFieldChoice? current = field.Choices.FirstOrDefault(
			choice => string.Equals(choice.Value, field.Value, StringComparison.Ordinal));

		if (!ImGui.BeginCombo(field.Name, current?.Label ?? field.Value))
		{
			return;
		}

		foreach (AstFieldChoice choice in field.Choices)
		{
			bool isCurrent = string.Equals(choice.Value, field.Value, StringComparison.Ordinal);
			if (ImGui.Selectable(choice.Label, isCurrent))
			{
				SetField(node, field.Name, choice.Value);
			}
		}

		ImGui.EndCombo();
	}

	/// <summary>
	/// Draws how many children a variadic slot holds, and the buttons that change it.
	/// </summary>
	/// <param name="node">The node whose slot to draw.</param>
	/// <param name="slot">The slot to draw.</param>
	private void DrawSlotCount(AstNode node, AstSlot slot)
	{
		int count = AstSchema.ChildrenOf(node, slot).Count;
		ImGui.TextUnformatted($"{slot.Name}: {count}");

		ImGui.SameLine();
		if (ImGui.Button($"+##add-{slot.Name}"))
		{
			AddChild(node, slot);
		}

		ImGui.SameLine();
		ImGui.BeginDisabled(count == 0);
		if (ImGui.Button($"-##remove-{slot.Name}"))
		{
			RemoveLastChild(node, slot);
		}

		ImGui.EndDisabled();
	}

	/// <summary>
	/// Steps the document back one edit.
	/// </summary>
	public void Undo()
	{
		if (!History.CanUndo)
		{
			return;
		}

		// The description of the step being reversed is read before the position moves, so the status
		// bar can say what was undone rather than just that something was. CurrentPosition indexes the
		// most recently executed command, which is precisely the one undo is about to reverse.
		string description = History.Commands[History.CurrentPosition].Description;
		if (History.Undo())
		{
			statusMessage = $"Undid: {description}";
		}
	}

	/// <summary>
	/// Steps the document forward one edit.
	/// </summary>
	public void Redo()
	{
		if (!History.CanRedo)
		{
			return;
		}

		string description = History.Commands[History.CurrentPosition + 1].Description;
		if (History.Redo())
		{
			statusMessage = $"Redid: {description}";
		}
	}

	/// <summary>
	/// Writes back the positions the user dragged nodes to, and the sizes ImNodes measured.
	/// </summary>
	/// <remarks>
	/// Dragging is not an edit to the document, so it is not recorded in the history: undoing a
	/// layout nudge is not what a user reaching for undo wants back.
	/// </remarks>
	private void ApplyNodeMovement()
	{
		foreach ((int nodeId, Vector2 position) in renderer.GetNodePositionUpdates(Graph.Engine))
		{
			Graph.Engine.UpdateNodePosition(nodeId, position);
		}

		foreach ((int nodeId, Vector2 dimensions) in renderer.GetNodeDimensionUpdates(Graph.Engine))
		{
			Graph.Engine.UpdateNodeDimensions(nodeId, dimensions);
		}

		Graph.Engine.SetDraggedNodes(renderer.CurrentlyDraggedNodes);
	}

	/// <summary>
	/// Turns a completed link drag, or a destroyed link, into an edit.
	/// </summary>
	private void ApplyLinkChanges()
	{
		int startPin = 0;
		int endPin = 0;
		if (ImNodes.IsLinkCreated(ref startPin, ref endPin))
		{
			Connect(startPin, endPin);
		}

		int destroyedLink = 0;
		if (ImNodes.IsLinkDestroyed(ref destroyedLink))
		{
			Disconnect(destroyedLink);
		}
	}

	/// <summary>
	/// Connects two pins as one undoable edit.
	/// </summary>
	/// <param name="outputPinId">The pin of the node being attached.</param>
	/// <param name="inputPinId">The slot pin it should fill.</param>
	/// <returns>What happened, and why if it was refused.</returns>
	/// <remarks>
	/// The connection is checked before anything is recorded, so a refused drag leaves the undo stack
	/// as it was rather than putting a step on it that undoes nothing.
	/// </remarks>
	public AstConnectResult Connect(int outputPinId, int inputPinId)
	{
		AstConnectResult check = Graph.ValidateConnection(outputPinId, inputPinId, out AstNode? child, out AstLocation target);
		if (!check.Success || child is null)
		{
			statusMessage = check.Message;
			return check;
		}

		AstLocation from = Graph.LocationOf(child);
		RecordMove(check.Message, ChangeType.Move, child, target, from);
		statusMessage = check.Message;
		return check;
	}

	/// <summary>
	/// Cuts a link as one undoable edit.
	/// </summary>
	/// <param name="linkId">The link to cut.</param>
	/// <returns>True if the link was found and cut.</returns>
	public bool Disconnect(int linkId)
	{
		AstNode? child = Graph.ChildOfLink(linkId);
		if (child is null)
		{
			return false;
		}

		AstLocation from = Graph.LocationOf(child);
		RecordMove($"Disconnect {AstSchema.Describe(child)}", ChangeType.Move, child, AstLocation.Detached, from);
		statusMessage = $"Disconnected {AstSchema.Describe(child)}.";
		return true;
	}

	/// <summary>
	/// Adds a node to the graph, unattached, as one undoable edit.
	/// </summary>
	/// <param name="node">The node to add.</param>
	/// <param name="position">Where to place it.</param>
	/// <remarks>
	/// A new node arrives detached and the user wires it up afterwards, so creating one and
	/// connecting it are two steps in the history rather than one. That matches what the user did.
	/// </remarks>
	public void Add(AstNode node, Vector2 position)
	{
		Ensure.NotNull(node);

		Record(
			$"Add {AstSchema.Describe(node)}",
			ChangeType.Insert,
			node,
			() => Graph.AddDetached(node, position),
			() => Graph.RemoveNode(node));

		statusMessage = $"Added {AstSchema.Describe(node)}.";
	}

	/// <summary>
	/// Brings the whole graph back into view.
	/// </summary>
	/// <returns>True if there was anything to bring into view.</returns>
	/// <remarks>
	/// The layout arranges nodes wherever the forces take them, so a document can drift off the top
	/// or the left of the view with no clue which way to scroll back. This moves the arrangement
	/// rather than the view: the renderer writes each node's position into the node editor every
	/// frame, so panning the editor is undone as soon as it is read back — the positions are the only
	/// thing that decides where a node is drawn.
	/// <para>
	/// The whole arrangement is translated, so the shape the layout settled into is preserved rather
	/// than being disturbed by the act of looking at it.
	/// </para>
	/// </remarks>
	public bool FitView()
	{
		if (Graph.Engine.Nodes.Count == 0)
		{
			return false;
		}

		const float margin = 40f;
		Vector2 offset = new(
			margin - Graph.Engine.Nodes.Min(node => node.Position.X),
			margin - Graph.Engine.Nodes.Min(node => node.Position.Y));

		foreach (Node node in Graph.Engine.Nodes.ToArray())
		{
			Graph.Engine.UpdateNodePosition(node.Id, node.Position + offset);
		}

		// Gravity pulls towards the world origin, which is still where the graph used to be: without
		// this the layout would drag everything straight back out of view.
		Graph.Engine.InitializeWorldOriginToCentroid();

		statusMessage = "Brought the graph back into view.";
		return true;
	}

	/// <summary>
	/// Selects a node, so the inspector shows it and the graph highlights it.
	/// </summary>
	/// <param name="node">The node to select.</param>
	/// <returns>True if the node is in the graph and was selected.</returns>
	/// <remarks>
	/// Selecting is not an edit, so it is not recorded: undo should not step back through what the
	/// user was looking at.
	/// </remarks>
	public bool Select(AstNode node)
	{
		Ensure.NotNull(node);

		if (!Graph.Nodes.Values.Any(candidate => ReferenceEquals(candidate, node)))
		{
			return false;
		}

		SelectedNode = node;

		// ImNodes keeps its own selection, and the highlight in the graph comes from that rather than
		// from the field above.
		int nodeId = Graph.Nodes.First(pair => ReferenceEquals(pair.Value, node)).Key;
		ImNodes.ClearNodeSelection();
		ImNodes.SelectNode(nodeId);
		return true;
	}

	/// <summary>
	/// Writes one of a node's own properties as one undoable edit.
	/// </summary>
	/// <param name="node">The node to edit.</param>
	/// <param name="fieldName">The property to write, as <see cref="AstFields"/> names it.</param>
	/// <param name="value">The value to write, as text.</param>
	/// <returns>True if the document changed.</returns>
	/// <remarks>
	/// The write is attempted before anything is recorded, because whether it is possible at all is
	/// what <see cref="AstFields.TryWrite"/> decides: half a number typed into an integer field is
	/// refused, and refusing must leave the undo stack alone. The recorded step then re-applies the
	/// same write, which is a no-op the first time and the actual edit on every redo.
	/// </remarks>
	public bool SetField(AstNode node, string fieldName, string value)
	{
		Ensure.NotNull(node);

		string? previous = AstFields.Read(node, fieldName);
		if (previous is null || !AstFields.TryWrite(node, fieldName, value))
		{
			return false;
		}

		// Rebuilding is what refreshes the node's caption: a node is titled by what it is, and what it
		// is has just changed.
		Record(
			$"Set {fieldName} of {AstSchema.Describe(node)}",
			ChangeType.Modify,
			node,
			() => Write(node, fieldName, value),
			() => Write(node, fieldName, previous));

		statusMessage = $"Set {fieldName} to {value}.";
		return true;
	}

	/// <summary>
	/// Writes a property and refreshes the view, which is the whole of what an inspector edit does.
	/// </summary>
	/// <param name="node">The node to edit.</param>
	/// <param name="fieldName">The property to write.</param>
	/// <param name="value">The value to write.</param>
	private void Write(AstNode node, string fieldName, string value)
	{
		AstFields.TryWrite(node, fieldName, value);
		Graph.Rebuild();
	}

	/// <summary>
	/// Puts a different kind of node in a node's place, as one undoable edit.
	/// </summary>
	/// <param name="existing">The node to replace.</param>
	/// <param name="replacement">The node to put there.</param>
	/// <returns>True if the document changed.</returns>
	/// <remarks>
	/// This is how a number literal becomes a text one, or a binary expression becomes a unary one,
	/// without rebuilding the connections around it. The operands the replacement can take move
	/// across; any it cannot are left on the replaced node, which stays in the graph as a loose node
	/// rather than taking them with it.
	/// </remarks>
	public bool Convert(AstNode existing, AstNode replacement)
	{
		Ensure.NotNull(existing);
		Ensure.NotNull(replacement);

		if (ReferenceEquals(existing, Graph.Root))
		{
			statusMessage = "The document's own node cannot be converted.";
			return false;
		}

		AstLocation from = Graph.LocationOf(existing);
		IReadOnlyList<(AstNode Child, AstLocation From)> moved = [];

		Record(
			$"Convert {AstSchema.Describe(existing)} to {AstSchema.Describe(replacement)}",
			ChangeType.Modify,
			existing,
			() => moved = Graph.Replace(existing, replacement),
			() =>
			{
				// Undone in the order the replacement was made: the original goes back into its place,
				// then each child that moved goes back into the slot it came from.
				Graph.Replace(replacement, existing);
				Graph.MoveTo(existing, from);
				foreach ((AstNode child, AstLocation origin) in moved)
				{
					Graph.MoveTo(child, origin);
				}

				Graph.RemoveNode(replacement);
			});

		statusMessage = $"Converted to {AstSchema.Describe(replacement)}.";
		return true;
	}

	/// <summary>
	/// Lists the kinds of node the selected one could be turned into.
	/// </summary>
	/// <param name="node">The node to convert.</param>
	/// <returns>The palette entries whose nodes would fit where this one sits.</returns>
	/// <remarks>
	/// Filtered by where the node sits rather than by what it is: a slot that takes an expression will
	/// take any expression, and offering a conversion the slot would then refuse would be offering to
	/// break the document.
	/// </remarks>
	public IEnumerable<AstNodeTemplate> ConversionsFor(AstNode node)
	{
		Ensure.NotNull(node);

		if (ReferenceEquals(node, Graph.Root))
		{
			return [];
		}

		AstSlot? slot = Graph.LocationOf(node).Slot;

		return AstNodeCatalog.Templates.Where(template =>
			slot is null || AstSchema.Accepts(slot, template.Create()));
	}

	/// <summary>
	/// Adds one more child to a variadic slot, as one undoable edit.
	/// </summary>
	/// <param name="parent">The node whose slot to grow.</param>
	/// <param name="slot">The slot to add to.</param>
	/// <returns>True if a child was added.</returns>
	/// <remarks>
	/// The child is the emptiest thing the slot will take, which is what makes adding a parameter or a
	/// statement one click rather than creating a node from the palette and dragging it into a pin.
	/// </remarks>
	public bool AddChild(AstNode parent, AstSlot slot)
	{
		Ensure.NotNull(parent);
		Ensure.NotNull(slot);

		if (slot.Cardinality != AstSlotCardinality.Many)
		{
			return false;
		}

		AstNode child = AstSchema.CreateDefaultChild(slot);
		int index = AstSchema.ChildrenOf(parent, slot).Count;

		Record(
			$"Add {AstSchema.Describe(child)} to {slot.Name} of {AstSchema.Describe(parent)}",
			ChangeType.Insert,
			child,
			() => AttachAt(parent, slot, index, child),
			() => Graph.RemoveNode(child));

		statusMessage = $"Added {AstSchema.Describe(child)} to {slot.Name}.";
		return true;
	}

	/// <summary>
	/// Removes the last child of a variadic slot, as one undoable edit.
	/// </summary>
	/// <param name="parent">The node whose slot to shrink.</param>
	/// <param name="slot">The slot to remove from.</param>
	/// <returns>True if a child was removed.</returns>
	/// <remarks>
	/// The last one rather than a chosen one: the slot is an ordered sequence, and removing from the
	/// middle is expressed by disconnecting the child the user means, which the graph already does.
	/// </remarks>
	public bool RemoveLastChild(AstNode parent, AstSlot slot)
	{
		Ensure.NotNull(parent);
		Ensure.NotNull(slot);

		IReadOnlyList<AstNode> children = AstSchema.ChildrenOf(parent, slot);
		if (children.Count == 0)
		{
			return false;
		}

		AstNode child = children[^1];
		AstLocation from = Graph.LocationOf(child);

		Record(
			$"Remove {AstSchema.Describe(child)} from {slot.Name} of {AstSchema.Describe(parent)}",
			ChangeType.Delete,
			child,
			() => Graph.RemoveNode(child),
			() => Graph.MoveTo(child, from));

		statusMessage = $"Removed {AstSchema.Describe(child)} from {slot.Name}.";
		return true;
	}

	/// <summary>
	/// Attaches a node at a position in a slot and refreshes the view.
	/// </summary>
	/// <param name="parent">The node to attach to.</param>
	/// <param name="slot">The slot to fill.</param>
	/// <param name="index">The position within the slot.</param>
	/// <param name="child">The node to attach.</param>
	private void AttachAt(AstNode parent, AstSlot slot, int index, AstNode child)
	{
		AstSchema.TryAttachAt(parent, slot, index, child);
		Graph.Rebuild();
	}

	/// <summary>
	/// Removes a node and everything under it, as one undoable edit.
	/// </summary>
	/// <param name="nodeId">The editor node to remove.</param>
	/// <returns>True if the node was removed.</returns>
	/// <remarks>
	/// The removal is checked before it is recorded, so pressing delete over the root — which is
	/// never removed — leaves the history alone rather than adding a step that does nothing.
	/// </remarks>
	public bool Remove(int nodeId)
	{
		AstNode? node = Graph.AstNodeFor(nodeId);
		if (node is null || ReferenceEquals(node, Graph.Root))
		{
			return false;
		}

		AstLocation from = Graph.LocationOf(node);
		Record(
			$"Remove {AstSchema.Describe(node)}",
			ChangeType.Delete,
			node,
			() => Graph.RemoveNode(node),
			() => Graph.MoveTo(node, from));

		statusMessage = $"Removed {AstSchema.Describe(node)}.";
		return true;
	}

	/// <summary>
	/// Records a move as an undoable step and performs it.
	/// </summary>
	/// <param name="description">What the step does, as the history shows it.</param>
	/// <param name="changeType">The kind of change, for the history's own metadata.</param>
	/// <param name="node">The node being moved.</param>
	/// <param name="to">Where it is going.</param>
	/// <param name="from">Where it came from, which is where undo puts it back.</param>
	private void RecordMove(string description, ChangeType changeType, AstNode node, AstLocation to, AstLocation from) =>
		Record(description, changeType, node, () => Graph.MoveTo(node, to), () => Graph.MoveTo(node, from));

	/// <summary>
	/// Records an edit as an undoable step and performs it.
	/// </summary>
	/// <param name="description">What the step does, as the history shows it.</param>
	/// <param name="changeType">The kind of change, for the history's own metadata.</param>
	/// <param name="node">The node the edit is about, which is what the history lists as affected.</param>
	/// <param name="apply">Performs the edit.</param>
	/// <param name="revert">Puts the document back the way it was.</param>
	private void Record(string description, ChangeType changeType, AstNode node, Action apply, Action revert) =>
		History.Execute(new DelegateCommand(
			description,
			apply,
			revert,
			changeType,
			[AstSchema.Describe(node)]));

	/// <summary>
	/// Draws one menu's worth of palette entries, creating whatever is picked.
	/// </summary>
	/// <param name="templates">The entries to list.</param>
	/// <param name="dropPosition">Where a created node is placed.</param>
	private void DrawPaletteEntries(IEnumerable<AstNodeTemplate> templates, Vector2 dropPosition)
	{
		foreach (AstNodeTemplate template in templates)
		{
			if (ImGui.MenuItem(template.Label))
			{
				Add(template.Create(), dropPosition);
			}
		}
	}

	/// <summary>
	/// Removes the selected nodes and links when the user asks for it.
	/// </summary>
	private void ApplyDeletions()
	{
		if (!ImGui.IsKeyPressed(ImGuiKey.Delete) || !ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
		{
			return;
		}

		int selectedCount = ImNodes.NumSelectedNodes();
		if (selectedCount <= 0)
		{
			return;
		}

		int[] selected = new int[selectedCount];
		ImNodes.GetSelectedNodes(ref selected[0]);

		bool removedAny = false;
		foreach (int nodeId in selected)
		{
			removedAny |= Remove(nodeId);
		}

		if (removedAny)
		{
			ImNodes.ClearNodeSelection();
		}
		else
		{
			statusMessage = "Nothing there can be removed.";
		}
	}

	/// <summary>
	/// Draws the right-click palette and creates whatever the user picks.
	/// </summary>
	/// <remarks>
	/// A new node is created where the pointer is and left unattached, which is why
	/// <see cref="AstGraph"/> draws detached nodes at all: it has to appear before it can be wired up.
	/// </remarks>
	private void DrawPalette()
	{
		if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
		{
			ImGui.OpenPopup("ast-graph-palette");
		}

		if (!ImGui.BeginPopup("ast-graph-palette"))
		{
			return;
		}

		Vector2 dropPosition = ImGui.GetMousePosOnOpeningCurrentPopup();

		foreach (string category in AstNodeCatalog.Categories)
		{
			if (!ImGui.BeginMenu(category))
			{
				continue;
			}

			DrawPaletteEntries(AstNodeCatalog.InGroup(category, null), dropPosition);

			// A category's operator entries are one submenu deep, so eighteen binary operators do not
			// bury the four literals.
			foreach (string group in AstNodeCatalog.GroupsIn(category))
			{
				if (ImGui.BeginMenu(group))
				{
					DrawPaletteEntries(AstNodeCatalog.InGroup(category, group), dropPosition);
					ImGui.EndMenu();
				}
			}

			ImGui.EndMenu();
		}

		ImGui.EndPopup();
	}
}
