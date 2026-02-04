# Contributing to Playwright POM Generator

Thank you for your interest in contributing to the Playwright POM Generator! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Submitting Changes](#submitting-changes)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)
- [Documentation](#documentation)
- [Release Process](#release-process)

## Code of Conduct

### Our Pledge

We are committed to providing a welcoming and inclusive environment for all contributors. Please:

- **Be respectful** - Treat everyone with respect and consideration
- **Be collaborative** - Work together constructively
- **Be patient** - Help others learn and grow
- **Be professional** - Keep discussions focused and productive

### Unacceptable Behavior

- Harassment, discrimination, or offensive comments
- Personal attacks or trolling
- Publishing private information
- Any conduct that could be considered inappropriate in a professional setting

## Getting Started

### Prerequisites

Before contributing, ensure you have:

- **.NET 10.0 SDK** or later installed
- **Git** for version control
- **Visual Studio 2022**, **VS Code**, or **JetBrains Rider** (recommended)
- **Node.js** and **npm** (for testing with Angular projects)
- Basic knowledge of **C#**, **TypeScript**, and **Playwright**

### First-Time Contributors

If you're new to the project:

1. **Read the documentation**
   - [README.md](README.md) - Project overview
   - [User Guides](docs/user-guides/) - Comprehensive usage documentation

2. **Explore the codebase**
   - Browse the source code in `src/`
   - Check out existing tests in `tests/`
   - Look at the playground examples in `playground/`

3. **Look for beginner-friendly issues**
   - Search for issues labeled `good first issue` or `help wanted`
   - Comment on an issue to express interest before starting work

4. **Join the discussion**
   - Read existing issues and pull requests
   - Ask questions if anything is unclear

## Development Setup

### 1. Fork and Clone

```bash
# Fork the repository on GitHub, then clone your fork
git clone https://github.com/YOUR-USERNAME/PageObjectModel.git
cd PageObjectModel

# Add upstream remote
git remote add upstream https://github.com/ORIGINAL-OWNER/PageObjectModel.git
```

### 2. Restore Dependencies

```bash
# Restore NuGet packages
dotnet restore PlaywrightPomGenerator.sln

# Verify the build
dotnet build PlaywrightPomGenerator.sln
```

### 3. Run Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test tests/PlaywrightPomGenerator.Tests/PlaywrightPomGenerator.Tests.csproj
```

### 4. Run the CLI Locally

```bash
# Run from source
cd src/PlaywrightPomGenerator.Cli
dotnet run -- --help

# Run a command
dotnet run -- app /path/to/angular-app

# Watch mode (auto-rebuild on changes)
dotnet watch run -- app /path/to/angular-app
```

### 5. IDE Setup

#### Visual Studio 2022
1. Open `PlaywrightPomGenerator.sln`
2. Set `PlaywrightPomGenerator.Cli` as startup project
3. Configure launch arguments in project properties

#### VS Code
1. Open the project folder
2. Install C# extension (ms-dotnettools.csharp)
3. Use provided `.vscode/launch.json` and `.vscode/tasks.json`

#### JetBrains Rider
1. Open `PlaywrightPomGenerator.sln`
2. Configure run configurations for CLI project

## Project Structure

```
PlaywrightPomGenerator/
├── src/
│   ├── PlaywrightPomGenerator.Core/      # Core generation logic
│   │   ├── Abstractions/                 # Interfaces
│   │   ├── Analyzers/                    # Angular analysis
│   │   ├── Models/                       # Data models
│   │   ├── Services/                     # Core services
│   │   └── Templates/                    # Code templates
│   │
│   └── PlaywrightPomGenerator.Cli/       # Command-line interface
│       ├── Commands/                     # CLI commands
│       └── Program.cs                    # Entry point
│
├── tests/
│   └── PlaywrightPomGenerator.Tests/     # Unit tests
│       ├── Analyzers/                    # Analyzer tests
│       ├── Services/                     # Service tests
│       └── Commands/                     # Command tests
│
├── playground/                            # Development testing
│   └── PlaygroundRunner/                 # Test runner
│
├── docs/                                  # Documentation
│   └── user-guides/                      # User documentation
│
└── artifacts/                             # Build outputs
```

### Key Components

- **Core** - Contains all business logic, analysis, and generation code
- **CLI** - Command-line interface using System.CommandLine
- **Tests** - Unit tests using xUnit
- **Playground** - Manual testing and experimentation

## Development Workflow

### 1. Create a Branch

```bash
# Update your fork
git checkout main
git pull upstream main

# Create a feature branch
git checkout -b feature/your-feature-name

# Or for bug fixes
git checkout -b fix/issue-number-description
```

### Branch Naming Conventions

- `feature/` - New features (e.g., `feature/add-vue-support`)
- `fix/` - Bug fixes (e.g., `fix/123-selector-generation`)
- `refactor/` - Code refactoring (e.g., `refactor/analyzer-cleanup`)
- `docs/` - Documentation updates (e.g., `docs/update-readme`)
- `test/` - Test additions/improvements (e.g., `test/add-analyzer-tests`)

### 2. Make Changes

```bash
# Make your changes
# Write tests for your changes
# Update documentation if needed

# Build and test frequently
dotnet build
dotnet test
```

### 3. Commit Changes

```bash
# Stage your changes
git add .

# Commit with a descriptive message
git commit -m "feat: add support for Vue component analysis"
```

#### Commit Message Format

Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation changes
- `refactor` - Code refactoring
- `test` - Test additions or modifications
- `chore` - Maintenance tasks
- `perf` - Performance improvements
- `style` - Code style changes (formatting, etc.)

**Examples:**
```bash
feat(analyzer): add support for standalone components

fix(generator): correct selector generation for nested components

docs(user-guides): add troubleshooting section for workspace command

test(analyzer): add tests for component detection

refactor(services): simplify file system abstraction

chore(deps): update Microsoft.Extensions.Logging to 10.0.3
```

### 4. Push and Create Pull Request

```bash
# Push to your fork
git push origin feature/your-feature-name

# Create a pull request on GitHub
```

## Coding Standards

### C# Coding Conventions

Follow the [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and these project-specific guidelines:

#### Naming

```csharp
// Classes: PascalCase
public class AngularAnalyzer { }

// Interfaces: IPascalCase
public interface ICodeGenerator { }

// Methods: PascalCase
public async Task<AnalysisResult> AnalyzeAsync() { }

// Private fields: _camelCase
private readonly ILogger _logger;

// Properties: PascalCase
public string OutputPath { get; set; }

// Local variables: camelCase
var projectPath = "./src/app";

// Constants: PascalCase
public const string DefaultTimeout = "30000";
```

#### Code Style

```csharp
// Use nullable reference types
#nullable enable

// Use var for obvious types
var service = new AngularAnalyzer();

// Be explicit for non-obvious types
ICodeGenerator generator = GetGenerator();

// Use expression-bodied members for simple cases
public string Name => _name;

// Use block body for complex logic
public async Task<Result> ProcessAsync()
{
    // Complex logic here
}

// Prefer primary constructors (C# 12+) when appropriate
public class MyService(ILogger logger, IFileSystem fileSystem)
{
    private readonly ILogger _logger = logger;
}

// Use file-scoped namespaces
namespace PlaywrightPomGenerator.Core.Services;

// One class per file
// File name matches class name
```

#### Documentation

```csharp
/// <summary>
/// Analyzes an Angular application and extracts component information.
/// </summary>
/// <param name="path">The path to the Angular application.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Analysis result containing component information.</returns>
/// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
/// <exception cref="InvalidOperationException">Thrown when analysis fails.</exception>
public async Task<AnalysisResult> AnalyzeApplicationAsync(
    string path,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(path);
    // Implementation
}
```

### Code Organization

```csharp
// Order: usings, namespace, class, fields, constructors, properties, methods
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PlaywrightPomGenerator.Core.Services;

public class MyService : IMyService
{
    // Fields
    private readonly ILogger<MyService> _logger;
    private readonly IFileSystem _fileSystem;
    
    // Constructor
    public MyService(ILogger<MyService> logger, IFileSystem fileSystem)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }
    
    // Properties
    public string OutputPath { get; set; }
    
    // Public methods
    public async Task<Result> ProcessAsync()
    {
        // Implementation
    }
    
    // Private methods
    private void ValidateInput()
    {
        // Implementation
    }
}
```

### Error Handling

```csharp
// Use ArgumentNullException.ThrowIfNull for null checks
public void Process(string input)
{
    ArgumentNullException.ThrowIfNull(input);
}

// Provide meaningful error messages
if (!_fileSystem.DirectoryExists(path))
{
    throw new InvalidOperationException(
        $"Directory not found: {path}. Please ensure the path exists and is accessible.");
}

// Log errors with context
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to analyze application at {Path}", path);
    throw;
}

// Use specific exceptions
throw new FileNotFoundException($"Component file not found: {componentPath}");
```

### Async/Await

```csharp
// Always use ConfigureAwait(false) in libraries
var result = await ProcessAsync().ConfigureAwait(false);

// Use CancellationToken
public async Task ProcessAsync(CancellationToken cancellationToken = default)
{
    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
}

// Avoid async void (except event handlers)
public async Task HandleAsync() { }  // Good
public async void Handle() { }       // Bad (except for events)
```

## Testing Guidelines

### Unit Tests

Every new feature or bug fix should include tests.

#### Test Structure

```csharp
using Xunit;
using FluentAssertions;

namespace PlaywrightPomGenerator.Tests.Services;

public class AngularAnalyzerTests
{
    [Fact]
    public async Task AnalyzeApplicationAsync_WithValidPath_ReturnsResult()
    {
        // Arrange
        var analyzer = new AngularAnalyzer(/* dependencies */);
        var path = "./test/app";
        
        // Act
        var result = await analyzer.AnalyzeApplicationAsync(path);
        
        // Assert
        result.Should().NotBeNull();
        result.Components.Should().NotBeEmpty();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnalyzeApplicationAsync_WithInvalidPath_ThrowsArgumentException(
        string invalidPath)
    {
        // Arrange
        var analyzer = new AngularAnalyzer(/* dependencies */);
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => analyzer.AnalyzeApplicationAsync(invalidPath));
    }
}
```

#### Test Naming

```csharp
// Pattern: MethodName_Scenario_ExpectedBehavior
[Fact]
public void GenerateCode_WhenComponentIsValid_ReturnsGeneratedCode() { }

[Fact]
public void GenerateCode_WhenComponentIsNull_ThrowsArgumentNullException() { }

[Fact]
public void GenerateCode_WithCustomTemplate_UsesProvidedTemplate() { }
```

#### Test Categories

```csharp
// Use traits to categorize tests
[Trait("Category", "Unit")]
public class UnitTests { }

[Trait("Category", "Integration")]
public class IntegrationTests { }

// Run specific categories
// dotnet test --filter "Category=Unit"
```

### Test Coverage

- **Aim for 80%+ code coverage** for new code
- **Test edge cases** and error conditions
- **Test public APIs** thoroughly
- Private methods are tested indirectly through public APIs

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test
dotnet test --filter "FullyQualifiedName~AnalyzeApplicationAsync"

# Run by category
dotnet test --filter "Category=Unit"
```

## Submitting Changes

### Before Submitting

- [ ] Code builds without errors: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] Code follows style guidelines
- [ ] XML documentation comments added for public APIs
- [ ] Tests added for new functionality
- [ ] Documentation updated (if applicable)
- [ ] Commit messages follow conventions
- [ ] Branch is up to date with main

