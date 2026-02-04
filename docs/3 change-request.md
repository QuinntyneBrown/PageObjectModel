# Implement Base Page

## Status: COMPLETED

**Completion Date:** 2026-02-04

## Explanation

All generated page object models shall extend from the following base page

### Base Page

```typescript
/**
 * Base Page Object
 *
 * Provides common functionality for all page objects.
 */

import { Page, Locator } from '@playwright/test';
import { URLS } from '../config/urls.config';
import { TIMEOUTS } from '../config/timeouts.config';

export abstract class BasePage {
  constructor(protected page: Page) {}

  /**
   * Navigate to the page
   */
  abstract navigate(): Promise<void>;

  /**
   * Wait for the page to be fully loaded
   */
  abstract waitForLoad(): Promise<void>;

  /**
   * Get the page title
   */
  async getPageTitle(): Promise<string> {
    return this.page.title();
  }

  /**
   * Wait for navigation to complete
   */
  protected async waitForNavigation(): Promise<void> {
    await this.page.waitForLoadState('networkidle', { timeout: TIMEOUTS.navigation });
  }

  /**
   * Get a locator by selector
   */
  protected getLocator(selector: string): Locator {
    return this.page.locator(selector);
  }

  /**
   * Check if element is visible
   */
  protected async isVisible(selector: string): Promise<boolean> {
    return this.getLocator(selector).isVisible();
  }

  /**
   * Wait for element to be visible
   */
  protected async waitForVisible(
    selector: string,
    timeout: number = TIMEOUTS.elementVisible
  ): Promise<void> {
    await this.getLocator(selector).waitFor({ state: 'visible', timeout });
  }

  /**
   * Wait for element to be hidden
   */
  protected async waitForHidden(
    selector: string,
    timeout: number = TIMEOUTS.elementHidden
  ): Promise<void> {
    await this.getLocator(selector).waitFor({ state: 'hidden', timeout });
  }

  /**
   * Click an element
   */
  protected async click(selector: string): Promise<void> {
    await this.getLocator(selector).click();
  }

  /**
   * Get text content of an element
   */
  protected async getTextContent(selector: string): Promise<string> {
    const content = await this.getLocator(selector).textContent();
    return content || '';
  }

  /**
   * Get count of elements matching selector
   */
  protected async getCount(selector: string): Promise<number> {
    return this.getLocator(selector).count();
  }
}
```

## Implementation Details

### Files Modified:

1. **ITemplateEngine.cs** - Added new interface method:
   - `GenerateBasePage()` - Generates the base page class TypeScript code

2. **TemplateEngine.cs** - Added implementation:
   - `GenerateBasePage()` - Generates the complete BasePage abstract class
   - Updated `GeneratePageObject()` to:
     - Import BasePage instead of Page
     - Extend BasePage
     - Call `super(page)` in constructor
     - Implement abstract `navigate()` method
     - Implement abstract `waitForLoad()` method
     - Store component selector in private `componentSelector` property

3. **CodeGenerator.cs** - Updated generation:
   - Added `GenerateBasePageFileAsync()` method
   - Updated `GenerateForApplicationAsync()` to generate base.page.ts first
   - Updated `GenerateArtifactsAsync()` to generate base.page.ts when generating page objects

4. **informal-requirements.md** - Updated requirements:
   - Requirement 11: Added base.page.ts to generated files list
   - Requirement 12: Updated page object requirements to reflect BasePage extension
   - Added Requirement 34: BasePage class specification

### Tests Updated:

1. **TemplateEngineTests.cs**:
   - Updated `GeneratePageObject_ShouldGenerateValidTypeScript` to check for BasePage import and extends
   - Updated `GeneratePageObject_WithEmptyHeader_ShouldNotIncludeHeader` to expect new import
   - Updated `GeneratePageObject_WithCustomHeader_ShouldIncludeHeader` to check for BasePage import

### Generated Page Object Example (New):

```typescript
import { Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

/**
 * Page Object for the Button component.
 * Selector: cb-button
 */
export class ButtonPage extends BasePage {
  /** The component selector used to identify this page. */
  private readonly componentSelector = 'cb-button';

  readonly clickButton: Locator;

  constructor(page: import('@playwright/test').Page) {
    super(page);
    this.clickButton = page.locator('button');
  }

  async navigate(): Promise<void> {
    await this.page.goto('/');
    await this.waitForLoad();
  }

  async clickClickButton(): Promise<void> {
    await this.clickButton.click();
  }

  async expectClickButtonVisible(): Promise<void> {
    await expect(this.clickButton).toBeVisible();
  }

  async waitForLoad(): Promise<void> {
    await this.page.waitForSelector(this.componentSelector);
  }
}
```

### All Tests Passing: 136/136
