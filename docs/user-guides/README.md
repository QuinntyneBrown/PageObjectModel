# Playwright POM Generator - User Guides

Comprehensive documentation for the Playwright Page Object Model Generator CLI tool.

## 📚 Guide Index

### Getting Started
- **[00. Overview](00-overview.md)** - Introduction, features, and quick start
- **[01. Getting Started](01-getting-started.md)** - Installation, first run, and basic setup

### Command Guides
- **[02. Generate App Command](02-generate-app.md)** - Generate tests for single Angular applications
- **[03. Generate Workspace Command](03-generate-workspace.md)** - Generate tests for Angular workspaces
- **[04. Generate Artifacts Command](04-generate-artifacts.md)** - Generate specific artifacts selectively
- **[05. Generate SignalR Mock Command](05-generate-signalr-mock.md)** - Generate RxJS-based SignalR mocks

### Configuration & Options
- **[06. Configuration Guide](06-configuration.md)** - Configure via files, environment variables, and CLI options
- **[07. Global Options](07-global-options.md)** - Options available across all commands

### Reference
- **[08. Output Structure](08-output-structure.md)** - Understanding generated files and structure
- **[09. Troubleshooting](09-troubleshooting.md)** - Common issues and solutions
- **[10. Best Practices](10-best-practices.md)** - Recommended patterns and workflows

## 🚀 Quick Navigation

### I want to...

#### ...get started quickly
1. [Install and verify](01-getting-started.md#installation)
2. [Run first generation](01-getting-started.md#first-run)
3. [Understand the output](08-output-structure.md)

#### ...generate tests for my project
- **Single app:** [Generate App Command](02-generate-app.md)
- **Workspace:** [Generate Workspace Command](03-generate-workspace.md)
- **Specific artifacts only:** [Generate Artifacts Command](04-generate-artifacts.md)

#### ...configure the tool
- [Configuration file setup](06-configuration.md#configuration-files)
- [Environment variables](06-configuration.md#environment-variables)
- [Command-line options](07-global-options.md)

#### ...solve a problem
- [Troubleshooting guide](09-troubleshooting.md)
- [Common issues by topic](09-troubleshooting.md#quick-reference)

#### ...follow best practices
- [Project organization](10-best-practices.md#project-organization)
- [CI/CD integration](10-best-practices.md#cicd-integration)
- [Team collaboration](10-best-practices.md#team-collaboration)

## 📖 Reading Guide

### For First-Time Users
1. Start with [Overview](00-overview.md) to understand what the tool does
2. Follow [Getting Started](01-getting-started.md) to install and run your first generation
3. Read the command guide for your project type:
   - [Generate App](02-generate-app.md) for single applications
   - [Generate Workspace](03-generate-workspace.md) for workspaces
4. Review [Output Structure](08-output-structure.md) to understand generated files

### For Regular Users
1. Reference command guides for specific tasks
2. Use [Configuration Guide](06-configuration.md) to customize output
3. Check [Troubleshooting](09-troubleshooting.md) when issues arise
4. Follow [Best Practices](10-best-practices.md) for optimal workflows

### For Teams/Organizations
1. Read [Best Practices](10-best-practices.md) for team workflows
2. Set up [Configuration](06-configuration.md) standards
3. Establish [CI/CD integration](10-best-practices.md#cicd-integration)
4. Document conventions in your repository

## 🎯 Common Scenarios

### Scenario 1: New Project Setup
```bash
# Step 1: Generate everything
playwright-pom-gen workspace .

# Step 2: Review output
ls -R e2e/

# Step 3: Install dependencies
npm install @playwright/test

# Step 4: Run tests
npx playwright test
```
**Guide:** [Getting Started](01-getting-started.md)

### Scenario 2: Component Updates
```bash
# Regenerate page objects after component changes
playwright-pom-gen artifacts . --page-objects --selectors
```
**Guide:** [Generate Artifacts](04-generate-artifacts.md)

### Scenario 3: Custom Configuration
```json
{
  "Generator": {
    "FileHeader": "// Copyright 2026 ACME Corp",
    "TestFileSuffix": "test",
    "DefaultTimeout": 45000
  }
}
```
**Guide:** [Configuration](06-configuration.md)

### Scenario 4: CI/CD Integration
```yaml
- name: Generate Playwright tests
  run: playwright-pom-gen workspace .
  env:
    POMGEN_Generator__BaseUrlPlaceholder: ${{ secrets.STAGING_URL }}
```
**Guide:** [Best Practices - CI/CD](10-best-practices.md#cicd-integration)

## 💡 Tips

- Use `--help` with any command for quick reference
- Use `--debug` to include HTML templates in generated files for debugging
- Enable debug logging when troubleshooting
- Commit configuration files to version control
- Extend generated files, don't modify them
- Regenerate regularly when components change

## 🔗 Additional Resources

- [Main README](../../README.md) - Project overview
- [API Documentation](../api/) - If available
- [Examples](../../playground/) - Sample projects and usage

## 📝 Documentation Conventions

Throughout these guides:
- **Code blocks** show exact commands and code
- **Examples** demonstrate real-world usage
- **Solutions** provide step-by-step fixes
- **Best practices** offer recommended approaches

### Syntax Conventions

```bash
# Comments explain what commands do
playwright-pom-gen app <path>  # <path> means required argument
playwright-pom-gen app . [--output <dir>]  # [--output] means optional
```

### Platform Notes
- Commands shown for Linux/macOS (bash)
- Windows equivalents noted where different
- Use `\` (backslash) for Windows paths

## 🆘 Getting Help

1. **Search these guides** - Use browser search (Ctrl+F / Cmd+F)
2. **Check troubleshooting** - [Troubleshooting Guide](09-troubleshooting.md)
3. **Run with --help** - `playwright-pom-gen <command> --help`
4. **Enable debug logging** - See [Configuration](06-configuration.md#logging-options)
5. **Check GitHub issues** - Search existing issues
6. **Ask the community** - Open a new issue with details

## 🔄 Keeping Up to Date

These guides are for **version 1.0.0** of the Playwright POM Generator.

When updating the tool:
- Review [CHANGELOG](../../CHANGELOG.md) for changes
- Update your configuration if needed
- Regenerate tests if breaking changes exist
- Review new features in updated guides

## 📄 License

This documentation is part of the Playwright POM Generator project.
See [LICENSE](../../LICENSE) for details.

---

**Last Updated:** 2026-02-04

**Questions or feedback?** Open an issue on GitHub.
