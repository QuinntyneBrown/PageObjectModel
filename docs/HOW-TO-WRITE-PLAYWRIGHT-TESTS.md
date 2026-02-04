# How to Write Playwright Tests: Agent Guide

## Overview

This guide is specifically designed for AI agents and developers to understand how to write Playwright tests following the refactored pattern in this repository. This document provides a step-by-step approach with concrete examples.

## Quick Reference: The Rules

**NEVER do these in test files:**
- ❌ NO URLs (use `config/urls.config.ts`)
- ❌ NO CSS selectors (use `helpers/selectors.ts`)
- ❌ NO regex patterns (use helpers or fixtures)
- ❌ NO hard-coded test data (use `config/test-data.config.ts`)
- ❌ NO hard-coded wait times (use `waitFor*` methods from page objects)
- ❌ NO direct `page.locator()` or `page.goto()` calls (use page objects)

**ALWAYS do these:**
- ✅ Use page objects for ALL page interactions
- ✅ Use configuration files for URLs, endpoints, and test data
- ✅ Use centralized selectors from `helpers/selectors.ts`
- ✅ Use fixtures for mocking HTTP and SignalR
- ✅ Follow Given-When-Then structure
- ✅ Write descriptive test names
- ✅ Keep tests independent

## Directory Structure

```
e2e/refactored/
├── config/                    # Configuration (URLs, test data, timeouts)
├── fixtures/                  # Mock data and HTTP/SignalR mocking
├── helpers/                   # Utilities (selectors, wait conditions)
├── page-objects/              # Page Object Models (POMs)
├── tests/                     # Test specifications
└── playwright.config.ts       # Playwright config
```

## Step-by-Step: Writing a New Test

### Step 1: Identify Required Page Objects

Before writing a test, identify which page objects you need:
- `DashboardPage` - For dashboard interactions
- `ConfigurationDialogPage` - For configuration dialog
- `BasePage` - Base functionality (rarely used directly)

If you need a new page, create a new page object (see "Creating a New Page Object" below).

### Step 2: Check Configuration Files

Verify that required data exists in configuration files:

**URLs** (`config/urls.config.ts`):
```typescript
export const URLS = {
  dashboard: '/',
  configurations: '/configurations',
} as const;
```

**Test Data** (`config/test-data.config.ts`):
```typescript
export const CONFIGURATIONS = {
  powerSystems: { name: 'Power Systems', version: '1.0.0' },
  navigation: { name: 'Navigation', version: '1.2.0' },
} as const;
```

**API Endpoints** (`config/urls.config.ts`):
```typescript
export const API_ENDPOINTS = {
  files: '**/api/files',
  fileById: (id: string) => `**/api/files/${id}`,
} as const;
```

### Step 3: Write the Test

Use this template:

```typescript
import { test, expect } from '@playwright/test';
import { DashboardPage } from '../page-objects/dashboard.page';
import { setupHttpMocks } from '../fixtures/http-mocks.fixture';
import { setupSignalRMock } from '../fixtures/signalr-mocks.fixture';
import { CONFIGURATIONS } from '../config/test-data.config';

test.describe('Feature Name', () => {
  let dashboardPage: DashboardPage;
  
  test.beforeEach(async ({ page }) => {
    // Setup mocks
    await setupHttpMocks(page);
    await setupSignalRMock(page);
    
    // Initialize page objects
    dashboardPage = new DashboardPage(page);
    
    // Navigate and wait for load
    await dashboardPage.navigate();
    await dashboardPage.waitForLoad();
  });
  
  test('user can perform action', async () => {
    // Given: Initial state (usually already set in beforeEach)
    
    // When: User performs action
    const dialog = await dashboardPage.openConfigurationDialog();
    await dialog.selectAndConfirm(CONFIGURATIONS.powerSystems.name);
    
    // Then: Verify expected outcome
    await dashboardPage.expectTileWithName(CONFIGURATIONS.powerSystems.name);
  });
});
```

## Common Test Patterns

### Pattern 1: Simple Navigation and Verification

```typescript
test('dashboard displays initial state', async () => {
  // Verify page is visible
  await dashboardPage.expectVisible();
  
  // Verify page elements
  await dashboardPage.expectTitle();
  await dashboardPage.expectStatCardCount();
});
```

### Pattern 2: User Interaction Flow

```typescript
test('user can load configuration', async () => {
  // Open dialog
  const dialog = await dashboardPage.openConfigurationDialog();
  await dialog.expectVisible();
  
  // Select and confirm
  await dialog.selectAndConfirm(CONFIGURATIONS.powerSystems.name);
  
  // Verify result
  await dashboardPage.expectTileWithName(CONFIGURATIONS.powerSystems.name);
});
```

### Pattern 3: Multi-Step Workflow

