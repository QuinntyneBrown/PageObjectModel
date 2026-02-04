# Best Practices

Recommended practices for using the Playwright POM Generator effectively.

## Project Organization

### 1. Consistent Output Location

Choose one output directory structure and stick with it:

```bash
# Option A: Inside each project
projects/
├── app-a/
│   ├── src/
│   └── e2e/          # Generated tests here

# Option B: Separate test directory
workspace/
├── projects/         # Source code
└── e2e-tests/        # All generated tests
    ├── app-a/
    └── app-b/
```

**Recommendation:** Option A for small projects, Option B for large workspaces.

### 2. Version Control Strategy

#### Commit Generated Files
**Pros:**
- Visible in code review
- Tracks test structure changes
- Works offline

**Cons:**
- Larger repository
- Merge conflicts possible

```bash
git add e2e/
git commit -m "Add generated Playwright tests"
```

#### Don't Commit (Regenerate in CI)
**Pros:**
- Smaller repository
- Always up-to-date
- No merge conflicts

**Cons:**
- Must regenerate to run tests locally
- CI dependency

```bash
# .gitignore
e2e/
**/playwright.config.ts
```

**Recommendation:** Commit generated files unless you have automated regeneration in CI/CD.

### 3. Separate Custom Code

Don't modify generated files directly. Use extension patterns:

```
e2e/
├── page-objects/        # Generated - don't modify
│   ├── home.page.ts
│   └── login.page.ts
├── custom-pages/        # Your extensions
│   ├── home.extended.ts
│   └── base.page.ts
├── fixtures/            # Generated
│   └── base.fixture.ts
└── custom-fixtures/     # Your additions
    └── auth.fixture.ts
```

**Example extension:**
```typescript
// custom-pages/home.extended.ts
import { HomePage } from '../page-objects/home.page';

export class HomePageExtended extends HomePage {
  // Add custom methods
  async loginAsAdmin() {
    await this.clickLogin();
    // ... custom logic
  }
}
```

## Configuration Management

### 1. Layer Configurations

Use configuration hierarchy:

```
appsettings.json              # Base settings (commit)
appsettings.Development.json  # Dev overrides (commit)
appsettings.Production.json   # Prod overrides (commit)
appsettings.Local.json        # Personal (don't commit)
```

### 2. Use Environment Variables in CI/CD

```yaml
# .github/workflows/generate-tests.yml
env:
  POMGEN_Generator__BaseUrlPlaceholder: ${{ secrets.STAGING_URL }}
  POMGEN_Generator__DefaultTimeout: "60000"

- name: Generate tests
  run: playwright-pom-gen workspace .
```

### 3. Document Configuration

Create `docs/pom-generator-config.md`:

```markdown
# POM Generator Configuration

## Settings
- TestFileSuffix: "spec" (company standard)
- DefaultTimeout: 45000 (CI is slow)
- BaseUrlPlaceholder: Changes per environment

## Usage
```bash
# Development
playwright-pom-gen app .

# Production config
DOTNET_ENVIRONMENT=Production playwright-pom-gen app .
```
```

## Generation Workflow

### 1. Initial Setup

```bash
# Step 1: Generate complete structure
playwright-pom-gen workspace .

# Step 2: Verify output
ls -R e2e/

# Step 3: Install dependencies
npm install @playwright/test
npx playwright install

# Step 4: Run sample tests
npx playwright test e2e/

# Step 5: Commit if satisfied
git add e2e/ appsettings.json
git commit -m "Initial Playwright POM generation"
```

### 2. Incremental Updates

When components change:

```bash
# Option A: Regenerate everything
playwright-pom-gen app .

# Option B: Regenerate selectively
playwright-pom-gen artifacts . --page-objects --selectors

# Review changes
git diff e2e/

# Commit or discard
git add e2e/
git commit -m "Update page objects for new components"
```

### 3. Automated Regeneration

#### Git Hook (Pre-commit)

