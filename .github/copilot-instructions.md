# GitHub Copilot Instructions for ktsu.Coder

This repository contains a flexible and extensible .NET library for representing code as Abstract Syntax Trees (AST), serializing to YAML, and generating code in multiple programming languages.

## Technology Stack

- **.NET Version**: 9.0
- **Language**: C#
- **Testing Framework**: MSTest with FluentAssertions
- **Package Management**: Central Package Version Management (CPM)
- **CI/CD**: PowerShell-based PSBuild pipeline
- **Key Dependencies**: YamlDotNet, ktsu.DeepClone, Microsoft.Extensions.DependencyInjection

## Project Structure

```
Coder/
├── Coder.Core/          # Core library with AST, serialization, and language generators
│   ├── Ast/            # Abstract Syntax Tree node definitions
│   ├── Serialization/  # YAML serialization/deserialization
│   └── Languages/      # Language-specific code generators (ILanguageGenerator)
├── Coder.ConsoleApp/   # Interactive TUI application (Spectre.Console)
├── Coder.Test/         # Unit tests (MSTest)
└── scripts/            # Build and release automation scripts (PSBuild)
```

## Build and Test Commands

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Running the Console Application
```bash
dotnet run --project Coder.ConsoleApp
```

## Code Style Requirements

### EditorConfig Rules
- **Indentation**: Tabs for C# files (`.cs`, `.csx`, `.cake`)
- **Line Endings**: CRLF (`\r\n`)
- **Encoding**: UTF-8 with BOM
- **Final Newline**: Required
- **Trailing Whitespace**: Must be trimmed
- **.NET Code Style**: All analyzer diagnostics are treated as **errors**

### File Headers
All C# files must include the following header:
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.
```

### Naming and Style
- Use language keywords over framework type names (`int` not `Int32`)
- Always specify accessibility modifiers
- Prefer `readonly` fields where applicable
- No `this.` qualifier unless necessary
- Parentheses required for clarity in complex expressions

## Architecture Principles

This library follows **SOLID principles**:

1. **Single Responsibility**: Each class handles one concern (AST structure, serialization, or code generation)
2. **Open/Closed**: Language support is extensible through `ILanguageGenerator` interface without modifying core code
3. **Liskov Substitution**: All language generators are interchangeable through the interface
4. **Interface Segregation**: Focused interfaces with only required methods
5. **Dependency Inversion**: Core library depends on abstractions (`ILanguageGenerator`), not concrete implementations

### Key Design Patterns
- **Visitor Pattern**: Used for AST traversal and code generation
- **Dependency Injection**: Language generators should be registered via DI container
- **Deep Cloning**: All AST nodes support cloning via `IDeepCloneable`

## Package Management

This repository uses **Central Package Version Management**:
- Package versions are defined in `Directory.Packages.props`
- **Never** specify versions in `.csproj` `<PackageReference>` elements
- Use `<PackageReference Include="PackageName" />` without `Version` attribute
- Add new package versions to `Directory.Packages.props` first

## Testing Requirements

### Required Tests
- Add unit tests for **all** new functionality
- Use **MSTest** with `[TestClass]` and `[TestMethod]` attributes
- Use **FluentAssertions** for assertions (e.g., `result.Should().Be(expected)`)
- Test files should match pattern `*Tests.cs`
- Tests should cover:
  - Happy path scenarios
  - Edge cases and boundary conditions
  - Error handling and validation

### Test Structure Example
```csharp
[TestClass]
public class MyFeatureTests
{
    [TestMethod]
    public void MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        var sut = new MyClass();

        // Act
        var result = sut.Method();

        // Assert
        result.Should().Be(expected);
    }
}
```

## Working with AST Nodes

### Creating New AST Node Types
1. Inherit from `AstNode` or `AstCompositeNode`
2. Implement `IDeepCloneable<T>` for cloning support
3. Add YAML serialization support in `YamlSerializer` and `YamlDeserializer`
4. Update language generators to handle the new node type
5. Add comprehensive unit tests

### Adding New Language Generators
1. Implement `ILanguageGenerator` interface (or extend `LanguageGeneratorBase`)
2. Override `LanguageId`, `DisplayName`, and `FileExtension` properties
3. Implement code generation logic in `Generate(AstNode)` method
4. Handle indentation correctly for block-structured languages
5. Add unit tests with various AST structures

## CI/CD Workflow

The repository uses a PowerShell-based build system (`PSBuild`):
- **Build Script**: `scripts/PSBuild.psm1`
- **Workflow**: `.github/workflows/dotnet.yml`
- **Process**: Restore → Build → Test → Package → Release
- **Version Management**: Automated via `VERSION.md` and changelog
- **Release**: Automatic on `main` branch with changelog updates

### Important Notes
- Do not modify version numbers manually
- Update `CHANGELOG.md` for user-facing changes
- CI runs on Windows (not cross-platform compatible yet)
- SonarQube integration for code quality analysis

## Common Tasks

### Adding a New Feature
1. Create feature branch from `main` or `develop`
2. Implement core functionality in `Coder.Core`
3. Add comprehensive unit tests in `Coder.Test`
4. Update README.md if API changes
5. Update CHANGELOG.md with feature description
6. Ensure all tests pass and build succeeds

### Fixing a Bug
1. Add a failing test that reproduces the bug
2. Fix the bug with minimal code changes
3. Verify the test now passes
4. Add regression tests if needed
5. Update CHANGELOG.md if user-facing

### Updating Dependencies
1. Update version in `Directory.Packages.props`
2. Run `dotnet restore` to verify compatibility
3. Run full test suite to check for breaking changes
4. Update code if API changes are required

## What NOT to Do

- ❌ Don't modify working tests without good reason
- ❌ Don't add package versions to `.csproj` files (use `Directory.Packages.props`)
- ❌ Don't change line endings from CRLF to LF
- ❌ Don't use spaces for indentation in C# files (use tabs)
- ❌ Don't skip file headers on new C# files
- ❌ Don't bypass .editorconfig rules
- ❌ Don't commit without running tests
- ❌ Don't modify CI/CD scripts unless specifically required

## Documentation

- **Main README**: `/README.md` - Library overview and quick start
- **Design Document**: `/docs/design.md` - Architecture and design decisions
- **Implementation Plan**: `/docs/implementation-plan.md` - Development roadmap

## Additional Resources

- Repository follows ktsu.dev conventions and standards
- Uses `ktsu.Sdk.Lib` for common build configurations
- Leverages ktsu.dev packages for common functionality (DeepClone, StrongStrings, etc.)
