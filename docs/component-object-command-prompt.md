# Prompt: Add a `component` command that generates Playwright Component Objects

You are working inside the **QuinntyneBrown/PageObjectModel** repository — a .NET 8 CLI
(`ppg`, package `PlaywrightPomGenerator`) that scans Angular and emits Playwright Page
Object Model (POM) test infrastructure. Your task is to add a new, **additive** capability:
generate **Component Objects** (root-`Locator`-scoped objects) for Angular components that
are *not* routable pages, so dashboard-style apps — one route, many components — can be
verified component-by-component instead of being crammed into a single Page Object.

Match the existing architecture and conventions exactly. Do not rewrite the generator;
extend it.

---

## 1. Why this is needed (the existing gap)

The tool already distinguishes pages from components via
`AngularComponentInfo.IsRoutable`. But today **every** component — routable or not — gets a
`BasePage`-derived Page Object whose locators are rooted on `page`. For a non-routable
component, `TemplateEngine.GeneratePageObject` emits a `navigate()` that simply throws:

```ts
async navigate(): Promise<void> {
  throw new Error('KpiCardComponent is not a routable component. Use it as a child component within a page.');
}
```

That is the exact seam to fill. A non-routable component should instead become a
**Component Object**:

- constructed from a **`root: Locator`** (the component host element), not from `Page`;
- every locator scoped under `this.root` (so the object works no matter where the
  component renders, and works when it renders **N times**);
- no `navigate()` / `goto()` — composition, not navigation;
- assertion-first (`expect*` helpers), since the use case is verification.

A Page Object then *vends* Component Objects instead of trying to own every descendant
element directly.

---

## 2. Repository orientation (read before editing)

Two projects, clean-architecture style:

- `src/PlaywrightPomGenerator.Cli`
  - `Program.cs` — host/DI bootstrap; `BuildRootCommand(IServiceProvider)` wires each
    command via `SetHandler`; `ConfigureServices(...)` registers services + handlers.
  - `Commands/Generate*Command.cs` — each file holds a `Command` subclass (args/options)
    **and** its `*Handler` (takes `IAngularAnalyzer`, `ICodeGenerator`, `ILogger`).
    `GenerateAppCommand.cs` is the canonical template to copy.
- `src/PlaywrightPomGenerator.Core`
  - `Abstractions/` — `IAngularAnalyzer`, `ICodeGenerator`, `ITemplateEngine`, `IFileSystem`.
  - `Services/` — `AngularAnalyzer`, `CodeGenerator`, `TemplateEngine`, `FileSystemService`.
  - `Models/` — `AngularComponentInfo`, `ElementSelector` (+ `SelectorStrategy` enum),
    `AngularProjectInfo`, `GenerationRequest`, `GeneratorOptions`, `GenerationResult`
    (+ `GeneratedFile`, `GeneratedFileType`).

Generation pipeline: a command handler calls `IAngularAnalyzer` to produce an
`AngularProjectInfo`, then `ICodeGenerator` to write files. `CodeGenerator` delegates the
actual TypeScript string building to `TemplateEngine` (`StringBuilder`-based), and writes
through `IFileSystem`. Page objects land in `page-objects/`, selectors in `helpers/`,
fixtures in `fixtures/`, configs in `configs/`, specs in `tests/`.

**Reuse these existing `TemplateEngine` helpers — do not duplicate their logic:**
`GenerateFileHeader`, `GetPageObjectClassName`, `GetLocatorMethod`,
`GenerateActionMethods`, `ToKebabCase`, `ToCamelCase`, `EscapeForJsString`, and the table
accessor emitters. Respect `GeneratorOptions` (`GenerateJsDocComments`, `FileHeader`,
`TestFileSuffix`, `DebugMode`, `DefaultTimeout`).

---

## 3. What to build

### 3.1 New CLI command — `component`

Create `src/PlaywrightPomGenerator.Cli/Commands/GenerateComponentCommand.cs`, modeled on
`GenerateAppCommand.cs`:

- `GenerateComponentCommand : Command` with name `"component"`, description
  *"Generate Playwright Component Object Model classes for Angular components (root-scoped, for composition inside pages)."*
  - `Argument<string> PathArgument` — path to the Angular app/library/folder.
  - `Option<string?> OutputOption` — `-o`/`--output`.
  - `Option<bool> ExcludeRoutableOption` — `--exclude-routable`: skip components where
    `IsRoutable == true` (default **false**: when the user explicitly asks for `component`,
    generate objects for everything discovered at the path).
- `GenerateComponentCommandHandler` — same shape/error handling as
  `GenerateAppCommandHandler`: validate the path (reuse `IsApplication`/`IsLibrary`, or
  fall back to `AnalyzeComponentsAtPathAsync` for arbitrary folders), analyze, call the new
  generator method, print generated files + warnings, return exit codes (0 / 1).

Wire it up in `Program.cs`:
- In `BuildRootCommand`, instantiate `GenerateComponentCommand`, `SetHandler` to resolve
  `GenerateComponentCommandHandler` from DI and call `ExecuteAsync`, and add it to the
  `RootCommand` collection.
- In `ConfigureServices`, register `services.AddTransient<GenerateComponentCommandHandler>();`.

### 3.2 New generation entry point — `ICodeGenerator` / `CodeGenerator`

Add to `ICodeGenerator`:

```csharp
Task<GenerationResult> GenerateComponentObjectsAsync(
    AngularProjectInfo project,
    string outputPath,
    bool excludeRoutable = false,
    CancellationToken cancellationToken = default);
```

Implement in `CodeGenerator`, mirroring `GenerateForApplicationAsync`:
- Resolve the full output path and create a **`component-objects/`** directory.
- Write the base class once via a new `GenerateBaseComponentFileAsync` →
  `component-objects/base.component.ts`.
- For each component (filtered by `excludeRoutable`), write
  `component-objects/{ToKebabCase(name)}.component.ts` from
  `TemplateEngine.GenerateComponentObject(component)`.
- Optionally emit a component-object spec per component (see 3.4) into `tests/`.
- Populate `GenerationResult` with `GeneratedFile`s (`RelativePath`, a fitting
  `GeneratedFileType` — add a `ComponentObject` enum member), warnings, and errors,
  consistent with the existing methods.

### 3.3 Template emission — `ITemplateEngine` / `TemplateEngine`

Add to `ITemplateEngine`: `string GenerateComponentObject(AngularComponentInfo component);`
and `string GenerateBaseComponent();`.

**Refactor for reuse instead of forking the emitter.** `GetLocatorMethod` and
`GenerateActionMethods` currently hard-code a `page.`-rooted base expression. Parameterize
them with a base-expression string (e.g. `"page"` for page objects, `"this.root"` for
component objects) and have the existing page-object path pass `"page"`. This keeps
selector strategy, action-method, and table-accessor logic single-sourced.

`GenerateComponentObject` should emit a class that:
- imports `Locator, expect` (and `Page` only if needed) from `@playwright/test`, plus
  `BaseComponent` from `./base.component`;
- is named via a new `GetComponentObjectClassName(name)` helper that maps the Angular class
  name to a `*ComponentObject` name (e.g. `KpiCardComponent` → `KpiCardComponentObject`),
  paralleling how `GetPageObjectClassName` maps `LoginComponent` → `LoginPage`;
- declares the same `readonly <prop>: Locator;` fields as the page-object path, but
  initialized with the **`this.root`-rooted** locator expressions;
- has **no** `navigate()` and **no** `componentSelector`/`goto`;
- exposes the component's host selector as a `static readonly hostSelector = '<selector>'`
  so callers/factories can build the root;
- includes the dynamic-content assertion helpers the tool already supports
  (`expect{X}Visible`, `expect{X}HasText`, `expect{X}ContainsText`, `get{X}Text`) and the
  click/fill action methods — all rooted on `this.root`;