```typescript
test('user can add multiple tiles', async () => {
  // Load first configuration
  const dialog1 = await dashboardPage.openConfigurationDialog();
  await dialog1.selectAndConfirm(CONFIGURATIONS.powerSystems.name);
  
  // Enter edit mode
  await dashboardPage.enterEditMode();
  await dashboardPage.expectInEditMode();
  
  // Add second tile
  const dialog2 = await dashboardPage.addNewTile();
  await dialog2.selectAndConfirm(CONFIGURATIONS.navigation.name);
  
  // Exit edit mode
  await dashboardPage.exitEditMode();
  
  // Verify both tiles
  await dashboardPage.expectTileWithName(CONFIGURATIONS.powerSystems.name);
  await dashboardPage.expectTileWithName(CONFIGURATIONS.navigation.name);
});
```

### Pattern 4: Error Handling

```typescript
test('handles API failure gracefully', async ({ page }) => {
  // Setup mocks with failure
  await setupHttpMocks(page, { shouldFail: true });
  await setupSignalRMock(page);
  
  dashboardPage = new DashboardPage(page);
  await dashboardPage.navigate();
  await dashboardPage.waitForLoad();
  
  // Verify app still works
  await dashboardPage.expectVisible();
  await dashboardPage.expectTitle();
});
```

### Pattern 5: State Changes

```typescript
test('user can switch tile configuration', async () => {
  // Load initial config
  const dialog1 = await dashboardPage.openConfigurationDialog();
  await dialog1.selectAndConfirm(CONFIGURATIONS.powerSystems.name);
  await dashboardPage.expectTileWithName(CONFIGURATIONS.powerSystems.name);
  
  // Switch to different config
  const dialog2 = await dashboardPage.openConfigurationDialog();
  await dialog2.selectAndConfirm(CONFIGURATIONS.navigation.name);
  
  // Verify new config loaded
  await dashboardPage.expectTileWithName(CONFIGURATIONS.navigation.name);
});
```

## Working with Page Objects

### Using Existing Page Objects

**DashboardPage** - Main dashboard interactions:
```typescript
const dashboardPage = new DashboardPage(page);

// Navigation
await dashboardPage.navigate();
await dashboardPage.waitForLoad();

// Getting data
const title = await dashboardPage.getTitle();
const tileCount = await dashboardPage.getTileCount();

// Actions
await dashboardPage.enterEditMode();
await dashboardPage.exitEditMode();
const dialog = await dashboardPage.openConfigurationDialog();
await dashboardPage.removeTileByName('Power Systems');

// Assertions
await dashboardPage.expectVisible();
await dashboardPage.expectTitle();
await dashboardPage.expectTileWithName('Power Systems');
await dashboardPage.expectInEditMode();
```

**ConfigurationDialogPage** - Dialog interactions:
```typescript
const dialog = new ConfigurationDialogPage(page);

// Actions
await dialog.selectConfiguration('Power Systems');
await dialog.confirm();
await dialog.cancel();
await dialog.selectAndConfirm('Power Systems');  // Combined action

// Assertions
await dialog.expectVisible();
await dialog.expectConfigurationCount(4);
await dialog.expectConfigurationAvailable('Power Systems');
await dialog.expectClosed();
```

### Creating a New Page Object

If you need a new page object:

1. Create file in `page-objects/` directory
2. Extend `BasePage`
3. Define private selectors
4. Implement public methods
5. Add assertion methods

```typescript
/**
 * NewPage Page Object
 * Encapsulates interactions with the new page.
 */

import { expect, Page } from '@playwright/test';
import { BasePage } from './base.page';
import { URLS } from '../config/urls.config';
import { NEW_PAGE_SELECTORS } from '../helpers/selectors';

export class NewPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }
  
  /**
   * Navigate to page
   */
  async navigate(): Promise<void> {
    await this.page.goto(URLS.newPage);
    await this.waitForNavigation();
  }
  
  /**
   * Wait for page to load
   */
  async waitForLoad(): Promise<void> {
    await this.waitForVisible(NEW_PAGE_SELECTORS.title);
  }
  
  /**
   * Get page title
   */
  async getTitle(): Promise<string> {
    return this.getTextContent(NEW_PAGE_SELECTORS.title);
  }
  
  /**
   * Click action button
   */
  async clickAction(): Promise<void> {
    await this.click(NEW_PAGE_SELECTORS.actionButton);
  }
  
  /**
   * Assertions
   */
  async expectVisible(): Promise<void> {
    await expect(this.getLocator(NEW_PAGE_SELECTORS.page)).toBeVisible();
  }
  
  async expectTitle(expectedTitle: string): Promise<void> {
    await expect(this.getLocator(NEW_PAGE_SELECTORS.title)).toHaveText(expectedTitle);
  }
}
```