```bash
#!/bin/bash
# .git/hooks/pre-commit

# Check if components changed
if git diff --cached --name-only | grep -q "\.component\.ts$"; then
    echo "Components changed, regenerating page objects..."
    playwright-pom-gen artifacts . --page-objects --selectors
    
    # Stage generated files
    git add e2e/page-objects/ e2e/selectors/
fi
```

#### CI/CD Pipeline

```yaml
# GitHub Actions
on:
  pull_request:
    paths:
      - 'src/**/*.component.ts'

jobs:
  update-pom:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Regenerate POMs
        run: playwright-pom-gen artifacts . --page-objects
      
      - name: Commit changes
        run: |
          git config user.name "Bot"
          git config user.email "bot@example.com"
          git add e2e/
          git commit -m "Auto-update page objects" || echo "No changes"
          git push
```

## Testing Practices

### 1. Don't Rely Solely on Generated Tests

Generated tests are **samples**. Write real tests:

```typescript
// Generated (sample)
test('should display title', async ({ homePage }) => {
  await homePage.navigate();
  const title = await homePage.getTitleText();
  expect(title).toBe('Welcome'); // May not match your app
});

// Your real test
test('should display personalized welcome', async ({ homePage, authenticatedPage }) => {
  await homePage.navigate();
  const title = await homePage.getTitleText();
  expect(title).toContain('Welcome, TestUser');
  expect(homePage.logoutButton).toBeVisible();
});
```

### 2. Use Generated Page Objects

Page objects are reusable - use them in custom tests:

```typescript
// Good: Use generated page objects
test('complete user flow', async ({ homePage, loginPage, dashboardPage }) => {
  await homePage.navigate();
  await homePage.clickLogin();
  await loginPage.login('user', 'pass');
  await dashboardPage.verifyUserLoggedIn();
});

// Bad: Direct page interactions
test('complete user flow', async ({ page }) => {
  await page.goto('/');
  await page.click('[data-testid="login-btn"]');
  await page.fill('#username', 'user');
  await page.fill('#password', 'pass');
  await page.click('#submit');
  // ... harder to maintain
});
```

### 3. Extend Fixtures for Complex Setup

```typescript
// custom-fixtures/app.fixture.ts
import { test as base } from '../fixtures/base.fixture';
import { loginPage, dashboardPage } from '../page-objects';

export const test = base.extend({
  authenticatedUser: async ({ page }, use) => {
    const login = new LoginPage(page);
    await login.navigate();
    await login.loginAsAdmin();
    await use(page);
    await login.logout();
  },
  
  preloadedDashboard: async ({ page, dashboardPage }, use) => {
    await dashboardPage.navigate();
    await dashboardPage.waitForData();
    await use(dashboardPage);
  },
});
```

## Maintenance

### 1. Regenerate Regularly

```bash
# Weekly or after major component changes
playwright-pom-gen artifacts . --page-objects --selectors
```

### 2. Keep Configuration Up to Date

```json
{
  "Generator": {
    "ToolVersion": "1.2.0",  // Update when tool updates
    "BaseUrlPlaceholder": "http://localhost:4200"  // Update if port changes
  }
}
```

### 3. Clean Up Obsolete Files

```bash
# After deleting components, remove corresponding page objects
rm e2e/page-objects/old-component.page.ts
rm e2e/selectors/old-component.selectors.ts
rm e2e/tests/old-component.spec.ts
```

### 4. Review Generated Files

After regeneration, review:
- Selector accuracy
- Method completeness
- Type safety

## CI/CD Integration

### 1. Cache Dependencies

```yaml
# GitHub Actions
- name: Cache npm dependencies
  uses: actions/cache@v3
  with:
    path: ~/.npm
    key: ${{ runner.os }}-node-${{ hashFiles('**/package-lock.json') }}

- name: Cache Playwright browsers
  uses: actions/cache@v3
  with:
    path: ~/.cache/ms-playwright
    key: ${{ runner.os }}-playwright-${{ hashFiles('**/package-lock.json') }}
```

### 2. Fail Fast on Generation Errors

```yaml
- name: Generate POMs
  run: |
    playwright-pom-gen workspace . --output ./e2e
    if [ $? -ne 0 ]; then
      echo "POM generation failed"
      exit 1
    fi
```

