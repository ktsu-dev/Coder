// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System.Collections.Generic;
using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using ktsu.Coder.Ast;
using ktsu.ImGuiNodeEditor;

/// <summary>
/// Draws an <see cref="AstGraph"/> and turns the gestures over it into edits.
/// </summary>
/// <remarks>
/// Everything that decides what an edit means lives in <see cref="AstGraph"/> and
/// <see cref="AstSchema"/>, which is why those can be tested headlessly. This type only reads what
/// the user did and asks the graph to do it, so the part that cannot be tested without a GPU holds
/// no rules of its own.
/// <para>
/// Undo snapshots are taken here rather than inside <see cref="AstGraph"/>, because only the caller
/// knows where one user-visible edit begins and ends: dragging a link is one edit even though it
/// passes through several graph calls.
/// </para>
/// </remarks>
/// <param name="root">The AST to edit.</param>
public sealed class AstGraphEditor(AstNode root)
{
	private readonly NodeEditorRenderer renderer = new();
	private readonly AstGraphHistory history = new();
	private string statusMessage = string.Empty;

	/// <summary>
	/// Gets the graph being edited.
	/// </summary>
	public AstGraph Graph { get; } = new AstGraph(root);

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
		ImGui.BeginDisabled(!history.CanUndo);
		if (ImGui.Button("Undo"))
		{
			Undo();
		}

		ImGui.EndDisabled();

		ImGui.SameLine();
		ImGui.BeginDisabled(!history.CanRedo);
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
		AstNode? previous = history.Undo(Graph.Root);
		if (previous is not null)
		{
			Graph.Load(previous);
			statusMessage = "Undone.";
		}
	}

	/// <summary>
	/// Steps the document forward one edit.
	/// </summary>
	public void Redo()
	{
		AstNode? next = history.Redo(Graph.Root);
		if (next is not null)
		{
			Graph.Load(next);
			statusMessage = "Redone.";
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
			history.Record(Graph.Root);
			AstConnectResult result = Graph.Connect(startPin, endPin);
			if (!result.Success)
			{
				// The user aimed at a pin the slot will not take; say why rather than doing nothing.
				history.Undo(Graph.Root);
			}

			statusMessage = result.Message;
		}

		int destroyedLink = 0;
		if (ImNodes.IsLinkDestroyed(ref destroyedLink))
		{
			history.Record(Graph.Root);
			if (!Graph.Disconnect(destroyedLink))
			{
				history.Undo(Graph.Root);
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

		history.Record(Graph.Root);
		bool removedAny = false;
		foreach (int nodeId in selected)
		{
			removedAny |= Graph.Remove(nodeId);
		}

		if (!removedAny)
		{
			history.Undo(Graph.Root);
			statusMessage = "Nothing there can be removed.";
		}
		else
		{
			ImNodes.ClearNodeSelection();
			statusMessage = "Removed.";
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
					history.Record(Graph.Root);
					AstNode created = template.Create();
					Graph.AddDetached(created, dropPosition);
					statusMessage = $"Added {AstSchema.Describe(created)}.";
				}
			}

			ImGui.EndMenu();
		}

		ImGui.EndPopup();
	}
}
