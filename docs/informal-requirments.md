1. The generated playwright library shall generate the following folders
    - page-objects
    - helpers
    - tests
    - configs
    - fixtures

2. The generated playwright library shall generate the following root-level files
    - playwright.config.ts
    - helpers.ts

3. The configs folder shall contain the following configuration files
    - timeout.config.ts - Standard timeout constants for navigation, API, element visibility, and SignalR
    - urls.config.ts - URL constants and API endpoint patterns for route mocking

4. The fixtures folder shall contain the following files
    - fixtures.ts - Extended Playwright test with custom page object fixtures

5. The CLI tool shall support the following commands
    - app - Generate POM tests for a single Angular application
    - workspace - Generate POM tests for an Angular workspace (includes all applications and libraries)
    - lib - Generate POM tests for a single Angular library
    - artifacts - Generate specific artifacts (fixtures, configs, selectors, page objects, helpers)
    - signalr-mock - Generate a SignalR mock fixture using RxJS

6. The CLI tool shall support the following global options
    - --header - Custom header template for generated files with placeholders ({FileName}, {GeneratedDate}, {ToolVersion})
    - --test-suffix - Custom suffix for test files (default: 'spec')
    - --output / -o - Output directory for generated files

7. The Angular analyzer shall detect and analyze the following project types
    - Angular applications (projectType: "application" in angular.json)
    - Angular libraries (projectType: "library" in angular.json or ng-package.json present)
    - Angular workspaces (angular.json present)

8. The Angular analyzer shall extract the following information from components
    - Component class name
    - Component selector
    - Component file path
    - Template file path
    - @Input() properties
    - @Output() properties
    - Template selectors (data-testid, id, formControlName, buttons with text)

9. The Angular analyzer shall extract the following information from routing
    - Route paths
    - Associated components
    - Redirect routes
    - Lazy-loaded routes (loadChildren, loadComponent)

10. The selector parser shall support the following selector strategies in priority order
    - data-testid attributes (highest priority)
    - id attributes
    - Role-based selectors (buttons with text)
    - CSS selectors (formControlName)
    - Elements with text content (e.g., `<m-foo>Text</m-foo>`)
    - Click handlers `(click)="method()"`
    - Router links (routerLink, href)
    - Angular Material components (mat-button, mat-raised-button, mat-form-field, etc.)
    - Angular CDK components
    - Tables and mat-tables

11. For each Angular component, the generator shall create the following files in respective folders
    - page-objects/base.page.ts - Base page class that all page objects extend from
    - page-objects/{component-name}.page.ts - Page object with element locators and actions
    - helpers/{component-name}.selectors.ts - Selector constants
    - tests/{component-name}.{test-suffix}.ts - Test specification file

12. The page object files shall include
    - Import statements for Playwright Page, Locator and expect
    - Import of BasePage from ./base.page
    - Class extending BasePage
    - Constructor with page parameter typed as Page (not inline import)
    - Constructor calling super(page)
    - Implementation of abstract navigate() method
    - Implementation of abstract waitForLoad() method
    - Getter methods for each detected element
    - Action methods for common interactions (click, fill, etc.)
    - expect{Element}Visible() methods for all buttons, text elements, and tables
    - Click methods for elements with (click) handlers
    - Table accessor methods (rows, columns, headers, row click)

13. The selector files shall include
    - Exported constants for each detected selector
    - Selector values using appropriate strategies (getByTestId, locator, etc.)

14. The test specification files shall include
    - Import statements for test fixtures from fixtures/fixtures.ts
    - Describe block with component name
    - Basic test case for component visibility
    - Placeholder for additional test cases

15. The fixtures/fixtures.ts file shall include
    - Extended Playwright test with page object fixtures (no explicit Fixtures type)
    - Page object imports from ../page-objects/
    - Page object instances for each component
    - TypeScript type inference (no custom type declarations)

16. The helpers.ts file shall include
    - Common utility functions for tests
    - Wait helpers
    - Assertion helpers
    - Navigation helpers

