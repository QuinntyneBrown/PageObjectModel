# Playwright POM Generator

[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com)

A powerful .NET CLI tool that automatically generates Playwright Page Object Model (POM) tests for Angular applications. Transform your Angular components into maintainable, type-safe Playwright test infrastructure with a single command.

## ✨ Features

- 🎯 **Automatic Angular Analysis** - Intelligently scans Angular workspaces and applications to detect components, templates, and routing
- 📄 **Page Object Generation** - Creates type-safe TypeScript page objects with properly typed locators and methods
- 🧪 **Test Scaffolding** - Generates complete Playwright test specs with fixture integration and best practices
- 🔌 **SignalR Mock Support** - Generates production-ready RxJS-based SignalR mock fixtures for real-time application testing
- 🏗️ **Workspace Support** - Handle complex Angular workspaces with multiple applications seamlessly
- ⚙️ **Highly Configurable** - Customize output via configuration files, environment variables, or command-line options
- 🔧 **Granular Control** - Generate specific artifacts (fixtures, configs, selectors, page objects, helpers) independently
- 🛠️ **CI/CD Ready** - Perfect for integration into automated pipelines with comprehensive logging
- 📚 **Enterprise Ready** - Supports custom file headers, naming conventions, and timeout configurations
- 🎨 **Best Practices** - Generated code follows Playwright and TypeScript best practices out of the box

