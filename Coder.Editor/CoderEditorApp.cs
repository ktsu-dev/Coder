// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.Coder.Ast;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.ImGui.App;
using ktsu.ImGui.SyntaxHighlighting;
using ktsu.ImGui.Widgets;
using Silk.NET.Windowing;

/// <summary>
/// The editor: a graph of the document on the left, the code it generates on the right.
/// </summary>
/// <remarks>
/// The application's state is an instance rather than statics, so a test can build one, drive it
/// through <see cref="BuildConfig"/> and inspect it afterwards without a previous test's document
/// still being open. <see cref="BuildConfig"/> is the same configuration the real entry point runs,
/// so a test exercises the application rather than a copy of it written for testing.
/// </remarks>
/// <param name="documents">Reads and writes documents.</param>
/// <param name="generators">The languages the preview pane can show.</param>
/// <param name="settings">What the editor remembers between runs.</param>
public sealed class CoderEditorApp(
	DocumentStore documents,
	IEnumerable<ILanguageGenerator> generators,
	EditorSettings settings)
{
	private readonly List<ILanguageGenerator> generators = [.. generators];
	private string pathBuffer = string.Empty;

	/// <summary>
	/// Gets the settings this run started with, updated as the user changes them.
	/// </summary>
	public EditorSettings Settings { get; } = settings;

	/// <summary>
	/// Gets the editor over the current document.
	/// </summary>
	public AstGraphEditor Editor { get; private set; } = EditorFor(NewDocument());

	/// <summary>
	/// Gets the file the document was last read from or written to, or null for an unsaved one.
	/// </summary>
	public string? DocumentPath { get; private set; }

	/// <summary>
	/// Gets the last thing that happened, as shown in the status bar.
	/// </summary>
	public string Status { get; private set; } = "New document.";

	/// <summary>
	/// Gets the code generated from the document, refreshed whenever it changes.
	/// </summary>
	public string GeneratedCode { get; private set; } = string.Empty;

	/// <summary>
	/// Gets a value indicating whether the document has been edited since it was last written.
	/// </summary>
	/// <remarks>
	/// Read from the undo stack rather than tracked separately: the stack already knows where the
	/// last save was, so undoing back past the edits made since one correctly reports the document as
	/// clean again. A private flag set on every edit would not.
	/// </remarks>
	public bool HasUnsavedChanges => Editor.History.HasUnsavedChanges;

	/// <summary>
	/// Builds the configuration the application runs under.
	/// </summary>
	/// <returns>The configuration to hand <see cref="ImGuiApp.Start(ImGuiAppConfig)"/>.</returns>
	public ImGuiAppConfig BuildConfig() => new()
	{
		Title = "Coder",
		OnStart = () => Editor.LayoutRunning = Settings.LayoutRunning,
		OnRender = Draw,

		// Opened where it was left. The window is part of what a user arranges, and an application
		// that forgets it is one they rearrange on every run.
		InitialWindowState = new ImGuiAppWindowState
		{
			Size = new Vector2(Settings.WindowWidth, Settings.WindowHeight),
			Pos = new Vector2(Settings.WindowX, Settings.WindowY),
			LayoutState = Settings.WindowMaximized ? WindowState.Maximized : WindowState.Normal,
		},
		OnMoveOrResize = RememberWindow,

		// The menu goes here rather than inside OnRender: ImGuiApp's main window carries no
		// ImGuiWindowFlags.MenuBar, so an ImGui.BeginMenuBar() call in the render delegate always
		// returns false and the menu silently never appears. OnAppMenu runs inside the application's
		// own BeginMainMenuBar.
		OnAppMenu = DrawMenu,
	};

	/// <summary>
	/// Notes where the window is, so the next run opens there.
	/// </summary>
	/// <remarks>
	/// The size and position read back are the ones the window has when it is not maximized, which is
	/// what should be restored when a maximized window is un-maximized. Settings are written to disk
	/// when the application exits, so this only has to keep them current.
	/// </remarks>
	private void RememberWindow()
	{
		ImGuiAppWindowState state = ImGuiApp.WindowState;

		Settings.WindowWidth = state.Size.X;
		Settings.WindowHeight = state.Size.Y;
		Settings.WindowX = state.Pos.X;
		Settings.WindowY = state.Pos.Y;
		Settings.WindowMaximized = state.LayoutState == WindowState.Maximized;
	}

	/// <summary>
	/// Builds an editor over a document, set up the way this application hosts one.
	/// </summary>
	/// <param name="document">The document to edit.</param>
	/// <returns>The editor.</returns>
	/// <remarks>
	/// The properties panel is one of this application's own panes, so the editor is told not to draw
	/// a second one of its own beside the canvas.
	/// </remarks>
	private static AstGraphEditor EditorFor(AstNode document) => new(document) { ShowInspector = false };

	/// <summary>
	/// Builds the document a fresh editor opens with.
	/// </summary>
	/// <returns>A small class with two fields and two methods that do something with them.</returns>
	/// <remarks>
	/// Something to read rather than something to start from: the AST has no node meaning "nothing
	/// yet", and a canvas holding one empty function shows neither what the node kinds are nor how
	/// they connect. This one puts a field, a parameter, an assignment, a binary expression, a local
	/// and a return on screen at once, so the shape of the graph is legible before anything is added.
	/// </remarks>
	public static ClassDeclaration NewDocument()
	{
		ClassDeclaration declaration = new("Counter");

		declaration.Members.Add(new VariableDeclaration("count", "int", new LiteralExpression<int>(0)));
		declaration.Members.Add(new VariableDeclaration("step", "int", new LiteralExpression<int>(1)));

		FunctionDeclaration add = new("Add") { ReturnType = "int" };
		add.Parameters.Add(new Parameter("amount", "int"));
		add.Body.Add(new AssignmentStatement(
			new VariableReference("count"),
			new BinaryExpression(new VariableReference("count"), BinaryOperator.Add, new VariableReference("amount"))));
		add.Body.Add(new ReturnStatement(new VariableReference("count")));
		declaration.Members.Add(add);

		FunctionDeclaration next = new("Next") { ReturnType = "int" };
		next.Body.Add(new VariableDeclaration("result", "int",
			new BinaryExpression(new VariableReference("count"), BinaryOperator.Add, new VariableReference("step"))));
		next.Body.Add(new ReturnStatement(new VariableReference("result")));
		declaration.Members.Add(next);

		return declaration;
	}

	/// <summary>
	/// Builds a document that is a class rather than a loose function.
	/// </summary>
	/// <returns>A class with one method in it.</returns>
	/// <remarks>
	/// The method is there for the same reason <see cref="NewDocument"/>'s function has a parameter:
	/// an empty class generates an empty class, and the user would have to guess that a method is
	/// what the Members slot is waiting for.
	/// </remarks>
	public static ClassDeclaration NewClassDocument()
	{
		ClassDeclaration declaration = new("NewClass");
		declaration.Members.Add(new FunctionDeclaration("newMethod") { ReturnType = "void" });
		return declaration;
	}

	/// <summary>
	/// Draws one frame.
	/// </summary>
	/// <param name="deltaTime">Seconds since the last frame.</param>
	public void Draw(float deltaTime)
	{
		HandleShortcuts();

		Vector2 available = ImGui.GetContentRegionAvail();

		// The container measures itself from the remaining content region, so it is given a child of
		// the height that is actually the panes' — otherwise it would take the status bar's row too
		// and draw the bar over its own bottom edge.
		ImGui.BeginChild("panes", new Vector2(0, available.Y - StatusBarHeight));
		Panes.Tick(deltaTime);
		ImGui.EndChild();

		DrawStatusBar();
	}

	/// <summary>
	/// Gets the pane layout, built on first use.
	/// </summary>
	/// <remarks>
	/// A divider container holds the sizes the user has dragged the panes to, so it has to outlive
	/// the frame. It is built lazily rather than in a field initializer because its zones call back
	/// into this instance, which a field initializer cannot refer to.
	/// </remarks>
	private ImGuiWidgets.DividerContainer Panes => field ??= BuildPanes();

	/// <summary>
	/// Builds the resizable pane layout: the graph, and beside it the properties above the code.
	/// </summary>
	/// <returns>The container to tick each frame.</returns>
	/// <remarks>
	/// Properties, code and layout tuning are stacked rather than placed side by side because they are
	/// read at different times and want different shapes: properties are a short column of labelled
	/// rows, generated source is lines that want to be read down, and the tuning is a long list of
	/// sliders. Sharing one column gives each of them the full width and lets the user decide how the
	/// height is split between them — which is the point of making these panes rather than fixed
	/// regions.
	/// <para>
	/// The tuning gets a pane of its own rather than a section inside the properties, because it is
	/// read while watching the graph move: it has to be able to be tall while the properties are
	/// short, and it must not close itself every time a different node is selected.
	/// </para>
	/// <para>
	/// The sizes are remembered between runs, so an arrangement the user settled on is the one they
	/// come back to.
	/// </para>
	/// </remarks>
	private ImGuiWidgets.DividerContainer BuildPanes()
	{
		ImGuiWidgets.DividerContainer side = new(
			"coder-side",
			container =>
			{
				Settings.PropertiesSplit = container.GetSizes()[0];
				Settings.CodeSplit = container.GetSizes()[1];
			},
			ImGuiWidgets.DividerLayout.Rows,
			[
				new ImGuiWidgets.DividerZone("properties", Settings.PropertiesSplit, DrawPropertiesPane),
				new ImGuiWidgets.DividerZone("code", Settings.CodeSplit, _ => DrawCodePane()),
				new ImGuiWidgets.DividerZone("layout", 1f - Settings.PropertiesSplit - Settings.CodeSplit, DrawLayoutPane),
			]);

		return new ImGuiWidgets.DividerContainer(
			"coder-panes",
			container => Settings.GraphSplit = container.GetSizes()[0],
			ImGuiWidgets.DividerLayout.Columns,
			[
				new ImGuiWidgets.DividerZone("graph", Settings.GraphSplit, deltaTime =>
					Editor.Draw(ImGui.GetContentRegionAvail(), deltaTime)),
				new ImGuiWidgets.DividerZone("side", 1f - Settings.GraphSplit, side.Tick),
			]);
	}

	/// <summary>
	/// Draws the selected node's properties, which the editor supplies but does not place.
	/// </summary>
	/// <param name="deltaTime">Seconds since the last frame; the panel does not animate, so unused.</param>
	private void DrawPropertiesPane(float deltaTime)
	{
		ImGui.TextUnformatted("Properties");
		Editor.DrawInspector(ImGui.GetContentRegionAvail());
	}

	/// <summary>
	/// Draws the layout tuning, which the editor supplies but does not place.
	/// </summary>
	/// <param name="deltaTime">Seconds since the last frame; the panel does not animate, so unused.</param>
	private void DrawLayoutPane(float deltaTime)
	{
		ImGui.TextUnformatted("Layout");
		Editor.DrawLayoutSettings(ImGui.GetContentRegionAvail());
	}

	/// <summary>
	/// Applies the keyboard shortcuts the menu also offers.
	/// </summary>
	/// <remarks>
	/// Undo and redo are the editor's, not the graph's, so they work wherever the keyboard focus is:
	/// a user who has just typed a name into the inspector and wants it back should not have to click
	/// on the canvas first. Saving without a path is left to the menu, which is where a path can be
	/// typed.
	/// </remarks>
	private void HandleShortcuts()
	{
		if (!ImGui.GetIO().KeyCtrl)
		{
			return;
		}

		if (ImGui.IsKeyPressed(ImGuiKey.Z))
		{
			if (ImGui.GetIO().KeyShift)
			{
				Editor.Redo();
			}
			else
			{
				Editor.Undo();
			}
		}

		if (ImGui.IsKeyPressed(ImGuiKey.Y))
		{
			Editor.Redo();
		}

		if (ImGui.IsKeyPressed(ImGuiKey.S) && DocumentPath is not null)
		{
			Save(DocumentPath);
		}
	}

	/// <summary>
	/// The height reserved for the status bar under both panes.
	/// </summary>
	private const float StatusBarHeight = 28f;

	/// <summary>
	/// Draws the application's File menu.
	/// </summary>
	/// <remarks>Called from inside the application's main menu bar, so it opens no bar of its own.</remarks>
	public void DrawMenu()
	{
		if (ImGui.BeginMenu("File"))
		{
			// Two items rather than a New submenu: which of the two a document starts as is the first
			// decision a user makes, and burying it a level deep makes the common case a hover.
			if (ImGui.MenuItem("New function"))
			{
				NewFile();
			}

			if (ImGui.MenuItem("New class"))
			{
				NewClassFile();
			}

			ImGui.Separator();
			ImGui.SetNextItemWidth(320f);
			ImGui.InputText("Path", ref pathBuffer, 512);

			if (ImGui.MenuItem("Open") && pathBuffer.Length > 0)
			{
				Open(pathBuffer);
			}

			if (ImGui.MenuItem("Save") && (DocumentPath ?? pathBuffer).Length > 0)
			{
				Save(DocumentPath ?? pathBuffer);
			}

			if (ImGui.MenuItem("Export generated code") && (DocumentPath ?? pathBuffer).Length > 0)
			{
				Export(DocumentPath ?? pathBuffer);
			}

			if (Settings.RecentFiles.Count > 0 && ImGui.BeginMenu("Recent"))
			{
				foreach (string recent in Settings.RecentFiles.ToArray())
				{
					if (ImGui.MenuItem(recent))
					{
						Open(recent);
					}
				}

				ImGui.EndMenu();
			}

			ImGui.EndMenu();
		}
	}

	private void DrawCodePane()
	{
		ImGui.TextUnformatted("Generated code");

		foreach (ILanguageGenerator generator in generators)
		{
			ImGui.SameLine();
			if (ImGui.RadioButton(generator.DisplayName, string.Equals(Settings.PreviewLanguageId, generator.LanguageId, StringComparison.Ordinal)))
			{
				Settings.PreviewLanguageId = generator.LanguageId;
				Regenerate();
			}
		}

		IReadOnlyList<AstGraphProblem> problems = Editor.Problems;
		if (problems.Count > 0)
		{
			DrawProblems(problems);
			return;
		}

		Regenerate();

		ImGui.SameLine();
		if (ImGui.Button("Copy"))
		{
			CopyGeneratedCode();
		}

		DrawGeneratedCode();
	}

	/// <summary>
	/// The configuration the preview draws generated source with.
	/// </summary>
	/// <remarks>
	/// Held once rather than built per frame because it never varies: it is a record, and a new one
	/// every frame would be an allocation per frame for a value that is the same each time. The
	/// palette is deliberately left unset, which makes the highlighter pick one per frame from the
	/// window background, so the preview keeps matching whatever theme the application is in.
	/// </remarks>
	private static readonly SyntaxHighlightConfig CodeStyle = new() { ShowLineNumbers = true };

	/// <summary>
	/// Draws the generated source, highlighted for the language it was generated in.
	/// </summary>
	/// <remarks>
	/// Every generator's <see cref="ILanguageGenerator.LanguageId"/> is already the name the
	/// highlighter knows that language by, so the two need nothing between them. An id it did not
	/// know would fall back to plain text rather than throwing, which is the right failure but a
	/// silent one, so a test pins that each generator's language is one it recognises.
	/// <para>
	/// Drawn inside a scrolling child because the highlighter does not wrap: a long line is clipped
	/// by whatever window it is in, and generated code is indented and can run wide. Scrolling
	/// horizontally is how the rest of it is reached.
	/// </para>
	/// </remarks>
	private void DrawGeneratedCode()
	{
		ImGui.BeginChild("generated-code", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
		ImGuiSyntaxHighlighting.Render(GeneratedCode, Settings.PreviewLanguageId, CodeStyle);
		ImGui.EndChild();
	}

	/// <summary>
	/// Draws what is still outstanding, in place of the code that cannot be generated yet.
	/// </summary>
	/// <param name="problems">What the document is missing.</param>
	/// <remarks>
	/// Each is a button rather than a bullet: a problem names the node it is about, and the fastest
	/// way to fix one is to be looking at that node with the inspector open.
	/// </remarks>
	private void DrawProblems(IReadOnlyList<AstGraphProblem> problems)
	{
		ImGui.TextUnformatted($"{problems.Count} thing(s) to finish before this generates:");

		for (int i = 0; i < problems.Count; i++)
		{
			if (ImGui.Selectable($"{problems[i].Message}##problem-{i}"))
			{
				Editor.Select(problems[i].Node);
				Status = $"Selected {AstSchema.Describe(problems[i].Node)}.";
			}
		}
	}

	/// <summary>
	/// Puts the generated code on the clipboard.
	/// </summary>
	/// <returns>True if there was code to copy.</returns>
	/// <remarks>
	/// The pane exists to produce source somebody is going to paste somewhere, and selecting text
	/// out of an ImGui label is not something a user can do.
	/// </remarks>
	public bool CopyGeneratedCode()
	{
		if (GeneratedCode.Length == 0)
		{
			return false;
		}

		ImGui.SetClipboardText(GeneratedCode);
		Status = "Copied the generated code.";
		return true;
	}

	/// <summary>
	/// Writes the generated code beside the document, in the file extension its language uses.
	/// </summary>
	/// <param name="documentPath">The document the source belongs to.</param>
	/// <returns>The file written, or null if nothing was written.</returns>
	/// <remarks>
	/// The point of the application is the source it produces, and a preview pane nobody can get the
	/// text out of stops one step short of that. The name comes from the document rather than being
	/// asked for: the two belong together, and a user who wants it elsewhere can move it.
	/// </remarks>
	public string? Export(string documentPath)
	{
		Ensure.NotNull(documentPath);

		ILanguageGenerator? generator = PreviewGenerator;
		if (generator is null)
		{
			Status = $"No generator for '{Settings.PreviewLanguageId}'.";
			return null;
		}

		if (Editor.Problems.Count > 0)
		{
			Status = $"{Editor.Problems.Count} thing(s) to finish before this generates.";
			return null;
		}

		Regenerate();

		// The document's own extension is two parts, so it is trimmed rather than replaced.
		string stem = documentPath.EndsWith(DocumentStore.Extension, StringComparison.OrdinalIgnoreCase)
			? documentPath[..^DocumentStore.Extension.Length]
			: documentPath;

		DocumentResult result = documents.Export(GeneratedCode, $"{stem}.{generator.FileExtension}");
		if (!result.Success)
		{
			Status = result.Error ?? "Could not write the generated code.";
			return null;
		}

		Status = $"Wrote {result.Path}.";
		return result.Path;
	}

	/// <summary>
	/// Gets the generator whose output the pane is showing, or null when the remembered language is
	/// one this build does not have.
	/// </summary>
	private ILanguageGenerator? PreviewGenerator => generators.FirstOrDefault(
		g => string.Equals(g.LanguageId, Settings.PreviewLanguageId, StringComparison.Ordinal));

	private void DrawStatusBar()
	{
		ImGui.Separator();
		string marker = HasUnsavedChanges ? "*" : string.Empty;
		ImGui.TextUnformatted($"{DocumentPath ?? "(unsaved)"}{marker} — {Status}");
	}

	/// <summary>
	/// Replaces the document with a fresh function.
	/// </summary>
	public void NewFile() => Replace(NewDocument(), "New document.");

	/// <summary>
	/// Replaces the document with a fresh class.
	/// </summary>
	public void NewClassFile() => Replace(NewClassDocument(), "New class.");

	/// <summary>
	/// Replaces the document, discarding whatever was open.
	/// </summary>
	/// <param name="document">The document to open.</param>
	/// <param name="status">What the status bar should say about it.</param>
	/// <remarks>
	/// A new editor rather than a new graph inside the existing one: the undo history belongs to the
	/// document that was edited, and carrying it across would let a user undo their way back into a
	/// document they had closed.
	/// </remarks>
	private void Replace(AstNode document, string status)
	{
		Editor = EditorFor(document);
		Editor.LayoutRunning = Settings.LayoutRunning;
		DocumentPath = null;
		Status = status;
		GeneratedCode = string.Empty;
	}

	/// <summary>
	/// Opens a document, replacing the current one.
	/// </summary>
	/// <param name="path">The file to open.</param>
	/// <returns>True if it opened.</returns>
	public bool Open(string path)
	{
		DocumentResult result = documents.Load(path);
		if (!result.Success || result.Root is null)
		{
			Status = result.Error ?? "Could not open the document.";
			return false;
		}

		Editor = EditorFor(result.Root);
		Editor.LayoutRunning = Settings.LayoutRunning;
		DocumentPath = result.Path;
		Settings.Remember(result.Path!);
		Status = $"Opened {result.Path}.";
		Regenerate();
		return true;
	}

	/// <summary>
	/// Writes the document.
	/// </summary>
	/// <param name="path">The file to write to.</param>
	/// <returns>True if it was written.</returns>
	public bool Save(string path)
	{
		DocumentResult result = documents.Save(Editor.Graph.Root, path);
		if (!result.Success)
		{
			Status = result.Error ?? "Could not save the document.";
			return false;
		}

		DocumentPath = result.Path;
		Settings.Remember(result.Path!);

		// Tells the undo stack that everything up to here is on disk, so the dirty marker clears and
		// comes back the moment the next edit is made — or if the user undoes back across the save.
		Editor.History.MarkAsSaved($"Saved {result.Path}");
		Status = $"Saved {result.Path}.";
		return true;
	}

	/// <summary>
	/// Regenerates the preview from the current document.
	/// </summary>
	/// <remarks>
	/// A generator refuses a node it cannot emit, and a document mid-edit can hold one, so the
	/// refusal is shown in the pane rather than thrown out of a draw call — a frame that throws takes
	/// the application down.
	/// </remarks>
	public void Regenerate()
	{
		ILanguageGenerator? generator = PreviewGenerator;

		if (generator is null)
		{
			GeneratedCode = $"No generator for '{Settings.PreviewLanguageId}'.";
			return;
		}

		try
		{
			GeneratedCode = generator.Generate(Editor.Graph.Root);
		}
		catch (NotSupportedException ex)
		{
			GeneratedCode = $"{generator.DisplayName} cannot generate this document yet: {ex.Message}";
		}
	}
}
