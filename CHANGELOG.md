# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.9.0] - 2026-06-05

### Changed
- **Renamed the generated harness from "E2E bridge" to "interface mock" naming.** The `bridge`
  command now generates an interface-mock harness instead of a bridge:
  - Output folder `e2e/bridge/` → `e2e/interface-mocks/`.
  - Files `bridge-registry.ts` → `interface-mock-registry.ts`, `bridge-providers.ts` →
    `interface-mock-providers.ts`, `playwright-bridge.ts` → `playwright-interface-mocks.ts`.
  - Generated symbols: `E2EBridgeRegistry` → `InterfaceMockRegistry`, `e2eBridge` → `interfaceMocks`,
    `E2EBridgeApi` → `InterfaceMockApi`, `installE2EBridge` → `exposeInterfaceMocks`,
    `provideE2EBridge` → `provideInterfaceMocks`, `window.__e2eBridge` → `window.__interfaceMocks`,
    `InterfaceBridge` → `InterfaceMockHandle`, `PlaywrightBridge` → `InterfaceMocks`,
    `BridgeCallRecord` → `InterfaceCallRecord`.
  - Generated comments describe interface mocks rather than an E2E bridge.
  - Behavior is unchanged. The `bridge` command name itself is retained.

### Fixed
- The generated registry's `reset()` now also completes and clears the `ReplaySubject`-backed
  observable streams (not just recorded calls and stubbed values), so stale observable emissions
  cannot leak between tests.

## [1.8.0] - 2026-06-05

### Added
- **`bridge` command** - Generates a Playwright "bridge" that exposes recording mocks of
  `InjectionToken`-backed service interfaces on `window.__e2eBridge`, so E2E tests can:
  - verify that calls to a service interface were made (`expectCalled`, `getCalls`, `getCallCount`),
  - stub return values (`setReturn`) to drive the UI from fake service data, and
  - push values through observable members (`emit`) to trigger service-level behaviour and verify the UI.
- **TypeScript AST sidecar** (`src/PlaywrightPomGenerator.Sidecar.Node`) - a Node process that uses
  the TypeScript compiler to discover `new InjectionToken<IFoo>(...)` declarations and resolve each
  interface's members (including members inherited via `extends`), returning structured JSON over
  JSON-RPC. The .NET host (`NodeSidecarTransport` + `TypeScriptAnalyzer`) consumes it. TypeScript is
  resolved from the target workspace's `node_modules` (or a copy installed in the sidecar directory).
- **Generated bridge output** (new `bridge/` directory):
  - `bridge-registry.ts` - the call recorder, stub store, observable subjects, and the
    `window.__e2eBridge` control API.
  - `mocks/{kebab}.mock.ts` per interface - a recording mock implementing the interface, with methods
    typed via `Parameters<I['m']>` / `ReturnType<I['m']>` (so referenced DTO types need not be imported).
  - `bridge-providers.ts` - `provideE2EBridge()` Angular providers that install the bridge and wire
    each token to its mock (add to the app config in E2E builds).
  - `playwright-bridge.ts` - a typed `PlaywrightBridge` client for use in tests.

### Changed
- The `bridge` command requires Node.js (it shells out to the TypeScript sidecar); the rest of the
  tool remains pure .NET.
- Bumped tool version to 1.8.0.

## [1.7.0] - 2026-06-05

### Changed
- Component object class names no longer carry the `ComponentObject` suffix; the `Component`
  suffix is stripped instead (e.g. `KpiCardComponent` now generates `export class KpiCard`,
  not `KpiCardComponentObject`).
- Generated component objects are now written to a `components/` directory instead of
  `component-objects/`. (The `--component-objects` flag name on `artifacts` is unchanged.)

## [1.6.0] - 2026-06-05

### Added
- **`component` command** - Generates Playwright **Component Objects**: classes scoped to a root
  `Locator` (the component host element) rather than to a `Page`. Component objects are built for
  composition inside page objects, so dashboard-style apps (one route, many components) can be
  verified component-by-component. They work wherever — and however many times — a component renders.
  - `--exclude-routable` skips components whose `IsRoutable` is true (default: generate for all
    components discovered at the path).
  - Accepts an application, library, or an arbitrary component/feature folder.
- **Generated component-object output** (new `component-objects/` directory):
  - `base.component.ts` - abstract `BaseComponent` holding a `protected readonly root: Locator` and
    providing `getRoot()`, `expectVisible()`, `expectHidden()`, `isVisible()`, and a scoped
    `getByTestId()`. It declares no `navigate()`.
  - `{kebab}.component.ts` per component - a `*ComponentObject` class extending `BaseComponent`,
    exposing the host selector as `static readonly hostSelector`, with every locator rooted on
    `this.root` and assertion-first (`expect*`) / action (`click*`, `fill*`) helpers.
  - `tests/{kebab}.component.spec.ts` - a component-object spec that composes the object from its
    host page (`page.locator(Host.hostSelector).first()`) instead of navigating.
- **`artifacts --component-objects`** flag and `GenerationRequest.GenerateComponentObjects` to emit
  component objects as part of selective artifact generation (also enabled by `--all`).

### Changed
- Internal: `TemplateEngine` selector-locator and action-method emitters are now parameterized by a
  base expression (`page` for page objects, `this.root` for component objects), so selector,
  action, and table-accessor logic is single-sourced. Page-object output is unchanged.
- Bumped tool version to 1.6.0.

## [1.5.0] - 2026-06-05

### Added
- **`remote` command** - Generates Playwright POM tests directly from a remote Git repository URL,
  cloning the repo and analyzing components at the target path (`IGitService`/`GitUrlParser`).
- `eng/scripts/install-tool.bat` helper to install or update the `ppg` dotnet tool.

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
