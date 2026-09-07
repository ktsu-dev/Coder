// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using System;
using System.Threading.Tasks;
using ktsu.Coder.Languages;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.Essentials.PersistenceProviders.ConfigHome;
using ktsu.Essentials.SerializationProviders.Yaml;
using ktsu.ImGui.App;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Entry point for the Coder editor.
/// </summary>
public static class Program
{
	/// <summary>
	/// The application name, which is also the directory the editor's settings live under.
	/// </summary>
	public const string ApplicationName = "coder";

	/// <summary>
	/// Starts the editor.
	/// </summary>
	/// <returns>A task that completes when the editor closes.</returns>
	public static async Task Main()
	{
		using ServiceProvider services = BuildServices();
		await RunAsync(services, ImGuiApp.Start).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the settings, runs the editor, and writes the settings back.
	/// </summary>
	/// <param name="services">The container to resolve the editor from.</param>
	/// <param name="start">Runs the application, returning when its window closes.</param>
	/// <returns>The settings as they stood when the editor closed.</returns>
	/// <remarks>
	/// <paramref name="start"/> is a parameter so this can be driven without opening a window. That
	/// leaves <see cref="Main"/> as the two lines that cannot be exercised any other way, rather than
	/// hiding the settings round-trip — the part with behaviour worth asserting — behind them.
	/// </remarks>
	public static async Task<EditorSettings> RunAsync(ServiceProvider services, Action<ImGuiAppConfig> start)
	{
		Ensure.NotNull(services);
		Ensure.NotNull(start);

		EditorSettingsStore settingsStore = services.GetRequiredService<EditorSettingsStore>();

		// Read before the window opens rather than on the first frame: the layout toggle and the
		// recent-files menu are both wanted by the time anything is drawn.
		EditorSettings settings = await settingsStore.LoadAsync().ConfigureAwait(false);

		CoderEditorApp app = new(
			services.GetRequiredService<DocumentStore>(),
			services.GetServices<ILanguageGenerator>(),
			settings);

		start(app.BuildConfig());

		// Start returns when the window closes, so this is the editor's last chance to remember
		// anything. A store that refuses is not worth failing the exit over.
		await settingsStore.SaveAsync(app.Settings).ConfigureAwait(false);
		return app.Settings;
	}

	/// <summary>
	/// Wires up everything the editor needs.
	/// </summary>
	/// <returns>The configured container.</returns>
	/// <remarks>
	/// The Essentials providers are registered by their own extension methods, so the editor takes
	/// the filesystem and the XDG configuration location as they are defined there rather than
	/// deciding either for itself.
	/// </remarks>
	public static ServiceProvider BuildServices()
	{
		ServiceCollection services = new();

		services.AddLanguageGenerators();
		services.AddCoderSerialization();

		services.AddNativeFileSystemProvider();
		services.AddYamlSerializationProvider();
		services.AddConfigHomePersistenceProvider<string>(ApplicationName);

		services.AddSingleton<DocumentStore>();
		services.AddSingleton<EditorSettingsStore>();

		return services.BuildServiceProvider();
	}
}
