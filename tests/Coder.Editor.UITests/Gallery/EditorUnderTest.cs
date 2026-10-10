// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor.UITests.Gallery;

using ktsu.Coder.Ast;
using ktsu.Coder.Editor;
using ktsu.ImGui.App.Testing;

/// <summary>The editor a gallery entry stages, and the harness drawing it.</summary>
/// <param name="App">The application, built the way <c>Program</c> builds it.</param>
/// <param name="Harness">The headless harness the application is running under.</param>
internal sealed record EditorUnderTest(CoderEditorApp App, ImGuiAppHarness Harness)
{
	/// <summary>Selects the one node in the graph matching a predicate, so the inspector shows it.</summary>
	/// <typeparam name="TNode">The kind of node to look for.</typeparam>
	/// <param name="match">Picks the node out from the others of its kind.</param>
	internal void Select<TNode>(Func<TNode, bool> match)
		where TNode : AstNode
	{
		TNode node = App.Editor.Graph.Nodes.Values.OfType<TNode>().Single(match);
		Assert.IsTrue(App.Editor.Select(node), $"The {typeof(TNode).Name} to select is not in the graph.");
	}

	/// <summary>Shows the generated source in another language, as its radio button would.</summary>
	/// <param name="languageId">The generator's language id.</param>
	internal void Preview(string languageId)
	{
		App.Settings.PreviewLanguageId = languageId;
		App.Regenerate();
	}

	/// <summary>The height of the main menu bar, which a column picture leaves out.</summary>
	private const int MenuBarHeight = 24;

	/// <summary>The height of the status bar, which a column picture leaves out.</summary>
	private const int StatusBarHeight = 34;

	/// <summary>
	/// How far into the column its own border is drawn. Cropping to the border, rather than to the
	/// divider the column is measured from, keeps a sliver of the graph pane's edge out of the picture.
	/// </summary>
	private const int ColumnInset = 2;

	/// <summary>Where the side column's panes start, below the menu bar and the column's own border.</summary>
	private const int PanesTop = 36;

	/// <summary>The chrome above and below the panes that is not shared out between them.</summary>
	private const int PanesChrome = 84;

	/// <summary>Gets the right-hand column: properties, generated code and layout tuning.</summary>
	/// <returns>The column, from the divider to the window's right edge, between the menu bar and the status bar.</returns>
	internal Rectangle SideColumn() => SideColumn(0f, 1f);

	/// <summary>Gets part of the right-hand column, between two of the boundaries its panes are split at.</summary>
	/// <param name="from">
	/// Where to start, as the share of the column above it: 0 for the top, or the sum of the splits of
	/// the panes to leave out above.
	/// </param>
	/// <param name="to">Where to stop, in the same terms: 1 for the bottom.</param>
	/// <returns>The part of the column, cropped to the borders of the panes it keeps.</returns>
	internal Rectangle SideColumn(float from, float to)
	{
		Bitmap32 frame = Harness.Target;
		int left = (int)(frame.Width * App.Settings.GraphSplit) + ColumnInset;
		int top = from <= 0f ? MenuBarHeight + ColumnInset + 2 : PaneBoundary(frame, from) + 6;
		int bottom = to >= 1f ? frame.Height - StatusBarHeight - ColumnInset : PaneBoundary(frame, to) - 2;
		return new Rectangle(left, top, frame.Width, bottom);
	}

	/// <summary>Finds where the column's panes are divided, for a share of the column above the division.</summary>
	/// <param name="share">The sum of the splits of the panes above the division.</param>
	/// <returns>The division's distance from the top of the window.</returns>
	internal int PaneBoundary(float share) => PaneBoundary(Harness.Target, share);

	private static int PaneBoundary(Bitmap32 frame, float share) =>
		PanesTop + (int)MathF.Round((frame.Height - PanesChrome) * share);
}
