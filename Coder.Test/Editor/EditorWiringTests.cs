// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Editor;

using ktsu.Coder.Editor;
using ktsu.Coder.Languages;
using ktsu.Essentials;
using ktsu.ImGui.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests the container the editor starts from, and the settings it remembers between runs.
/// </summary>
/// <remarks>
/// The settings tests point <c>XDG_CONFIG_HOME</c> at a temporary directory, so the real
/// ConfigHome provider is exercised without writing into the machine's actual configuration. That
/// is the resolution rule the provider documents, so pointing it elsewhere is using it rather than
/// working around it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class EditorWiringTests
{
	private const string ConfigHomeVariable = "XDG_CONFIG_HOME";

	private static readonly string[] ExpectedLanguageIds = ["python", "csharp", "javascript", "cpp", "c"];
	private static readonly string[] ExpectedRecentFiles = ["/work/second.coder.yaml", "/work/first.coder.yaml"];

	private string root = string.Empty;
	private string? previousConfigHome;

	/// <summary>
	/// Redirects the configuration directory into a temporary one for the duration of a test.
	/// </summary>
	[TestInitialize]
	public void SetUp()
	{
		root = Path.Combine(Path.GetTempPath(), $"coder-config-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		previousConfigHome = Environment.GetEnvironmentVariable(ConfigHomeVariable);
		Environment.SetEnvironmentVariable(ConfigHomeVariable, root);
	}

	/// <summary>
	/// Puts the configuration directory back and removes the temporary one.
	/// </summary>
	[TestCleanup]
	public void TearDown()
	{
		Environment.SetEnvironmentVariable(ConfigHomeVariable, previousConfigHome);

		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// Tests that the container the entry point builds can supply everything the editor asks it for.
	/// </summary>
	/// <remarks>
	/// A missing registration only shows up when the application starts, which is the one moment a
	/// user is present. Resolving each service here turns that into a test failure instead.
	/// </remarks>
	[TestMethod]
	public void BuildServices_ResolvesEverythingTheEditorNeeds()
	{
		using ServiceProvider services = Program.BuildServices();

		Assert.IsNotNull(services.GetRequiredService<DocumentStore>());
		Assert.IsNotNull(services.GetRequiredService<EditorSettingsStore>());
		Assert.IsNotNull(services.GetRequiredService<IFileSystemProvider>());
		Assert.IsNotNull(services.GetRequiredService<IPersistenceProvider<string>>());

		string[] languages = [.. services.GetServices<ILanguageGenerator>().Select(g => g.LanguageId)];
		CollectionAssert.AreEquivalent(ExpectedLanguageIds, languages);
	}

	/// <summary>
	/// Tests that settings written on one run are read back on the next.
	/// </summary>
	[TestMethod]
	public async Task Settings_RoundTripThroughTheConfigDirectory()
	{
		using ServiceProvider services = Program.BuildServices();
		EditorSettingsStore store = services.GetRequiredService<EditorSettingsStore>();

		EditorSettings written = new() { PreviewLanguageId = "cpp", LayoutRunning = false };
		written.Remember("/work/first.coder.yaml");
		written.Remember("/work/second.coder.yaml");

		Assert.IsTrue(await store.SaveAsync(written).ConfigureAwait(false));

		EditorSettings read = await store.LoadAsync().ConfigureAwait(false);

		Assert.AreEqual("cpp", read.PreviewLanguageId);
		Assert.IsFalse(read.LayoutRunning);
		CollectionAssert.AreEqual(ExpectedRecentFiles, read.RecentFiles.ToArray());
	}

	/// <summary>
	/// Tests that a run reads the stored settings, hands the editor a configuration, and writes back
	/// whatever the editor left behind.
	/// </summary>
	/// <remarks>
	/// This is the whole reason the editor persists anything, and the one place the order matters:
	/// settings read after the window opened would arrive too late, and settings written before it
	/// closed would lose the run's changes.
	/// </remarks>
	[TestMethod]
	public async Task RunAsync_ReadsSettingsBeforeStartingAndWritesThemAfter()
	{
		using ServiceProvider services = Program.BuildServices();
		EditorSettingsStore store = services.GetRequiredService<EditorSettingsStore>();
		EditorSettings stored = new()
		{
			PreviewLanguageId = "cpp",
			WindowWidth = 1600f,
			WindowHeight = 900f,
			WindowX = 120f,
			WindowY = 80f,
			WindowMaximized = true,
			GraphSplit = 0.45f,
			PropertiesSplit = 0.7f,
			CodeSplit = 0.2f,
		};
		Assert.IsTrue(await store.SaveAsync(stored).ConfigureAwait(false));

		ImGuiAppConfig? handed = null;

		EditorSettings after = await Program.RunAsync(services, config =>
		{
			handed = config;

			// Stand in for the user's session: change something while the "window" is open.
			config.OnStart?.Invoke();
		}).ConfigureAwait(false);

		Assert.IsNotNull(handed, "the editor should have been handed a configuration to run");
		Assert.AreEqual("Coder", handed.Title);
		Assert.AreEqual("cpp", after.PreviewLanguageId, "the stored settings should have been read before starting");

		// The window and the panes are arrangements the user made, so they come back with everything
		// else rather than being reset on every run.
		Assert.AreEqual(1600f, handed.InitialWindowState.Size.X);
		Assert.AreEqual(900f, handed.InitialWindowState.Size.Y);
		Assert.AreEqual(120f, handed.InitialWindowState.Pos.X);
		Assert.AreEqual(80f, handed.InitialWindowState.Pos.Y);
		Assert.AreEqual(Silk.NET.Windowing.WindowState.Maximized, handed.InitialWindowState.LayoutState);

		EditorSettings reread = await store.LoadAsync().ConfigureAwait(false);
		Assert.AreEqual("cpp", reread.PreviewLanguageId, "the settings should have been written back on exit");
		Assert.AreEqual(1600f, reread.WindowWidth, "the window size should have been written back on exit");
		Assert.AreEqual(120f, reread.WindowX);
		Assert.IsTrue(reread.WindowMaximized);
		Assert.AreEqual(0.45f, reread.GraphSplit, "the pane split should have been written back on exit");
		Assert.AreEqual(0.7f, reread.PropertiesSplit);
		Assert.AreEqual(0.2f, reread.CodeSplit, "the layout pane's split should round-trip too");
	}

	/// <summary>
	/// Tests that a fresh run opens a window the platform places, rather than one this application
	/// put in a corner.
	/// </summary>
	[TestMethod]
	public void BuildConfig_LeavesAFirstRunsPositionToThePlatform()
	{
		using ServiceProvider services = Program.BuildServices();
		CoderEditorApp app = new(
			services.GetRequiredService<DocumentStore>(),
			services.GetServices<ILanguageGenerator>(),
			new EditorSettings());

		ImGuiAppConfig config = app.BuildConfig();

		Assert.AreEqual(new ImGuiAppWindowState().Pos, config.InitialWindowState.Pos);
		Assert.AreEqual(Silk.NET.Windowing.WindowState.Normal, config.InitialWindowState.LayoutState);
	}

	/// <summary>
	/// Tests that a first run, with nothing stored, opens on the defaults rather than failing.
	/// </summary>
	[TestMethod]
	public async Task Settings_FallBackToDefaultsOnAFirstRun()
	{
		using ServiceProvider services = Program.BuildServices();
		EditorSettingsStore store = services.GetRequiredService<EditorSettingsStore>();

		EditorSettings settings = await store.LoadAsync().ConfigureAwait(false);

		Assert.AreEqual("csharp", settings.PreviewLanguageId);
		Assert.IsTrue(settings.LayoutRunning);
		Assert.AreEqual(0, settings.RecentFiles.Count);
	}
}