### Checklist

```bash
# Update your branch
git checkout main
git pull upstream main
git checkout your-branch
git rebase main

# Build and test
dotnet build --configuration Release
dotnet test

# Format code (if formatter is configured)
dotnet format

# Review your changes
git diff upstream/main
```

## Pull Request Process

### 1. Create Pull Request

- **Title**: Clear, concise description (e.g., "feat: add Vue component support")
- **Description**: Explain what, why, and how
- **Reference issues**: Use "Fixes #123" or "Closes #456"
- **Screenshots**: Include if UI changes are involved

### Pull Request Template

```markdown
## Description
Brief description of the changes.

## Motivation and Context
Why is this change required? What problem does it solve?

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## How Has This Been Tested?
Describe the tests you ran to verify your changes.

## Checklist
- [ ] My code follows the style guidelines of this project
- [ ] I have performed a self-review of my own code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes

## Related Issues
Fixes #(issue number)
```

### 2. Code Review

- Address review comments promptly
- Be open to feedback and suggestions
- Ask questions if feedback is unclear
- Update your PR based on feedback

### 3. Merging

- **Squash commits** may be required for cleaner history
- **Rebase** on main if requested
- Maintainers will merge when approved

## Issue Reporting

### Bug Reports

Use the bug report template:

```markdown
## Bug Description
Clear and concise description of the bug.

## Steps to Reproduce
1. Run command '...'
2. With arguments '...'
3. See error

## Expected Behavior
What you expected to happen.

## Actual Behavior
What actually happened.

## Environment
- OS: [e.g., Windows 11, Ubuntu 22.04]
- .NET Version: [e.g., 10.0.100]
- Tool Version: [e.g., 1.0.0]
- Project Type: [e.g., Angular 17 workspace]

## Additional Context
Any other relevant information, logs, or screenshots.
```

