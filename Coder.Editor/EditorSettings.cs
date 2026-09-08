// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;

/// <summary>
/// What the editor remembers between runs.
/// </summary>
public sealed class EditorSettings
{
	/// <summary>
	/// How many recent files to keep. Enough to be useful, short enough to stay a menu.
	/// </summary>
	public const int RecentFileLimit = 8;

	/// <summary>
	/// Gets the files most recently opened, newest first.
	/// </summary>
	public Collection<string> RecentFiles { get; init; } = [];

	/// <summary>
	/// Gets or sets the language whose generated output the editor shows.
	/// </summary>
	public string PreviewLanguageId { get; set; } = "csharp";

	/// <summary>
	/// Gets or sets a value indicating whether the force-directed layout runs.
	/// </summary>
	public bool LayoutRunning { get; set; } = true;

	/// <summary>
	/// Gets or sets the window's width when it is not maximized.
	/// </summary>
	public float WindowWidth { get; set; } = 1280f;

	/// <summary>
	/// Gets or sets the window's height when it is not maximized.
	/// </summary>
	public float WindowHeight { get; set; } = 720f;

	/// <summary>
	/// Gets or sets the window's horizontal position when it is not maximized.
	/// </summary>
	/// <remarks>
	/// Defaulted to the windowing layer's own "no position yet" value, which is deliberately far off
	/// screen and means the platform should place the window. A first run therefore opens wherever
	/// the system would have put it rather than in a corner this application chose.
	/// </remarks>
	public float WindowX { get; set; } = -short.MinValue;

	/// <summary>
	/// Gets or sets the window's vertical position when it is not maximized.
	/// </summary>
	public float WindowY { get; set; } = -short.MinValue;

	/// <summary>
	/// Gets or sets a value indicating whether the window was maximized.
	/// </summary>
	/// <remarks>
	/// Kept alongside the size rather than instead of it: a maximized window still has a size to go
	/// back to when it is restored, and that is the one worth remembering.
	/// </remarks>
	public bool WindowMaximized { get; set; }

	/// <summary>
	/// Gets or sets the share of the window's width the graph takes, the rest going to the panel
	/// beside it.
	/// </summary>
	public float GraphSplit { get; set; } = 0.62f;

	/// <summary>
	/// Gets or sets the share of that panel's height the properties take, the rest going to the code
	/// preview under them.
	/// </summary>
	public float PropertiesSplit { get; set; } = 0.4f;

	/// <summary>
	/// Records a file as the most recently opened, without letting the list grow or repeat.
	/// </summary>
	/// <param name="path">The file that was opened.</param>
	public void Remember(string path)
	{
		Ensure.NotNull(path);

		// Ordinal: these are paths, and a culture-sensitive comparison can decide two different
		// files are the same one.
		string? existing = RecentFiles.FirstOrDefault(f => string.Equals(f, path, StringComparison.Ordinal));
		if (existing is not null)
		{
			RecentFiles.Remove(existing);
		}

		RecentFiles.Insert(0, path);

		while (RecentFiles.Count > RecentFileLimit)
		{
			RecentFiles.RemoveAt(RecentFiles.Count - 1);
		}
	}
}

/// <summary>
/// Loads and stores <see cref="EditorSettings"/> in the user's configuration directory.
/// </summary>
/// <remarks>
/// Backed by an <see cref="IPersistenceProvider{TKey}"/> over the XDG config location, which is
/// where a per-user preference belongs and is exactly what the ConfigHome provider resolves. The
/// editor therefore does not decide where settings live, or have to create the directory itself.
/// </remarks>
/// <param name="persistence">The store to keep settings in.</param>
public sealed class EditorSettingsStore(IPersistenceProvider<string> persistence)
{
	/// <summary>
	/// The key settings are stored under.
	/// </summary>
	public const string Key = "editor-settings";

	/// <summary>
	/// Reads the stored settings, or the defaults when there are none.
	/// </summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The settings to run with.</returns>
	/// <remarks>
	/// A first run and a settings file that has become unreadable are the same thing to a user: the
	/// editor should open. Neither is worth refusing to start over, so both fall back to defaults.
	/// </remarks>
	public async Task<EditorSettings> LoadAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			return await persistence.RetrieveAsync<EditorSettings>(Key, cancellationToken).ConfigureAwait(false)
				?? new EditorSettings();
		}
		catch (PersistenceProviderException)
		{
			return new EditorSettings();
		}
	}

	/// <summary>
	/// Stores the settings.
	/// </summary>
	/// <param name="settings">The settings to store.</param>
	/// <param name="cancellationToken">Cancels the write.</param>
	/// <returns>True if they were stored; false if the store refused, which is not worth interrupting the user for.</returns>
	public async Task<bool> SaveAsync(EditorSettings settings, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(settings);

		try
		{
			await persistence.StoreAsync(Key, settings, cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (PersistenceProviderException)
		{
			return false;
		}
	}
}
