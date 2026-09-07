# Design Document: Multilingual Code Generation Library

This document outlines the architecture and design decisions for building a multilingual code generation library using .NET technologies. The library leverages Tree-sitter's AST structure, serialized to YAML, and employs SOLID principles, including a dependency injection model for language-specific conversions.

## Implementation Status

**Status: Core Infrastructure Implemented, Extensions In Progress**

This library has successfully implemented the core infrastructure with working AST nodes, YAML serialization, and code generation for Python, C#, JavaScript and C++. The CLI and TUI applications are functional and demonstrate the library's capabilities.

## Objectives

* Allow users to programmatically generate code structures using an intermediate Tree-sitter-compatible representation.
* Serialize and deserialize representations to and from YAML.
* Enable easy extension to support multiple programming languages through a dependency-injection-based architecture.

## Technical Overview

### Intermediate Representation (IR)

The IR is structured based on Tree-sitter AST conventions, represented clearly and explicitly in YAML.

**Example YAML representation:**

```yaml
functionDeclaration:
  name: "calculate_sum"
  returnType: "int"
  parameters:
    - name: "a"
      type: "int"
    - name: "b" 
      type: "int"
    - name: "include_debug"
      type: "bool"
      isOptional: true
      defaultValue: "False"
  body:
    - returnStatement:
        leaf:
          value: "a + b"
          type: "String"
```

### Core Components

1. **AST Node Classes** ✅ **IMPLEMENTED**:

   * Define generic AST node classes (`AstNode`, `AstLeafNode`, `AstCompositeNode`) to represent hierarchical structures using C#.
   * Specific node types: `FunctionDeclaration`, `Parameter`, `ReturnStatement`
   * Deep cloning support and metadata attachments

2. **Serializer and Deserializer** ✅ **IMPLEMENTED**:

   * `YamlSerializer`: Converts AST node structures into YAML format using YamlDotNet.
   * `YamlDeserializer`: Parses YAML back into AST structures with proper type resolution.
   * Round-trip serialization/deserialization verified

3. **Language Conversion Interfaces** ✅ **IMPLEMENTED**:

   * Abstract interface (`ILanguageGenerator`) for converting AST nodes into specific languages.
   * Base implementation (`LanguageGeneratorBase`) provides common functionality.
   * Four generators, each with proper indentation and language-appropriate type handling:
     Python (type hints), C#, JavaScript (untyped, strict equality), and C++ (mapped type spellings).
   * Dependency injection configuration (`ServiceCollectionExtensions`) registers all four.

4. **Applications** ✅ **IMPLEMENTED**:

   * **CLI Application**: Command-line interface with demo functionality and code generation.
   * **TUI Application**: Interactive terminal user interface using Spectre.Console for creating and managing functions.

### Architectural Design (SOLID Principles)

#### Single Responsibility Principle (SRP) ✅

* Each class/module handles only one responsibility:

  * AST classes: Structure representation
  * YAML serialization: IR conversion
  * Language-specific modules: Code generation
  * CLI/TUI: User interface and interaction

#### Open/Closed Principle (OCP) 🔄

* Extensible language support without modifying core library code by injecting language converters at runtime.
* **STATUS**: Interface structure supports this, but DI configuration is missing.

#### Liskov Substitution Principle (LSP) ✅

* Language-specific classes adhere to the `ILanguageGenerator` interface and can substitute each other seamlessly.

#### Interface Segregation Principle (ISP) ✅

* Clearly separated interfaces ensure no component depends on methods it does not use.

#### Dependency Inversion Principle (DIP) 🔄

* High-level modules (core library) depend on abstract interfaces rather than concrete implementations of language-specific converters.
* **STATUS**: Partially implemented - interfaces exist but DI container setup is missing.

## Implementation Plan

### Project Structure (.NET Solution) ✅ **IMPLEMENTED**

