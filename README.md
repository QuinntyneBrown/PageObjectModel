# Playwright POM Generator

[![NuGet Version](https://img.shields.io/nuget/v/PlaywrightPomGenerator.svg)](https://www.nuget.org/packages/PlaywrightPomGenerator)
[![.NET Version](https://img.shields.io/badge/.NET-8.0+-purple.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A powerful .NET CLI tool that automatically generates Playwright Page Object Model (POM) tests for Angular applications. Transform your Angular components into maintainable, type-safe Playwright test infrastructure with a single command.

## Features

- **Automatic Angular Analysis** - Intelligently scans Angular workspaces, applications, and libraries to detect components, templates, and routing
- **BasePage Pattern** - All generated page objects extend from a common BasePage class with shared functionality
- **Page Object Generation** - Creates type-safe TypeScript page objects with properly typed locators and methods
- **Component Object Generation** - Creates root-`Locator`-scoped component objects (`ppg component`) for non-routable components, so dashboard-style apps can be verified component-by-component and composed inside pages
- **Service Bridge Generation** - Creates window-exposed recording mocks of `InjectionToken`-backed service interfaces (`ppg bridge`), so E2E tests can verify service calls, stub return values, and push observable values to drive the UI (uses a TypeScript-compiler sidecar for accurate type analysis)
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

# From a remote git URL (GitHub, GitLab, Bitbucket, Azure DevOps, etc.)
ppg remote https://github.com/owner/repo/blob/main/src/app/my-component/my-component.ts
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

### `component` - Generate Component Objects

Generates **Component Objects**: classes scoped to a root `Locator` (the component host element)
rather than to a `Page`. Use this for non-routable components — cards, panels, widgets — so a
dashboard-style app (one route, many components) can be verified component-by-component. A component
object works wherever, and however many times, the component renders.

```bash
ppg component <path> [options]

# Examples
ppg component ./src/app/dashboard
ppg component ./src/app -o ./e2e
ppg component ./src/app --exclude-routable
```

**Options:**
- `-o, --output` - Output directory for generated files
- `--exclude-routable` - Skip components that are routable pages (default: generate a component
  object for every component discovered at the path)

The path may be an application, a library, or an arbitrary component/feature folder. See
[Generated Component Objects](#generated-component-objects) for the output shape.

### `bridge` - Generate a Playwright Service Bridge

Generates a **bridge** that exposes recording mocks of your `InjectionToken`-backed service
interfaces on `window.__e2eBridge`. Playwright tests can then **verify that a service was called**,
**stub return values** to drive the UI from fake data, and **push observable values** to trigger
service-level behaviour — all without touching the real service implementations.

```bash
ppg bridge <path> [options]

# Examples
ppg bridge ./my-workspace
ppg bridge ./src/app -o ./e2e
```

**Options:**
- `-o, --output` - Output directory for generated files

> **Requires Node.js.** The `bridge` command shells out to a TypeScript-compiler "sidecar" to parse
> interfaces accurately (including members inherited via `extends`). TypeScript is resolved from the
> target workspace's `node_modules`. The rest of the tool is pure .NET. See
> [Generated Playwright Bridge](#generated-playwright-bridge) for the output and usage.

### `artifacts` - Generate Specific Artifacts

```bash
ppg artifacts <path> [options]

# Examples
ppg artifacts . --all
ppg artifacts . --fixtures --configs
ppg artifacts . --page-objects --selectors
ppg artifacts . --component-objects
```

**Options:**
- `-f, --fixtures` - Generate test fixtures
- `-c, --configs` - Generate Playwright configuration
- `-s, --selectors` - Generate selector files
- `--page-objects` - Generate page object files
- `--helpers` - Generate helper utilities
- `--component-objects` - Generate component objects (root-scoped, for composition inside pages)
- `-a, --all` - Generate all artifacts

### `remote` - Generate from Remote Git URL

Generate tests directly from a remote git repository URL. The tool clones the repository to a temporary directory, analyzes the Angular components at the specified path, generates tests, and cleans up automatically. Requires `git` to be installed and available on the system PATH.

```bash
ppg remote <url> [options]

# Examples

# GitHub - component file
ppg remote https://github.com/owner/repo/blob/main/src/app/my-component/my-component.ts

# GitHub - component folder
ppg remote https://github.com/owner/repo/tree/main/src/app/components -o ./e2e

# GitLab (including self-hosted)
ppg remote https://gitlab.com/owner/repo/-/blob/develop/src/app/table/table.ts
ppg remote https://gitscm.company.com/team/repo/-/blob/main/src/components/form

# Bitbucket
ppg remote https://bitbucket.org/owner/repo/src/main/src/app/dashboard

# Azure DevOps
ppg remote "https://dev.azure.com/org/project/_git/repo?path=/src/app/login&version=GBmain"

# Commit hash instead of branch
ppg remote https://github.com/owner/repo/blob/a1b2c3d4e5f6/src/app/my-component
```

**Options:**
- `-o, --output <dir>` - Output directory (defaults to `./e2e` in the current working directory)

**Supported URL formats:**
| Provider | URL Pattern |
|---|---|
| GitHub | `https://github.com/{owner}/{repo}/blob/{ref}/{path}` |
| GitLab | `https://gitlab.com/{owner}/{repo}/-/blob/{ref}/{path}` |
| Self-hosted GitLab | `https://gitscm.example.com/{owner}/{repo}/-/blob/{ref}/{path}` |
| Bitbucket | `https://bitbucket.org/{owner}/{repo}/src/{ref}/{path}` |
| Azure DevOps | `https://dev.azure.com/{org}/{project}/_git/{repo}?path={path}&version=GB{branch}` |
| Generic | `https://git.example.com/{owner}/{repo}.git` |

**Notes:**
- The ref can be a branch name (e.g., `main`, `develop`) or a commit hash
- When pointing to a file, tests are generated for that single component
- When pointing to a folder, all components in that folder (and subfolders) are scanned
- The tool automatically detects Angular workspaces, applications, and libraries within the cloned repo
- No third-party git libraries are used; only the `git` CLI

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
├── components/                 # Component Object Model classes (ppg component)
│   ├── base.component.ts       # Abstract base class for all component objects
│   ├── kpi-card.component.ts   # Root-Locator-scoped component objects
│   └── ...
│
├── bridge/                     # Service-interface mocks + window bridge (ppg bridge)
│   ├── bridge-registry.ts      # Call recorder + window.__e2eBridge control API
│   ├── bridge-providers.ts     # provideE2EBridge() Angular providers
│   ├── playwright-bridge.ts    # Typed PlaywrightBridge client for tests
│   └── mocks/                  # One recording mock per InjectionToken interface
│
├── helpers/                    # Selector constants
│   ├── login.selectors.ts
│   ├── dashboard.selectors.ts
│   └── ...
│
└── tests/                      # Test specification files
    ├── login.spec.ts
    ├── dashboard.spec.ts
    ├── kpi-card.component.spec.ts
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

### Generated Component Objects

Run `ppg component <path>` to generate **Component Objects**. Unlike page objects (rooted on `page`
and navigable), a component object is scoped to a root `Locator` — the component's host element — so
it composes inside pages and works even when the component renders many times.

All component objects extend `BaseComponent`:

```typescript
// components/base.component.ts
import { Locator, expect } from '@playwright/test';

/** Abstract base class for all component objects. Scoped to a root Locator, not a Page. */
export abstract class BaseComponent {
  /** The root locator this component is scoped to. */
  protected readonly root: Locator;

  constructor(root: Locator) {
    this.root = root;
  }

  /** The root locator for this component instance. */
  getRoot(): Locator {
    return this.root;
  }

  /** Asserts the component's root is visible. */
  async expectVisible(): Promise<void> {
    await expect(this.root).toBeVisible();
  }

  /** Asserts the component's root is hidden/detached. */
  async expectHidden(): Promise<void> {
    await expect(this.root).toBeHidden();
  }

  /** Returns whether the component's root is currently visible. */
  async isVisible(): Promise<boolean> {
    return this.root.isVisible();
  }

  /** Scoped test-id lookup within this component. */
  protected getByTestId(testId: string): Locator {
    return this.root.getByTestId(testId);
  }
}
```

A component object exposes its host selector as `static readonly hostSelector` and roots every
locator on `this.root` — there is no `navigate()`:

```typescript
// components/kpi-card.component.ts
import { Locator, expect } from '@playwright/test';
import { BaseComponent } from './base.component';

export class KpiCard extends BaseComponent {
  /** Host element selector. Use to build the root locator from a Page or parent. */
  static readonly hostSelector = 'app-kpi-card';

  readonly kpiTitle: Locator;
  readonly kpiValue: Locator;
  readonly refreshButton: Locator;

  constructor(root: Locator) {
    super(root);
    this.kpiTitle = this.root.getByTestId('kpi-title');
    this.kpiValue = this.root.getByTestId('kpi-value');
    this.refreshButton = this.root.getByRole('button', { name: 'Refresh' });
  }

  async clickRefreshButton(): Promise<void> {
    await this.refreshButton.click();
  }

  async expectRefreshButtonVisible(): Promise<void> {
    await expect(this.refreshButton).toBeVisible();
  }
}
```

#### A Page Object that vends Component Objects

Because a component object's root is just a `Locator`, a page object can hand out scoped instances —
by index, or by a stable attribute such as a visible title — and the same object works when the
component repeats `N` times:

```typescript
// dashboard.page.ts (excerpt)
import { KpiCard } from '../components/kpi-card.component';

export class DashboardPage extends BasePage {
  /** A single KPI card scoped by index. */
  kpiCard(index = 0): KpiCard {
    return new KpiCard(
      this.page.locator(KpiCard.hostSelector).nth(index),
    );
  }

  /** A KPI card scoped by its visible title (stable across reorders). */
  kpiCardByTitle(title: string): KpiCard {
    return new KpiCard(
      this.page.locator(KpiCard.hostSelector).filter({ hasText: title }),
    );
  }
}
```

A generated component-object spec composes the object from its host page rather than navigating:

```typescript
// tests/kpi-card.component.spec.ts
import { test, expect } from '@playwright/test';
import { KpiCard } from '../components/kpi-card.component';

const HOST_PAGE_URL = '/'; // TODO: replace with the URL of the page that renders <app-kpi-card>

test.describe('KpiCard', () => {
  test('should render the component', async ({ page }) => {
    await page.goto(HOST_PAGE_URL);
    const component = new KpiCard(
      page.locator(KpiCard.hostSelector).first(),
    );
    await component.expectVisible();
  });
});
```

### Generated Playwright Bridge

Run `ppg bridge <path>` to scan for `InjectionToken`-backed service interfaces and generate a bridge
that lets tests verify service calls, stub return values, and drive the UI from fake service data.
Given a tokenized service:

```typescript
// src/app/services/local-storage.token.ts
export interface ILocalStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export const LOCAL_STORAGE = new InjectionToken<ILocalStorage>('ILocalStorage');
```

**1. Install the bridge in your E2E build.** `provideE2EBridge()` replaces the real service with a
recording mock and exposes `window.__e2eBridge`:

```typescript
// main.e2e.ts (or your test application config)
import { provideE2EBridge } from './e2e/bridge/bridge-providers';

bootstrapApplication(AppComponent, {
  providers: [...appConfig.providers, provideE2EBridge()],
});
```

**2. Drive and verify services from Playwright tests** using the typed client:

```typescript
import { test, expect } from '@playwright/test';
import { PlaywrightBridge } from '../bridge/playwright-bridge';

test('renders stored data and persists changes', async ({ page }) => {
  const bridge = new PlaywrightBridge(page);

  // Stub the value the component reads from ILocalStorage, then verify the UI.
  await bridge.localStorage.setReturn('getItem', 'Ada');
  await page.goto('/profile');
  await expect(page.getByTestId('greeting')).toHaveText('Welcome, Ada');

  // Act in the UI, then verify the service interface was actually called.
  await page.getByRole('button', { name: 'Save' }).click();
  await bridge.localStorage.expectCalled('setItem');
  const calls = await bridge.localStorage.getCalls('setItem');
  expect(calls[0].args).toEqual(['username', 'Ada']);
});
```

For interfaces that expose observables (e.g. `watch(): Observable<Notification>`), push values to
trigger service-level behaviour and verify the UI reacts:

```typescript
await bridge.notifications.emit('watch', { message: 'New alert' });
await expect(page.getByRole('alert')).toHaveText('New alert');
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
- `git` CLI (required for `ppg remote` command)

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
