# Design Document: Multilingual Code Generation Library

This document outlines the architecture and design decisions for building a multilingual code generation library using .NET technologies. The library leverages Tree-sitter's AST structure, serialized to YAML, and employs SOLID principles, including a dependency injection model for language-specific conversions.

## Objectives

* Allow users to programmatically generate code structures using an intermediate Tree-sitter-compatible representation.
* Serialize and deserialize representations to and from YAML.
* Enable easy extension to support multiple programming languages through a dependency-injection-based architecture.

## Technical Overview

### Intermediate Representation (IR)

The IR is structured based on Tree-sitter AST conventions, represented clearly and explicitly in YAML.

**Example YAML representation:**

```yaml
function_declaration:
  name: "MyFunction"
  parameters:
    - name: "param1"
      type: "int"
  body:
    - return_statement:
        integer: "42"
```

### Core Components

1. **AST Node Classes**:

   * Define generic AST node classes (`AstNode`, `AstLeafNode`, `AstCompositeNode`) to represent hierarchical structures using C#.

2. **Serializer and Deserializer**:

   * `YamlSerializer`: Converts AST node structures into YAML format.
   * `YamlDeserializer`: Parses YAML back into AST structures.

3. **Language Conversion Interfaces**:

   * Abstract interface (`ILanguageGenerator`) for converting AST nodes into specific languages.
   * Each supported language implements this interface, allowing clean separation of language-specific logic.

4. **Dependency Injection Container**:

   * Utilize Microsoft's built-in Dependency Injection (`Microsoft.Extensions.DependencyInjection`) to manage language-specific converters dynamically at runtime, adhering to the Open/Closed Principle.

### Architectural Design (SOLID Principles)

#### Single Responsibility Principle (SRP)

* Each class/module handles only one responsibility:

  * AST classes: Structure representation
  * YAML serialization: IR conversion
  * Language-specific modules: Code generation

#### Open/Closed Principle (OCP)

* Extensible language support without modifying core library code by injecting language converters at runtime.

#### Liskov Substitution Principle (LSP)

* Language-specific classes adhere to the `ILanguageGenerator` interface and can substitute each other seamlessly.

#### Interface Segregation Principle (ISP)

* Clearly separated interfaces ensure no component depends on methods it does not use.

#### Dependency Inversion Principle (DIP)

* High-level modules (core library) depend on abstract interfaces rather than concrete implementations of language-specific converters.

## Implementation Plan

### Project Structure (.NET Solution)

```plaintext
CodegenLibrary/
├── Ast/
│   ├── AstNode.cs
│   ├── AstLeafNode.cs
│   └── AstCompositeNode.cs
├── Serialization/
│   ├── YamlSerializer.cs
│   └── YamlDeserializer.cs
├── Languages/
│   ├── ILanguageGenerator.cs
│   ├── PythonGenerator.cs
│   ├── CppGenerator.cs
│   ├── TypeScriptGenerator.cs
│   └── ...
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── Tests/
    ├── YamlSerializerTests.cs
    ├── LanguageGeneratorsTests.cs
    └── ...
```

### Core Interfaces and Classes

**Abstract Interface:**

```csharp
public interface ILanguageGenerator
{
    string Generate(AstNode astNode);
}
```

**Example Language Implementation:**

```csharp
public class PythonGenerator : ILanguageGenerator
{
    public string Generate(AstNode astNode)
    {
        // Conversion logic here
    }
}
```

**Dependency Injection Configuration Example:**

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLanguageGenerators(this IServiceCollection services)
    {
        services.AddSingleton<ILanguageGenerator, PythonGenerator>();
        services.AddSingleton<ILanguageGenerator, CppGenerator>();
        // additional languages...

        return services;
    }
}
```

## Workflow Example

1. User creates AST structure programmatically.
2. AST structure serialized to YAML for readability and diff-ability.
3. Language-specific generator is retrieved from the DI container.
4. The generator converts AST to target language code.

## Usage Example (C# Pseudo-code)

```csharp
var ast = new FunctionDeclaration("MyFunction", new [] { new Parameter("param1", "int") }, new [] { new ReturnStatement(42) });

// Serialize to YAML
var serializer = new YamlSerializer();
string yamlStr = serializer.Serialize(ast);

// Deserialize from YAML
var deserializer = new YamlDeserializer();
AstNode astLoaded = deserializer.Deserialize(yamlStr);

// Setup DI container
var services = new ServiceCollection();
services.AddLanguageGenerators();
var provider = services.BuildServiceProvider();

// Generate Python code
var pythonGenerator = provider.GetService<ILanguageGenerator>();
string pythonCode = pythonGenerator.Generate(astLoaded);
```

## Conclusion

This .NET-based design provides a robust, flexible, and scalable framework to programmatically generate and serialize AST-based intermediate representations and convert them into multiple target languages, ensuring adherence to SOLID principles and ease of future extensibility.
