// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor.UITests.Gallery;

using System.Text;

using ktsu.ImGui.App.Testing;

/// <summary>One picture in the editor gallery: how to stage it, and what to say about it.</summary>
/// <param name="Name">The caption, which also names the picture's file.</param>
/// <param name="Description">One or two sentences under the picture in the gallery's index.</param>
/// <param name="Stage">
/// Drives the editor into the state worth photographing, starting from the document open, its
/// layout settled and the graph fitted to the canvas.
/// </param>
internal sealed record GalleryEntry(string Name, string Description, Action<EditorUnderTest> Stage)
{
	/// <summary>
	/// Gets what to do to the application before its first frame: arranging the panes, which are
	/// built on that frame, or editing the document so the layout settles around the edit.
	/// </summary>
	public Action<CoderEditorApp>? Prepare { get; init; }

	/// <summary>
	/// Gets the part of the window to keep, or null for all of it. Asked after <see cref="Stage"/>
	/// has run, so it can measure what the stage put on screen.
	/// </summary>
	public Func<EditorUnderTest, Rectangle?>? Crop { get; init; }

	/// <summary>Gets the file name the picture is written under, without its extension.</summary>
	public string Slug => MakeSlug(Name);

	/// <inheritdoc/>
	public override string ToString() => Name;

	/// <summary>Turns a caption into a lower-case, hyphenated file name.</summary>
	/// <remarks>
	/// A language's punctuation is spelled out first, so that "C++" names a file of its own rather than
	/// the one "C" would.
	/// </remarks>
	internal static string MakeSlug(string text)
	{
		string spelled = text.Replace("++", "pp", StringComparison.Ordinal).Replace("#", "sharp", StringComparison.Ordinal);
		StringBuilder slug = new(spelled.Length);
		foreach (char character in spelled)
		{
			if (char.IsAsciiLetterOrDigit(character))
			{
				slug.Append(char.ToLowerInvariant(character));
			}
			else if (slug.Length > 0 && slug[^1] != '-')
			{
				slug.Append('-');
			}
		}

		return slug.ToString().TrimEnd('-');
	}
}
