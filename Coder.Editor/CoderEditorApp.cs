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
	public AstGraphEditor Editor { get; private set; } = new(NewDocument());

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

		// The menu goes here rather than inside OnRender: ImGuiApp's main window carries no
		// ImGuiWindowFlags.MenuBar, so an ImGui.BeginMenuBar() call in the render delegate always
		// returns false and the menu silently never appears. OnAppMenu runs inside the application's
		// own BeginMainMenuBar.
		OnAppMenu = DrawMenu,
	};

	/// <summary>
	/// Builds the document a fresh editor opens with.
	/// </summary>
	/// <returns>A function with one parameter and an empty body.</returns>
	/// <remarks>
	/// An empty function rather than an empty graph: the AST has no node that means "nothing yet",
	/// and a user who has just opened the editor is better served by something to attach to than by
	/// a blank canvas and no way to start.
	/// </remarks>
	public static FunctionDeclaration NewDocument()
	{
		FunctionDeclaration function = new("newFunction") { ReturnType = "void" };
		function.Parameters.Add(new Parameter("value", "int"));
		return function;
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
		float graphWidth = available.X * 0.62f;

		ImGui.BeginChild("graph-pane", new Vector2(graphWidth, available.Y - StatusBarHeight));
		Editor.Draw(new Vector2(graphWidth - PaneInset, available.Y - StatusBarHeight - PaneInset), deltaTime);
		ImGui.EndChild();

		ImGui.SameLine();

		ImGui.BeginChild("code-pane", new Vector2(0, available.Y - StatusBarHeight));
		DrawCodePane();
		ImGui.EndChild();

		DrawStatusBar();
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
	/// The margin between a pane's edge and what it contains.
	/// </summary>
	private const float PaneInset = 12f;

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
			ImGui.TextUnformatted($"{problems.Count} thing(s) to finish before this generates:");
			foreach (AstGraphProblem problem in problems)
			{
				ImGui.BulletText(problem.Message);
			}

			return;
		}

		Regenerate();
		ImGui.TextUnformatted(GeneratedCode);
	}

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
		Editor = new AstGraphEditor(document) { LayoutRunning = Settings.LayoutRunning };
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

		Editor = new AstGraphEditor(result.Root) { LayoutRunning = Settings.LayoutRunning };
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
		ILanguageGenerator? generator = generators.FirstOrDefault(
			g => string.Equals(g.LanguageId, Settings.PreviewLanguageId, StringComparison.Ordinal));

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
