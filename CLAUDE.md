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
source in seven target languages. The solution uses:

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
  scope and `static constexpr` inside a type, C# writes `static readonly`, C writes `static const`
  at file scope and a note inside a struct, having no static data member at all, Rust picks between
  a `const` and a `static` with it and lets it decide whether a local is `let` or `let mut`, Go
  honours it only where the language can — a Go `const` holds a number, a string or a boolean and
  nothing with a field in it, so a table is a `var` with a note — and a language with no spelling for
  it omits it the way it omits an indirection.
- `Coder/Ast/PropertyDeclaration.cs` — a member read and written through code rather than stored.
  Three targets have the thing itself (C# `T Name { get; set; }`, Python `@property`, JavaScript
  `get name()`); the other four have the two halves of what it is and no word joining them, which
  makes the decision here not the syntax but **where a property lands**. A property whose accessors
  have no bodies *is* a field with a storage location the compiler supplies, so it comes out as a
  field; one with bodies *is* a pair of functions, so it comes out as the pair. Neither is an
  approximation. The separation happens to the member list, in `StandardLanguageGenerator.Separated`,
  rather than at the point each member is written — Rust puts data in a `struct` and behaviour in an
  `impl`, Go writes fields in the type and methods beside it, C lowers a method to a free function
  taking the instance, and every one of those routes reads the member list before anything is
  written, so a property separated afterwards arrives too late to be routed. Separated first, each
  generator's existing routing sees an ordinary field or function and needs to know nothing about
  properties. `NamingStyle` is for the setter's name, which is the one name a generator has to
  *invent* rather than repeat: rustc warns on a member name that is not snake case and an unexported
  Go name cannot be called from outside its package, so the convention is more than taste.
- `Coder/Ast/Annotation.cs` — metadata attached to a declaration: an attribute in C# and C++, an
  attribute macro in Rust, a decorator in Python. The name here is the one that is nobody's keyword.
  Its `Name` and `Arguments` are **text, written verbatim**, for the reason `CallExpression.Callee`
  is: `[Obsolete]`, `#[serde(rename = "x")]` and `@staticmethod` have nothing underneath them to
  hold, and one of them usually means nothing at all in the others. What *is* shared, and is what
  each generator supplies, is the syntax around them — `[…]`, `[[…]]`, `#[…]`, `@…` — which is
  exactly the split `SpellImport` already makes. C, JavaScript and Go have no metadata syntax and
  write the annotation down, because a file that quietly loses its `[Obsolete]` looks like a file
  that never had one. The arguments are a sequence rather than one string so that a comma inside an
  argument stays inside it.
