# ktsu.Coder

A flexible and extensible .NET library for representing code as Abstract Syntax Trees (AST), serializing to YAML, and generating code in multiple programming languages.

[![License](https://img.shields.io/github/license/ktsu-dev/Coder.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.Coder?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.Coder)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.Coder?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.Coder)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.Coder?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.Coder)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/Coder?label=Commits&logo=github)](https://github.com/ktsu-dev/Coder/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/Coder?label=Contributors&logo=github)](https://github.com/ktsu-dev/Coder/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/Coder/dotnet.yml?branch=main&label=Build&logo=github)](https://github.com/ktsu-dev/Coder/actions)

## Overview

ktsu.Coder provides a language-agnostic way to represent code structures using Abstract Syntax Trees. The library allows you to:

-   Build AST structures programmatically
-   Serialize AST to human-readable YAML format
-   Generate code in supported programming languages
-   Perform round-trip serialization/deserialization

This makes it ideal for code generation tools, transpilers, and any application that needs to work with code structures in a language-independent way.

## Features

-   **Language-Agnostic AST**: Represent code structures without being tied to a specific programming language
-   **YAML Serialization**: Human-readable serialization format that's perfect for version control and diffs
-   **Extensible Language Support**: Plugin-based architecture for adding new target languages
-   **Deep Cloning**: Full support for cloning AST structures
-   **Type Safety**: Strongly-typed AST nodes with compile-time safety
-   **Metadata Support**: Attach custom metadata to any AST node

### Supported AST Node Types

-   **ClassDeclaration**: A named class with an optional base type, holding methods, fields and nested classes
-   **EntryPoint**: Where the program starts running, optionally reading arguments and returning an exit code
-   **FunctionDeclaration**: Represents function/method declarations with parameters and body
-   **Parameter**: Function parameters with optional default values
-   **ReturnStatement**: Return statements with optional expressions
-   **VariableDeclaration**: Variable declarations, optionally constant or type-inferred
-   **AssignmentStatement**: Assignments, including the compound operators (`+=`, `<<=`, …)
-   **BinaryExpression**: Two operands and an operator (`a + b`, `x == y`, `p && q`)
-   **UnaryExpression**: One operator applied to one operand (`-x`, `!ready`, `~mask`)
-   **VariableReference**: A reference to a variable by name
-   **LiteralExpression<T>**: Typed literals (string, int, bool, double)
-   **AstLeafNode<T>**: Generic leaf nodes for literals (strings, numbers, booleans)

Every operator in `UnaryOperator` and `BinaryOperator` exists in all four target languages, so no
AST built from them is untranslatable. Only the spelling varies — Python's `not`/`and`/`or`,
JavaScript's strict `===`/`!==` — and each generator overrides just the operators it spells
differently.

Increment and decrement are deliberately absent from `UnaryOperator`: Python has no spelling for
them, and an `AssignmentStatement` with `AssignmentOperator.AddAssign` expresses the same effect in
every target language.

### Visibility

A `ClassDeclaration`, a `FunctionDeclaration` and a `VariableDeclaration` each carry a `Visibility`:
`Public`, `Protected`, `Internal`, `Private`, or `Unspecified` — the default, meaning the
declaration is written the way the target language would write it anyway. It is an enumeration
rather than the modifier's text because no two languages spell visibility the same way:

| Language | How it is spelled |
|---|---|
| C# | The keyword, in front of the declaration; a class or function with none is `public` |
| C++ | An access label (`public:`, `protected:`, `private:`) the members are grouped under; `Internal` becomes `public:` |
| JavaScript | A private class member takes the `#` prefix, which is JavaScript's own private syntax; nothing for the rest |
| Python | Nothing — Python has no access modifiers, and its leading-underscore convention renames the declaration rather than modifying it |

### Constants and entry points

A `VariableDeclaration` marked `IsConstant` with a literal `InitialValue` is a constant. C# writes
`const`, C++ writes `const` for a local and `static constexpr` for a class member, JavaScript writes
`const` for a local and `static` for a class member, and Python writes a plain assignment, having no
constant declaration to spell.

An `EntryPoint` holds the statements a program runs. Each generator writes the spelling its language
looks for: C#'s `static Main`, C++'s free `int main`, Python's `main` with the `__main__` guard that
calls it (and the `import sys` its arguments and exit code need), and JavaScript's `main` with the
call that runs it.

### Visual graph editor

`ktsu.Coder.Graph` renders an AST as an editable node graph, built on
[`ktsu.ImGui.NodeEditor`](https://github.com/ktsu-dev/ImGuiApp) with its force-directed layout.

```csharp
AstGraphEditor editor = new(functionDeclaration);

// once per frame, inside your ImGui render loop
editor.Draw(new Vector2(1200, 800), deltaTime);

foreach (AstGraphProblem problem in editor.Problems)
{
    Console.WriteLine(problem.Message);
}
```

Each AST node becomes one editor node with a single output pin — itself — and one input pin per slot
it can hold a child in, so a link reads "this node fills that slot of that parent". Right-click for
the palette, drag between pins to connect, Delete to remove, and Undo/Redo on the toolbar. The
palette lists every binary, unary and assignment operator, so which expression to create is a choice
made when creating it.

Selecting a node opens it in the inspector — the panel under the canvas — which edits everything
about it that a link cannot express — a literal's value, a declaration's name and type, an expression's operator — and each edit
is one step on the undo stack. `AstFields` is the model behind it: a node's editable properties as
named fields of a kind, so a caller building its own UI does not need a panel per node type.

```csharp
foreach (AstField field in AstFields.Of(node))
{
    Console.WriteLine($"{field.Name} = {field.Value} ({field.Kind})");
}

AstFields.TryWrite(node, "Operator", nameof(BinaryOperator.Multiply));
```

A slot that holds a sequence — a function's parameters, its body, a class's members — has `+` and
`-` buttons in the inspector, so the number of them is changed without dragging nodes in from the
palette. "Convert to" replaces a node with a different kind in place, moving the operands the new
one can take across and keeping the rest rather than discarding them.

The origin of the graph's space is the middle of the canvas, and it follows the window as that is
resized. Everything measured against it agrees as a result: gravity holds an untouched arrangement
in the middle of the view, a document arrives centred on the frame it first appears, a node with no
position of its own is seeded near the middle rather than in a corner, and "Fit" re-centres an
arrangement that has been dragged away without disturbing its shape.

The AST is the document and the graph is a view of it: every edit is applied to the AST and the
graph rebuilt from it, preserving on-screen positions by node identity. A graph is allowed to be
incomplete while you work — `AstGraphEditor.Problems` (or `AstGraph.Validate()`) lists outstanding
operands and disconnected nodes rather than throwing, so consult it before handing the AST to a
generator.

The package targets `net10.0` only, since the node editor does. `ktsu.Coder` itself has no UI
dependency and continues to cross-target.

### Editor application

`Coder.Editor` is a desktop application built on `ktsu.ImGui.App`: the document as a node graph on
the left, the code it generates on the right, and a File menu for New function / New class / Open /
Save / Export with a recent files list. Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y and Ctrl+S work wherever the
keyboard focus is.

```bash
dotnet run --project Coder.Editor
```

Documents are `.coder.yaml` files — the same YAML the serializer already round-trips, so anything the
library can write, the editor can open. The code pane follows the document live and lists what is
outstanding instead of generating while an operand is unfilled; clicking one of those selects the
node it is about. Generated source can be copied to the clipboard or exported beside the document in
the extension of whichever language is being previewed.

It uses the ktsu.Essentials providers where they fit rather than reaching for `System.IO` directly:
`IFileSystemProvider` for reading and writing documents, and an `IPersistenceProvider` over the XDG
config directory for the recent-files list and the preview language. Neither location is a decision
the editor makes for itself.

### Supported Target Languages

Resolve `IEnumerable<ILanguageGenerator>` and select on `LanguageId`, or construct a generator
directly.

| `LanguageId` | Generator | Extension | Notes |
|---|---|---|---|
| `python` | `PythonGenerator` | `py` | Type hints, `None` for void, `pass` for an empty body or class; `self` on methods; `main` with its `__main__` guard |
| `csharp` | `CSharpGenerator` | `cs` | Mapped type names, `var` for inferred declarations, visibility keywords, `const`, `static Main` |
| `javascript` | `JavaScriptGenerator` | `js` | Untyped; `const`/`let`; strict `===` and `!==`; method, `static` and `#private` syntax inside a class |
| `cpp` | `CppGenerator` | `cpp` | Mapped type spellings (`str` → `std::string`); `auto` for inferred declarations; access labels, `static constexpr` members and a terminating `;` on a class |

## Installation

Add the NuGet package:

```bash
dotnet add package ktsu.Coder
```

Releases through 1.8.2 were published as `ktsu.Coder.Core`; from 2.0.0 the package ID is
`ktsu.Coder`. The namespaces (`ktsu.Coder.Ast`, `ktsu.Coder.Languages`, `ktsu.Coder.Serialization`)
are unchanged, so migrating is a package reference edit and nothing more.

## Quick Start

### Creating an AST

```csharp
using ktsu.Coder.Ast;
using ktsu.Coder.Languages;
using ktsu.Coder.Serialization;

// Create a function declaration
var function = new FunctionDeclaration("calculate_sum")
{
    ReturnType = "int"
};

// Add parameters
function.Parameters.Add(new Parameter("a", "int"));
function.Parameters.Add(new Parameter("b", "int"));
function.Parameters.Add(new Parameter("debug", "bool")
{
    IsOptional = true,
    DefaultValue = "False"
});

// Add function body
function.Body.Add(new ReturnStatement(new AstLeafNode<string>("a + b")));
```

### Serializing to YAML

```csharp
var serializer = new YamlSerializer();
string yamlContent = serializer.Serialize(function);
Console.WriteLine(yamlContent);
```

Output:

```yaml
functionDeclaration:
    name: calculate_sum
    returnType: int
    parameters:
        - name: a
          type: int
        - name: b
          type: int
        - name: debug
          type: bool
          isOptional: true
          defaultValue: False
    body:
        - returnStatement:
              expression:
                  Leaf<String>: a + b
```

### Generating Code

```csharp
var pythonGenerator = new PythonGenerator();
string pythonCode = pythonGenerator.Generate(function);
Console.WriteLine(pythonCode);
```

Output:

```python
def calculate_sum(a: int, b: int, debug: bool = False) -> int:
    return "a + b"
```

### Round-trip Serialization

```csharp
// Deserialize from YAML
var deserializer = new YamlDeserializer();
AstNode deserializedAst = deserializer.Deserialize(yamlContent);

// Generate code from deserialized AST
string regeneratedCode = pythonGenerator.Generate(deserializedAst);
```

## Architecture

The library follows SOLID principles with a clean separation of concerns:

-   **AST Layer**: Language-agnostic representation of code structures
-   **Serialization Layer**: YAML serialization/deserialization
-   **Language Layer**: Pluggable code generators for specific languages

### Extending with New Languages

To add support for a new language, implement the `ILanguageGenerator` interface:

```csharp
public class JavaScriptGenerator : LanguageGeneratorBase
{
    public override string LanguageId => "javascript";
    public override string DisplayName => "JavaScript";
    public override string FileExtension => "js";

    protected override void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel)
    {
        // Implementation for JavaScript code generation
    }
}
```

## Examples

The repository includes a sample console application:

-   **Coder.Cli**: Command-line tool demonstrating the AST, YAML round-tripping and code generation

Run it to see the library in action:

```bash
dotnet run --project Coder.Cli
```

For the interactive experience, run the desktop editor instead:

```bash
dotnet run --project Coder.Editor
```

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for:

-   New language generators
-   Additional AST node types
-   Bug fixes and improvements
-   Documentation enhancements

## License

MIT License. Copyright (c) ktsu.dev