## 📋 Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Commands](#commands)
- [Generated Output](#generated-output)
- [Configuration](#configuration)
- [Examples](#examples)
- [Documentation](#documentation)
- [Requirements](#requirements)
- [Building from Source](#building-from-source)
- [Contributing](#contributing)
- [License](#license)

## 🚀 Installation

### Global Tool Installation (Recommended)

Install as a global .NET tool:

```bash
dotnet tool install -g PlaywrightPomGenerator
```

### Update Existing Installation

```bash
dotnet tool update -g PlaywrightPomGenerator
```

### Local Installation

Install for a specific project:

```bash
dotnet tool install PlaywrightPomGenerator --tool-path ./tools
```

### Build from Source

See [Building from Source](#building-from-source) section below.

## ⚡ Quick Start

### 1. Install the Tool

```bash
dotnet tool install -g PlaywrightPomGenerator
```

### 2. Generate Tests for Your Angular App

```bash
# For a single application
playwright-pom-gen app ./src/my-app --output ./e2e

# For an Angular workspace
playwright-pom-gen workspace . --output ./e2e-tests
```

### 3. Install Playwright and Run Tests

```bash
npm install @playwright/test
npx playwright install
npx playwright test
```

That's it! You now have a complete Playwright test infrastructure.

## 📖 Commands

### `app` - Generate for Single Application

Generate Playwright POM tests for a single Angular application.

```bash
playwright-pom-gen app <path> [options]

# Examples
playwright-pom-gen app ./src/my-app
playwright-pom-gen app ./src/my-app --output ./e2e-tests
playwright-pom-gen app . --test-suffix "test"
```

**Options:**
- `<path>` - Path to Angular application (required)
- `-o, --output <dir>` - Output directory (default: `<path>/e2e`)

[📘 Full Documentation](docs/user-guides/02-generate-app.md)

### `workspace` - Generate for Angular Workspace

Generate Playwright POM tests for an Angular workspace with multiple projects.

```bash
playwright-pom-gen workspace <path> [options]

# Examples
playwright-pom-gen workspace .
playwright-pom-gen workspace . --project my-app
playwright-pom-gen workspace . --output ./tests
```

**Options:**
- `<path>` - Path to Angular workspace (required)
- `-o, --output <dir>` - Output directory (default: workspace root)
- `-p, --project <name>` - Generate for specific project only

[📘 Full Documentation](docs/user-guides/03-generate-workspace.md)

### `artifacts` - Generate Specific Artifacts

Generate only specific artifacts (fixtures, configs, selectors, page objects, helpers).

```bash
playwright-pom-gen artifacts <path> [options]

# Examples
playwright-pom-gen artifacts . --all
playwright-pom-gen artifacts . --fixtures --configs
playwright-pom-gen artifacts . --page-objects --selectors
playwright-pom-gen artifacts . --project my-app --fixtures
```

**Options:**
- `<path>` - Path to Angular workspace or application (required)
- `-f, --fixtures` - Generate test fixtures
- `-c, --configs` - Generate Playwright configuration
- `-s, --selectors` - Generate selector files
- `--page-objects` - Generate page object files
- `--helpers` - Generate helper utilities
- `-a, --all` - Generate all artifacts
- `-o, --output <dir>` - Output directory
- `-p, --project <name>` - Specific project (for workspaces)

[📘 Full Documentation](docs/user-guides/04-generate-artifacts.md)

### `signalr-mock` - Generate SignalR Mock

Generate a fully functional RxJS-based SignalR mock fixture for testing real-time applications.

```bash
playwright-pom-gen signalr-mock <output>

# Examples
playwright-pom-gen signalr-mock ./fixtures
playwright-pom-gen signalr-mock ./e2e/mocks
```

**Features of the generated mock:**
- ✅ RxJS Observable streams (not promises)
- ✅ Connection state management
- ✅ Method invocation tracking
- ✅ Server message simulation
- ✅ Error and reconnection simulation

[📘 Full Documentation](docs/user-guides/05-generate-signalr-mock.md)

### Global Options

Available across all commands:

```bash
# Custom file header with placeholders
--header "// Copyright 2026\n// File: {FileName}\n// Generated: {GeneratedDate}"

# Custom test file suffix (default: "spec")
--test-suffix "test"  # Creates *.test.ts instead of *.spec.ts

# Examples
playwright-pom-gen app . --header "// Auto-generated" --test-suffix "e2e"
playwright-pom-gen workspace . --header "/**\n * Generated: {GeneratedDate}\n */"
```

**Placeholders:**
- `{FileName}` - Name of the generated file
- `{GeneratedDate}` - ISO 8601 timestamp
- `{ToolVersion}` - Tool version

[📘 Full Documentation](docs/user-guides/07-global-options.md)

## 📂 Generated Output

The tool generates a complete, production-ready Playwright test structure:

```
e2e/
├── page-objects/              # Page Object Model classes
│   ├── home.page.ts
│   ├── login.page.ts
│   ├── dashboard.page.ts
│   └── ...
│
├── fixtures/                  # Test fixtures
│   ├── base.fixture.ts       # Base fixture with page object setup
│   └── auth.fixture.ts       # Authentication fixture (if detected)
│
├── selectors/                 # Centralized element selectors
│   ├── app.selectors.ts      # Application-wide selectors
│   ├── home.selectors.ts     # Per-component selectors
│   └── ...
│
├── helpers/                   # Utility functions
│   ├── test.helpers.ts       # Common test utilities
│   └── assertions.ts         # Custom assertions
│
├── tests/                     # Sample test files
│   ├── home.spec.ts          # Demonstrates page object usage
│   ├── login.spec.ts
│   └── ...
│
└── playwright.config.ts       # Playwright configuration
```

### Output Features

- ✅ **Type-safe** - Full TypeScript support with proper typing
- ✅ **Maintainable** - Centralized selectors for easy updates
- ✅ **Reusable** - Page objects and fixtures for any test
- ✅ **Best Practices** - Follows Playwright recommendations
- ✅ **Ready to Use** - Sample tests demonstrate usage
- ✅ **Customizable** - Extend generated classes easily

[📘 Detailed Output Structure](docs/user-guides/08-output-structure.md)

## 💡 Examples

### Generated Page Object

```typescript
// page-objects/login.page.ts
import { Page, Locator } from '@playwright/test';
import { loginSelectors } from '../selectors/login.selectors';

/**
 * Page Object for Login component
 */
export class LoginPage {
  readonly page: Page;
  readonly usernameInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.usernameInput = page.locator(loginSelectors.usernameInput);
    this.passwordInput = page.locator(loginSelectors.passwordInput);
    this.submitButton = page.locator(loginSelectors.submitButton);
  }

  /**
   * Navigate to the login page
   */
  async navigate(): Promise<void> {
    await this.page.goto('/login');
  }

  /**
   * Fill the username field
   */
  async fillUsername(value: string): Promise<void> {
    await this.usernameInput.fill(value);
  }

  /**
   * Fill the password field
   */
  async fillPassword(value: string): Promise<void> {
    await this.passwordInput.fill(value);
  }

  /**
   * Click the submit button
   */
  async clickSubmit(): Promise<void> {
    await this.submitButton.click();
  }

  /**
   * Complete login flow
   */
  async login(username: string, password: string): Promise<void> {
    await this.fillUsername(username);
    await this.fillPassword(password);
    await this.clickSubmit();
  }
}
```

### Generated Test Fixture

```typescript
// fixtures/base.fixture.ts
import { test as base, expect } from '@playwright/test';
import { HomePage } from '../page-objects/home.page';
import { LoginPage } from '../page-objects/login.page';
import { DashboardPage } from '../page-objects/dashboard.page';

type PageFixtures = {
  homePage: HomePage;
  loginPage: LoginPage;
  dashboardPage: DashboardPage;
};

export const test = base.extend<PageFixtures>({
  homePage: async ({ page }, use) => {
    const homePage = new HomePage(page);
    await use(homePage);
  },

  loginPage: async ({ page }, use) => {
    const loginPage = new LoginPage(page);
    await use(loginPage);
  },

  dashboardPage: async ({ page }, use) => {
    const dashboardPage = new DashboardPage(page);
    await use(dashboardPage);
  },
});

export { expect };
```

### Using Generated Code in Tests

```typescript
// tests/login.spec.ts
import { test, expect } from '../fixtures/base.fixture';

test.describe('Login Flow', () => {
  test('should login successfully with valid credentials', async ({ loginPage, dashboardPage }) => {
    await loginPage.navigate();
    await loginPage.login('testuser', 'password123');
    
    // Verify redirect to dashboard
    await expect(dashboardPage.page).toHaveURL(/\/dashboard/);
    await expect(dashboardPage.welcomeMessage).toBeVisible();
  });

  test('should show error with invalid credentials', async ({ loginPage }) => {
    await loginPage.navigate();
    await loginPage.login('invalid', 'wrong');
    
    // Verify error message
    await expect(loginPage.errorMessage).toBeVisible();
    await expect(loginPage.errorMessage).toHaveText('Invalid username or password');
  });
});
```

### CI/CD Integration Example

```yaml
# .github/workflows/generate-tests.yml
name: Generate Playwright Tests

on:
  push:
    paths:
      - 'src/**/*.component.ts'

jobs:
  generate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Install POM Generator
        run: dotnet tool install -g PlaywrightPomGenerator
      
      - name: Generate Playwright tests
        run: playwright-pom-gen workspace . --output ./e2e-tests
        env:
          POMGEN_Generator__BaseUrlPlaceholder: ${{ secrets.STAGING_URL }}
      
      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'
      
      - name: Install Playwright
        run: |
          npm install @playwright/test
          npx playwright install
      
      - name: Run tests
        run: npx playwright test
      
      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: playwright-report
          path: playwright-report/
```

## ⚙️ Configuration

Configure the tool using multiple methods (in order of precedence):

1. **Command-line options** (highest priority)
2. **Environment variables**
3. **Configuration files** (lowest priority)

### Configuration File (appsettings.json)

Place an `appsettings.json` in your working directory or project root:

```json
{
  "Generator": {
    "FileHeader": "/**\n * @fileoverview {FileName}\n * @generated {GeneratedDate}\n * @version {ToolVersion}\n */",
    "TestFileSuffix": "spec",
    "ToolVersion": "1.0.0",
    "OutputDirectoryName": "e2e",
    "GenerateJsDocComments": true,
    "DefaultTimeout": 30000,
    "BaseUrlPlaceholder": "http://localhost:4200"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "PlaywrightPomGenerator": "Information"
    }
  }
}
```

### Environment Variables

Use the `POMGEN_` prefix with double underscores:

```bash
# Linux/macOS
export POMGEN_Generator__FileHeader="// My Custom Header"
export POMGEN_Generator__TestFileSuffix="test"
export POMGEN_Generator__DefaultTimeout="60000"

# Windows PowerShell
$env:POMGEN_Generator__FileHeader = "// My Custom Header"
$env:POMGEN_Generator__TestFileSuffix = "test"
$env:POMGEN_Generator__DefaultTimeout = "60000"
```

### Environment-Specific Configuration

Create environment-specific files:

```json
// appsettings.Development.json
{
  "Generator": {
    "BaseUrlPlaceholder": "http://localhost:4200"
  }
}

// appsettings.Production.json
{
  "Generator": {
    "BaseUrlPlaceholder": "https://app.example.com"
  }
}
```

**Usage:**
```bash
DOTNET_ENVIRONMENT=Production playwright-pom-gen app .
```

[📘 Full Configuration Guide](docs/user-guides/06-configuration.md)

## 📚 Documentation

Comprehensive documentation is available in the [docs/user-guides](docs/user-guides/) directory:

### Getting Started
- [📖 Overview](docs/user-guides/00-overview.md) - Introduction and features
- [🚀 Getting Started](docs/user-guides/01-getting-started.md) - Installation and first run

### Command Guides
- [📘 Generate App Command](docs/user-guides/02-generate-app.md)
- [📘 Generate Workspace Command](docs/user-guides/03-generate-workspace.md)
- [📘 Generate Artifacts Command](docs/user-guides/04-generate-artifacts.md)
- [📘 Generate SignalR Mock Command](docs/user-guides/05-generate-signalr-mock.md)

### Configuration & Reference
- [⚙️ Configuration Guide](docs/user-guides/06-configuration.md)
- [🔧 Global Options](docs/user-guides/07-global-options.md)
- [📂 Output Structure](docs/user-guides/08-output-structure.md)
- [🔍 Troubleshooting](docs/user-guides/09-troubleshooting.md)
- [✨ Best Practices](docs/user-guides/10-best-practices.md)

### Quick Help

```bash
# General help
playwright-pom-gen --help

# Command-specific help
playwright-pom-gen app --help
playwright-pom-gen workspace --help
playwright-pom-gen artifacts --help
playwright-pom-gen signalr-mock --help
```

## 📋 Requirements

### Runtime Requirements
- **.NET 10.0 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **Angular application or workspace** (Angular CLI project structure)

### For Generated Tests
- **Node.js** 16+ and npm
- **@playwright/test** package
- Modern browser(s) for Playwright

### Supported Platforms
- ✅ Windows 10/11
- ✅ macOS 11+
- ✅ Linux (Ubuntu 20.04+, other distributions)

### Tested With
- Angular 14, 15, 16, 17
- Playwright 1.40+
- TypeScript 5.0+

## 🔨 Building from Source

### Clone and Build

```bash
# Clone the repository
git clone <repository-url>
cd PageObjectModel

# Restore dependencies
dotnet restore PlaywrightPomGenerator.sln

# Build the solution
dotnet build PlaywrightPomGenerator.sln --configuration Release

# Run tests
dotnet test
```

### Run from Source

```bash
cd src/PlaywrightPomGenerator.Cli
dotnet run -- app /path/to/angular-app

# Watch mode (auto-rebuild on changes)
dotnet watch run -- app /path/to/angular-app
```

### Create NuGet Package

```bash
# Create package
dotnet pack src/PlaywrightPomGenerator.Cli \
  --configuration Release \
  --output ./artifacts

# Install locally from package
dotnet tool install -g \
  --add-source ./artifacts \
  PlaywrightPomGenerator
```

### Development Setup

```bash
# Open in Visual Studio
start PlaywrightPomGenerator.sln

# Or open in VS Code
code .

# Or open in Rider
rider PlaywrightPomGenerator.sln
```

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

### Quick Start for Contributors

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/my-feature`
3. **Make** your changes with tests
4. **Commit** using [Conventional Commits](https://www.conventionalcommits.org/):
   ```bash
   git commit -m "feat: add Vue component support"
   ```
5. **Push** to your fork: `git push origin feature/my-feature`
6. **Open** a Pull Request

### Areas to Contribute
- 🐛 Bug fixes and improvements
- ✨ New features (Vue, React support, etc.)
- 📚 Documentation improvements
- 🧪 Additional tests
- 💡 Feature ideas and suggestions

See [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Development setup
- Coding standards
- Testing guidelines
- Pull request process

## 🆘 Support

### Getting Help
- 📖 **Documentation**: [User Guides](docs/user-guides/)
- 🐛 **Bug Reports**: [Open an issue](../../issues)
- 💡 **Feature Requests**: [Open an issue](../../issues)
- 💬 **Discussions**: [GitHub Discussions](../../discussions)

### Troubleshooting
- Check the [Troubleshooting Guide](docs/user-guides/09-troubleshooting.md)
- Search [existing issues](../../issues)
- Enable debug logging in `appsettings.json`

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🌟 Acknowledgments

- Built with [.NET](https://dotnet.microsoft.com/)
- Powered by [Playwright](https://playwright.dev/)
- Designed for [Angular](https://angular.io/)
- Inspired by the Page Object Model pattern

## 📊 Project Status

- ✅ **Active Development**
- ✅ **Stable Release**
- ✅ **Production Ready**
- ✅ **Well Documented**

---

**Made with ❤️ for the Angular and Playwright communities**

*Star ⭐ this repository if you find it useful!*