- `Coder/Ast/TypeParameter.cs` and `TypeConstraint.cs` — what a declaration is written *over*, on
  `ClassDeclaration` and on `FunctionDeclaration`. Values rather than nodes, like
  `SpecialisationArguments` and for the same reason: a type parameter is part of the thing being
  declared rather than a member of it, and `Parse`/`ToString` are inverses so a document carries a
  whole parameter on one line. **The constraints are where the targets part, and that is the
  decision worth knowing.** A parameter's *name* travels everywhere; what a language can say about
  that name does not. `TypeConstraintKind` names four intents — implements a type, is a value, is a
  reference, is constructible — and stops there, because those are the ones with a shared idea
  underneath; a C++ concept is a predicate that can ask anything at all (`requires (T a) {
  a.begin(); }`), which is the same reason `CompileTimeAssertion.Condition` is text. Then: C# spells
  all four, and reorders them, because C# requires the class or struct constraint first and `new()`
  last and the AST has no reason to know that. Rust spells two — a trait bound is exactly
  `Implements` and `Default` is exactly `Constructible` — and carries the parameters onto every
  `impl` block, which is what makes `impl<T: Bound> Mass<T>` compile where `impl Mass` would not.
  Go spells one, `Implements` being exactly a Go constraint interface, and only for a *function*: a
  method on a generic type needs the parameters in three places and spelled two ways (`NewPoint` for
  the constructor's name, `Point[T]` for its receiver and result), so a generic type is written
  down instead. C++ writes `template <typename T>` and notes every constraint, the standard concepts
  needing an include the AST does not carry. C, Python and JavaScript write the whole parameter
  down. `RustGeneratedSourceCompilesTests` compiles a generic struct with a load-bearing bound, so
  the `impl` repetition is checked rather than asserted.
- `Coder/Ast/ClassDeclaration.cs`'s `Interfaces`, `IsRecord`, `IsPartial` and `IsReadOnly` — what a
  type declaration says about itself beyond its name. `Interfaces` is separate from `BaseType`
  rather than folded into one list, because what a target does with the two differs: C# writes them
  in one list but takes at most one class in it and puts it first, C++ writes `public` before each
  and does not distinguish them at all, Rust makes both supertraits of a trait and has no answer for
  a struct's at all, C can give the first-member position — the one that makes a pointer to the
  whole a pointer to the member — to exactly one of them, and Go satisfies an interface structurally
  and so writes `var _ Contract = (*Type)(nil)`, an assertion the compiler checks rather than a
  declaration. A generator handed one list would be guessing which entry was the class.
  The three modifiers split along a line worth stating once: `IsRecord` and `IsReadOnly` are claims
  about the type — it compares by value, no member of it modifies it — so a target with no word for
  one writes it down, the same as `CompileTimeAssertion`, while Rust's `#[derive(Clone, Debug,
  PartialEq)]` is a word for the first and is used. `IsPartial` claims nothing about the type; it is
  permission to declare the rest of it elsewhere, and a generator that has written the whole
  declaration has not used the permission for anything a reader could miss, so it is dropped in
  silence. Python's `@dataclass` is the obvious answer for a record and is *not* taken: the
  decorator needs an import, and a class is generated on its own as readily as inside a file whose
  imports the AST carries, so emitting one would change how that generator writes a file rather than
  how it writes a class.
- `Coder/Ast/ClassDeclaration.cs`'s `SpecialisationArguments` — what makes a declaration be *for* a
  type rather than *of* one. `template<> struct Describe<RigidBody>` is how C++ attaches a fact to a
  type without touching the type, which is what a generated reflection table needs: the alternative
  is naming, and a `DescribeRigidBody` every consumer has to spell for itself is the thing a lookup
  by type exists to avoid. C++ has it, and Rust answers it exactly — `impl Describe for RigidBody`
  attaches facts to a type without touching the type, which is the whole of what the specialisation
  is for; the rest write a comment, the same as `CompileTimeAssertion` — Go included, and for the one
  reason worth reading: a method may only be declared in the package that declares its type, so there
  is nowhere outside it for facts about it to be attached. The arguments are
  `TypeReference` rather than text, though, because a
  specialisation argument is a type and the comma in `Result<Handle, Error>` belongs to one of them
  rather than separating two.
 - `Coder/Ast/CompileTimeAssertion.cs` — what a generated type promises that the type itself cannot
  say. Its `Condition` is text for the same reason `SourceFile.Imports` are: a compile-time predicate
  is language-specific in a way most of the AST is not, and there is no shared idea underneath
  `std::is_trivially_copyable_v<T>` to model. Only C++ and C have one — `static_assert` and
  `_Static_assert`, the second of which requires a message, so an assertion with none is given its
  own condition — and Rust, whose `const _: () = assert!(…)` needs no macro crate because a constant
  nobody names still has to be evaluated for the program to build, and Go, which has no assertion and
  something that works as one: the keys of a map literal must be distinct and a constant key is
  checked while compiling, so `map[bool]struct{}{false: {}, cond: {}}` is a compile error exactly when
  `cond` is false. The rest write a comment, because a file that quietly loses a guarantee looks like
  one that still makes it.
- `Coder/Ast/CallExpression.cs`, `ExpressionStatement.cs`, `ConditionalExpression.cs` — what a
  function *body* is made of beyond an operator applied to operands. `CallExpression`'s `Callee` is
  text and written verbatim, for the reason `SourceFile.Imports` and `CompileTimeAssertion.Condition`
  are: a square root is `std::sqrt`, `Math.Sqrt`, `math.sqrt` and `f64::sqrt`, and there is no shared
  idea underneath those to model. Its `Receiver` *is* modelled, because that is the part the
  languages disagree about — `a.b(c)` in six of them and `b(&a, c)` in C, which is the same lowering
  `CGenerator` already performs on the declaration, so the call site follows the declaration.
  `ExpressionStatement` is where a call made for its effect stands: without it a `void` call has
  nowhere to go, since the AST could say what to do with a value but not that a value is beside the
  point. `ConditionalExpression` is a choice between two values rather than between two statements,
  and it is the one node where Go is the language with least to offer: `?:` in the four C-family
  ones, `go if ready else wait` in Python, `if ready { go } else { wait }` in Rust, which makes `if`
  an expression — and in Go a closure called where it stands, because `if` there is a statement and
  yields nothing at all.
- `Coder/Languages/LanguageGeneratorBase.cs` — the emitters every generator shares.
- `Coder/Languages/StandardLanguageGenerator.cs` — owns the node dispatch, so a derived
  generator supplies only the syntax its language does not share. `CSharpGenerator` deliberately
  does not derive from it. It also owns the three things more than one generator needs and no
  language owns: the braced list, the rule that two undocumented members of a kind stay in one block,
  and the operator names built from the AST's own vocabulary for the two targets — C and Go — that
  cannot overload one and so have to call it something.
- `Coder/Languages/CFamilyGenerator.cs` — what C and C++ share beyond what every generator shares,
  and all of it is about C: the preprocessor (`#pragma once`, `#include`), the braced list with its
  designated initialisers, and the declarator that puts an array's brackets after the name. Every
  position in either language that declares a name goes through that declarator, which is the whole
  of why it exists: a type spelled on its own carries no brackets, because the positions that spell
  one without a name — a return type, a base type, an enumeration's underlying type — are ones
  neither language lets an array stand in at all. `CppGeneratedSourceCompilesTests` is what holds
  C++ to it, and is why the rule is checked rather than asserted; before it there was no C++ compile
  test and an array-typed parameter came out as `int[] steps`. The type
  mappings deliberately stay
  with each generator, because `str` is a `std::string` in one language and a `const char*` in the
  other and the whole of what a mapping is is the spelling.
