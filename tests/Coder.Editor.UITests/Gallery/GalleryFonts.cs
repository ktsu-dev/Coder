// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor.UITests.Gallery;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;

using Hexa.NET.ImGui;

using ktsu.ImGui.App;

/// <summary>Gives the gallery the font a running application draws in.</summary>
/// <remarks>
/// The harness never builds <c>ImGuiApp</c>'s own fonts, so a test draws in Dear ImGui's built-in
/// bitmap font. That is right for tests, which measure layout and must not move when a font
/// does, and wrong for pictures of the application. This loads the Nerd Font <c>ImGuiApp</c> ships
/// and makes it the default, which is what the widget gallery in ImGuiApp does for the same reason.
/// </remarks>
internal static class GalleryFonts
{
	/// <summary>The size <c>ImGuiApp</c> draws its interface at, at a scale of one.</summary>
	private const float Pixels = 14f;

	private static readonly Lazy<byte[]?> NerdFont = new(() =>
	{
		try
		{
			ResourceManager resources = new("ktsu.ImGui.App.Resources.Resources", typeof(ImGuiApp).Assembly);
			return resources.GetObject("NerdFont", CultureInfo.InvariantCulture) as byte[];
		}
		catch (MissingManifestResourceException)
		{
			return null;
		}
	});

	/// <summary>Gets a value indicating whether the application's font could be found.</summary>
	internal static bool Available => NerdFont.Value is not null;

	/// <summary>Adds the application's font and makes it the default. Call between frames.</summary>
	/// <returns>True when the font was loaded; false leaves the built-in font in place.</returns>
	[SuppressMessage("Major Code Smell", "S6640:Make sure that using \"unsafe\" is safe here", Justification = "FontHelper's glyph range helper returns a pointer ImGui owns for the atlas's lifetime; it is passed straight through and never dereferenced here.")]
	internal static bool Load()
	{
		if (NerdFont.Value is not byte[] data)
		{
			return false;
		}

		ImGuiIOPtr io = ImGui.GetIO();
		unsafe
		{
			ImFontPtr? font = FontHelper.AddCustomFont(io, data, Pixels, FontHelper.GetExtendedUnicodeRanges(io.Fonts));
			if (font is not ImFontPtr loaded)
			{
				return false;
			}

			io.FontDefault = loaded;
		}

		return true;
	}
}
