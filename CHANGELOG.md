# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2026-02-05

### Added
- **Dynamic content detection** - Automatically detects and creates selectors for elements with:
  - Angular interpolation (`{{ property }}`) - works with any HTML tag
  - Content projection (`<ng-content></ng-content>`, `<ng-content/>`, `<ng-content select="...">`)
- **Debug mode** (`--debug` flag) - Includes HTML template content as comments in generated page object files for debugging
- **Text validation methods** for elements with dynamic content:
  - `expect{Element}Visible()` - Visibility assertion
  - `expect{Element}HasText(expected)` - Exact text assertion
  - `expect{Element}ContainsText(expected)` - Partial text assertion
  - `get{Element}Text()` - Text content retrieval
- New property name mappings for semantic HTML elements (section, article, aside, header, footer, nav, etc.)

### Changed
- Improved element detection to work with any HTML tag containing dynamic content
- Enhanced template parsing to handle ng-content with attributes (`select`, etc.)
- Updated documentation with new features

## [1.3.0] - 2026-02-04

### Added
- Comprehensive user documentation with 11 detailed guides in `docs/user-guides/`
  - Overview and getting started guide
  - Complete command reference for all 4 commands (app, workspace, artifacts, signalr-mock)
  - Configuration guide covering files, environment variables, and CLI options
  - Global options documentation
  - Output structure reference
  - Troubleshooting guide with common issues and solutions
  - Best practices guide for CI/CD integration and team workflows
- CONTRIBUTING.md with complete contributor guidelines
  - Development setup instructions
  - Coding standards and conventions
  - Testing guidelines
  - Pull request process
  - Issue reporting templates
- Enhanced README.md with:
  - Professional badges and visual improvements
  - Comprehensive examples including CI/CD integration
  - Detailed command documentation
  - Complete configuration reference
  - Links to all documentation

### Changed
- Updated README.md with more detailed examples and better structure
- Improved project documentation organization

## [1.2.0] - Previous Release

### Added
- SignalR mock generation command
- RxJS-based SignalR mock fixtures
- Workspace support for multi-project Angular applications
- Artifacts command for selective generation

### Features
- Generate Page Object Model tests for Angular applications
- Support for single applications and workspaces
- Configurable output via appsettings.json and environment variables
- Custom file headers with placeholder support
- Customizable test file suffixes
- Comprehensive logging

---

[1.4.0]: https://github.com/anthropics/PlaywrightPomGenerator/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/anthropics/PlaywrightPomGenerator/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/anthropics/PlaywrightPomGenerator/releases/tag/v1.2.0
