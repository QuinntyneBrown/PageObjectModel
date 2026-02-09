# Playwright POM Generator

[![NuGet Version](https://img.shields.io/nuget/v/PlaywrightPomGenerator.svg)](https://www.nuget.org/packages/PlaywrightPomGenerator)
[![.NET Version](https://img.shields.io/badge/.NET-8.0+-purple.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A powerful .NET CLI tool that automatically generates Playwright Page Object Model (POM) tests for Angular applications. Transform your Angular components into maintainable, type-safe Playwright test infrastructure with a single command.

## Features

- **Automatic Angular Analysis** - Intelligently scans Angular workspaces, applications, and libraries to detect components, templates, and routing
- **BasePage Pattern** - All generated page objects extend from a common BasePage class with shared functionality
- **Page Object Generation** - Creates type-safe TypeScript page objects with properly typed locators and methods
- **Inline Template Support** - Parses both external HTML templates and inline templates in component files
- **Angular Material Support** - Detects and creates selectors for mat-button, mat-table, mat-form-field, and other Material components
- **Click Handler Detection** - Automatically generates click methods for elements with `(click)` event bindings
- **Table Accessor Methods** - Generates comprehensive table methods (getRows, getHeaders, getRowCount, clickRow, etc.)
- **Test Scaffolding** - Generates complete Playwright test specs with fixture integration
- **SignalR Mock Support** - Generates RxJS-based SignalR mock fixtures for real-time application testing
- **Workspace Support** - Handle complex Angular workspaces with multiple applications and libraries
- **Remote Repository Support** - Generate tests directly from a git URL (GitHub, GitLab, Bitbucket, Azure DevOps, or any git host) without manually cloning
- **Highly Configurable** - Customize output via configuration files, environment variables, or command-line options

## Installation

### Global Tool Installation (Recommended)

```bash
dotnet tool install -g PlaywrightPomGenerator
```

### Update Existing Installation

```bash
dotnet tool update -g PlaywrightPomGenerator
```

The tool command is `ppg`.

## Quick Start

### 1. Generate Tests for Your Angular App

```bash
# For a single application
ppg app ./my-angular-app -o ./e2e

# For an Angular workspace
ppg workspace . -o ./e2e-tests

# For an Angular library
ppg lib ./projects/my-lib -o ./e2e
```

### 2. Install Playwright and Run Tests

```bash
cd ./e2e
npm install
npx playwright install
npx playwright test
```

## Commands

### `app` - Generate for Single Application

```bash
ppg app <path> [options]

# Examples
ppg app ./src/my-app
ppg app ./src/my-app -o ./e2e-tests
ppg app . --test-suffix "test"
```

### `workspace` - Generate for Angular Workspace

```bash
ppg workspace <path> [options]

# Examples
ppg workspace .
ppg workspace . -p my-app
ppg workspace . -o ./tests
```

**Options:**
- `-o, --output <dir>` - Output directory
- `-p, --project <name>` - Generate for specific project only

### `lib` - Generate for Angular Library

```bash
ppg lib <path> [options]

# Examples
ppg lib ./projects/my-lib
ppg lib ./projects/components -o ./e2e
```

### `artifacts` - Generate Specific Artifacts

```bash
ppg artifacts <path> [options]

# Examples
ppg artifacts . --all
ppg artifacts . --fixtures --configs
ppg artifacts . --page-objects --selectors
```

**Options:**
- `-f, --fixtures` - Generate test fixtures
- `-c, --configs` - Generate Playwright configuration
- `-s, --selectors` - Generate selector files
- `--page-objects` - Generate page object files
- `--helpers` - Generate helper utilities
- `-a, --all` - Generate all artifacts

### `signalr-mock` - Generate SignalR Mock

```bash
ppg signalr-mock <output>

# Examples
ppg signalr-mock ./fixtures
ppg signalr-mock ./e2e/mocks
```

### Global Options

```bash
# Custom file header with placeholders
--header "// Copyright 2026\n// File: {FileName}\n// Generated: {GeneratedDate}"

# Custom test file suffix (default: "spec")
--test-suffix "test"  # Creates *.test.ts instead of *.spec.ts

# Enable debug mode (includes HTML template as comments in page objects)
--debug
```

## Generated Output

```
e2e/
├── playwright.config.ts        # Playwright configuration
├── helpers.ts                  # Common utility functions
│
├── configs/                    # Configuration files
│   ├── timeout.config.ts       # Timeout constants
│   └── urls.config.ts          # URL constants and API endpoints
│
├── fixtures/                   # Test fixtures
│   └── fixtures.ts             # Extended test with page object fixtures
│
├── page-objects/               # Page Object Model classes
│   ├── base.page.ts            # Abstract base class for all page objects
│   ├── login.page.ts           # Component page objects
│   ├── dashboard.page.ts
│   └── ...
│
├── helpers/                    # Selector constants
│   ├── login.selectors.ts
│   ├── dashboard.selectors.ts
│   └── ...
│
└── tests/                      # Test specification files
    ├── login.spec.ts
    ├── dashboard.spec.ts
    └── ...
```

## Generated Code Examples

### Base Page Class

All page objects extend from `BasePage`, providing common functionality:

```typescript
// page-objects/base.page.ts
import { Page, Locator } from '@playwright/test';
import { TIMEOUTS } from '../configs/timeout.config';

export abstract class BasePage {
  constructor(protected page: Page) {}

  abstract navigate(): Promise<void>;
  abstract waitForLoad(): Promise<void>;

  async getPageTitle(): Promise<string> {
    return this.page.title();
  }

  protected async waitForNavigation(): Promise<void> {
    await this.page.waitForLoadState('networkidle', { timeout: TIMEOUTS.navigation });
  }

  protected getLocator(selector: string): Locator {
    return this.page.locator(selector);
  }

  protected async waitForVisible(selector: string, timeout = TIMEOUTS.elementVisible): Promise<void> {
    await this.getLocator(selector).waitFor({ state: 'visible', timeout });
  }

  protected async waitForHidden(selector: string, timeout = TIMEOUTS.elementHidden): Promise<void> {
    await this.getLocator(selector).waitFor({ state: 'hidden', timeout });
  }

  // ... additional utility methods
}
```

### Generated Page Object

```typescript
// page-objects/login.page.ts
import { Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

export class LoginPage extends BasePage {
  private readonly componentSelector = 'app-login';

  readonly usernameInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;

  constructor(page: import('@playwright/test').Page) {
    super(page);
    this.usernameInput = page.locator('[formControlName="username"]');
    this.passwordInput = page.locator('[formControlName="password"]');
    this.submitButton = page.getByRole('button', { name: 'Login' });
  }

  async navigate(): Promise<void> {
    await this.page.goto('/login');
    await this.waitForLoad();
  }

  async fillUsernameInput(value: string): Promise<void> {
    await this.usernameInput.fill(value);
  }

  async fillPasswordInput(value: string): Promise<void> {
    await this.passwordInput.fill(value);
  }

  async clickSubmitButton(): Promise<void> {
    await this.submitButton.click();
  }

  async expectSubmitButtonVisible(): Promise<void> {
    await expect(this.submitButton).toBeVisible();
  }

  async waitForLoad(): Promise<void> {
    await this.page.waitForSelector(this.componentSelector);
  }
}
```

### Generated Table Page Object

For components with tables, comprehensive accessor methods are generated:

```typescript
// page-objects/data-table.page.ts
export class DataTablePage extends BasePage {
  readonly dataTable: Locator;

  async expectDataTableVisible(): Promise<void> {
    await expect(this.dataTable).toBeVisible();
  }

  getDataTableRows(): Locator {
    return this.dataTable.locator('mat-row, tr[mat-row], [mat-row]');
  }

  getDataTableRow(index: number): Locator {
    return this.getDataTableRows().nth(index);
  }

  getDataTableHeaders(): Locator {
    return this.dataTable.locator('mat-header-cell, th[mat-header-cell], [mat-header-cell]');
  }

  async getDataTableRowCount(): Promise<number> {
    return this.getDataTableRows().count();
  }

  async clickDataTableRow(index: number): Promise<void> {
    await this.getDataTableRow(index).click();
  }
}
```

### Using Generated Code in Tests

```typescript
// tests/login.spec.ts
import { test, expect } from '../fixtures/fixtures';

test.describe('Login', () => {
  test('should display the login form', async ({ loginPage }) => {
    await loginPage.navigate();
    await loginPage.expectSubmitButtonVisible();
  });

  test('should login successfully', async ({ loginPage, dashboardPage }) => {
    await loginPage.navigate();
    await loginPage.fillUsernameInput('testuser');
    await loginPage.fillPasswordInput('password123');
    await loginPage.clickSubmitButton();

    await dashboardPage.waitForLoad();
    await expect(dashboardPage.page).toHaveURL(/dashboard/);
  });
});
```

## Configuration

### Configuration File (appsettings.json)

```json
{
  "Generator": {
    "FileHeader": "/**\n * @generated {GeneratedDate}\n * @version {ToolVersion}\n */",
    "TestFileSuffix": "spec",
    "ToolVersion": "1.3.0",
    "OutputDirectoryName": "e2e",
    "GenerateJsDocComments": true,
    "DefaultTimeout": 30000,
    "BaseUrlPlaceholder": "http://localhost:4200"
  }
}
```

### Environment Variables

Use the `POMGEN_` prefix:

```bash
export POMGEN_Generator__TestFileSuffix="test"
export POMGEN_Generator__DefaultTimeout="60000"
export POMGEN_Generator__BaseUrlPlaceholder="http://localhost:3000"
```

## Selector Detection

The tool automatically detects and creates selectors for:

- `data-testid` attributes (highest priority)
- `id` attributes
- Role-based selectors (buttons with text)
- `formControlName` attributes
- Click handlers `(click)="method()"`
- Router links (`routerLink`, `href`)
- Angular Material components:
  - `mat-button`, `mat-raised-button`, `mat-flat-button`, `mat-stroked-button`
  - `mat-icon-button`, `mat-fab`, `mat-mini-fab`
  - `mat-form-field` with `mat-label`
  - `mat-table` with rows, headers, and cells
- Tables (`<table>`, `mat-table`)
- Input elements with `type` attribute
- Custom elements with text content
- **Elements with Angular interpolation** (`{{ property }}`)
- **Elements with ng-content projection** (`<ng-content></ng-content>`)

### Dynamic Content Detection

The generator detects elements that will render dynamic content:

```html
<!-- Angular interpolation - detected -->
<h1>{{ title }}</h1>
<p>{{ description }}</p>
<div>{{ content }}</div>

<!-- ng-content projection - detected -->
<div><ng-content></ng-content></div>
<span><ng-content select="[header]"></ng-content></span>
```

For these elements, the generated page object includes:
- `expect{Element}Visible()` - Visibility assertion
- `expect{Element}HasText(expected)` - Exact text assertion
- `expect{Element}ContainsText(expected)` - Partial text assertion
- `get{Element}Text()` - Text content retrieval

## Requirements

### Runtime
- .NET 8.0 SDK or later
- Angular application, library, or workspace

### For Generated Tests
- Node.js 16+ and npm
- @playwright/test package

### Supported Platforms
- Windows 10/11
- macOS 11+
- Linux (Ubuntu 20.04+)

### Tested With
- Angular 14, 15, 16, 17, 18
- Playwright 1.40+
- TypeScript 5.0+

## Building from Source

```bash
# Clone the repository
git clone <repository-url>
cd PageObjectModel

# Build
dotnet build -c Release

# Run tests
dotnet test

# Create NuGet package
dotnet pack src/PlaywrightPomGenerator.Cli -c Release
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Made with care for the Angular and Playwright communities**
