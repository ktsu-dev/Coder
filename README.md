# ktsu.Coder

A flexible and extensible .NET library for representing code as Abstract Syntax Trees (AST), serializing to YAML, and generating code in multiple programming languages.

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
-   **AstLeafNode<T>**: Generic leaf nodes for literals (strings, numbers, booleans)

### Supported Target Languages

-   **Python**: Full support for function generation with type hints and proper indentation

## Installation

Add the NuGet package:

```bash
dotnet add package ktsu.Coder
```

## Quick Start

### Creating an AST

```csharp
using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;

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
