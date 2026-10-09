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
	/// <summary>Gets the entries.</summary>
	internal static IReadOnlyList<GalleryEntry> Entries { get; } =
	[
		new(
			"Editing a document",
			"The document is an abstract syntax tree drawn as a node graph, laid out by a force-directed simulation. Beside it, the selected node's properties and the source the document generates, here in C#.",
			editor => editor.Select<ClassDeclaration>(declaration => declaration.Name == "Counter")),
		new(
			"Inspecting a node",
			"Selecting a node shows its own properties in the inspector, each with the control that suits it: a text field, a checkbox or a picker. Every edit is one step on the undo stack.",
			editor => editor.Select<FunctionDeclaration>(function => function.Name == "Add"))
		{
			Prepare = app => app.Settings.PropertiesSplit = 0.6f,
			Crop = editor => editor.SideColumn(),
		},
		Language("python", "Python", "One document, seven languages. The preview regenerates as the graph is edited; Python writes the receiver as `self` and the types as annotations."),
		Language("cpp", "C++", "C++ gathers the members under an access section and closes the class with the semicolon the language requires."),
		Language("rust", "Rust", "Rust puts the data in a `struct` and the behaviour in an `impl` block beside it."),
		Language("go", "Go", "Go writes a struct with its methods beside it, laid out exactly as `gofmt` would."),
		new(
			"Problems before generating",
			"A document with an operand nobody has filled in, and a node left detached from the tree, does not generate. The code pane lists what is outstanding instead, and clicking a problem selects the node it is about.",
			_ => { })
		{
			Prepare = Unfinished,
		},
		new(
			"Layout tuning",
			"Every force in the layout simulation can be tuned while watching the graph move, grouped by what it does and captioned with its effect, with the simulation's own state below.",
			editor =>
			{
				// The Repulsion header, the first of the layout panel's groups.
				editor.Harness.Mouse.Click(1092f, 354f);
				editor.Harness.Step(2);
				editor.Harness.Mouse.MoveTo(-100f, -100f);
			})
		{
			Prepare = app =>
			{
				app.Settings.PropertiesSplit = 0.12f;
				app.Settings.CodeSplit = 0.18f;
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
			Crop = _ => new Rectangle(0, 0, 400, 160),
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
				app.Settings.PropertiesSplit = 0.12f;
				app.Settings.CodeSplit = 0.55f;
			},
			Crop = editor => editor.SideColumn(),
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