- `Coder/Languages/CGenerator.cs` — the target with the least to map onto, and so the one whose
  decisions are worth reading. C has no classes, namespaces, overloading or generics, so a type is a
  `typedef struct`, a member function is a free function taking the instance, an interface is a
  struct of function pointers, a base type is the first member (which is what makes the two
  layout-compatible), and a namespace is a comment — folding its name into the declarations would
  rename them without renaming the references to them. The dialect is C99 plus C11's
  `_Static_assert`, which is why a pure function gets no `[[nodiscard]]` and an enumeration no fixed
  underlying type. `Coder.Test/Languages/CGeneratedSourceCompilesTests.cs` compiles what it writes,
  because C's rules about linkage, empty parameter lists and what may initialise an object with
  static storage duration are not visible in the text.
- `Coder/Languages/RustGenerator.cs` — the target with the most to map onto, and so the one where the
  interesting question is which feature each part of a declaration became rather than what to write
  in place of it. Data goes in a `struct` and behaviour in an `impl` block; an interface is a `trait`
  and a base type on one is a supertrait; a destructor is `impl Drop`, an operator is its `std::ops`
  trait, a conversion is `impl From`, and a specialisation is `impl Trait for Type` — which is the
  one place a target answers C++'s explicit specialisation exactly. What is left over is inheritance,
  which Rust does not have, and the operators it supplies from another one and will not let a type
  define by itself.
- `Coder/Languages/GoGenerator.cs` — the target that answers most of the AST with something it
  already had. A type is a `struct` with its methods beside it, an interface is an `interface` that
  nothing declares it implements, and a base type is an embedded field, whose members are promoted —
  as near as Go comes to inheritance and nearer than the other targets without it manage. A member
  that promises not to modify what it is called on takes a value receiver and one that does takes a
  pointer receiver, which is the same promise C++ writes as a trailing `const` made by the shape of
  the declaration. What Go left out it left out on purpose, so an operator is a method named for what
  it does, a constructor is `New<Type>`, a destructor is the `Close` a caller defers, and a constant
  table is a `var`. Visibility is the one thing no generator can write: Go exports a name whose first
  letter is a capital, so a name disagreeing with its declared visibility gets a note rather than a
  rename the references would not follow. Output is already what `gofmt` would write, tab and aligned
  columns included, which is why `IndentString` is overridable at all.
- `Coder.Editor/EditorSyntax.cs` — hands the highlighter the definitions in `RustSyntax.cs` and
  `GoSyntax.cs`, because it ships fifteen languages and neither of those is one of them. An id it has
  never heard of is not an error to it, so without this the preview pane would draw generated Rust or
  Go in one colour and say nothing.
- `Coder.Test/Languages/CompiledExemplar.cs` — one AST, compiled by three real compilers. The C, Rust
  and Go generators are each checked by compiling what they write, and they are checked against the
  same declarations, which says more than three parallel fixtures could: the claim being made is that
  the same AST comes out as valid source in each target. `GoGeneratedSourceCompilesTests` adds the
  check no other generator here can have — that the output is what `gofmt` would write — because Go
  has one formatter and everybody runs it.
- `Coder.Graph/AstSchema.cs` — the uniform view of the AST's parent/child structure, hand-written
  rather than reflective. Adding a node type means adding it here.
- `Coder.Graph/AstFields.cs` — a node's editable properties as named fields of a kind, which is what
  the editor's inspector draws and what makes those edits testable without a GPU.
- `Coder.Test/Graph/AstNodeCoverageTests.cs` — the reflective walk the two files above deliberately
  are not. Both end in a silent default, so a node type left out of either draws an empty inspector
  rather than failing anything; this reflects over every concrete `AstNode` subclass and asserts
  that the schema has a decision about its children, that the inspector offers a field for
  everything about it that is not a child, and that each field reads back what it is written. A
  node with no children says so in the test's `Childless` list, so the exemption is a line somebody
  wrote rather than an omission nobody noticed.
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
