using System.Text;
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Renders code templates for Playwright Page Object Model files.
/// </summary>
public sealed class TemplateEngine : ITemplateEngine
{
    private readonly GeneratorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateEngine"/> class.
    /// </summary>
    /// <param name="options">The generator options.</param>
    public TemplateEngine(IOptions<GeneratorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public string GenerateFileHeader(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        if (string.IsNullOrEmpty(_options.FileHeader))
        {
            return string.Empty;
        }

        return _options.FileHeader
            .Replace("{FileName}", fileName)
            .Replace("{GeneratedDate}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"))
            .Replace("{ToolVersion}", _options.ToolVersion);
    }

    /// <inheritdoc />
    public string GeneratePageObject(AngularComponentInfo component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var sb = new StringBuilder();
        var className = GetPageObjectClassName(component.Name);
        var fileName = $"{ToKebabCase(component.Name)}.page.ts";

        var header = GenerateFileHeader(fileName);
        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }
        sb.AppendLine("import { Page, Locator } from '@playwright/test';");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * Page Object for the {component.Name} component.");
            sb.AppendLine($" * Selector: {component.Selector}");
            sb.AppendLine(" */");
        }

        sb.AppendLine($"export class {className} {{");
        sb.AppendLine("  readonly page: Page;");
        sb.AppendLine();

        // Generate selector properties
        foreach (var selector in component.Selectors)
        {
            if (_options.GenerateJsDocComments)
            {
                sb.AppendLine("  /**");
                sb.AppendLine($"   * Locator for the {selector.PropertyName} element.");
                sb.AppendLine($"   * Element type: {selector.ElementType}");
                sb.AppendLine($"   * Strategy: {selector.Strategy}");
                sb.AppendLine("   */");
            }
            sb.AppendLine($"  readonly {ToCamelCase(selector.PropertyName)}: Locator;");
        }

        sb.AppendLine();

        // Constructor
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine($"   * Creates a new instance of {className}.");
            sb.AppendLine("   * @param page - The Playwright page instance.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  constructor(page: Page) {");
        sb.AppendLine("    this.page = page;");

        foreach (var selector in component.Selectors)
        {
            var locatorMethod = GetLocatorMethod(selector);
            sb.AppendLine($"    this.{ToCamelCase(selector.PropertyName)} = {locatorMethod};");
        }

        sb.AppendLine("  }");
        sb.AppendLine();

        // Navigation method if route is available
        if (!string.IsNullOrEmpty(component.RoutePath))
        {
            if (_options.GenerateJsDocComments)
            {
                sb.AppendLine("  /**");
                sb.AppendLine("   * Navigates to this page.");
                sb.AppendLine("   * @returns Promise that resolves when navigation is complete.");
                sb.AppendLine("   */");
            }
            sb.AppendLine("  async goto(): Promise<void> {");
            sb.AppendLine($"    await this.page.goto('/{component.RoutePath}');");
            sb.AppendLine("  }");
            sb.AppendLine();
        }

        // Generate action methods for common element types
        GenerateActionMethods(sb, component.Selectors);

        // Wait for load method
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Waits for the page to be fully loaded.");
            sb.AppendLine("   * @returns Promise that resolves when the page is loaded.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  async waitForLoad(): Promise<void> {");
        sb.AppendLine($"    await this.page.waitForSelector('{component.Selector}');");
        sb.AppendLine("  }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateSelectors(AngularComponentInfo component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var sb = new StringBuilder();
        var fileName = $"{ToKebabCase(component.Name)}.selectors.ts";

        var header = GenerateFileHeader(fileName);
        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * Selectors for the {component.Name} component.");
            sb.AppendLine(" */");
        }

        sb.AppendLine($"export const {ToCamelCase(component.Name)}Selectors = {{");

        foreach (var selector in component.Selectors)
        {
            if (_options.GenerateJsDocComments)
            {
                sb.AppendLine($"  /** Selector for {selector.PropertyName} ({selector.ElementType}) */");
            }
            sb.AppendLine($"  {ToCamelCase(selector.PropertyName)}: {FormatSelectorValueForJs(selector.SelectorValue)},");
        }

        sb.AppendLine("} as const;");

        return sb.ToString();
    }

    private static string FormatSelectorValueForJs(string value)
    {
        // If the value contains single quotes, use double quotes
        if (value.Contains('\''))
        {
            return $"\"{value}\"";
        }
        return $"'{value}'";
    }

    /// <inheritdoc />
    public string GenerateFixture(AngularProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var sb = new StringBuilder();
        var fileName = "fixtures.ts";

        var header = GenerateFileHeader(fileName);
        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }
        sb.AppendLine("import { test as base } from '@playwright/test';");

        // Import page objects
        foreach (var component in project.Components)
        {
            var className = GetPageObjectClassName(component.Name);
            var importPath = $"./pages/{ToKebabCase(component.Name)}.page";
            sb.AppendLine($"import {{ {className} }} from '{importPath}';");
        }

        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Custom fixture type extending Playwright's test fixtures.");
            sb.AppendLine(" */");
        }

        sb.AppendLine("type Fixtures = {");
        foreach (var component in project.Components)
        {
            var className = GetPageObjectClassName(component.Name);
            var propName = ToCamelCase(className);
            sb.AppendLine($"  {propName}: {className};");
        }
        sb.AppendLine("};");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Extended test object with page object fixtures.");
            sb.AppendLine(" */");
        }

        sb.AppendLine("export const test = base.extend<Fixtures>({");

        foreach (var component in project.Components)
        {
            var className = GetPageObjectClassName(component.Name);
            var propName = ToCamelCase(className);
            sb.AppendLine($"  {propName}: async ({{ page }}, use) => {{");
            sb.AppendLine($"    await use(new {className}(page));");
            sb.AppendLine("  },");
        }

        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("export { expect } from '@playwright/test';");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateConfig(AngularProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var sb = new StringBuilder();
        var fileName = "playwright.config.ts";

        var header = GenerateFileHeader(fileName);
        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }
        sb.AppendLine("import { defineConfig, devices } from '@playwright/test';");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * Playwright configuration for {project.Name}.");
            sb.AppendLine(" * @see https://playwright.dev/docs/test-configuration");
            sb.AppendLine(" */");
        }

        sb.AppendLine("export default defineConfig({");
        sb.AppendLine("  testDir: './tests',");
        sb.AppendLine("  fullyParallel: true,");
        sb.AppendLine("  forbidOnly: !!process.env.CI,");
        sb.AppendLine("  retries: process.env.CI ? 2 : 0,");
        sb.AppendLine("  workers: process.env.CI ? 1 : undefined,");
        sb.AppendLine("  reporter: 'html',");
        sb.AppendLine("  use: {");
        sb.AppendLine($"    baseURL: '{_options.BaseUrlPlaceholder}',");
        sb.AppendLine("    trace: 'on-first-retry',");
        sb.AppendLine($"    actionTimeout: {_options.DefaultTimeout},");
        sb.AppendLine("  },");
        sb.AppendLine("  projects: [");
        sb.AppendLine("    {");
        sb.AppendLine("      name: 'chromium',");
        sb.AppendLine("      use: { ...devices['Desktop Chrome'] },");
        sb.AppendLine("    },");
        sb.AppendLine("    {");
        sb.AppendLine("      name: 'firefox',");
        sb.AppendLine("      use: { ...devices['Desktop Firefox'] },");
        sb.AppendLine("    },");
        sb.AppendLine("    {");
        sb.AppendLine("      name: 'webkit',");
        sb.AppendLine("      use: { ...devices['Desktop Safari'] },");
        sb.AppendLine("    },");
        sb.AppendLine("  ],");
        sb.AppendLine("  webServer: {");
        sb.AppendLine("    command: 'npm start',");
        sb.AppendLine($"    url: '{_options.BaseUrlPlaceholder}',");
        sb.AppendLine("    reuseExistingServer: !process.env.CI,");
        sb.AppendLine("  },");
        sb.AppendLine("});");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateHelpers()
    {
        var sb = new StringBuilder();
        var fileName = "helpers.ts";

        var header = GenerateFileHeader(fileName);
        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }
        sb.AppendLine("import { Page, Locator, expect } from '@playwright/test';");
        sb.AppendLine();

        // Wait utilities
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Waits for an element to be visible and stable.");
            sb.AppendLine(" * @param locator - The element locator.");
            sb.AppendLine(" * @param timeout - Optional timeout in milliseconds.");
            sb.AppendLine(" * @returns Promise that resolves when the element is stable.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export async function waitForStable(locator: Locator, timeout?: number): Promise<void> {");
        sb.AppendLine("  await locator.waitFor({ state: 'visible', timeout });");
        sb.AppendLine("  await expect(locator).toBeVisible();");
        sb.AppendLine("}");
        sb.AppendLine();

        // Fill and verify
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Fills an input and verifies the value was set.");
            sb.AppendLine(" * @param locator - The input locator.");
            sb.AppendLine(" * @param value - The value to fill.");
            sb.AppendLine(" * @returns Promise that resolves when the input is filled.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export async function fillAndVerify(locator: Locator, value: string): Promise<void> {");
        sb.AppendLine("  await locator.fill(value);");
        sb.AppendLine("  await expect(locator).toHaveValue(value);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Click and wait for navigation
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Clicks an element and waits for navigation.");
            sb.AppendLine(" * @param page - The page instance.");
            sb.AppendLine(" * @param locator - The element locator to click.");
            sb.AppendLine(" * @param urlPattern - Optional URL pattern to wait for.");
            sb.AppendLine(" * @returns Promise that resolves when navigation is complete.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export async function clickAndWaitForNavigation(");
        sb.AppendLine("  page: Page,");
        sb.AppendLine("  locator: Locator,");
        sb.AppendLine("  urlPattern?: string | RegExp");
        sb.AppendLine("): Promise<void> {");
        sb.AppendLine("  await Promise.all([");
        sb.AppendLine("    urlPattern ? page.waitForURL(urlPattern) : page.waitForNavigation(),");
        sb.AppendLine("    locator.click(),");
        sb.AppendLine("  ]);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Retry utility
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Retries an action with exponential backoff.");
            sb.AppendLine(" * @param action - The action to retry.");
            sb.AppendLine(" * @param maxRetries - Maximum number of retries.");
            sb.AppendLine(" * @param baseDelay - Base delay in milliseconds.");
            sb.AppendLine(" * @returns Promise that resolves with the action result.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export async function retry<T>(");
        sb.AppendLine("  action: () => Promise<T>,");
        sb.AppendLine("  maxRetries: number = 3,");
        sb.AppendLine("  baseDelay: number = 1000");
        sb.AppendLine("): Promise<T> {");
        sb.AppendLine("  let lastError: Error | undefined;");
        sb.AppendLine("  for (let i = 0; i < maxRetries; i++) {");
        sb.AppendLine("    try {");
        sb.AppendLine("      return await action();");
        sb.AppendLine("    } catch (error) {");
        sb.AppendLine("      lastError = error as Error;");
        sb.AppendLine("      if (i < maxRetries - 1) {");
        sb.AppendLine("        await new Promise(resolve => setTimeout(resolve, baseDelay * Math.pow(2, i)));");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("  throw lastError;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Screenshot on failure helper
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Takes a screenshot with a descriptive name.");
            sb.AppendLine(" * @param page - The page instance.");
            sb.AppendLine(" * @param name - The screenshot name.");
            sb.AppendLine(" * @returns Promise that resolves with the screenshot path.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export async function takeScreenshot(page: Page, name: string): Promise<string> {");
        sb.AppendLine("  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');");
        sb.AppendLine("  const path = `screenshots/${name}-${timestamp}.png`;");
        sb.AppendLine("  await page.screenshot({ path, fullPage: true });");
        sb.AppendLine("  return path;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateTestSpec(AngularComponentInfo component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var sb = new StringBuilder();
        var className = GetPageObjectClassName(component.Name);
        var fileName = $"{ToKebabCase(component.Name)}.{_options.TestFileSuffix}.ts";

        var header = GenerateFileHeader(fileName);
        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }
        sb.AppendLine("import { test, expect } from '../fixtures';");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * Tests for the {component.Name} component.");
            sb.AppendLine(" */");
        }

        sb.AppendLine($"test.describe('{component.Name}', () => {{");
        sb.AppendLine();

        // Basic visibility test
        sb.AppendLine($"  test('should display the component', async ({{ {ToCamelCase(className)} }}) => {{");
        sb.AppendLine($"    await {ToCamelCase(className)}.waitForLoad();");
        sb.AppendLine("  });");

        // Generate tests for each selector
        foreach (var selector in component.Selectors)
        {
            sb.AppendLine();

            if (_options.GenerateJsDocComments)
            {
                sb.AppendLine($"  /** Tests that {selector.PropertyName} is visible */");
            }

            sb.AppendLine($"  test('{selector.PropertyName} should be visible', async ({{ {ToCamelCase(className)} }}) => {{");
            sb.AppendLine($"    await expect({ToCamelCase(className)}.{ToCamelCase(selector.PropertyName)}).toBeVisible();");
            sb.AppendLine("  });");
        }

        sb.AppendLine("});");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateSignalRMock()
    {
        var sb = new StringBuilder();
        var fileName = "signalr-mock.fixture.ts";

        var header = GenerateFileHeader(fileName);
        if (!string.IsNullOrEmpty(header))
        {
            sb.AppendLine(header);
            sb.AppendLine();
        }
        sb.AppendLine("import { Subject, Observable, BehaviorSubject, ReplaySubject } from 'rxjs';");
        sb.AppendLine("import { filter, map, takeUntil } from 'rxjs/operators';");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Message type for SignalR hub messages.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export interface HubMessage<T = unknown> {");
        sb.AppendLine("  methodName: string;");
        sb.AppendLine("  args: T[];");
        sb.AppendLine("}");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Connection state enumeration.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export enum HubConnectionState {");
        sb.AppendLine("  Disconnected = 'Disconnected',");
        sb.AppendLine("  Connecting = 'Connecting',");
        sb.AppendLine("  Connected = 'Connected',");
        sb.AppendLine("  Disconnecting = 'Disconnecting',");
        sb.AppendLine("  Reconnecting = 'Reconnecting',");
        sb.AppendLine("}");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Mock SignalR Hub Connection using RxJS.");
            sb.AppendLine(" * Provides a fully functional mock for testing SignalR interactions.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export class MockHubConnection {");
        sb.AppendLine("  private readonly _messages$ = new Subject<HubMessage>();");
        sb.AppendLine("  private readonly _state$ = new BehaviorSubject<HubConnectionState>(HubConnectionState.Disconnected);");
        sb.AppendLine("  private readonly _errors$ = new Subject<Error>();");
        sb.AppendLine("  private readonly _destroy$ = new Subject<void>();");
        sb.AppendLine("  private readonly _handlers = new Map<string, Set<(...args: unknown[]) => void>>();");
        sb.AppendLine("  private readonly _invocations$ = new ReplaySubject<HubMessage>(100);");
        sb.AppendLine();

        // State property
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Observable of the current connection state.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  get state$(): Observable<HubConnectionState> {");
        sb.AppendLine("    return this._state$.asObservable();");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Current state property
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Gets the current connection state.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  get state(): HubConnectionState {");
        sb.AppendLine("    return this._state$.getValue();");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Errors observable
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Observable of connection errors.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  get errors$(): Observable<Error> {");
        sb.AppendLine("    return this._errors$.asObservable();");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Invocations observable
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Observable of all method invocations (for assertions).");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  get invocations$(): Observable<HubMessage> {");
        sb.AppendLine("    return this._invocations$.asObservable();");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Start method
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Starts the mock connection.");
            sb.AppendLine("   * @returns Observable that emits when connected.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  start(): Observable<void> {");
        sb.AppendLine("    return new Observable(observer => {");
        sb.AppendLine("      this._state$.next(HubConnectionState.Connecting);");
        sb.AppendLine("      setTimeout(() => {");
        sb.AppendLine("        this._state$.next(HubConnectionState.Connected);");
        sb.AppendLine("        observer.next();");
        sb.AppendLine("        observer.complete();");
        sb.AppendLine("      }, 0);");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Stop method
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Stops the mock connection.");
            sb.AppendLine("   * @returns Observable that emits when disconnected.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  stop(): Observable<void> {");
        sb.AppendLine("    return new Observable(observer => {");
        sb.AppendLine("      this._state$.next(HubConnectionState.Disconnecting);");
        sb.AppendLine("      setTimeout(() => {");
        sb.AppendLine("        this._state$.next(HubConnectionState.Disconnected);");
        sb.AppendLine("        observer.next();");
        sb.AppendLine("        observer.complete();");
        sb.AppendLine("      }, 0);");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine();

        // On method
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Registers a handler for the specified method.");
            sb.AppendLine("   * @param methodName - The method name to listen for.");
            sb.AppendLine("   * @param handler - The handler function.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  on<T extends unknown[]>(methodName: string, handler: (...args: T) => void): void {");
        sb.AppendLine("    if (!this._handlers.has(methodName)) {");
        sb.AppendLine("      this._handlers.set(methodName, new Set());");
        sb.AppendLine("    }");
        sb.AppendLine("    this._handlers.get(methodName)!.add(handler as (...args: unknown[]) => void);");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Off method
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Removes a handler for the specified method.");
            sb.AppendLine("   * @param methodName - The method name.");
            sb.AppendLine("   * @param handler - The handler to remove.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  off<T extends unknown[]>(methodName: string, handler: (...args: T) => void): void {");
        sb.AppendLine("    this._handlers.get(methodName)?.delete(handler as (...args: unknown[]) => void);");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Stream method for RxJS
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Returns an observable stream for the specified method.");
            sb.AppendLine("   * @param methodName - The method name to stream.");
            sb.AppendLine("   * @returns Observable of method arguments.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  stream<T>(methodName: string): Observable<T> {");
        sb.AppendLine("    return this._messages$.pipe(");
        sb.AppendLine("      takeUntil(this._destroy$),");
        sb.AppendLine("      filter(msg => msg.methodName === methodName),");
        sb.AppendLine("      map(msg => msg.args[0] as T)");
        sb.AppendLine("    );");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Invoke method
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Invokes a method on the hub.");
            sb.AppendLine("   * @param methodName - The method name to invoke.");
            sb.AppendLine("   * @param args - The method arguments.");
            sb.AppendLine("   * @returns Observable that emits the result.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  invoke<T>(methodName: string, ...args: unknown[]): Observable<T> {");
        sb.AppendLine("    return new Observable(observer => {");
        sb.AppendLine("      if (this._state$.getValue() !== HubConnectionState.Connected) {");
        sb.AppendLine("        observer.error(new Error('Cannot invoke method: connection is not in the Connected state.'));");
        sb.AppendLine("        return;");
        sb.AppendLine("      }");
        sb.AppendLine("      this._invocations$.next({ methodName, args });");
        sb.AppendLine("      // Simulate async response");
        sb.AppendLine("      setTimeout(() => {");
        sb.AppendLine("        observer.next(undefined as T);");
        sb.AppendLine("        observer.complete();");
        sb.AppendLine("      }, 0);");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Send method
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Sends a message without expecting a response.");
            sb.AppendLine("   * @param methodName - The method name to send.");
            sb.AppendLine("   * @param args - The method arguments.");
            sb.AppendLine("   * @returns Observable that completes when sent.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  send(methodName: string, ...args: unknown[]): Observable<void> {");
        sb.AppendLine("    return this.invoke<void>(methodName, ...args);");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Test helper: simulate server message
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Simulates receiving a message from the server (for testing).");
            sb.AppendLine("   * @param methodName - The method name.");
            sb.AppendLine("   * @param args - The message arguments.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  simulateServerMessage<T extends unknown[]>(methodName: string, ...args: T): void {");
        sb.AppendLine("    this._messages$.next({ methodName, args });");
        sb.AppendLine("    const handlers = this._handlers.get(methodName);");
        sb.AppendLine("    handlers?.forEach(handler => handler(...args));");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Test helper: simulate error
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Simulates a connection error (for testing).");
            sb.AppendLine("   * @param error - The error to simulate.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  simulateError(error: Error): void {");
        sb.AppendLine("    this._errors$.next(error);");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Test helper: simulate reconnect
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Simulates a reconnection (for testing).");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  simulateReconnect(): void {");
        sb.AppendLine("    this._state$.next(HubConnectionState.Reconnecting);");
        sb.AppendLine("    setTimeout(() => {");
        sb.AppendLine("      this._state$.next(HubConnectionState.Connected);");
        sb.AppendLine("    }, 0);");
        sb.AppendLine("  }");
        sb.AppendLine();

        // Dispose
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /**");
            sb.AppendLine("   * Disposes the mock connection and cleans up resources.");
            sb.AppendLine("   */");
        }
        sb.AppendLine("  dispose(): void {");
        sb.AppendLine("    this._destroy$.next();");
        sb.AppendLine("    this._destroy$.complete();");
        sb.AppendLine("    this._messages$.complete();");
        sb.AppendLine("    this._state$.complete();");
        sb.AppendLine("    this._errors$.complete();");
        sb.AppendLine("    this._invocations$.complete();");
        sb.AppendLine("    this._handlers.clear();");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Factory function
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("/**");
            sb.AppendLine(" * Creates a new mock hub connection.");
            sb.AppendLine(" * @returns A new MockHubConnection instance.");
            sb.AppendLine(" */");
        }
        sb.AppendLine("export function createMockHubConnection(): MockHubConnection {");
        sb.AppendLine("  return new MockHubConnection();");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateActionMethods(StringBuilder sb, IReadOnlyList<ElementSelector> selectors)
    {
        var buttons = selectors.Where(s => s.ElementType == "button").ToList();
        var inputs = selectors.Where(s => s.ElementType is "input" or "textarea").ToList();

        foreach (var button in buttons)
        {
            if (_options.GenerateJsDocComments)
            {
                sb.AppendLine("  /**");
                sb.AppendLine($"   * Clicks the {button.PropertyName} button.");
                sb.AppendLine("   * @returns Promise that resolves when the click is complete.");
                sb.AppendLine("   */");
            }
            sb.AppendLine($"  async click{button.PropertyName}(): Promise<void> {{");
            sb.AppendLine($"    await this.{ToCamelCase(button.PropertyName)}.click();");
            sb.AppendLine("  }");
            sb.AppendLine();
        }

        foreach (var input in inputs)
        {
            if (_options.GenerateJsDocComments)
            {
                sb.AppendLine("  /**");
                sb.AppendLine($"   * Fills the {input.PropertyName} input with the specified value.");
                sb.AppendLine("   * @param value - The value to fill.");
                sb.AppendLine("   * @returns Promise that resolves when the input is filled.");
                sb.AppendLine("   */");
            }
            sb.AppendLine($"  async fill{input.PropertyName}(value: string): Promise<void> {{");
            sb.AppendLine($"    await this.{ToCamelCase(input.PropertyName)}.fill(value);");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
    }

    private static string GetLocatorMethod(ElementSelector selector)
    {
        return selector.Strategy switch
        {
            SelectorStrategy.TestId => $"page.getByTestId('{EscapeForJsString(selector.SelectorValue.Replace("[data-testid='", "").Replace("']", ""))}')",
            SelectorStrategy.Id => $"page.locator('{EscapeForJsString(selector.SelectorValue)}')",
            SelectorStrategy.Role when selector.ElementType == "button" => $"page.getByRole('button', {{ name: '{EscapeForJsString(selector.TextContent ?? "")}' }})",
            SelectorStrategy.Text => $"page.getByText('{EscapeForJsString(selector.TextContent ?? "")}')",
            SelectorStrategy.Placeholder => $"page.getByPlaceholder('{EscapeForJsString(selector.TextContent ?? "")}')",
            SelectorStrategy.Label => $"page.getByLabel('{EscapeForJsString(selector.TextContent ?? "")}')",
            _ => FormatLocatorWithQuotes(selector.SelectorValue)
        };
    }

    private static string FormatLocatorWithQuotes(string selectorValue)
    {
        // If the selector contains single quotes, wrap in double quotes
        if (selectorValue.Contains('\''))
        {
            return $"page.locator(\"{selectorValue}\")";
        }
        return $"page.locator('{selectorValue}')";
    }

    private static string EscapeForJsString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private static string GetPageObjectClassName(string componentName)
    {
        var name = componentName.EndsWith("Component", StringComparison.Ordinal)
            ? componentName[..^"Component".Length]
            : componentName;
        return $"{name}Page";
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        return char.ToLowerInvariant(input[0]) + input[1..];
    }

    private static string ToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    result.Append('-');
                }
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        // Remove "Component" suffix if present
        var str = result.ToString();
        if (str.EndsWith("-component", StringComparison.Ordinal))
        {
            str = str[..^"-component".Length];
        }

        return str;
    }
}
