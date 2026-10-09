// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor.UITests.Gallery;

using ktsu.Coder.Editor;
using ktsu.Coder.Languages;
using ktsu.ImGui.App.Testing;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Photographs the editor for <c>docs/gallery</c>: one picture per <see cref="GalleryCatalog"/>
/// entry, and an index that captions them.
/// </summary>
/// <remarks>
/// <para>
/// These run as ordinary tests, so every pull request proves each picture can still be staged: a
/// stage that reaches for a node, a pane or a language that has gone fails here rather than at the
/// next regeneration on main. Pictures are written to a temporary directory unless
/// <c>CODER_GALLERY_OUT</c> names one, which is how the gallery workflow, and anyone regenerating by
/// hand, sends them to <c>docs/gallery</c>.
/// </para>
/// <para>
/// Each entry builds its own application from <see cref="Program.BuildServices"/>, the container the
/// real editor runs with, and opens nothing from disk, so the status bar never shows a path from the
/// machine that drew it. The layout simulation steps at the harness's fixed frame time, which is what
/// makes a settled graph come out the same on every run.
/// </para>
/// </remarks>
[TestClass]
public sealed class EditorGallery
{
	/// <summary>The environment variable naming the directory the gallery is written to.</summary>
	internal const string OutputVariable = "CODER_GALLERY_OUT";

	/// <summary>The display the gallery is drawn at.</summary>
	internal static readonly (int Width, int Height) Display = (1600, 900);

	/// <summary>
	/// How many frames the layout runs before the picture is staged: about four seconds of the
	/// simulation, which is long enough for the default document to come to rest.
	/// </summary>
	private const int SettleFrames = 240;

	private static readonly Lazy<string> TemporaryOutput = new(() =>
		Path.Combine(Path.GetTempPath(), $"coder-gallery-{Guid.NewGuid():N}"));

	/// <summary>Gets or sets the test context, for reporting where each picture went.</summary>
	public TestContext TestContext { get; set; } = null!;

	/// <summary>Gets every entry's name, one test case each.</summary>
	public static IEnumerable<object[]> EntryNames => GalleryCatalog.Entries.Select(entry => new object[] { entry.Name });

	/// <summary>Gets the directory pictures are written to.</summary>
	internal static string OutputDirectory =>
		Environment.GetEnvironmentVariable(OutputVariable) is string output && output.Length > 0
			? Path.GetFullPath(output)
			: TemporaryOutput.Value;

	/// <summary>Removes the temporary directory, when pictures went there rather than to a named one.</summary>
	[ClassCleanup]
	public static void DeleteTemporaryOutput()
	{
		if (TemporaryOutput.IsValueCreated && Directory.Exists(TemporaryOutput.Value))
		{
			Directory.Delete(TemporaryOutput.Value, recursive: true);
		}
	}

	[TestMethod]
	[DynamicData(nameof(EntryNames))]
	public void Photograph(string name)
	{
		GalleryEntry entry = GalleryCatalog.Entries.Single(candidate => candidate.Name == name);

		using ServiceProvider services = Program.BuildServices();
		CoderEditorApp app = new(
			services.GetRequiredService<DocumentStore>(),
			services.GetServices<ILanguageGenerator>(),
			new EditorSettings());
		entry.Prepare?.Invoke(app);

		using ImGuiAppHarness harness = ImGuiAppHarness.Start(app.BuildConfig(), new HarnessOptions { Width = Display.Width, Height = Display.Height });
		Assert.IsTrue(GalleryFonts.Load(), "ImGuiApp's own font could not be found, so the pictures would not look like the application.");
		harness.Mouse.MoveTo(-100f, -100f);
		harness.Step(SettleFrames);

		// The editor fits the graph to the canvas on its first frames, before the layout has moved
		// anything; fitting again once it has settled is what puts the whole document in the picture.
		app.Editor.FitView();
		harness.Step(2);

		EditorUnderTest editor = new(app, harness);
		entry.Stage(editor);
		harness.Step(2);

		Bitmap32 frame = harness.Target;
		Rectangle region = entry.Crop?.Invoke(editor) ?? new Rectangle(0, 0, frame.Width, frame.Height);
		Bitmap32 picture = Crop(frame, region);

		Directory.CreateDirectory(OutputDirectory);
		string path = Path.Combine(OutputDirectory, entry.Slug + ".png");
		picture.SavePng(path);
		TestContext.WriteLine($"Wrote {path} ({picture.Width}x{picture.Height}).");
	}

	[TestMethod]
	public void WriteTheIndex()
	{
		string[] slugs = [.. GalleryCatalog.Entries.Select(entry => entry.Slug)];
		Assert.HasCount(slugs.Length, slugs.Distinct(StringComparer.Ordinal), "Two gallery entries would write the same file.");

		Directory.CreateDirectory(OutputDirectory);
		File.WriteAllText(Path.Combine(OutputDirectory, "README.md"), GalleryIndex.Render(GalleryCatalog.Entries));
	}

	/// <summary>Copies a rectangle out of a frame, clamped to its edges.</summary>
	internal static Bitmap32 Crop(Bitmap32 source, Rectangle region)
	{
		int minX = Math.Clamp(region.MinX, 0, source.Width);
		int minY = Math.Clamp(region.MinY, 0, source.Height);
		int maxX = Math.Clamp(region.MaxX, minX, source.Width);
		int maxY = Math.Clamp(region.MaxY, minY, source.Height);
		Assert.IsTrue(maxX > minX && maxY > minY, $"The crop {region} leaves nothing of a {source.Width}x{source.Height} frame.");

		Bitmap32 cropped = new(maxX - minX, maxY - minY);
		int rowBytes = cropped.Width * 4;
		for (int y = minY; y < maxY; y++)
		{
			source.Pixels.Slice(((y * source.Width) + minX) * 4, rowBytes)
				.CopyTo(cropped.Pixels.Slice((y - minY) * rowBytes, rowBytes));
		}

		return cropped;
	}
}