## Working with Configuration

### Adding URLs

Edit `config/urls.config.ts`:
```typescript
export const URLS = {
  dashboard: '/',
  configurations: '/configurations',
  newPage: '/new-page',  // Add new URL
} as const;
```

### Adding Test Data

Edit `config/test-data.config.ts`:
```typescript
export const CONFIGURATIONS = {
  powerSystems: { name: 'Power Systems', version: '1.0.0' },
  newConfig: { name: 'New Config', version: '2.0.0' },  // Add new data
} as const;
```

### Adding Selectors

Edit `helpers/selectors.ts`:
```typescript
export const DASHBOARD_SELECTORS = {
  page: '.dashboard-page',
  title: '.dashboard-page__title',
  newElement: '.new-element',  // Add new selector
} as const;
```

## Working with Mocks

### Setting up HTTP Mocks

Standard setup (in test `beforeEach`):
```typescript
await setupHttpMocks(page);
```

With options:
```typescript
// Empty response
await setupHttpMocks(page, { emptyResponse: true });

// API failure
await setupHttpMocks(page, { shouldFail: true });

// With delay
await setupHttpMocks(page, { delay: 1000 });

// Custom data
await setupHttpMocks(page, { customData: myCustomData });
```

### Setting up SignalR Mocks

Standard setup:
```typescript
await setupSignalRMock(page);
```

Block connection:
```typescript
const signalRMock = new SignalRMockBuilder(page);
await signalRMock.blockConnection();
```

Mock failure:
```typescript
const signalRMock = new SignalRMockBuilder(page);
await signalRMock.mockConnectionFailure();
```

### Creating Custom Mock Data

Use builders in `fixtures/mock-data.fixture.ts`:
```typescript
import { TileConfigurationBuilder } from '../fixtures/mock-data.fixture';

const customConfig = new TileConfigurationBuilder()
  .withName('Custom Config')
  .withVersion('2.0.0')
  .withElements([
    { name: 'Item 1', path: 'Path\\Item1' },
    { name: 'Item 2', path: 'Path\\Item2' },
  ])
  .build();
```

## Test Naming Conventions

### Format
`[subject] [can/should] [action] [expected result]`

### Good Examples
✅ `user can load configuration onto tile`
✅ `dashboard displays initial state with title and stats`
✅ `configuration dialog shows available configurations`
✅ `user can switch tile configuration`
✅ `handles API failure gracefully`

### Bad Examples
❌ `test1` - Not descriptive
❌ `it works` - Too vague
❌ `test the button click` - Implementation detail
❌ `configuration-test` - Unclear purpose

## Common Mistakes and How to Avoid Them

### Mistake 1: Using URLs Directly
❌ **Wrong:**
```typescript
await page.goto('http://localhost:4200/');
```

✅ **Correct:**
```typescript
await dashboardPage.navigate();
```

### Mistake 2: Using Selectors in Tests
❌ **Wrong:**
```typescript
await page.locator('.dashboard-page__title').textContent();
```

✅ **Correct:**
```typescript
await dashboardPage.getTitle();
```

### Mistake 3: Hard-coding Test Data
❌ **Wrong:**
```typescript
await dialog.selectAndConfirm('Power Systems');
```

✅ **Correct:**
```typescript
await dialog.selectAndConfirm(CONFIGURATIONS.powerSystems.name);
```

### Mistake 4: Using Hard-coded Waits
❌ **Wrong:**
```typescript
await page.waitForTimeout(5000);
```

✅ **Correct:**
```typescript
await dashboardPage.waitForLoad();
// or
await dialog.expectVisible();
```

### Mistake 5: Direct Page Interactions
❌ **Wrong:**
```typescript
await page.click('button:has-text("Load Configuration")');
```

✅ **Correct:**
```typescript
await dialog.confirm();
```

### Mistake 6: Creating Test Dependencies
❌ **Wrong:**
```typescript
test('loads config', async () => {
  // Test 1 modifies state
});

test('uses loaded config', async () => {
  // Test 2 depends on Test 1 - BAD!
});
```

✅ **Correct:**
```typescript
test('uses loaded config', async () => {
  // Setup state in this test
  const dialog = await dashboardPage.openConfigurationDialog();
  await dialog.selectAndConfirm(CONFIGURATIONS.powerSystems.name);
  
  // Now test what you need
});
```

## Troubleshooting

### Test is Flaky

**Problem:** Test sometimes passes, sometimes fails

**Solutions:**
1. Use `waitFor*` methods instead of hard-coded timeouts
2. Ensure proper wait conditions in page objects
3. Check if test depends on timing or other tests
4. Use `expectVisible()` before interacting with elements

### Element Not Found

**Problem:** Locator can't find element

