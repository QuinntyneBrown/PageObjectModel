# Generate Artifacts Command

The `artifacts` command generates **specific Playwright artifacts** (fixtures, configs, selectors, page objects, helpers) without full regeneration. This provides granular control over what gets generated.

## Command Syntax

```bash
playwright-pom-gen artifacts <path> [options]
```

## Arguments

### `path` (Required)
The path to the Angular workspace or application directory.

- **Type:** String (positional argument)
- **Description:** Root directory of Angular workspace or application
- **Can contain:** `angular.json` (workspace) or component files (application)

**Examples:**
```bash
# Current directory
playwright-pom-gen artifacts .

# Specific application
playwright-pom-gen artifacts ./src/my-app

# Workspace
playwright-pom-gen artifacts ./my-workspace
```

## Options

### Artifact Type Options

At least one artifact type must be specified (or use `--all`).

#### `-f, --fixtures`
Generate test fixtures.

- **Type:** Boolean flag
- **Generates:** `fixtures/base.fixture.ts` and related fixture files
- **Use case:** Shared test setup/teardown, authentication, test data

```bash
playwright-pom-gen artifacts . --fixtures
```

#### `-c, --configs`
Generate Playwright configuration.

- **Type:** Boolean flag
- **Generates:** `playwright.config.ts`
- **Use case:** Playwright test runner configuration, browser settings, timeouts

```bash
playwright-pom-gen artifacts . --configs
```

#### `-s, --selectors`
Generate selector files.

- **Type:** Boolean flag
- **Generates:** `selectors/app.selectors.ts` and component selector files
- **Use case:** Centralized element selectors for maintainability

```bash
playwright-pom-gen artifacts . --selectors
```

#### `--page-objects`
Generate page object files.

- **Type:** Boolean flag
- **Generates:** `page-objects/*.page.ts` files
- **Use case:** Page Object Model classes for components

```bash
playwright-pom-gen artifacts . --page-objects
```

#### `--helpers`
Generate helper utilities.

- **Type:** Boolean flag
- **Generates:** `helpers/test.helpers.ts` and utility files
- **Use case:** Common testing utilities, custom matchers, assertion helpers

```bash
playwright-pom-gen artifacts . --helpers
```

#### `-a, --all`
Generate all artifacts.

- **Type:** Boolean flag
- **Equivalent to:** Specifying all individual artifact flags
- **Use case:** Complete generation when you need everything

```bash
playwright-pom-gen artifacts . --all
```

### Output Options

#### `-o, --output <directory>`
Specifies the output directory.

- **Type:** String (optional)
- **Default:** `<path>` or configuration value
- **Description:** Where to write generated files

```bash
playwright-pom-gen artifacts . --all --output ./e2e-tests
```

#### `-p, --project <name>`
For workspaces: specify which project to generate for.

- **Type:** String (optional)
- **Default:** All application projects
- **Description:** Project name from `angular.json`

```bash
playwright-pom-gen artifacts . --fixtures --project my-app
```

### Global Options

See [Global Options Guide](07-global-options.md):
- `--header` - Custom file header template
- `--test-suffix` - Test file suffix

## When to Use

Use the `artifacts` command when:

- ✅ You need **selective generation** of specific artifacts
- ✅ You want to **regenerate only configs** without touching page objects
- ✅ You're **incrementally building** your test infrastructure
- ✅ You want to **update fixtures** without regenerating everything
- ✅ You need **fine-grained control** over what gets generated

**Don't use when:**
- ❌ You want complete generation → Use `app` or `workspace` commands
- ❌ You're starting fresh → Use `app` or `workspace` for initial generation

## How It Works

### Step 1: Validation
1. Validates at least one artifact type is specified
2. Checks path is valid Angular workspace or application
3. Verifies output directory is writable

### Step 2: Analysis
1. Analyzes Angular structure (workspace or application)
2. Parses component metadata (if needed for selected artifacts)
3. Builds generation context

### Step 3: Selective Generation
1. Generates only the requested artifact types
2. Skips unrequested artifacts
3. Preserves existing files that aren't being regenerated

### Step 4: Output
Displays generated files grouped by artifact type.

## Examples

### Example 1: Generate Only Fixtures

```bash
playwright-pom-gen artifacts ./src/my-app --fixtures
```

**Output:**
```
Successfully generated 2 files:

Fixture:
  - fixtures/base.fixture.ts
  - fixtures/auth.fixture.ts
```