17. The playwright.config.ts file shall include
    - Base URL configuration
    - Browser configurations (chromium, firefox, webkit)
    - Test directory configuration
    - Reporter configuration
    - Timeout settings

18. The timeout.config.ts file shall include
    - Navigation timeout (30000ms)
    - API response timeout (5000ms)
    - API slow response timeout (15000ms)
    - Element visible timeout (10000ms)
    - Element hidden timeout (5000ms)
    - SignalR connection timeout (15000ms)
    - SignalR connection failure timeout (35000ms)

19. The urls.config.ts file shall include
    - Base URL with environment variable support
    - URL entries for each routable page component (URLS object)
    - API endpoint patterns using glob syntax for route mocking
    - SignalR hub endpoint patterns

19a. A component is considered routable (a page) if any of these conditions are met:
    - The file path contains /pages/ or /page/ directory
    - The file path contains /features/ directory
    - The file path contains /views/ or /screens/ directory
    - The component class name ends with "Page" or "PageComponent"

19b. A component is NOT routable if the class name contains any of these patterns:
    - dialog, modal, button, link, nav, avatar, toolbar, tab
    - header, footer, sidebar, sidenav, menu, dropdown, tooltip
    - spinner, loader, icon, badge, chip, card, list, item
    - form-field, input, select, checkbox, radio, toggle, switch
    - snackbar, toast, alert, banner, notification, popover
    - breadcrumb, pagination, stepper, progress, skeleton
    - divider, spacer, container, wrapper, layout, grid
    - table, row, cell, column, panel, accordion, expansion
    - fab, action, search, filter, sort

19c. Routable page object models shall:
    - Import URLS from ../configs/urls.config
    - Use URLS.{componentName} in the navigate() method instead of hardcoded paths

19d. Non-routable page object models shall:
    - NOT import URLS config
    - Throw an error in the navigate() method with message: "{ClassName} is not a routable component. Use it as a child component within a page."

20. The SignalR mock fixture shall provide
    - RxJS-based observable streams (not promises)
    - Connection state management (connecting, connected, disconnected, reconnecting)
    - Method invocation tracking
    - Server message simulation
    - Error simulation
    - Reconnection simulation
    - Hub method registration and invocation

21. The workspace command shall support
    - Analyzing all projects in an Angular workspace
    - Filtering by specific project name with -p/--project option
    - Including both applications and libraries
    - Generating separate output folders per project

22. The artifacts command shall support generating individual artifact types
    - --fixtures / -f - Generate test fixtures only
    - --configs / -c - Generate Playwright configuration only
    - --selectors / -s - Generate selector files only
    - --page-objects - Generate page object files only
    - --helpers - Generate helper utilities only
    - --all / -a - Generate all artifacts

23. The tool shall be distributed as a .NET global tool
    - Package name: PlaywrightPomGenerator
    - Command name: ppg
    - Published to NuGet.org
    - Support for .NET 8.0 and later (RollForward: LatestMajor)

24. The generated TypeScript code shall be
    - Properly formatted and indented
    - Using modern ES2020+ syntax
    - Strongly typed with TypeScript
    - Compatible with Playwright Test framework
    - Free of TypeScript compilation errors

25. The tool shall provide appropriate error handling for
    - Invalid Angular workspace/application paths
    - Missing angular.json file
    - Missing ng-package.json for libraries
    - File system access errors
    - Component parsing failures

26. The tool shall provide informative console output including
    - Progress information during analysis
    - Count of discovered components and routes
    - List of generated files
    - Warnings for potential issues
    - Error messages with actionable guidance

27. The generator options shall be configurable via
    - Command line arguments (highest priority)
    - appsettings.json configuration file
    - Environment variables with POMGEN_ prefix
    - Default values (lowest priority)

28. The tool shall support modern Angular patterns including
    - Standalone components
    - Traditional module-based components
    - Signal-based inputs and outputs
    - Modern routing with loadComponent
    - Both .component.ts suffix and non-suffixed component files