- honors `GenerateJsDocComments` and `GenerateFileHeader` exactly as the page-object path.

`GenerateBaseComponent` should emit an abstract `BaseComponent` that is the root-scoped
analog of `BasePage` (see target output in §4): holds `protected readonly root: Locator`,
provides `expectVisible`/`expectHidden`/`isVisible`, a scoped `getByTestId`, and a
`getRoot()` accessor. It must **not** declare `navigate()`.

### 3.4 (Recommended) Component-object spec scaffold

Add `string GenerateComponentObjectTestSpec(AngularComponentInfo component);` and emit
`tests/{kebab}.component.{TestFileSuffix}.ts`. Unlike page specs, it must not call
`navigate()`. It should `goto` the component's *host page* (best-effort: the containing
routable page if known, else a `TODO` placeholder URL constant), construct the object from
`page.locator(HostObject.hostSelector)`, and assert `expectVisible()` plus one or two
generated `expect*` calls.

### 3.5 Optional integrations (additive, behind flags — do not change defaults)

- `GenerationRequest`: add `bool GenerateComponentObjects { get; init; }`, include it in
  `HasAnyGenerationOption` and `All(...)`, and honor it in `GenerateArtifactsAsync`.
- `GenerateArtifactsCommand`: add a `--component-objects` option mapped to the above.
- `app`/`workspace`: add an opt-in `--component-objects` (or
  `--non-routable-as-components`) flag so that, when set, non-routable components are
  emitted as Component Objects instead of throwing-`navigate()` Page Objects. **Default
  behavior must remain unchanged** to avoid a breaking release.

---

## 4. Target generated output (make the emitter produce this shape)

`component-objects/base.component.ts`:

```ts
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

`component-objects/kpi-card.component.ts` (example for `KpiCardComponent`, selector
`app-kpi-card`, with an interpolated value and a click target):

```ts
import { Locator, expect } from '@playwright/test';
import { BaseComponent } from './base.component';

/**
 * Component Object for the KpiCardComponent component.
 * Host selector: app-kpi-card
 */
export class KpiCardComponentObject extends BaseComponent {
  /** Host element selector. Use to build the root locator from a Page or parent. */
  static readonly hostSelector = 'app-kpi-card';

  readonly title: Locator;
  readonly value: Locator;
  readonly refreshButton: Locator;

  constructor(root: Locator) {
    super(root);
    this.title = this.root.getByTestId('kpi-title');
    this.value = this.root.getByTestId('kpi-value');
    this.refreshButton = this.root.getByRole('button', { name: 'Refresh' });
  }

  async clickRefreshButton(): Promise<void> {
    await this.refreshButton.click();
  }

  async expectValueVisible(): Promise<void> {
    await expect(this.value).toBeVisible();
  }

  async expectValueHasText(expected: string): Promise<void> {
    await expect(this.value).toHaveText(expected);
  }

  async expectValueContainsText(expected: string): Promise<void> {
    await expect(this.value).toContainText(expected);
  }

  async getValueText(): Promise<string> {
    return (await this.value.textContent()) ?? '';
  }
}
```

How a Page Object should vend Component Objects (illustrate in README, and emit when the
`app`/`workspace` integration flag is set):

```ts
// dashboard.page.ts (excerpt)
import { KpiCardComponentObject } from '../component-objects/kpi-card.component';

export class DashboardPage extends BasePage {
  /** A single KPI card scoped by index. */
  kpiCard(index = 0): KpiCardComponentObject {
    return new KpiCardComponentObject(
      this.page.locator(KpiCardComponentObject.hostSelector).nth(index),
    );
  }

