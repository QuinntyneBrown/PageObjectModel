# Playwright POM Generator

A command-line tool for automatically generating Page Object Model (POM) classes for Playwright browser automation projects.

## Overview

The Playwright POM Generator helps you create maintainable and scalable test automation code by automatically generating Page Object Model classes from your web applications. This tool analyzes your web pages and creates strongly-typed C# classes that encapsulate page elements and interactions.

## Features

- 🎯 Automatic generation of Page Object Model classes
- 🔍 Smart element detection and selector generation
- 📝 Strongly-typed C# code generation (.NET 10.0)
- ⚙️ Configurable via JSON configuration files
- 🛠️ Command-line interface for easy integration into CI/CD pipelines
- 📦 Modular architecture with Core and CLI separation

## Project Structure

```
PlaywrightPomGenerator/
├── src/
│   ├── PlaywrightPomGenerator.Core/    # Core generation logic
│   └── PlaywrightPomGenerator.Cli/     # Command-line interface
├── tests/
│   └── PlaywrightPomGenerator.Tests/   # Unit tests
├── playground/                          # Development playground
├── docs/                                # Documentation
└── artifacts/                           # Build outputs
```

## Prerequisites

- .NET 10.0 SDK or later
- Playwright installed in your target project

## Installation

### Build from Source

```bash
git clone <repository-url>
cd PageObjectModel
dotnet build PlaywrightPomGenerator.sln
```

### Run the CLI

```bash
cd src/PlaywrightPomGenerator.Cli
dotnet run -- [options]
```

## Usage

The CLI tool supports various commands for generating Page Object Model classes:

```bash
# Basic usage
dotnet run -- generate --url <page-url> --output <output-directory>

# With configuration file
dotnet run -- generate --config appsettings.json
```

### Configuration

Configure the generator using `appsettings.json`:

```json
{
  "Generator": {
    "OutputPath": "./PageObjects",
    "Namespace": "YourProject.PageObjects",
    "Options": {
      // Generator-specific options
    }
  }
}
```

Environment variables can be used with the `POMGEN_` prefix:

```bash
export POMGEN_Generator__OutputPath="./CustomPath"
```

## Development

### Building the Project

```bash
# Build solution
dotnet build

# Build specific configuration
dotnet build -c Release
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Project Dependencies

- Microsoft.Extensions.FileSystemGlobbing
- Microsoft.Extensions.Logging.Abstractions
- Microsoft.Extensions.Options
- System.CommandLine

## Architecture

The project follows a clean architecture pattern:

- **PlaywrightPomGenerator.Core**: Contains the core generation logic, models, and abstractions
- **PlaywrightPomGenerator.Cli**: Provides the command-line interface and user interaction
- **PlaywrightPomGenerator.Tests**: Contains unit tests for both projects

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch
3. Make your changes with appropriate tests
4. Ensure all tests pass
5. Submit a pull request

## License

[License information to be added]

## Support

For issues, questions, or contributions, please [open an issue](../../issues) on GitHub.