```plaintext
Coder/
├── Coder.Core/
│   ├── Ast/
│   │   ├── AstNode.cs ✅
│   │   ├── AstLeafNode.cs ✅
│   │   ├── AstCompositeNode.cs ✅
│   │   ├── FunctionDeclaration.cs ✅
│   │   ├── Parameter.cs ✅
│   │   └── ReturnStatement.cs ✅
│   ├── Serialization/
│   │   ├── YamlSerializer.cs ✅
│   │   └── YamlDeserializer.cs ✅
│   ├── Languages/
│   │   ├── ILanguageGenerator.cs ✅
│   │   ├── LanguageGeneratorBase.cs ✅
│   │   ├── PythonGenerator.cs ✅
│   │   ├── CSharpGenerator.cs ✅
│   │   ├── JavaScriptGenerator.cs ✅
│   │   └── CppGenerator.cs ✅
│   └── ServiceCollectionExtensions.cs ✅
├── Coder.Test/
│   ├── Ast/
│   │   └── AstNodeTests.cs ✅
│   ├── Languages/
│   │   ├── JavaScriptGeneratorTests.cs ✅
│   │   └── CppGeneratorTests.cs ✅
│   ├── Serialization/
│   │   └── YamlSerializationTests.cs ✅
│   └── ServiceCollectionExtensionsTests.cs ✅
├── Coder.CLI/
│   └── SampleCLI.cs ✅
└── Coder.App/
    └── Program.cs ✅ (TUI with Spectre.Console)
```

### Core Interfaces and Classes

**Abstract Interface:** ✅ **IMPLEMENTED**

```csharp
public interface ILanguageGenerator
{
    string LanguageId { get; }
    string DisplayName { get; }
    string FileExtension { get; }
    string Generate(AstNode astNode);
    bool CanGenerate(AstNode astNode);
}
```

**Example Language Implementation:** ✅ **IMPLEMENTED**

```csharp
public class PythonGenerator : LanguageGeneratorBase
{
    public override string LanguageId => "python";
    public override string DisplayName => "Python";
    public override string FileExtension => "py";
    
    public override string Generate(AstNode astNode)
    {
        // Full implementation with proper indentation and type hints
    }
}
```

**Dependency Injection Configuration:** ✅ **IMPLEMENTED**

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLanguageGenerators(this IServiceCollection services)
    {
        services.AddSingleton<ILanguageGenerator, PythonGenerator>();
        services.AddSingleton<ILanguageGenerator, CSharpGenerator>();
        services.AddSingleton<ILanguageGenerator, JavaScriptGenerator>();
        services.AddSingleton<ILanguageGenerator, CppGenerator>();
        return services;
    }
}
```

A generator is reached by resolving `IEnumerable<ILanguageGenerator>` and selecting on `LanguageId`
(`python`, `csharp`, `javascript`, `cpp`), so adding a language is one registration and no caller
changes.

## Current Workflow Example ✅ **WORKING**

1. User creates AST structure programmatically (via CLI/TUI or code).
2. AST structure serialized to YAML for readability and diff-ability.
3. Language-specific generator processes AST to target language code.
4. Generated code can be displayed, saved, or further processed.

## Usage Example (C# Working Code)

```csharp
// This works in the current implementation
var function = new FunctionDeclaration("calculate_sum")
{
    ReturnType = "int"
};

function.Parameters.Add(new Parameter("a", "int"));
function.Parameters.Add(new Parameter("b", "int"));
function.Body.Add(new ReturnStatement(new AstLeafNode<string>("a + b")));

// Serialize to YAML
var serializer = new YamlSerializer();
string yamlStr = serializer.Serialize(function);

// Deserialize from YAML
var deserializer = new YamlDeserializer();
AstNode astLoaded = deserializer.Deserialize(yamlStr);

// Generate Python code
var pythonGenerator = new PythonGenerator();
string pythonCode = pythonGenerator.Generate(astLoaded);
```

## What's Missing for Full Implementation

### High Priority
1. **Comprehensive testing** for all existing functionality
2. **Additional AST node types** (control flow: conditionals and loops)
3. **Expression system** — function calls (binary operators are implemented)

### Medium Priority
4. **Further language generators** beyond the four that exist
5. **Error handling and validation** improvements
6. **Performance optimization** and benchmarking

### Low Priority
8. **Advanced documentation** and API reference
9. **Example projects** for each supported language
10. **Mutation testing and advanced QA**

## Conclusion

This .NET-based design has successfully implemented a robust, flexible foundation for AST-based code generation. The core infrastructure adheres to SOLID principles and provides working serialization and code generation for four target languages. The CLI and TUI applications demonstrate practical usage. 

The next development phase should focus on completing the dependency injection infrastructure and expanding the AST node types to support more complex code structures.