  /** A KPI card scoped by its visible title (stable across reorders). */
  kpiCardByTitle(title: string): KpiCardComponentObject {
    return new KpiCardComponentObject(
      this.page.locator(KpiCardComponentObject.hostSelector).filter({ hasText: title }),
    );
  }
}
```

---

## 5. Naming & convention rules

- New command name: **`component`** (parallels `app`, `lib`, `workspace`).
- Output directory: **`component-objects/`**; base class file **`base.component.ts`**;
  per-component file **`{kebab}.component.ts`**. (These live in the `e2e/` output tree, not
  in Angular `src/`, so the `.component.ts` suffix won't collide with app source. If you
  prefer zero visual overlap, gate the suffix behind a `GeneratorOptions` setting rather
  than hard-coding an alternative.)
- TS class name: `*ComponentObject` (e.g. `FilterPanelComponentObject`).
- Reuse `ToKebabCase`/`ToCamelCase` for file and property names; reuse `GenerateFileHeader`
  and the `GenerateJsDocComments` toggle.

---

## 6. Edge cases to handle

- **Repeated components:** never assume a singleton root. The host selector + `nth(index)`
  and the `filter({ hasText })` accessors above must both be generated/illustrated. Prefer
  test-id/role/text scoping over index in examples.
- **`ng-content` projection:** components are the common case for content projection; the
  existing ng-content detection should still produce `expect*`/`get*Text` helpers, rooted
  on `this.root`.
- **Tables inside components:** route through the refactored, base-expression-parameterized
  table accessors so a `mat-table` inside a component is scoped to `this.root`, not `page`.
- **`@Output()` events:** these aren't directly observable from Playwright E2E. Don't invent
  assertions for them; at most emit a JSDoc note. (`Inputs`/`Outputs` already exist on
  `AngularComponentInfo`.)
- **No selectors found:** still emit the class with `static hostSelector`, `expectVisible`,
  and `expectHidden`, plus a generator warning (consistent with current warning behavior).
- **Nested component objects:** a component object's root is itself a `Locator`, so nested
  objects compose naturally — keep accessors returning child objects built from
  `this.root.locator(Child.hostSelector)`.

---

## 7. Backward compatibility & non-goals

- Purely additive. The new `component` command and the new `ICodeGenerator`/`ITemplateEngine`
  members must not alter the output of existing commands.
- The `app`/`workspace` "non-routable as component objects" behavior is **opt-in** only.
- Do not add third-party dependencies. Stay on System.CommandLine + the existing DI/host
  setup. Keep models as `record`s and follow the existing nullable/`required` style.

---

## 8. Tests, docs, versioning (definition of done)

- Add unit tests under `tests/PlaywrightPomGenerator.Tests` mirroring existing generator
  tests: assert that a non-routable component yields a `*.component.ts` whose locators are
  rooted on `this.root`, that `base.component.ts` is emitted once, that `static hostSelector`
  is present, that no `navigate()`/`goto` appears, and that `--exclude-routable` filters
  routable components. Add a CLI-level test that `ppg component <path>` exits 0 and lists the
  expected files.
- Update `README.md`: add the `component` command to the Commands section, add a
  "Generated Component Objects" subsection with the §4 base/child examples and the
  page-vends-component pattern, and note the `component-objects/` directory in the
  Generated Output tree.
- Update `CHANGELOG.md` and bump `GeneratorOptions.ToolVersion`.

---

## 9. Suggested order of work

1. Refactor `GetLocatorMethod` / `GenerateActionMethods` (and table emitters) to take a base
   expression; verify existing page-object output is byte-identical via tests.
2. Add `GenerateBaseComponent` + `GenerateComponentObject` (+ `GetComponentObjectClassName`)
   to `TemplateEngine`/`ITemplateEngine`.
3. Add `GenerateComponentObjectsAsync` to `ICodeGenerator`/`CodeGenerator` and the
   `ComponentObject` `GeneratedFileType`.
4. Add `GenerateComponentCommand` + handler; wire into `Program.cs` (`BuildRootCommand` +
   `ConfigureServices`).
5. Add the optional spec scaffold and the opt-in `artifacts`/`app`/`workspace` flags.
6. Tests, README, CHANGELOG, version bump.

Report back with the list of files created/changed and any analyzer assumptions you had to
make about how the host page for a component spec is resolved.
