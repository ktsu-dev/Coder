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

	/// <summary>Gets the right-hand column: properties, generated code and layout tuning.</summary>
	/// <returns>The column, from the divider to the window's right edge, between the menu bar and the status bar.</returns>
	internal Rectangle SideColumn()
	{
		Bitmap32 frame = Harness.Target;
		int left = (int)(frame.Width * App.Settings.GraphSplit);
		return new Rectangle(left, MenuBarHeight, frame.Width, frame.Height - StatusBarHeight);
	}
}