**Solutions:**
1. Verify selector in `helpers/selectors.ts` is correct
2. Ensure page has loaded with `waitForLoad()`
3. Check if element is in a different state (edit mode, etc.)
4. Use `page.pause()` to debug interactively

### Mock Not Working

**Problem:** Test receives real API calls instead of mocks

**Solutions:**
1. Ensure `setupHttpMocks(page)` is called before navigation
2. Verify route pattern in `config/urls.config.ts`
3. Check if route is being called with correct method (GET, POST, etc.)
4. Add logging in mock fixture to verify it's being hit

### Dialog Not Opening

**Problem:** Dialog methods fail because dialog didn't open

**Solutions:**
1. Verify tile has loaded: `await dashboardPage.waitForLoad()`
2. Check if you're in the right mode (edit vs normal)
3. Ensure configuration button is clicked on correct tile
4. Add explicit wait: `await dialog.waitForLoad()`

## Running Tests

### Run all refactored tests
```bash
npm run e2e:refactored
```

### Run specific test file
```bash
npm run e2e:refactored tests/configuration-loading.spec.ts
```

### Run tests matching pattern
```bash
npm run e2e:refactored -- -g "user can load"
```

### Run with UI mode (best for development)
```bash
npm run e2e:refactored:ui
```

### Debug specific test
```bash
npx playwright test --config=projects/dashboard/e2e/refactored/playwright.config.ts --debug -g "test name"
```

## Quick Checklist for New Tests

Before submitting a test, verify:

- [ ] No URLs in test file (use `URLS` from config)
- [ ] No selectors in test file (use page object methods)
- [ ] No hard-coded test data (use `TEST_DATA` from config)
- [ ] No hard-coded waits (use `waitFor*` methods)
- [ ] No direct `page.locator()` calls (use page objects)
- [ ] Test has descriptive name following convention
- [ ] Test uses Given-When-Then structure
- [ ] Test is independent (doesn't depend on other tests)
- [ ] HTTP and SignalR mocks are set up
- [ ] Test uses `beforeEach` for common setup
- [ ] Assertions use page object `expect*` methods

## Example: Complete Test from Scratch

Let's write a complete test for a new feature "user can filter configurations by type":

### Step 1: Add test data
```typescript
// config/test-data.config.ts
export const FILTER_TYPES = {
  all: 'All',
  systems: 'Systems',
  subsystems: 'Subsystems',
} as const;
```

### Step 2: Add selectors (if needed)
```typescript
// helpers/selectors.ts
export const CONFIG_DIALOG_SELECTORS = {
  // ... existing selectors
  filterDropdown: '.filter-dropdown',
  filterOption: '.filter-option',
} as const;
```

### Step 3: Add page object method (if needed)
```typescript
// page-objects/configuration-dialog.page.ts
async selectFilter(filterType: string): Promise<void> {
  await this.click(CONFIG_DIALOG_SELECTORS.filterDropdown);
  await this.page.locator(CONFIG_DIALOG_SELECTORS.filterOption, {
    hasText: filterType
  }).click();
}

async expectFilteredCount(count: number): Promise<void> {
  await expect(this.getLocator(CONFIG_DIALOG_SELECTORS.tableRow))
    .toHaveCount(count);
}
```

### Step 4: Write the test
```typescript
// tests/configuration-loading.spec.ts
test('user can filter configurations by type', async () => {
  // Given: dialog is open with all configurations
  const dialog = await dashboardPage.openConfigurationDialog();
  await dialog.expectVisible();
  await dialog.expectConfigurationCount(EXPECTED_COUNTS.defaultConfigurations);
  
  // When: user selects a filter
  await dialog.selectFilter(FILTER_TYPES.systems);
  
  // Then: only matching configurations are shown
  await dialog.expectFilteredCount(2);
  await dialog.expectConfigurationAvailable(CONFIGURATIONS.powerSystems.name);
  await dialog.expectConfigurationAvailable(CONFIGURATIONS.navigation.name);
});
```

## Summary

This pattern ensures:
- **Maintainability**: UI changes only affect page objects
- **Readability**: Tests read like requirements
- **Reliability**: Consistent patterns reduce flakiness
- **Reusability**: Common functionality is shared
- **Debuggability**: Clear separation of concerns

By following these patterns, you create tests that are easy to write, understand, and maintain.

## Additional Resources

- **Full Pattern Guide**: `/docs/playwright-test-patterns.md`
- **Test Examples**: `/src/MainDashboardFramework/projects/dashboard/e2e/refactored/tests/`
- **Quick Reference**: `/src/MainDashboardFramework/projects/dashboard/e2e/refactored/QUICK_REFERENCE.md`
- **Implementation Details**: `/src/MainDashboardFramework/projects/dashboard/e2e/refactored/IMPLEMENTATION_SUMMARY.md`