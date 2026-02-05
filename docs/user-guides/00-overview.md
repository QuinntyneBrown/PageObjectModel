# Playwright POM Generator - User Guide Overview

Welcome to the comprehensive user guide for the Playwright POM Generator CLI tool. This documentation will help you understand and use all features of the tool effectively.

## Table of Contents

1. [Overview](#overview) (this document)
2. [Getting Started](01-getting-started.md)
3. [Generate App Command](02-generate-app.md)
4. [Generate Workspace Command](03-generate-workspace.md)
5. [Generate Artifacts Command](04-generate-artifacts.md)
6. [Generate SignalR Mock Command](05-generate-signalr-mock.md)
7. [Configuration Guide](06-configuration.md)
8. [Global Options](07-global-options.md)
9. [Output Structure](08-output-structure.md)
10. [Troubleshooting](09-troubleshooting.md)
11. [Best Practices](10-best-practices.md)

## What is Playwright POM Generator?

The Playwright POM Generator is a command-line tool that automatically generates Page Object Model (POM) test files for Angular applications using Playwright. It analyzes your Angular workspace or application and creates:

- **Page Object classes** - TypeScript classes representing your Angular components
- **Test fixtures** - Reusable test setup and teardown logic
- **Playwright configuration** - Pre-configured playwright.config.ts files
- **Selector files** - Centralized element selectors for maintainability
- **Helper utilities** - Common testing utilities and helper functions
- **SignalR mocks** - RxJS-based SignalR mock implementations

## Key Features

### 🎯 Automatic Generation
Analyzes Angular components and generates corresponding Playwright test structures automatically.

### 🏗️ Workspace Support
Works with both single Angular applications and multi-project Angular workspaces.

### ⚙️ Highly Configurable
Configure output paths, file headers, test file suffixes, timeouts, and more through:
- JSON configuration files (`appsettings.json`)
- Environment variables (with `POMGEN_` prefix)
- Command-line options

### 🔌 Modular Architecture
Generate only what you need - fixtures, configs, selectors, page objects, or helpers individually or all at once.

### 📦 Production Ready
Built with .NET 10.0, featuring:
- Dependency injection
- Structured logging
- Error handling
- Validation

## Command Overview

The CLI provides four main commands:

### `app`
Generate Playwright POM tests for a **single Angular application**.
```bash
playwright-pom-gen app ./src/my-app --output ./e2e
```

### `workspace`
Generate Playwright POM tests for an **entire Angular workspace** (or specific projects within it).
```bash
playwright-pom-gen workspace ./my-workspace --project my-app
```

### `artifacts`
Generate **specific artifacts** (fixtures, configs, selectors, page objects, helpers) without full generation.
```bash
playwright-pom-gen artifacts ./my-app --fixtures --configs
```

### `signalr-mock`
Generate a **fully functional SignalR mock** fixture using RxJS (not promises).
```bash
playwright-pom-gen signalr-mock ./fixtures
```

## Global Options

Available across all commands:

- `--header` - Custom file header template with placeholders
- `--test-suffix` - Test file suffix (default: "spec")
- `--debug` - Include HTML template as comments in generated page objects (for debugging)

## Target Audience

This tool is designed for:

- **QA Engineers** writing end-to-end tests for Angular applications
- **Developers** implementing test automation in their CI/CD pipelines
- **Teams** standardizing their Playwright test structure across projects
- **Architects** establishing testing patterns for large Angular workspaces

## Prerequisites

Before using this tool, ensure you have:

1. **.NET 10.0 SDK** or later installed
2. An **Angular workspace or application** (with `angular.json` or project structure)
3. **Playwright** installed in your target project (or ready to install)
4. Basic knowledge of **TypeScript** and **Playwright**

## Quick Start Example

```bash
# Navigate to your Angular workspace
cd /path/to/angular-workspace

# Generate tests for all applications in workspace
playwright-pom-gen workspace . --output ./e2e-tests

# Or generate for a specific app
playwright-pom-gen app ./projects/my-app --output ./tests

# Or generate only specific artifacts
playwright-pom-gen artifacts ./projects/my-app --fixtures --configs
```

## Exit Codes

The CLI returns standard exit codes:

- **0** - Success
- **1** - Error occurred (validation failure, generation failure, exception)

## Next Steps

1. Read [Getting Started](01-getting-started.md) to install and run your first generation
2. Choose the appropriate command guide based on your project structure:
   - Single app: [Generate App Command](02-generate-app.md)
   - Workspace: [Generate Workspace Command](03-generate-workspace.md)
   - Specific artifacts: [Generate Artifacts Command](04-generate-artifacts.md)
3. Learn about [Configuration](06-configuration.md) to customize the output
4. Review [Best Practices](10-best-practices.md) for optimal usage

## Getting Help

Within the CLI, you can always get help:

```bash
# General help
playwright-pom-gen --help

# Command-specific help
playwright-pom-gen app --help
playwright-pom-gen workspace --help
playwright-pom-gen artifacts --help
playwright-pom-gen signalr-mock --help
```

## Feedback and Support

If you encounter issues or have suggestions, please refer to the [Troubleshooting](09-troubleshooting.md) guide or open an issue on the project repository.
