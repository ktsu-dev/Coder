# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Restore, build, and test (standard workflow)
dotnet restore
dotnet build
dotnet test --project Coder.Test/Coder.Test.csproj

# Run a single test
dotnet test --project Coder.Test/Coder.Test.csproj --filter "FullyQualifiedName~TestMethodName"

# Build specific configuration
dotnet build -c Release

# Run the editor application
dotnet run --project Coder.Editor
```

## Project Structure

`ktsu.Coder` represents code as a language-agnostic AST, round-trips it through YAML, and generates
source in four target languages. The solution uses:

- **ktsu.Sdk** — custom SDK providing shared build configuration
- **MSTest.Sdk** — test project SDK with Microsoft Testing Platform
- `Coder` cross-targets `net10.0` and `net9.0`; `Coder.Graph` and `Coder.Editor` are `net10.0`
  only, because `ktsu.ImGui.NodeEditor` and `ktsu.ImGui.App` are

### Projects

- `Coder` — the AST, the YAML serializer and deserializer, and the language generators. No UI
  dependency, which is why it can cross-target.
- `Coder.Graph` — the node-graph view of an AST: `AstSchema`, `AstGraph`, `AstFields` and
  `AstGraphEditor`.
- `Coder.Editor` — the desktop application: panes, menu, document store, settings.
- `Coder.Cli` — the sample console application.
- `Coder.Test` — MSTest suite covering all of the above.

### Key Files

- `Coder/Ast/*.cs` — one file per node type. `AstNode` is the base; `AstCompositeNode` adds a
  keyed child dictionary; `Expression` marks the nodes that evaluate to a value. `Visibility` is an
  enumeration rather than the modifier's text, because each generator spells it differently — or,
  in Python's case, not at all — and `IHasVisibility` is how a generator reads it off a member
  without switching on which kind of member it is.
- `Coder/Languages/LanguageGeneratorBase.cs` — the emitters every generator shares.
- `Coder/Languages/StandardLanguageGenerator.cs` — owns the node dispatch, so a derived
  generator supplies only the syntax its language does not share. `CSharpGenerator` deliberately
  does not derive from it.
- `Coder.Graph/AstSchema.cs` — the uniform view of the AST's parent/child structure, hand-written
  rather than reflective. Adding a node type means adding it here.
- `Coder.Graph/AstFields.cs` — a node's editable properties as named fields of a kind, which is what
  the editor's inspector draws and what makes those edits testable without a GPU.
- `Coder.Graph/AstGraph.cs` — the AST is the document, the graph is a view: every edit is applied to
  the AST and the engine graph rebuilt from it, preserving positions by node identity.

### Dependencies

- `ktsu.CodeBlocker` — writes generated source, owning indentation and the line terminator
- `ktsu.DeepClone` — cloning support for AST nodes
- `YamlDotNet` — YAML serialization
- `ktsu.ImGui.NodeEditor`, `Hexa.NET.ImGui`, `Hexa.NET.ImNodes` — the node-graph editor
- `ktsu.ImGui.App` — the desktop application shell
- `ktsu.UndoRedo.Core` — the graph editor's undo stack
- `ktsu.Essentials` — filesystem and persistence providers the editor reads and writes through

## Architecture

The AST is the document; everything else is a projection of it.

- **Generators** walk a node and emit text. Structural nodes go to per-language emitters, and
  everything spelled identically everywhere — literals, variable references — goes to
  `TryGenerateCommonNode`. A language that spells one operator differently overrides just that
  operator.
- **The graph** converts in both directions through `AstSchema`, which presents every node's
  children as slots regardless of how the AST happens to store them (typed properties, collections,
  or the keyed dictionary). That is what lets `AstGraph` walk any node without a switch over its
  type.
- **Editing rules live in `Coder.Graph`, not in the draw calls.** `AstGraphEditor` reads what the
  user did and asks the graph to do it; the deciding is in `AstGraph`, `AstSchema` and `AstFields`,
  all of which are tested headlessly. Undo is recorded in the editor because only it knows where one
  user-visible edit begins and ends.
- **A graph is allowed to be incomplete.** `AstGraph.Validate()` reports outstanding operands and
  stranded nodes rather than throwing, and the editor shows them instead of generating.

## Testing

MSTest through the Microsoft Testing Platform. Two kinds of test:

- Headless tests over the AST, the generators, the serializer and the graph — the bulk of the suite,
  and where any rule belongs.
- Tests that render real frames through `ktsu.ImGui.App.Testing`'s `ImGuiAppHarness`, for the ImGui
  surface itself. ImNodes faults inside native code rather than throwing when its context is
  missing, so rendering a frame is the only way to find that out. ImGui contexts are process-global,
  so those classes are marked `[DoNotParallelize]`.

## CI/CD

`.github/workflows/dotnet.yml` runs the shared ktsu pipeline through the `ktsu.KtsuBuild.Tool`
dotnet tool (`ktsubuild test all` per platform, `ktsubuild ci` for the official build). Version
increments are controlled by commit message tags: `[major]`, `[minor]`, `[patch]`, `[pre]`.

`VERSION.md`, `CHANGELOG.md`, `AUTHORS.md` and `LICENSE.md` are generated by that pipeline and
should never be edited by hand.

## Code Quality

Do not add global suppressions for warnings. Use explicit suppression attributes with
justifications when needed, with preprocessor defines only as fallback. Make the smallest, most
targeted suppressions possible.