**Use case:** You need to update test fixtures but don't want to regenerate page objects.

### Example 2: Generate Fixtures and Configs

```bash
playwright-pom-gen artifacts ./src/my-app --fixtures --configs
```

**Output:**
```
Successfully generated 3 files:

Fixture:
  - fixtures/base.fixture.ts

Config:
  - playwright.config.ts
```

**Use case:** Initial test infrastructure setup without page objects yet.

### Example 3: Generate All Artifacts

```bash
playwright-pom-gen artifacts ./src/my-app --all --output ./tests
```

**Output:**
```
Successfully generated 28 files:

Fixture:
  - fixtures/base.fixture.ts
  - fixtures/auth.fixture.ts

Config:
  - playwright.config.ts

Selector:
  - selectors/app.selectors.ts
  - selectors/home.selectors.ts
  - selectors/login.selectors.ts

PageObject:
  - page-objects/home.page.ts
  - page-objects/login.page.ts
  - page-objects/dashboard.page.ts

Helper:
  - helpers/test.helpers.ts
  - helpers/assertions.ts
```

**Use case:** Complete generation equivalent to `app` command.

### Example 4: Workspace with Specific Project

```bash
playwright-pom-gen artifacts . --project admin-portal --fixtures --configs
```

**Output:**
```
Successfully generated 3 files:

Fixture:
  - admin-portal/e2e/fixtures/base.fixture.ts

Config:
  - admin-portal/e2e/playwright.config.ts
```

**Use case:** Update only fixtures and config for one project in a workspace.

### Example 5: Regenerate Only Page Objects

```bash
playwright-pom-gen artifacts ./src/my-app --page-objects
```

**Output:**
```
Successfully generated 12 files:

PageObject:
  - page-objects/home.page.ts
  - page-objects/login.page.ts
  - page-objects/dashboard.page.ts
  - page-objects/profile.page.ts
  - ...
```

**Use case:** Components changed, need to update page objects without touching configs.

### Example 6: Update Only Selectors

```bash
playwright-pom-gen artifacts ./src/my-app --selectors
```

**Output:**
```
Successfully generated 5 files:

Selector:
  - selectors/app.selectors.ts
  - selectors/home.selectors.ts
  - selectors/login.selectors.ts
  - selectors/dashboard.selectors.ts
```

**Use case:** Refactor selectors into centralized files.

### Example 7: CI Pipeline - Incremental Updates

```bash
#!/bin/bash
# ci-update-tests.sh

# Check what changed
if git diff --name-only HEAD~1 | grep -q "src/.*\.component\.ts"; then
    echo "Components changed, regenerating page objects..."
    playwright-pom-gen artifacts . --page-objects --selectors
fi

if git diff --name-only HEAD~1 | grep -q "angular.json"; then
    echo "Workspace config changed, regenerating Playwright config..."
    playwright-pom-gen artifacts . --configs
fi
```

### Example 8: Progressive Test Setup

```bash
# Step 1: Set up basic infrastructure
playwright-pom-gen artifacts ./src/my-app --fixtures --configs

# Step 2: Install dependencies and verify config works
npm install @playwright/test
npx playwright install
npx playwright test --config=e2e/playwright.config.ts --list

# Step 3: Generate page objects once infrastructure is tested
playwright-pom-gen artifacts ./src/my-app --page-objects --selectors

# Step 4: Add helpers later as needed
playwright-pom-gen artifacts ./src/my-app --helpers
```

## Error Handling

### Error: No Artifact Type Specified

```
Error: At least one artifact type must be specified.
Use --all to generate all artifacts, or specify individual options:
  --fixtures, --configs, --selectors, --page-objects, --helpers
Exit code: 1
```

**Cause:** No artifact flags provided.

**Solution:**
```bash
# Specify at least one artifact type
playwright-pom-gen artifacts . --fixtures

# Or use --all
playwright-pom-gen artifacts . --all
```

### Error: Invalid Path

```
Error: Path './invalid' does not exist
Exit code: 1
```

**Solution:**
```bash
# Verify path exists
ls -la <path>

# Use correct path
playwright-pom-gen artifacts <correct-path> --fixtures
```

### Error: Project Not Found (Workspace)

```
Error: Project 'xyz' not found in workspace
Exit code: 1
```

**Solution:**
```bash
# List available projects
cat angular.json | jq '.projects | keys'

# Use correct project name
playwright-pom-gen artifacts . --project correct-name --fixtures
```

