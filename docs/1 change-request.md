# Change generated folders.

## Status: COMPLETED

**Completion Date:** 2026-02-04

## Existing Requirement

1. The generated playwright library shall generate the following folders
    - pages
    - selectors
    - tests

 ## New Requirement

 1. The generated playwright library shall generate the following folders
    - page-objects
    - helpers
    - tests
    - configs
    - fixtures


## Explanation

Change pages folder to page-objects. Change selectors to helpers. contents of the folders remain the same.

Add folders configs where configs contains constants with standard timeouts and url configs

timeout.config.ts

```typescript
export const TIMEOUTS = {
  // Navigation timeouts
  navigation: 30000,

  // API response timeouts
  apiResponse: 5000,
  apiSlowResponse: 15000,

  // Element visibility timeouts
  elementVisible: 10000,
  elementHidden: 5000,

  // SignalR connection timeouts
  signalrConnection: 15000,
  signalrConnectionFailure: 35000,
} as const;
```

urls.config.ts

```typescript
/**
 * URL Configuration
 *
 * Centralizes all URLs and API endpoints used in tests.
 * NO URLs should appear directly in test files.
 */

/**
 * Base URLs for the application
 */
export const URLS = {
  base: process.env.BASE_URL || 'http://localhost:4200',
  dashboard: '/',
  configurations: '/configurations',
} as const;

/**
 * API endpoints for route mocking
 * Using glob patterns for flexible matching
 */
export const API_ENDPOINTS = {
  // DFS (Distributed File System) endpoints
  files: '**/api/files',
  fileById: (id: string) => `**/api/files/${id}`,
  fileContent: (id: string) => `**/api/files/${id}/content`,

  // SignalR endpoints
  telemetryHub: '**/telemetryhub',
  telemetryHubNegotiate: '**/telemetryhub/negotiate**',
} as const;
```

## Implementation Details

### Files Modified:

1. **CodeGenerator.cs** - Updated folder structure generation:
   - Changed "pages" directory to "page-objects"
   - Changed "selectors" directory to "helpers"
   - Added "configs" directory generation
   - Added "fixtures" directory generation
   - Added `GenerateTimeoutConfigFileAsync()` method
   - Added `GenerateUrlsConfigFileAsync()` method
   - Updated relative paths in GeneratedFile objects

2. **ITemplateEngine.cs** - Added new interface methods:
   - `GenerateTimeoutConfig()` - Generates timeout.config.ts
   - `GenerateUrlsConfig()` - Generates urls.config.ts

3. **TemplateEngine.cs** - Added template implementations:
   - `GenerateTimeoutConfig()` - Generates timeout constants
   - `GenerateUrlsConfig()` - Generates URL and API endpoint constants
   - Updated fixture imports to use `../page-objects/` path
   - Updated test spec imports to use `../fixtures/fixtures` path

4. **informal-requirements.md** - Updated requirements:
   - Requirement 1: Updated folder list
   - Added Requirement 3: configs folder contents
   - Added Requirement 4: fixtures folder contents
   - Added Requirement 18: timeout.config.ts contents
   - Added Requirement 19: urls.config.ts contents
   - Renumbered all subsequent requirements

### Tests Updated:

1. **CodeGeneratorTests.cs** - Updated `GenerateForApplicationAsync_ShouldCreateOutputDirectories`:
   - Changed assertions for "pages" to "page-objects"
   - Changed assertions for "selectors" to "helpers"
   - Added assertions for "configs" and "fixtures" directories

2. **TemplateEngineTests.cs** - Updated `GenerateTestSpec_ShouldGenerateTestFile`:
   - Changed expected import path from `../fixtures` to `../fixtures/fixtures`

### Generated Output Structure (New):

```
output/
├── playwright.config.ts
├── helpers.ts
├── configs/
│   ├── timeout.config.ts
│   └── urls.config.ts
├── fixtures/
│   └── fixtures.ts
├── page-objects/
│   └── {component}.page.ts
├── helpers/
│   └── {component}.selectors.ts
└── tests/
    └── {component}.spec.ts
```

### All Tests Passing: 136/136