### Feature Requests

Use the feature request template:

```markdown
## Feature Description
Clear and concise description of the feature.

## Use Case
Describe the problem this feature would solve.

## Proposed Solution
Describe how you envision the feature working.

## Alternatives Considered
Any alternative solutions or features you've considered.

## Additional Context
Any other context, mockups, or examples.
```

## Documentation

### User Documentation

Located in `docs/user-guides/`:
- Update relevant guides when changing functionality
- Add examples for new features
- Keep troubleshooting guide current

### Code Documentation

- **XML comments** for all public APIs
- **Inline comments** for complex logic
- **README files** in major directories

### Documentation Standards

```csharp
/// <summary>
/// One-line summary of what this does.
/// </summary>
/// <remarks>
/// Additional details, usage notes, or examples.
/// </remarks>
/// <param name="paramName">Description of parameter.</param>
/// <returns>Description of return value.</returns>
/// <exception cref="ExceptionType">When this exception is thrown.</exception>
/// <example>
/// <code>
/// var result = await service.ProcessAsync("input");
/// </code>
/// </example>
```

## Release Process

### Versioning

We follow [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible API changes
- **MINOR** version for backwards-compatible functionality
- **PATCH** version for backwards-compatible bug fixes

### Release Checklist

1. Update version in project files
2. Update CHANGELOG.md
3. Update documentation
4. Run full test suite
5. Create release tag
6. Publish release notes

## Questions?

- **General questions**: Open a discussion on GitHub
- **Bug reports**: Create an issue
- **Feature requests**: Create an issue
- **Security issues**: Email [security contact] privately

## Thank You!

Your contributions make this project better for everyone. We appreciate your time and effort! 🎉

---

**Happy Coding!** 🚀
