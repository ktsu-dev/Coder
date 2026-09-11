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
  without switching on which kind of member it is. `TypeReference` is structure rather than the
  type's text for the same reason, and a stronger one: a generator handed `std::span<const Velocity>`
  as a string can only paste it, so the caller would have to spell the target language itself.
  `Parse` and `ToString` are inverses and text the grammar cannot read becomes a name holding it
  verbatim, so a property that used to hold a string still takes and gives one.
- `Coder/Ast/SourceFile.cs`, `NamespaceDeclaration.cs`, `EnumDeclaration.cs`, `FieldDeclaration.cs` —
  what a generated *header* is made of rather than a snippet. `FieldDeclaration` is deliberately not
  `VariableDeclaration`: a field with no initialiser is value-initialised, so a default-constructed
  instance is the one the declaration described, and a local with none is ordinary. `SourceFile`'s
  `Imports` are the one part of the AST that does not translate — a C++ include path, a C# namespace
  and a Python module are different kinds of thing — so a file is built for a language.
- `Coder/Ast/FunctionKind.cs`, `FunctionDefinition.cs` — what a function declares (method,
  constructor, destructor, operator, conversion) and where its behaviour comes from (provided,
  defaulted, deleted). A declaration with no statements is otherwise ambiguous between a function
  that does nothing, one the language supplies, and one that exists to be refused. `IsAbstract` is
  C++'s *pure virtual*, which is a different thing from `IsPure`: one says a declaration has no
  definition, the other that a call has no effect.
- `Coder/Ast/UsingAlias.cs`, `MemberInitialiser.cs`, `ConstructionExpression.cs` — what a type that
  shims another needs. A member is *initialised* rather than assigned, which is the only way to start
  one that cannot be assigned at all; a language without an initialiser list assigns at the top of
  the constructor instead. `ConstructionExpression` is the one expression that needs a type rather
  than a name, which is why it could not exist before `TypeReference` did. It is also what a braced
  list is: with no type it is the list alone, which is what initialises a declaration that has
  already said its type, and a `MemberInitialiser` among its arguments is an element that names the
  member it is for — a designated initialiser in C++, an object initialiser in C#, a keyword
  argument in Python, an object literal in JavaScript. A list whose own elements are lists is a
  table and is written one per line; a list of plain values stays on one.
- `Coder/Ast/TypeReference.cs`'s `IsArray` and `Coder/Ast/FieldDeclaration.cs`'s `IsStatic` and
  `IsConstant` — what a generated constant table needs beyond a type and a name. `IsArray` says only
  that it is an array, with no bound, because where the brackets go is the generator's business and
  C++ is the one language here that puts them on the declarator rather than the type.
  `IsConstant` is the intent rather than the keyword: C++ writes `inline constexpr` at namespace
  scope and `static constexpr` inside a type, C# writes `static readonly`, and a language with no
  spelling for it omits it the way it omits an indirection.
- `Coder/Ast/ClassDeclaration.cs`'s `SpecialisationArguments` — what makes a declaration be *for* a
  type rather than *of* one. `template<> struct Describe<RigidBody>` is how C++ attaches a fact to a
  type without touching the type, which is what a generated reflection table needs: the alternative
  is naming, and a `DescribeRigidBody` every consumer has to spell for itself is the thing a lookup
  by type exists to avoid. Only C++ has it and the other three write a comment, the same as
  `CompileTimeAssertion`; the arguments are `TypeReference` rather than text, though, because a
  specialisation argument is a type and the comma in `Result<Handle, Error>` belongs to one of them
  rather than separating two.
- `Coder/Ast/CompileTimeAssertion.cs` — what a generated type promises that the type itself cannot
  say. Its `Condition` is text for the same reason `SourceFile.Imports` are: a compile-time predicate
  is language-specific in a way most of the AST is not, and there is no shared idea underneath
  `std::is_trivially_copyable_v<T>` to model. Only C++ has one; the others write a comment, because a
  file that quietly loses a guarantee looks like one that still makes it.
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
- `ktsu.ImGui.Widgets` — the editor's divider panes, and the property grid the inspector's rows are
- `ktsu.ImGui.Probes` — names the two inspector rows composed by hand, so a headless test finds them
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
