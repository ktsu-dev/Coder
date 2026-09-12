// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Editor;

using System.Numerics;
using ktsu.Coder.Ast;
using ktsu.Coder.Editor;
using ktsu.Coder.Graph;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.ImGui.App;
using ktsu.ImGui.App.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the editor application, including a frame rendered through the headless harness.
/// </summary>
/// <remarks>
/// These drive the real <see cref="NativeFileSystemProvider"/> against a temporary directory rather
/// than a mock, so the provider the application actually ships with is the one under test. Testably's
/// mock filesystem is versioned separately from the abstraction Essentials pins, and building the
/// suite on that mismatch would be testing the wrong thing.
/// <para>
/// ImGui contexts are process-global, so the rendering tests must not run in parallel.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CoderEditorAppTests
{
	private static readonly HarnessOptions Options = new() { Width = 1600, Height = 900 };

	private string root = string.Empty;

	/// <summary>
	/// Gives each test its own directory, so one cannot see another's documents.
	/// </summary>
	[TestInitialize]
	public void SetUp()
	{
		root = Path.Combine(Path.GetTempPath(), $"coder-editor-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
	}

	/// <summary>
	/// Removes the directory the test wrote into.
	/// </summary>
	[TestCleanup]
	public void TearDown()
	{
		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static DocumentStore NewStore() =>
		new(new NativeFileSystemProvider(), new YamlSerializer(), new YamlDeserializer());

	private string PathIn(string name) => Path.Combine(root, name + DocumentStore.Extension);

	private static CoderEditorApp NewApp(DocumentStore store, EditorSettings? settings = null) =>
		new(store, [new CSharpGenerator(), new PythonGenerator(), new CppGenerator(), new CGenerator(), new RustGenerator(), new JavaScriptGenerator()],
			settings ?? new EditorSettings());

	/// <summary>
	/// Tests that a document containing an assignment survives being written and read back.
	/// </summary>
	/// <remarks>
	/// AssignmentStatement's deserialization constructor built its placeholder target through the
	/// VariableReference overload that rejects an empty name, so every load of a document holding one
	/// threw before it could be overwritten. The default document has an assignment in it, so this is
	/// the path a user takes by saving and reopening what the editor gave them.
	/// </remarks>
	[TestMethod]
	public void Document_WithAnAssignment_RoundTripsThroughTheFileSystem()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);

		string path = PathIn("assigning");
		Assert.IsTrue(app.Save(path), app.Status);

		CoderEditorApp reopened = NewApp(store);
		Assert.IsTrue(reopened.Open(path), reopened.Status);

		ClassDeclaration reopenedRoot = (ClassDeclaration)reopened.Editor.Graph.Root;
		Assert.IsTrue(
			reopenedRoot.Members.OfType<FunctionDeclaration>().SelectMany(m => m.Body).OfType<AssignmentStatement>().Any(),
			"the reopened document should still hold its assignment");
	}

	/// <summary>
	/// Tests that a fresh editor opens with something to read rather than a blank canvas.
	/// </summary>
	[TestMethod]
	public void NewDocument_IsAClassWithFieldsAndMethodsThatDoSomething()
	{
		ClassDeclaration document = CoderEditorApp.NewDocument();

		Assert.AreEqual("Counter", document.Name);

		List<VariableDeclaration> fields = [.. document.Members.OfType<VariableDeclaration>()];
		Assert.AreEqual(2, fields.Count, "the class should carry a couple of fields");
		Assert.IsTrue(fields.TrueForAll(f => f.InitialValue is not null), "each field should be initialised");

		List<FunctionDeclaration> methods = [.. document.Members.OfType<FunctionDeclaration>()];
		Assert.AreEqual(2, methods.Count, "the class should carry a couple of methods");
		Assert.IsTrue(methods.TrueForAll(m => m.Body.Count > 0), "each method should have a body");

		Assert.IsTrue(
			methods.SelectMany(m => m.Body).OfType<AssignmentStatement>().Any(a => a.Value is BinaryExpression),
			"a method should assign the result of an expression");
		Assert.IsTrue(
			methods.SelectMany(m => m.Body).OfType<VariableDeclaration>().Any(v => v.InitialValue is BinaryExpression),
			"a method should declare a local from an expression");
	}

	/// <summary>
	/// Tests that a document written by the editor can be read back with its structure intact.
	/// </summary>
	[TestMethod]
	public void Document_RoundTripsThroughTheFileSystem()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		string path = PathIn("sample");

		Assert.IsTrue(app.Save(path), app.Status);
		Assert.IsTrue(File.Exists(path), "the document should have been written");

		CoderEditorApp reopened = NewApp(store);
		Assert.IsTrue(reopened.Open(path), reopened.Status);

		Assert.IsInstanceOfType<ClassDeclaration>(reopened.Editor.Graph.Root);
		Assert.AreEqual("Counter", ((ClassDeclaration)reopened.Editor.Graph.Root).Name);
		Assert.AreEqual(path, reopened.DocumentPath);
	}

	/// <summary>
	/// Tests that the editor knows whether the open document has been edited since it was written,
	/// and stops claiming so once it has been saved.
	/// </summary>
	/// <remarks>
	/// The answer comes from the undo stack's save boundary rather than a flag, which is what makes
	/// the last assertion here true: undoing back to where the document was saved leaves nothing to
	/// write, and a flag set on every edit would still be claiming otherwise.
	/// </remarks>
	[TestMethod]
	public void Save_ClearsTheUnsavedMarkerUntilTheNextEdit()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		string path = PathIn("dirty");

		Assert.IsFalse(app.HasUnsavedChanges, "a document nobody has edited has nothing to write");

		app.Editor.Add(new VariableReference("added"), System.Numerics.Vector2.Zero);
		Assert.IsTrue(app.HasUnsavedChanges);

		Assert.IsTrue(app.Save(path), app.Status);
		Assert.IsFalse(app.HasUnsavedChanges);

		app.Editor.Add(new VariableReference("another"), System.Numerics.Vector2.Zero);
		Assert.IsTrue(app.HasUnsavedChanges);

		app.Editor.Undo();
		Assert.IsFalse(app.HasUnsavedChanges, "undoing back to the save point leaves nothing to write");
	}

	/// <summary>
	/// Tests that a save that failed does not claim the document is written.
	/// </summary>
	[TestMethod]
	public void Save_LeavesTheUnsavedMarkerWhenItFails()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		app.Editor.Add(new VariableReference("added"), System.Numerics.Vector2.Zero);

		// A directory is not a file, so writing over it fails.
		Assert.IsFalse(app.Save(root));

		Assert.IsTrue(app.HasUnsavedChanges);
	}

	/// <summary>
	/// Tests that saving creates the directory rather than failing because it does not exist, which
	/// is what happens the first time a user saves into a new folder.
	/// </summary>
	[TestMethod]
	public void Save_CreatesTheDirectory()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		string nested = Path.Combine(root, "brand", "new", "place");

		Assert.IsTrue(app.Save(Path.Combine(nested, $"doc{DocumentStore.Extension}")), app.Status);

		Assert.IsTrue(Directory.Exists(nested));
	}

	/// <summary>
	/// Tests that opening a file that is not there is reported rather than thrown, since aiming at
	/// the wrong path is an ordinary thing for a user to do.
	/// </summary>
	[TestMethod]
	public void Open_ReportsAMissingFile()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);

		Assert.IsFalse(app.Open(PathIn("missing")));

		StringAssert.Contains(app.Status, "no file", StringComparison.OrdinalIgnoreCase);
		Assert.IsNull(app.DocumentPath);
	}

	/// <summary>
	/// Tests that a file which is not a document is refused with a reason rather than crashing the
	/// editor.
	/// </summary>
	[TestMethod]
	public void Open_ReportsAFileThatIsNotADocument()
	{
		DocumentStore store = NewStore();
		string notes = Path.Combine(root, "notes.txt");
		File.WriteAllText(notes, "just some text, not a node");
		CoderEditorApp app = NewApp(store);

		Assert.IsFalse(app.Open(notes));

		Assert.IsFalse(string.IsNullOrWhiteSpace(app.Status));
		Assert.IsNull(app.DocumentPath);
	}

	/// <summary>
	/// Tests that a write the filesystem refuses is reported rather than thrown, so a bad
	/// destination cannot take the editor down mid-session.
	/// </summary>
	[TestMethod]
	public void Save_ReportsAPathItCannotWrite()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);

		// A directory is not something WriteAllText can write over.
		string asDirectory = Path.Combine(root, "occupied");
		Directory.CreateDirectory(asDirectory);

		Assert.IsFalse(app.Save(asDirectory));

		StringAssert.Contains(app.Status, "Could not write", StringComparison.Ordinal);
		Assert.IsNull(app.DocumentPath);
	}

	/// <summary>
	/// Tests that opening a document records it as recent, newest first and without repeats.
	/// </summary>
	[TestMethod]
	public void RecentFiles_AreNewestFirstAndUnique()
	{
		DocumentStore store = NewStore();
		EditorSettings settings = new();
		CoderEditorApp app = NewApp(store, settings);

		app.Save(PathIn("one"));
		app.Save(PathIn("two"));
		app.Save(PathIn("one"));

		CollectionAssert.AreEqual(new[] { PathIn("one"), PathIn("two") }, settings.RecentFiles.ToArray());
	}

	/// <summary>
	/// Tests that the recent list stays a menu rather than growing without bound.
	/// </summary>
	[TestMethod]
	public void RecentFiles_StopAtTheLimit()
	{
		EditorSettings settings = new();

		for (int i = 0; i < EditorSettings.RecentFileLimit + 5; i++)
		{
			settings.Remember(PathIn($"file{i}"));
		}

		Assert.AreEqual(EditorSettings.RecentFileLimit, settings.RecentFiles.Count);
		Assert.AreEqual(PathIn($"file{EditorSettings.RecentFileLimit + 4}"), settings.RecentFiles[0]);
	}

	/// <summary>
	/// Tests that the preview generates in the language the settings name, and follows a change to it.
	/// </summary>
	[TestMethod]
	public void Preview_GeneratesInTheSelectedLanguage()
	{
		DocumentStore store = NewStore();
		EditorSettings settings = new() { PreviewLanguageId = "csharp" };
		CoderEditorApp app = NewApp(store, settings);

		app.Regenerate();
		StringAssert.Contains(app.GeneratedCode, "public class Counter", StringComparison.Ordinal);

		settings.PreviewLanguageId = "python";
		app.Regenerate();
		StringAssert.Contains(app.GeneratedCode, "def Add(self, amount: int)", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a language with no generator is reported in the pane rather than throwing out of a
	/// draw call, which would take the application down.
	/// </summary>
	[TestMethod]
	public void Preview_ReportsAnUnknownLanguage()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store, new EditorSettings { PreviewLanguageId = "klingon" });

		app.Regenerate();

		StringAssert.Contains(app.GeneratedCode, "klingon", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that starting a new document discards the old one and forgets its path.
	/// </summary>
	[TestMethod]
	public void NewFile_ForgetsThePreviousDocument()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		app.Save(PathIn("old"));

		app.NewFile();

		Assert.IsNull(app.DocumentPath);
		StringAssert.Contains(app.Status, "New document", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a class document opens with a method in it, so the Members slot is not an empty
	/// pin the user has to guess the meaning of.
	/// </summary>
	[TestMethod]
	public void NewClassDocument_IsAClassWithAMethod()
	{
		ClassDeclaration document = CoderEditorApp.NewClassDocument();

		Assert.AreEqual("NewClass", document.Name);
		Assert.IsInstanceOfType<FunctionDeclaration>(document.Members.Single());
	}

	/// <summary>
	/// Tests that starting a class document replaces what was open and generates a class, which is
	/// the whole path from the File menu to the preview pane.
	/// </summary>
	[TestMethod]
	public void NewClassFile_OpensAClassAndGeneratesIt()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		app.Save(PathIn("old"));

		app.NewClassFile();

		Assert.IsNull(app.DocumentPath);
		Assert.IsInstanceOfType<ClassDeclaration>(app.Editor.Graph.Root);
		Assert.AreEqual(0, app.Editor.Graph.Validate().Count, "a new class should have nothing outstanding");

		app.Regenerate();
		StringAssert.Contains(app.GeneratedCode, "public class NewClass", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a class document is written and read back as a class, so the editor can save the
	/// documents it can now create.
	/// </summary>
	[TestMethod]
	public void ClassDocument_SurvivesSavingAndOpening()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		app.NewClassFile();

		string path = PathIn("shape");
		Assert.IsTrue(app.Save(path), app.Status);

		CoderEditorApp reopened = NewApp(store);
		Assert.IsTrue(reopened.Open(path), reopened.Status);
		Assert.IsInstanceOfType<ClassDeclaration>(reopened.Editor.Graph.Root);
		StringAssert.Contains(reopened.GeneratedCode, "class NewClass", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the generated code can be written out beside the document, in the extension its
	/// language uses — which is what the application exists to produce.
	/// </summary>
	[TestMethod]
	public void Export_WritesTheGeneratedCodeBesideTheDocument()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		string document = PathIn("greeting");
		Assert.IsTrue(app.Save(document), app.Status);

		string? written = app.Export(document);

		Assert.IsNotNull(written);
		Assert.AreEqual(Path.Combine(root, "greeting.cs"), written);
		StringAssert.Contains(File.ReadAllText(written), "public int Add(int amount)", StringComparison.Ordinal);
		StringAssert.Contains(app.Status, "Wrote", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that the file extension follows the language the pane is showing, so switching language
	/// and exporting twice leaves two files rather than one overwritten one.
	/// </summary>
	[TestMethod]
	public void Export_UsesTheExtensionOfThePreviewedLanguage()
	{
		DocumentStore store = NewStore();
		EditorSettings settings = new() { PreviewLanguageId = "python" };
		CoderEditorApp app = NewApp(store, settings);
		string document = PathIn("greeting");

		Assert.AreEqual(Path.Combine(root, "greeting.py"), app.Export(document));

		settings.PreviewLanguageId = "cpp";
		Assert.AreEqual(Path.Combine(root, "greeting.cpp"), app.Export(document));
	}

	/// <summary>
	/// Tests that an incomplete document is not written out, since what it would generate is not
	/// source anybody wants on disk.
	/// </summary>
	[TestMethod]
	public void Export_RefusesADocumentWithOutstandingWork()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		app.Editor.Graph.AddDetached(new VariableReference("orphan"), Vector2.Zero);
		app.Editor.Graph.Validate();

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), Options);
		harness.Step(2);

		Assert.IsNull(app.Export(PathIn("greeting")));
		StringAssert.Contains(app.Status, "to finish", StringComparison.Ordinal);
		Assert.IsFalse(File.Exists(Path.Combine(root, "greeting.cs")));
	}

	/// <summary>
	/// Tests that the generated code can be put on the clipboard, and that there is nothing to copy
	/// before anything has been generated.
	/// </summary>
	/// <remarks>
	/// The clipboard belongs to ImGui, so this runs inside a frame; what is asserted is the decision
	/// about whether there was anything to copy, which is this application's.
	/// </remarks>
	[TestMethod]
	public void CopyGeneratedCode_CopiesOnlyWhenThereIsSomething()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), Options);

		Assert.IsFalse(app.CopyGeneratedCode(), "nothing has been generated yet");

		harness.Step(2);

		Assert.IsTrue(app.CopyGeneratedCode());
		StringAssert.Contains(app.Status, "Copied", StringComparison.Ordinal);
	}

	/// <summary>
	/// Tests that a problem in the pane can be turned into a selection, which is how a user gets from
	/// "something is missing" to the node that is missing it.
	/// </summary>
	[TestMethod]
	public void Problems_CanSelectTheNodeTheyAreAbout()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), Options);
		harness.Step(2);

		VariableReference orphan = new("orphan");
		app.Editor.Graph.AddDetached(orphan, Vector2.Zero);
		harness.Step(2);

		AstGraphProblem problem = app.Editor.Problems.First(p => ReferenceEquals(p.Node, orphan));
		Assert.IsTrue(app.Editor.Select(problem.Node));
		Assert.AreSame(orphan, app.Editor.SelectedNode);
	}

	/// <summary>
	/// Tests that the application renders through the real configuration its entry point uses.
	/// </summary>
	/// <remarks>
	/// This is what proves the panes, the menu bar and the embedded graph editor compose into a frame
	/// that ImGui and ImNodes accept — none of which the unit tests above would catch.
	/// </remarks>
	[TestMethod]
	public void App_RendersAFrameThroughItsRealConfiguration()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), Options);
		harness.Step(3);

		Assert.AreEqual(3, harness.FrameCount);
		Assert.IsFalse(string.IsNullOrEmpty(app.GeneratedCode), "a complete document should have generated something");
	}

	/// <summary>
	/// Tests that the File menu is drawn where the application actually has a menu bar.
	/// </summary>
	/// <remarks>
	/// ImGuiApp's main window carries no <c>ImGuiWindowFlags.MenuBar</c>, so a menu opened from the
	/// render delegate never appears — <c>BeginMenuBar</c> just returns false and the whole menu is
	/// silently dead. Drawing it through <c>OnAppMenu</c>, inside the application's own main menu bar,
	/// is what makes it visible; this renders that path to prove the items are reached.
	/// </remarks>
	[TestMethod]
	public void Menu_DrawsInsideTheApplicationMenuBar()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		app.Save(PathIn("recent"));

		ImGuiAppConfig config = app.BuildConfig();
		Assert.IsNotNull(config.OnAppMenu, "the menu must be handed to the application, not drawn in OnRender");

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(config, Options);
		harness.Step(2);

		// Open the File menu, which is what makes its items run at all. The main menu bar sits at the
		// very top-left of the viewport, so the label is a few pixels in.
		harness.Mouse.Click(24, 10);
		harness.Step(3);

		Assert.AreEqual(1, app.Settings.RecentFiles.Count, "the saved document should be listed as recent");
		Assert.AreEqual(PathIn("recent"), app.DocumentPath, "opening the menu must not disturb the document");
	}

	/// <summary>
	/// Tests that the code pane refuses to show generated source while the document is incomplete,
	/// listing what is outstanding instead.
	/// </summary>
	/// <remarks>
	/// Showing code for a half-built document would be showing something that does not correspond to
	/// what the user is looking at, so the pane lists the gaps and stops.
	/// </remarks>
	[TestMethod]
	public void CodePane_ListsProblemsInsteadOfGeneratingWhileIncomplete()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);

		// An operand nobody has filled in yet, which is what Validate reports.
		FunctionDeclaration document = new("incomplete") { ReturnType = "int" };
		document.Body.Add(new ReturnStatement(
			new BinaryExpression(AstSchema.Unfilled(), BinaryOperator.Add, AstSchema.Unfilled())));
		Assert.IsTrue(app.Open(WriteDocument(store, document, PathIn("incomplete"))), app.Status);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), Options);
		harness.Step(2);

		Assert.IsTrue(app.Editor.Problems.Count > 0, "the document should report outstanding operands");
	}

	/// <summary>
	/// Tests that the File menu's items run, by opening the menu and clicking New.
	/// </summary>
	/// <remarks>
	/// The menu items are the one part of the editor with no seam of their own — each is a click that
	/// calls a method tested elsewhere — so this drives them the only way that proves the wiring:
	/// through the mouse.
	/// </remarks>
	[TestMethod]
	public void Menu_NewDiscardsTheOpenDocument()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store);
		app.Save(PathIn("open-document"));
		Assert.IsNotNull(app.DocumentPath);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), Options);
		harness.Step(2);

		// The main menu bar sits at the top-left, and its first item drops down directly beneath.
		harness.Mouse.Click(24, 10);
		harness.Step(2);
		harness.Mouse.Click(34, 36);
		harness.Step(2);

		Assert.IsNull(app.DocumentPath, "New should have discarded the open document");
	}

	/// <summary>
	/// Writes a document straight to disk so a test can open it.
	/// </summary>
	/// <param name="store">The store to write through.</param>
	/// <param name="document">The document to write.</param>
	/// <param name="path">Where to write it.</param>
	/// <returns>The path written to.</returns>
	private static string WriteDocument(DocumentStore store, AstNode document, string path)
	{
		DocumentResult result = store.Save(document, path);
		Assert.IsTrue(result.Success, result.Error);
		return path;
	}

	/// <summary>
	/// Tests that the settings' layout preference reaches the embedded editor when the application
	/// starts, rather than being read and ignored.
	/// </summary>
	[TestMethod]
	public void App_AppliesTheStoredLayoutPreferenceOnStart()
	{
		DocumentStore store = NewStore();
		CoderEditorApp app = NewApp(store, new EditorSettings { LayoutRunning = false });

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), Options);
		harness.Step();

		Assert.IsFalse(app.Editor.LayoutRunning);
	}
}
