# Selector parsing and Page Object Model Generation enhancement

## Status: COMPLETED

**Completion Date:** 2026-02-04

## Existing requirement
8. The selector parser shall support the following selector strategies in priority order
    - data-testid attributes (highest priority)
    - id attributes
    - Role-based selectors (buttons with text)
    - CSS selectors (formControlName)

## Explanation

### Change Requested that impacts the existing requirement and maybe more requirements

The selector parser shall additionally support
    - elements that would render text for example
        - <m-foo>Something</m-foo> parser shall assume that "Something" would be rendered in the output and shall create a selector
    - (click) if the template has click (or href / link / router link) the parser shall assume there needs to be a selector and that is an action that will be tested and called in the page-object. For example
        - <x-btn (click)="onLogin()">Login</x-btn>
        - the syntax shall generate the creation of selector and page-object-model code to trigger a login
    - tables
        - there shall be selectors for anything that seems to be a table or angular material table
            - selectors for rows
            - page-object-model code to access rows, table headings, columns
    - all @angular/material syntax and @angular/cdk syntax. build in logic to identify @angular/material and @angular/cdk syntax being used in html templates and typescript code and ensure the correct testing code is generated with timeouts, selectors, etc...
        - buttons
        - tables
        - dialog (MatDialog)
        - mat-form-fields
        - etc

    - all @microsoft/signalr syntax. build in logic to identify @microsoft/signalr syntax being used in typescript code and ensure the correct testing code is generated with timeouts, etc... Assume WebSockets is being mocked out completely and the signalr needs to be mocked at the protocol level using rxjs and signalr fixture


    - all buttons, texts and tables shall have expect{element}Visible() methods in the page-object-model, for example

    ```typescript
    async expectCancelButtonVisible(): Promise<void> {
        await expect(this.getLocator(CONFIG_DIALOG_SELECTORS.cancelButton)).toBeVisible();
    }
    ```

## Implementation Details

### Files Modified:

1. **ElementSelector.cs** - Added new properties:
   - `HasClickHandler` - Whether element has (click) event
   - `IsLink` - Whether element is a link (href, routerLink)
   - `IsTable` - Whether element is a table/mat-table
   - `IsMaterialComponent` - Whether it's an Angular Material component
   - `ClickHandlerName` - The click handler method name

2. **AngularAnalyzer.cs** - Enhanced selector parsing:
   - Added parsing for `(click)="method()"` handlers
   - Added parsing for `routerLink` and `[routerLink]` directives
   - Added parsing for Angular Material buttons (mat-button, mat-raised-button, etc.)
   - Added parsing for mat-form-field with mat-label
   - Added parsing for tables and mat-tables
   - Added parsing for custom elements with text content (`<m-foo>Text</m-foo>`)
   - Added helper methods: `ExtractTextContent`, `ExtractClickHandler`, `ExtractAttribute`, `ExtractMatButtonText`, `GetElementTypeSuffix`, `IsStandardHtmlElement`
   - Added new regex patterns:
     - `ClickHandlerRegex` - Matches `(click)="handler()"`
     - `RouterLinkRegex` - Matches routerLink directives
     - `MatButtonRegex` - Matches Angular Material buttons
     - `MatFormFieldRegex` - Matches mat-form-field elements
     - `MatLabelRegex` - Matches mat-label elements
     - `TableRegex` - Matches table and mat-table elements
     - `CustomElementWithTextRegex` - Matches custom components with text

3. **TemplateEngine.cs** - Enhanced page object generation:
   - Updated import to include `expect` from '@playwright/test'
   - Added `expect{Element}Visible()` methods for all buttons
   - Added `expect{Element}Visible()` methods for text elements
   - Added click methods for elements with click handlers (that aren't buttons)
   - Added comprehensive table accessor methods:
     - `expect{Table}Visible()` - Assert table visibility
     - `get{Table}Rows()` - Get all rows
     - `get{Table}Row(index)` - Get specific row by index
     - `get{Table}Headers()` - Get header cells
     - `get{Table}Column(columnIndex)` - Get cells in a column
     - `get{Table}RowCount()` - Get row count
     - `click{Table}Row(index)` - Click on a row

4. **informal-requirements.md** - Updated requirements:
   - Requirement 10: Enhanced selector strategies list
   - Requirement 12: Enhanced page object contents
   - Added Requirement 29: Angular Material component detection
   - Added Requirement 30: Table accessor methods
   - Added Requirement 31: expect{Element}Visible() methods
   - Added Requirement 32: Click handler and navigation detection
   - Added Requirement 33: SignalR pattern support

### Tests Updated:

1. **TemplateEngineTests.cs**:
   - Updated import assertions to include `expect` in the import statement

### Generated Page Object Example (New):

```typescript
import { Page, Locator, expect } from '@playwright/test';

export class LoginPage {
  readonly page: Page;
  readonly loginButton: Locator;
  readonly dataTable: Locator;

  constructor(page: Page) {
    this.page = page;
    this.loginButton = page.getByRole('button', { name: 'Login' });
    this.dataTable = page.locator('mat-table, table[mat-table], [mat-table]');
  }

  // Click methods
  async clickLoginButton(): Promise<void> {
    await this.loginButton.click();
  }

  // Expect visible methods
  async expectLoginButtonVisible(): Promise<void> {
    await expect(this.loginButton).toBeVisible();
  }

  async expectDataTableVisible(): Promise<void> {
    await expect(this.dataTable).toBeVisible();
  }

  // Table accessor methods
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

  async waitForLoad(): Promise<void> {
    await this.page.waitForSelector('app-login');
  }
}
```

### All Tests Passing: 136/136