### Warning: No Components Found (Page Objects)

```
Warning: No components found, page objects not generated
```

**Cause:** Requesting `--page-objects` but no components exist.

**Solution:**
```bash
# Verify components exist
find <path> -name "*.component.ts"

# Don't request page objects if no components
playwright-pom-gen artifacts . --fixtures --configs
```

## Artifact Type Details

### Fixtures (`--fixtures`)

**What's generated:**
- `fixtures/base.fixture.ts` - Base test fixture with common setup
- `fixtures/auth.fixture.ts` - Authentication fixture (if auth detected)
- Custom fixtures based on application structure

**Contents:**
```typescript
// fixtures/base.fixture.ts
import { test as base } from '@playwright/test';

export const test = base.extend({
  // Fixture code
});
```

**When to regenerate:**
- Authentication flow changes
- New common test setup needed
- Browser configuration changes

### Configs (`--configs`)

**What's generated:**
- `playwright.config.ts` - Playwright test configuration

**Contents:**
```typescript
// playwright.config.ts
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30000,
  use: {
    baseURL: 'http://localhost:4200',
    // ...
  },
  // ...
});
```

**When to regenerate:**
- Base URL changes
- Timeout adjustments needed
- Browser configuration changes
- Reporter settings change

### Selectors (`--selectors`)

**What's generated:**
- `selectors/app.selectors.ts` - Application-wide selectors
- `selectors/<component>.selectors.ts` - Per-component selectors

**Contents:**
```typescript
// selectors/home.selectors.ts
export const homeSelectors = {
  title: '[data-testid="home-title"]',
  loginButton: '[data-testid="login-btn"]',
  // ...
};
```

**When to regenerate:**
- Component templates change
- Data-testid attributes added/removed
- Selector strategy changes

### Page Objects (`--page-objects`)

**What's generated:**
- `page-objects/<component>.page.ts` - One per component

**Contents:**
```typescript
// page-objects/home.page.ts
import { Page } from '@playwright/test';
import { homeSelectors } from '../selectors/home.selectors';

export class HomePage {
  constructor(private page: Page) {}

  async navigate() {
    await this.page.goto('/home');
  }

  async clickLogin() {
    await this.page.click(homeSelectors.loginButton);
  }
  // ...
}
```

**When to regenerate:**
- Components added/removed/renamed
- Component structure changes
- New interactions needed

### Helpers (`--helpers`)

**What's generated:**
- `helpers/test.helpers.ts` - Common test utilities
- `helpers/assertions.ts` - Custom assertions
- `helpers/data-builders.ts` - Test data builders

**Contents:**
```typescript
// helpers/test.helpers.ts
export async function waitForAngular(page: Page) {
  // Wait for Angular to be ready
}

export async function login(page: Page, username: string, password: string) {
  // Common login logic
}
```

**When to regenerate:**
- New common utilities needed
- Framework changes
- Shared test logic added

## Use Case Scenarios

### Scenario 1: Config-Only Updates

**Situation:** Playwright version updated, need new config.

```bash
# Regenerate only config
playwright-pom-gen artifacts . --configs

# Keep existing page objects, fixtures, etc.
```

### Scenario 2: Component Refactoring

**Situation:** Refactored components, need updated page objects.

```bash
# Regenerate page objects and selectors
playwright-pom-gen artifacts . --page-objects --selectors

# Keep existing fixtures and configs
```

### Scenario 3: New Project Setup

**Situation:** Setting up tests for first time.

```bash
# Step-by-step approach

# 1. Infrastructure first
playwright-pom-gen artifacts . --fixtures --configs

# 2. Test the setup
npm test

# 3. Generate rest
playwright-pom-gen artifacts . --page-objects --selectors --helpers
```

### Scenario 4: Workspace Partial Update

**Situation:** One project in workspace needs updates.

```bash
# Update only admin-portal project's fixtures
playwright-pom-gen artifacts . --project admin-portal --fixtures

# Other projects remain unchanged
```

### Scenario 5: Selector Migration

**Situation:** Migrating from inline selectors to centralized.

```bash
# Generate selector files
playwright-pom-gen artifacts . --selectors

# Manually update page objects to use them
# Then regenerate page objects
playwright-pom-gen artifacts . --page-objects
```

## Performance Benefits

### Faster Regeneration

Compared to full `app` or `workspace` generation:

