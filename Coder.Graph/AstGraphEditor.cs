// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
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
	/// Gets the problems the document currently has, refreshed each frame it is drawn.
	/// </summary>
	public IReadOnlyList<AstGraphProblem> Problems { get; private set; } = [];

	/// <summary>
	/// Draws the editor and applies whatever the user did this frame.
	/// </summary>
	/// <param name="size">The area to draw the graph in.</param>
	/// <param name="deltaTime">Seconds since the last frame, for the layout simulation.</param>
	public void Draw(Vector2 size, float deltaTime)
	{
		DrawToolbar();

		Vector2 origin = ImGui.GetCursorScreenPos();
		renderer.Render(Graph.Engine, size);

		ApplyNodeMovement();
		ApplyLinkChanges();
		ApplyDeletions();
		DrawPalette();

		if (LayoutRunning)
		{
			Graph.Engine.UpdatePhysics(deltaTime);
		}

		if (ShowDebugOverlays)
		{
			renderer.RenderDebugOverlays(Graph.Engine, origin, size, showDebug: true);
		}

		Problems = Graph.Validate();
	}

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
		ImGui.TextUnformatted(Problems.Count == 0
			? statusMessage
			: $"{Problems.Count} outstanding");
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

			foreach (AstNodeTemplate template in AstNodeCatalog.InCategory(category))
			{
				if (ImGui.MenuItem(template.Label))
				{
					Add(template.Create(), dropPosition);
				}
			}

			ImGui.EndMenu();
		}

		ImGui.EndPopup();
	}
}