29. The selector parser shall detect and create selectors for Angular Material components
    - mat-button, mat-raised-button, mat-flat-button, mat-stroked-button
    - mat-icon-button, mat-fab, mat-mini-fab
    - mat-form-field with mat-label
    - mat-table with mat-header-cell, mat-cell, mat-row
    - MatDialog components

30. The page object generator shall create table accessor methods including
    - getTableRows() - Returns locator for all rows
    - getTableRow(index) - Returns locator for specific row
    - getTableHeaders() - Returns locator for header cells
    - getTableColumn(index) - Returns locator for cells in a column
    - getTableRowCount() - Returns count of rows
    - clickTableRow(index) - Clicks on a specific row
    - expectTableVisible() - Asserts table is visible

31. The page object generator shall create expect{Element}Visible() methods for
    - All buttons (including mat-buttons)
    - All text elements with content
    - All tables (including mat-tables)
    - All elements with click handlers

32. The selector parser shall detect click handlers and navigation
    - (click)="methodName()" event bindings
    - routerLink directives
    - href attributes
    - Generate click action methods for these elements

33. The tool shall support @microsoft/signalr patterns
    - Detect SignalR hub connections in TypeScript code
    - Generate appropriate mocking fixtures using RxJS
    - Support WebSocket protocol-level mocking
    - Provide connection state management helpers

34. The page-objects/base.page.ts file shall include
    - Abstract BasePage class that all page objects extend from
    - Protected page property of type Page
    - Abstract navigate() method for page navigation
    - Abstract waitForLoad() method for page load verification
    - getPageTitle() method returning the page title
    - waitForNavigation() method using networkidle state with TIMEOUTS.navigation
    - getLocator(selector) method for creating locators
    - isVisible(selector) method for visibility checks
    - waitForVisible(selector, timeout?) method with TIMEOUTS.elementVisible default
    - waitForHidden(selector, timeout?) method with TIMEOUTS.elementHidden default
    - click(selector) method for element clicks
    - getTextContent(selector) method for text extraction
    - getCount(selector) method for element counting
    - Import of TIMEOUTS from ../configs/timeout.config

35. The selector parser shall detect and create selectors for text elements
    - h1, h2, h3, h4, h5, h6 (headings)
    - p (paragraphs)
    - span (inline text)
    - label (form labels)
    - strong, em, b, i (emphasis)
    - small, mark, sub, sup (text formatting)
    - blockquote, cite, q (quotations)
    - code, pre (code blocks)
    - abbr, address, time (semantic text)

36. The page object generator shall create text validation methods for text elements
    - expect{Element}Visible() - Asserts the text element is visible
    - expect{Element}HasText(expected: string) - Asserts the element has exact text
    - expect{Element}ContainsText(expected: string) - Asserts the element contains text
    - get{Element}Text() - Returns the text content of the element

37. The selector parser shall detect elements with dynamic content
    - Elements with Angular interpolation (e.g., `<h1>{{ title }}</h1>`, `<div>{{ content }}</div>`)
    - Elements with ng-content projection (e.g., `<div><ng-content></ng-content></div>`)
    - This applies to ANY HTML tag, not just specific text elements
    - ng-content variations: `<ng-content></ng-content>`, `<ng-content/>`, `<ng-content select="..."></ng-content>`
    - The selector shall use the element tag type
    - Property names shall be derived from the tag type (e.g., `h1` -> `Heading1`, `div` -> `Container`)

38. For elements with dynamic content (interpolation or ng-content), the page object shall include
    - A tag-based selector for the containing element
    - expect{Element}Visible() method for visibility verification
    - expect{Element}HasText(expected: string) method for text assertion
    - expect{Element}ContainsText(expected: string) method for partial text assertion
    - get{Element}Text() method to retrieve the text content

39. The CLI tool shall support a --debug global option
    - When enabled, the HTML template content is included as a multi-line comment at the top of generated page object files
    - The comment format is: /* DEBUG: HTML Template Content ... */
    - This helps developers understand what template was parsed and debug selector generation
    - The option applies to all generation commands (app, workspace, lib, artifacts)
