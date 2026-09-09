// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Graph;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using ktsu.Coder.Ast;
using ktsu.ForceDirectedLayout;
using ktsu.ImGui.NodeEditor;
using ktsu.ImGui.Probes;
using ktsu.ImGui.Widgets;
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

	/// <summary>
	/// How the inspector's property grid is laid out.
	/// </summary>
	/// <remarks>
	/// A name column of very nearly half the panel, rather than the roughly a third the widget's
	/// default leaves it. The weight is a share of the value column's own rather than of the whole,
	/// so 0.95 against that column's 1 is the even split a panel this narrow needs: the inspector
	/// sits beside the graph, and the names it draws — <c>Visibility</c>, <c>Parameters</c> — are
	/// longer than most of the values beside them. It stays the user's to drag either way.
	/// <para>
	/// Fractions are spelled to fifteen significant digits rather than the widget's six decimal
	/// places. A literal is a value the generated source will carry, so the row has to show what the
	/// document holds: 3.14 has to read as 3.14 rather than as 3.140000, and a value with more digits
	/// than that has to keep them when the user opens the box to change something else about it.
	/// </para>
	/// </remarks>
	private static readonly ImGuiWidgets.PropertyGridOptions InspectorGridOptions = new()
	{
		LabelColumnWeight = 0.95f,
		DoubleFormat = "%.15g",
	};

	private string statusMessage = string.Empty;

	private string fieldBuffer = string.Empty;

	private bool fitted;

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
	/// Gets or sets how large the graph is drawn, as a multiplier: 1 draws it at its own size.
	/// </summary>
	/// <remarks>
	/// The node editor's own, forwarded rather than held here. It is the renderer that scales node
	/// positions on their way into ImNodes and unscales them on the way back, which is the only seam
	/// a zoom can sit at when ImNodes has none of its own; a second copy of the value here would only
	/// be something to keep in step with it.
	/// </remarks>
	public float Zoom
	{
		get => renderer.Zoom;
		set => renderer.Zoom = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether <see cref="Draw"/> puts the inspector panel beside the
	/// canvas itself.
	/// </summary>
	/// <remarks>
	/// On for a host that just wants an editor. A host with a layout of its own turns it off and
	/// calls <see cref="DrawInspector"/> wherever it wants the panel, which is what the Coder
	/// application does — there it is a pane the user can resize rather than a fixed column.
	/// </remarks>
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

		// The inspector's room is taken out of the graph's rather than added beside it: the caller gave
		// this editor a fixed area, and a panel drawn past the edge of it is one the user has to
		// scroll a node editor to reach.
		//
		// It goes down the right-hand side rather than along the bottom because a node's properties
		// are a column of labelled rows, which wants height and very little width — the shape a strip
		// across the bottom has exactly backwards. It also leaves the graph its full height, which is
		// the direction a tree of nodes grows in.
		//
		// The canvas gets a child window of its own because that is the only thing the node editor
		// will size itself to: it fills whatever window it is drawn in, whatever size it is passed.
		float reserved = ShowInspector ? InspectorWidth : 0f;
		Vector2 graphSize = new(Math.Max(size.X - reserved, MinimumGraphWidth), size.Y);

		// The origin of the graph's own space is the middle of the canvas, and it is set every frame
		// so it follows the window as that is resized. Node positions are canvas-relative, so an
		// origin left at zero would be the top-left corner: gravity would pull the document off the
		// edge, a new node would be seeded into the corner, and fitting the view would push the
		// arrangement up against it. One point decides all three, and it is the middle.
		Graph.Engine.WorldOrigin = graphSize * 0.5f;

		// A graph is built before anyone knows how big the canvas will be, so its nodes are seeded
		// around an origin that is still zero. The first frame is where that becomes knowable, and
		// fitting once there is what puts a freshly opened document in the middle of the view
		// immediately rather than leaving the simulation to drag it in from the corner.
		// Fitted on the first frame so a freshly opened document is in the middle of the view
		// immediately rather than being dragged in from a corner, and then again until the nodes have
		// been measured: how big a node is drawn is only known once it has been, and the zoom a fit
		// chooses depends on that. Latching on the first measured frame is what stops it from
		// overriding a view the user has since changed.
		if (!fitted)
		{
			FitView();
			fitted = Graph.Engine.Nodes.Count > 0 && Graph.Engine.Nodes.All(node => node.Dimensions.X > 0f);
			statusMessage = string.Empty;
		}

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

		if (ShowInspector)
		{
			ImGui.SameLine();
			DrawInspector(new Vector2(Math.Max(reserved - ImGui.GetStyle().ItemSpacing.X, 1f), graphSize.Y));
		}

		if (LayoutRunning)
		{
			// Gravity pulls towards the world origin, which is what keeps an arrangement the user has
			// not touched in the middle of the view rather than drifting out of it.
			// The simulation resolves overlapping node boxes itself, since ktsu.ForceDirectedLayout
			// 3.18.0, so nothing here has to undo them afterwards.
			Graph.Engine.UpdatePhysics(deltaTime);
		}

		Problems = Graph.Validate();
	}

	/// <summary>
	/// The width the inspector panel is given down the right of the editor's area.
	/// </summary>
	private const float InspectorWidth = 280f;

	/// <summary>
	/// The least room the graph keeps, however little the editor was given.
	/// </summary>
	private const float MinimumGraphWidth = 160f;

	/// <summary>
	/// The width of the toolbar's zoom slider.
	/// </summary>
	private const float ZoomSliderWidth = 120f;

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
		if (ImGui.Button("Fit to canvas"))
		{
			FitView();
		}

		// Shown and dragged as a percentage, which is how every other application spells zoom, while
		// the property itself is the multiplier everything is measured in.
		ImGui.SameLine();
		ImGui.SetNextItemWidth(ZoomSliderWidth);
		float percent = Zoom * 100f;
		if (ImGui.SliderFloat("##zoom", ref percent, NodeEditorRenderer.MinZoom * 100f, NodeEditorRenderer.MaxZoom * 100f, "%.0f%%"))
		{
			Zoom = percent / 100f;
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
	/// <param name="size">The area to draw it in.</param>
	public void DrawInspector(Vector2 size)
	{
		// Bordered rather than separated: down the side of the graph a rule is what tells the panel
		// apart from the canvas it sits beside, where along the bottom a single line would do.
		ImGui.BeginChild("ast-inspector", size, ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar);

		AstNode? node = SelectedNode;
		if (node is null)
		{
			ImGui.TextUnformatted("Select a node to edit it.");
			ImGui.EndChild();
			return;
		}

		ImGui.TextUnformatted(AstSchema.Describe(node));

		// One property grid rather than a column of individually labelled controls: the names line up
		// in a column of their own, every editor fills the width left over instead of whatever the
		// widest label made room for, and the divider between the two is the user's to drag. Which
		// rows a node has is still AstFields' answer, so what the panel offers has not changed — only
		// how it is laid out, which is the widget's to decide.
		using (ImGuiWidgets.PropertyGrid grid = new("ast-inspector-properties", InspectorGridOptions))
		{
			// A grid whose table never opened is one the panel is too small to show, and each of its
			// rows is a no-op. Its own rows know that; the two composed below have to be told, and so
			// does the commit that reads back the item a row left behind.
			if (grid.IsDrawing)
			{
				foreach (AstField field in AstFields.Of(node))
				{
					DrawField(grid, node, field);
				}

				foreach (AstSlot slot in AstSchema.SlotsOf(node).Where(slot => slot.Cardinality == AstSlotCardinality.Many))
				{
					DrawSlotCount(node, slot);
				}
			}
		}

		DrawConversions(node);
		ImGui.EndChild();
	}

	/// <summary>
	/// Draws the panel that tunes the force-directed layout, so the graph can be arranged while it is
	/// on screen rather than by rebuilding.
	/// </summary>
	/// <remarks>
	/// The whole tuning surface comes from <see cref="PhysicsSettingsPanel"/>, which the node editor
	/// library supplies: every setting the simulation has, grouped by the force it belongs to. It is
	/// drawn beside the graph deliberately - the forces interact, so a change to any one of them is
	/// only judgeable by watching what the graph does about it.
	/// <para>
	/// The panel's run toggle is shown and written as <see cref="LayoutRunning"/>, the same flag the
	/// toolbar's checkbox holds, so the two agree whichever the user reaches for. This editor stops
	/// the layout by not advancing it rather than by the engine's own <c>Enabled</c>, which stays on:
	/// a step the engine is never given cannot run either way, and keeping one flag rather than two
	/// means there is no arrangement where the graph is stopped for a reason the user cannot see.
	/// </para>
	/// </remarks>
	/// <param name="size">The area to draw it in.</param>
	public void DrawLayoutSettings(Vector2 size)
	{
		ImGui.BeginChild("ast-layout-settings", size, ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar);

		PhysicsSettings settings = Graph.Engine.PhysicsSettings with { Enabled = LayoutRunning };
		if (PhysicsSettingsPanel.Draw(ref settings))
		{
			LayoutRunning = settings.Enabled;
			Graph.Engine.UpdatePhysicsSettings(settings with { Enabled = true });
		}

		ImGui.Separator();
		PhysicsSettingsPanel.DrawDiagnostics(Graph.Engine);

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
				if (!ImGui.MenuItem(template.Group is null ? template.Label : $"{template.Group}: {template.Label}"))
				{
					continue;
				}

				Convert(node, template.Create());
			}

			ImGui.EndMenu();
		}

		ImGui.EndMenu();
	}

	/// <summary>
	/// Draws one editable property as a row of the inspector's grid, using the editor its kind calls
	/// for.
	/// </summary>
	/// <param name="grid">The grid the row is drawn in.</param>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	/// <remarks>
	/// A flag is committed as it is clicked, since a checkbox has nowhere to be half-set. Everything
	/// that is typed is committed when the box is left or the user presses enter rather than on every
	/// keystroke, so typing a name puts one step on the undo stack instead of one per character.
	/// </remarks>
	private void DrawField(ImGuiWidgets.PropertyGrid grid, AstNode node, AstField field)
	{
		switch (field.Kind)
		{
			case AstFieldKind.Flag:
				bool flag = string.Equals(field.Value, "true", StringComparison.Ordinal);
				if (grid.Value(field.Name, ref flag))
				{
					SetField(node, field.Name, flag ? "true" : "false");
				}

				break;

			case AstFieldKind.Number:
				DrawNumberField(grid, node, field);
				break;

			case AstFieldKind.Fraction:
				DrawFractionField(grid, node, field);
				break;

			case AstFieldKind.Choice:
				DrawChoiceField(node, field);
				break;

			default:
				DrawTextField(grid, node, field);
				break;
		}
	}

	/// <summary>
	/// Draws a property the user types into.
	/// </summary>
	/// <param name="grid">The grid the row is drawn in.</param>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	private void DrawTextField(ImGuiWidgets.PropertyGrid grid, AstNode node, AstField field)
	{
		string buffer = ShownValue(field);

		if (grid.Value(field.Name, ref buffer))
		{
			HoldEdit(field, buffer);
		}

		CommitWhenLeft(node, field);
	}

	/// <summary>
	/// Draws a property that holds a whole number.
	/// </summary>
	/// <param name="grid">The grid the row is drawn in.</param>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	/// <remarks>
	/// A number row rather than a text one, so the box steps with the arrow keys and refuses what is
	/// not a number before <see cref="AstFields.TryWrite"/> has to. The value still travels as text
	/// between the two, which is what lets one undoable command record any field whatever it holds.
	/// </remarks>
	private void DrawNumberField(ImGuiWidgets.PropertyGrid grid, AstNode node, AstField field)
	{
		int value = int.TryParse(ShownValue(field), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
			? number
			: 0;

		if (grid.Value(field.Name, ref value))
		{
			HoldEdit(field, value.ToString(CultureInfo.InvariantCulture));
		}

		CommitWhenLeft(node, field);
	}

	/// <summary>
	/// Draws a property that holds a number with a fractional part.
	/// </summary>
	/// <param name="grid">The grid the row is drawn in.</param>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	private void DrawFractionField(ImGuiWidgets.PropertyGrid grid, AstNode node, AstField field)
	{
		double value = double.TryParse(ShownValue(field), NumberStyles.Float, CultureInfo.InvariantCulture, out double fraction)
			? fraction
			: 0d;

		if (grid.Value(field.Name, ref value))
		{
			HoldEdit(field, value.ToString(CultureInfo.InvariantCulture));
		}

		CommitWhenLeft(node, field);
	}

	/// <summary>
	/// Gets the value a field's editor should show: what is being typed into it, or what the document
	/// holds when it is not the field being typed into.
	/// </summary>
	/// <param name="field">The property being drawn.</param>
	/// <returns>The value to show, as text.</returns>
	/// <remarks>
	/// One buffer is shared across every field, keyed by which one is being typed into: only one box
	/// can have the keyboard at a time, so a buffer per field would be state to keep in step with the
	/// document for no gain. A field that is not being typed into shows the document's value, so an
	/// undo while the box is open is reflected rather than overwritten.
	/// </remarks>
	private string ShownValue(AstField field) => IsBeingEdited(field) ? fieldBuffer : field.Value;

	/// <summary>Gets whether a field is the one currently being typed into.</summary>
	/// <param name="field">The property being drawn.</param>
	/// <returns>True when the shared buffer holds this field's half-finished value.</returns>
	private bool IsBeingEdited(AstField field) => string.Equals(editingField, field.Name, StringComparison.Ordinal);

	/// <summary>Remembers what has been typed into a field, which is not the document's value yet.</summary>
	/// <param name="field">The property being edited.</param>
	/// <param name="value">What the editor now holds, as text.</param>
	private void HoldEdit(AstField field, string value)
	{
		editingField = field.Name;
		fieldBuffer = value;
	}

	/// <summary>
	/// Writes a held edit to the document once the user has left the editor that made it.
	/// </summary>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property being drawn, whose editor is the most recent item.</param>
	private void CommitWhenLeft(AstNode node, AstField field)
	{
		if (IsBeingEdited(field) && ImGui.IsItemDeactivatedAfterEdit())
		{
			SetField(node, field.Name, fieldBuffer);
			editingField = null;
		}
	}

	/// <summary>
	/// Starts a row the property grid has no method for, in the table it is already drawing.
	/// </summary>
	/// <param name="label">The row's name, drawn in the first column.</param>
	/// <remarks>
	/// Two of the inspector's rows are ones the widget does not offer: a choice between arbitrary
	/// labelled values, and a count with the buttons that change it. Its combo row spells an option
	/// by its enumeration name, where an operator here reads as its symbol beside its name and a
	/// visibility the language does not spell reads as "(language default)"; and it has no row that
	/// ends in buttons at all. A row is two cells of the table the grid is already inside, though, so
	/// composing one leaves these properties in the same two columns, either side of the same
	/// divider, as every row the widget draws itself.
	/// </remarks>
	private static void BeginComposedRow(string label)
	{
		ImGui.TableNextRow();
		ImGui.TableSetColumnIndex(0);
		ImGui.AlignTextToFramePadding();
		ImGui.TextUnformatted(label);
		ImGui.TableSetColumnIndex(1);
	}

	/// <summary>
	/// Draws a property the user picks from a fixed set, which is how an operator is chosen.
	/// </summary>
	/// <param name="node">The node being edited.</param>
	/// <param name="field">The property to draw.</param>
	private void DrawChoiceField(AstNode node, AstField field)
	{
		BeginComposedRow(field.Name);

		AstFieldChoice? current = field.Choices.FirstOrDefault(
			choice => string.Equals(choice.Value, field.Value, StringComparison.Ordinal));

		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		bool open = ImGui.BeginCombo($"##{field.Name}", current?.Label ?? field.Value);

		// Named for the probes the same way the grid names its own rows, so a test finds this one
		// beside them rather than having to know it was composed rather than drawn by the widget.
		ImGuiProbes.MarkItem(field.Name);

		if (!open)
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

			// The options are named for the probes too, so a test picks an operator the way a user
			// does — by the label it reads as — rather than by where the list happens to put it.
			ImGuiProbes.MarkItem(choice.Label);
		}

		ImGui.EndCombo();
	}

	/// <summary>
	/// Draws how many children a variadic slot holds, and the buttons that change it.
	/// </summary>
	/// <param name="node">The node whose slot to draw.</param>
	/// <param name="slot">The slot to draw.</param>
	/// <remarks>
	/// The count is read rather than typed: adding a parameter and removing the last one are the two
	/// edits the document knows how to undo, and a box that accepts any number would be asking for a
	/// third that neither button can make.
	/// </remarks>
	private void DrawSlotCount(AstNode node, AstSlot slot)
	{
		BeginComposedRow(slot.Name);

		int count = AstSchema.ChildrenOf(node, slot).Count;
		ImGui.AlignTextToFramePadding();
		ImGui.TextUnformatted(count.ToString(CultureInfo.InvariantCulture));

		ImGui.SameLine();
		if (ImGui.Button($"+##add-{slot.Name}"))
		{
			AddChild(node, slot);
		}

		ImGuiProbes.MarkItem($"Add {slot.Name}");

		ImGui.SameLine();
		ImGui.BeginDisabled(count == 0);
		if (ImGui.Button($"-##remove-{slot.Name}"))
		{
			RemoveLastChild(node, slot);
		}

		ImGuiProbes.MarkItem($"Remove {slot.Name}");
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
	/// Brings the whole graph into view: centred on the canvas, and zoomed out far enough to fit.
	/// </summary>
	/// <returns>True if there was anything to bring into view.</returns>
	/// <remarks>
	/// The layout arranges nodes wherever the forces take them, and a user can drag one anywhere, so a
	/// document can end up off the edge of the view with no clue which way to scroll back. Worse, a
	/// graph can simply be bigger than the canvas, which no amount of centring fixes.
	/// <para>
	/// The node editor does both parts — it owns the zoom, and centring means moving the nodes, which
	/// is its business rather than this application's. What is decided here is the canvas: the world
	/// origin is kept on the middle of it, so twice the origin is the whole of it.
	/// </para>
	/// </remarks>
	public bool FitView()
	{
		if (!renderer.FitToView(Graph.Engine, Graph.Engine.WorldOrigin * 2f))
		{
			return false;
		}

		statusMessage = "Brought the graph into view.";
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
			if (!ImGui.MenuItem(template.Label))
			{
				continue;
			}

			Add(template.Create(), dropPosition);
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