### 3. Run Tests After Generation

```yaml
- name: Generate POMs
  run: playwright-pom-gen workspace .

- name: Install test dependencies
  run: npm install

- name: Run Playwright tests
  run: npx playwright test
```

## Team Collaboration

### 1. Document Commands

Create `scripts/` directory:

```bash
# scripts/generate-pom.sh
#!/bin/bash
playwright-pom-gen workspace . \
  --header "// Copyright 2026 ACME Corp" \
  --test-suffix "spec"
```

Make executable:
```bash
chmod +x scripts/generate-pom.sh
```

Usage:
```bash
./scripts/generate-pom.sh
```

### 2. Establish Conventions

Document in `CONTRIBUTING.md`:

```markdown
## Playwright POM Generator

### When to Regenerate
- After adding/removing components
- After changing component templates significantly
- Weekly during active development

### How to Regenerate
```bash
./scripts/generate-pom.sh
```

### What to Commit
- Generated page objects
- Generated selectors
- Configuration files
```

### 3. Code Review Generated Files

Review generated POMs in pull requests:
- Verify selectors match UI
- Check for breaking changes
- Ensure tests still pass

## Performance Optimization

### 1. Generate Only What's Needed

```bash
# Instead of full regeneration
playwright-pom-gen app .  # Slow

# Generate specific artifacts
playwright-pom-gen artifacts . --page-objects  # Faster
```

### 2. Parallelize Workspace Generation

```yaml
# GitHub Actions
jobs:
  generate:
    strategy:
      matrix:
        project: [app-a, app-b, app-c]
    steps:
      - run: playwright-pom-gen workspace . --project ${{ matrix.project }}
```

### 3. Use Incremental Regeneration

```bash
# Only regenerate if components changed
if git diff --name-only HEAD~1 | grep -q "\.component\.ts"; then
  playwright-pom-gen artifacts . --page-objects
fi
```

## Anti-Patterns to Avoid

### ❌ Don't Modify Generated Files

```typescript
// Bad: Modifying generated file
// e2e/page-objects/home.page.ts
export class HomePage {
  async navigate() {
    await this.page.goto('/home');
    await this.customMethod();  // ❌ Will be lost on regeneration
  }
}
```

```typescript
// Good: Extend in separate file
// e2e/custom-pages/home.extended.ts
export class HomePageExtended extends HomePage {
  async customMethod() {
    // Your logic
  }
}
```

### ❌ Don't Over-Generate

```bash
# Bad: Regenerating unnecessarily
playwright-pom-gen app .  # Every commit

# Good: Regenerate when needed
playwright-pom-gen app .  # When components change
```

### ❌ Don't Ignore Configuration

```bash
# Bad: All settings in command-line
playwright-pom-gen app . \
  --header "..." \
  --test-suffix "..." \
  --output "..."

# Good: Use configuration file
# appsettings.json contains settings
playwright-pom-gen app .
```

### ❌ Don't Mix Manual and Generated

```
e2e/
├── page-objects/
│   ├── home.page.ts       # Generated
│   ├── login.page.ts      # Generated
│   └── custom.page.ts     # ❌ Manual file here
```

```
# Good: Separate directories
e2e/
├── page-objects/          # Generated only
└── custom-pages/          # Manual only
```

## Summary Checklist

- [ ] Choose consistent output location
- [ ] Decide on version control strategy
- [ ] Set up configuration files
- [ ] Document team conventions
- [ ] Create helper scripts
- [ ] Integrate into CI/CD
- [ ] Establish regeneration schedule
- [ ] Extend, don't modify generated files
- [ ] Write real tests using generated page objects
- [ ] Review generated files in PRs
- [ ] Keep configuration up to date
- [ ] Clean up obsolete files

## Next Steps

- Review [Configuration Guide](06-configuration.md) for detailed settings
- Check [Output Structure](08-output-structure.md) for file organization
- Read [Troubleshooting](09-troubleshooting.md) for common issues
- Explore command-specific guides for detailed usage