| Command | Time (10 components) | Time (100 components) |
|---------|---------------------|----------------------|
| Full generation | ~5 seconds | ~30 seconds |
| Only fixtures | ~1 second | ~2 seconds |
| Only configs | <1 second | <1 second |
| Only page objects | ~3 seconds | ~20 seconds |

### Reduced File Churn

Generate only what changed:
- Fewer git diffs to review
- Reduced CI/CD build times
- Smaller pull requests
- Easier code review

## Integration Patterns

### Git Hooks

**Pre-commit hook:**
```bash
#!/bin/bash
# .git/hooks/pre-commit

# Check if components changed
if git diff --cached --name-only | grep -q "\.component\.ts$"; then
    echo "Components changed, updating page objects..."
    playwright-pom-gen artifacts . --page-objects --selectors
    
    # Stage generated files
    git add e2e/page-objects/ e2e/selectors/
fi
```

### CI/CD Conditional Generation

**GitHub Actions:**
```yaml
name: Update Tests

on:
  push:
    paths:
      - 'src/**/*.component.ts'
      - 'angular.json'

jobs:
  update:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Determine what to regenerate
        id: changed
        run: |
          if git diff --name-only HEAD~1 | grep -q "component\.ts"; then
            echo "components=true" >> $GITHUB_OUTPUT
          fi
          if git diff --name-only HEAD~1 | grep -q "angular\.json"; then
            echo "config=true" >> $GITHUB_OUTPUT
          fi
      
      - name: Regenerate page objects
        if: steps.changed.outputs.components == 'true'
        run: playwright-pom-gen artifacts . --page-objects --selectors
      
      - name: Regenerate configs
        if: steps.changed.outputs.config == 'true'
        run: playwright-pom-gen artifacts . --configs
```

### Watch Mode Integration

```bash
#!/bin/bash
# watch-and-generate.sh

# Watch for component changes
while inotifywait -e modify -r ./src --include ".*\.component\.ts$"; do
    echo "Component changed, regenerating page objects..."
    playwright-pom-gen artifacts . --page-objects --selectors
done
```

## Comparison with Other Commands

### artifacts vs app

| Feature | artifacts | app |
|---------|-----------|-----|
| Selective generation | ✅ Yes | ❌ No (all) |
| Granular control | ✅ Full | ❌ Limited |
| Speed | ✅ Fast | Slower |
| Use case | Updates | Initial setup |

### artifacts vs workspace

| Feature | artifacts | workspace |
|---------|-----------|-----------|
| Works with workspaces | ✅ Yes | ✅ Yes |
| Selective artifacts | ✅ Yes | ❌ No |
| Per-project control | ✅ Yes | ✅ Yes |
| Use case | Updates | Complete gen |

## Best Practices

1. **Use for updates, not initial setup**
   ```bash
   # Initial setup: use app/workspace
   playwright-pom-gen app .
   
   # Updates: use artifacts
   playwright-pom-gen artifacts . --page-objects
   ```

2. **Combine related artifacts**
   ```bash
   # Good: Selectors and page objects together
   playwright-pom-gen artifacts . --selectors --page-objects
   
   # Good: Fixtures and configs together
   playwright-pom-gen artifacts . --fixtures --configs
   ```

3. **Version control before regeneration**
   ```bash
   git add .
   git commit -m "Before regeneration"
   playwright-pom-gen artifacts . --page-objects
   git diff  # Review changes
   ```

4. **Use in CI for specific scenarios**
   ```bash
   # Only regenerate what's affected by changes
   if [ "$(git diff --name-only HEAD~1 | grep config)" ]; then
       playwright-pom-gen artifacts . --configs
   fi
   ```

## Troubleshooting

See [Troubleshooting Guide](09-troubleshooting.md) for detailed solutions.

**Quick checks:**
```bash
# Verify at least one flag specified
playwright-pom-gen artifacts . --fixtures  # ✓

# Not valid (no artifact type)
playwright-pom-gen artifacts .  # ✗

# Check generated files
ls -R e2e/

# Verify specific artifact type
ls e2e/fixtures/
ls e2e/playwright.config.ts
```

## Next Steps

- Understand each artifact type in [Output Structure](08-output-structure.md)
- Learn [Configuration Options](06-configuration.md) for customization
- Review [Best Practices](10-best-practices.md) for artifact management
- Check [Global Options](07-global-options.md) for cross-artifact settings
