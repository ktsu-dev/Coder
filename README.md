# ktsu.Coder

A flexible and extensible .NET library for representing code as Abstract Syntax Trees (AST), serializing to YAML, and generating code in multiple programming languages.

[![License](https://img.shields.io/github/license/ktsu-dev/Coder.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.Coder.Core?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.Coder.Core)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.Coder.Core?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.Coder.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.Coder.Core?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.Coder.Core)
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

### Supported Target Languages

Resolve `IEnumerable<ILanguageGenerator>` and select on `LanguageId`, or construct a generator
directly.

| `LanguageId` | Generator | Extension | Notes |
|---|---|---|---|
| `python` | `PythonGenerator` | `py` | Type hints, `None` for void, `pass` for an empty body |
| `csharp` | `CSharpGenerator` | `cs` | Mapped type names, `var` for inferred declarations |
| `javascript` | `JavaScriptGenerator` | `js` | Untyped; `const`/`let`; strict `===` and `!==` |
| `cpp` | `CppGenerator` | `cpp` | Mapped type spellings (`str` → `std::string`); `auto` for inferred declarations |

## Installation

Add the NuGet package:

```bash
dotnet add package ktsu.Coder
```

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

The repository includes two example applications:

-   **Coder.CLI**: Command-line tool demonstrating basic functionality
-   **Coder.App**: Console application with more complex examples

Run them to see the library in action:

```bash
dotnet run --project Coder.CLI
dotnet run --project Coder.App
```

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for:

-   New language generators
-   Additional AST node types
-   Bug fixes and improvements
-   Documentation enhancements

## License

MIT License. Copyright (c) ktsu.dev
