// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor.UITests.Gallery;

using System.Numerics;

using ktsu.Coder.Ast;
using ktsu.Coder.Editor;
using ktsu.Coder.Graph;
using ktsu.ImGui.App.Testing;

/// <summary>Every picture in the editor gallery, in the order the index shows them.</summary>
/// <remarks>
/// Each stage starts from the document a new editor opens with, <see cref="CoderEditorApp.NewDocument"/>,
/// its layout settled and fitted to the canvas. A new feature earns a picture by adding an entry here;
/// nothing else needs to change, because the runner, the index and the workflow all read this list.
/// </remarks>
internal static class GalleryCatalog
{
	/// <summary>
	/// The display the pictures of the whole graph are drawn at. The graph is fitted to its canvas,
	/// and on the usual display that is about half size, where node text cannot be read.
	/// </summary>
	private static readonly (int Width, int Height) GraphDisplay = (2560, 1440);

	/// <summary>How much of the window the graph is given in the pictures of the whole graph.</summary>
	private const float GraphSplit = 0.66f;

	/// <summary>Gets the entries.</summary>
	internal static IReadOnlyList<GalleryEntry> Entries { get; } =
	[
		new(
			"Editing a document",
			"The document is an abstract syntax tree drawn as a node graph, laid out by a force-directed simulation. Beside it, the selected node's properties and the source the document generates, here in C#.",
			editor => editor.Select<ClassDeclaration>(declaration => declaration.Name == "Counter"))
		{
			Display = GraphDisplay,
			Prepare = app => app.Settings.GraphSplit = GraphSplit,
		},
		new(
			"Inspecting a node",
			"Selecting a node shows its own properties in the inspector, each with the control that suits it: a text field, a checkbox or a picker. Every edit is one step on the undo stack.",
			editor => editor.Select<FunctionDeclaration>(function => function.Name == "Add"))
		{
			// The layout pane is left out: squeezed under an inspector this tall it is a sliver of
			// sliders, and it has a picture of its own.
			Prepare = app =>
			{
				app.Settings.PropertiesSplit = 0.5f;
				app.Settings.CodeSplit = 0.45f;
			},
			Crop = editor => editor.SideColumn(0f, 0.87f),
		},
		Language("python", "Python", "One document, seven languages. The preview regenerates as the graph is edited; Python writes the receiver as `self` and the types as annotations. The method bodies still name the fields bare, because the AST has no way yet to say \"this instance's `count`\" ([#141](https://github.com/ktsu-dev/Coder/issues/141))."),
		Language("cpp", "C++", "C++ gathers the members under an access section and closes the class with the semicolon the language requires."),
		Language("rust", "Rust", "Rust puts the data in a `struct` and the behaviour in an `impl` block beside it."),
		Language("go", "Go", "Go writes a struct with its methods beside it, laid out exactly as `gofmt` would."),
		new(
			"Problems before generating",
			"A document with an operand nobody has filled in, and a node left detached from the tree, does not generate. The code pane lists what is outstanding instead, and clicking a problem selects the node it is about.",
			_ => { })
		{
			Display = GraphDisplay,
			Prepare = app =>
			{
				app.Settings.GraphSplit = GraphSplit;
				Unfinished(app);
			},
		},
		new(
			"Layout tuning",
			"Every force in the layout simulation can be tuned while watching the graph move, grouped by what it does and captioned with its effect, with the simulation's own state below.",
			editor =>
			{
				// The Repulsion header, the first of the layout panel's groups, sits a fixed distance
				// below the top of its pane.
				editor.Harness.Mouse.Click(1092f, editor.PaneBoundary(0.55f) + 73f);
				editor.Harness.Step(2);
				editor.Harness.Mouse.MoveTo(-100f, -100f);
			})
		{
			Prepare = app =>
			{
				app.Settings.PropertiesSplit = 0.10f;
				app.Settings.CodeSplit = 0.45f;
			},
			Crop = editor => editor.SideColumn(),
		},
		new(
			"File menu",
			"A document starts as a function or a class, is saved and opened as YAML, and exports the generated source in the language being previewed.",
			editor =>
			{
				editor.Harness.Mouse.Click(24, 10);
				editor.Harness.Step(2);
				editor.Harness.Mouse.MoveTo(-100f, -100f);
			})
		{
			// Wide enough to end in the gap after the graph's "Fit to canvas" button rather than through it.
			Crop = _ => new Rectangle(0, 0, 446, 160),
		},
	];

	/// <summary>Builds an entry showing the generated source in one language.</summary>
	/// <param name="languageId">The generator's language id.</param>
	/// <param name="displayName">The language's name, as the radio button shows it.</param>
	/// <param name="description">What to say about it.</param>
	private static GalleryEntry Language(string languageId, string displayName, string description) =>
		new($"Generated {displayName}", description, editor => editor.Preview(languageId))
		{
			Prepare = app =>
			{
				// Wide enough for Go's notes on its field defaults, the longest line any of them writes.
				app.Settings.GraphSplit = 0.5f;
				app.Settings.PropertiesSplit = 0.12f;
				app.Settings.CodeSplit = 0.42f;
			},

			// Down to just below the longest listing, C++'s: the layout pane below has a picture of its own.
			Crop = editor => editor.SideColumn(0f, 0.525f),
		};

	/// <summary>
	/// Takes the right-hand operand out of <c>Next</c>'s addition and leaves it beside the tree, as a
	/// user disconnecting the link would.
	/// </summary>
	private static void Unfinished(CoderEditorApp app)
	{
		BinaryExpression addition = app.Editor.Graph.Nodes.Values.OfType<BinaryExpression>()
			.Single(expression => expression.Right is VariableReference { Name: "step" });
		Expression step = addition.Right;
		addition.Right = AstSchema.Unfilled();
		app.Editor.Graph.AddDetached(step, new Vector2(200f, 0f));
	}
}
