# Generate App Command

The `app` command generates Playwright Page Object Model test files for a **single Angular application**.

## Command Syntax

```bash
playwright-pom-gen app <path> [options]
```

## Arguments

### `path` (Required)
The path to the Angular application directory.

- **Type:** String (positional argument)
- **Description:** Root directory of the Angular application
- **Must contain:** Angular component files (*.component.ts)

**Examples:**
```bash
# Current directory
playwright-pom-gen app .

# Specific path
playwright-pom-gen app ./src/my-app

# Absolute path (Windows)
playwright-pom-gen app C:\projects\my-angular-app

# Absolute path (Linux/macOS)
playwright-pom-gen app /home/user/projects/my-angular-app
```

## Options

### `-o, --output <directory>`
Specifies the output directory for generated files.

- **Type:** String (optional)
- **Default:** `<path>/e2e` (or value from configuration)
- **Description:** Where to write generated Page Object Model files

**Examples:**
```bash
# Default output (./e2e relative to app path)
playwright-pom-gen app ./src/my-app

# Custom output directory
playwright-pom-gen app ./src/my-app --output ./tests

# Output to parent directory
playwright-pom-gen app ./src/my-app --output ../e2e-tests
```

### Global Options

See [Global Options Guide](07-global-options.md) for options available to all commands:
- `--header` - Custom file header template
- `--test-suffix` - Test file suffix (default: "spec")

## When to Use

Use the `app` command when:

- ✅ You have a **single Angular application** (not a workspace)
- ✅ Your application is **standalone** or in a simple structure
- ✅ You want to generate tests for **one specific application**
- ✅ You're working with a **library project** structure

**Don't use when:**
- ❌ You have an Angular workspace with `angular.json` → Use [`workspace` command](03-generate-workspace.md)
- ❌ You only need specific artifacts → Use [`artifacts` command](04-generate-artifacts.md)

## How It Works

### Step 1: Analysis Phase
The tool analyzes the Angular application:
1. Validates the path is a valid Angular application
2. Scans for Angular component files (*.component.ts)
3. Parses component metadata (selectors, templates, inputs, outputs)
4. Identifies component relationships and routing

### Step 2: Generation Phase
The tool generates files:
1. **Page Object Models** - One per component
2. **Test Fixtures** - Shared test setup/teardown
3. **Playwright Configuration** - playwright.config.ts
4. **Selector Files** - Centralized element selectors
5. **Helper Utilities** - Common testing utilities
6. **Sample Tests** - Basic test files for each page object

### Step 3: Output
Displays summary of generated files and any warnings.

## Examples

### Example 1: Basic Generation

Generate POMs with default settings:

```bash
playwright-pom-gen app ./src/my-app
```

**Output:**
```
info: Analyzing Angular application at ./src/my-app
info: Found 12 components in MyApp
Successfully generated 26 files in ./src/my-app/e2e

  - page-objects/home.page.ts
  - page-objects/login.page.ts
  - page-objects/dashboard.page.ts
  - fixtures/base.fixture.ts
  - playwright.config.ts
  - tests/home.spec.ts
  - tests/login.spec.ts
  - tests/dashboard.spec.ts
```

**Exit code:** 0 (success)

### Example 2: Custom Output Directory

Generate to a specific output location:

```bash
playwright-pom-gen app ./src/my-app --output ./playwright-tests
```

**Result:** Files generated in `./playwright-tests` instead of default `./src/my-app/e2e`

### Example 3: With Custom Header

Generate with custom file header:

```bash
playwright-pom-gen app ./src/my-app \
  --header "// Copyright 2026 MyCompany\n// Generated: {GeneratedDate}"
```

**Result:** All generated files include the custom header with placeholder substitution.

### Example 4: Custom Test Suffix

Generate with ".test.ts" suffix instead of ".spec.ts":

```bash
playwright-pom-gen app ./src/my-app --test-suffix test
```

**Result:**
- Tests named: `home.test.ts`, `login.test.ts` (instead of `.spec.ts`)

### Example 5: Complete Workflow with Configuration

```bash
# Create configuration file first
cat > appsettings.json << EOF
{
  "Generator": {
    "OutputDirectoryName": "playwright",
    "TestFileSuffix": "test",
    "DefaultTimeout": 60000,
    "GenerateJsDocComments": true
  }
}
EOF

# Run generation
playwright-pom-gen app ./src/my-app
```

### Example 6: Using in Scripts

**PowerShell script:**
```powershell
# generate-tests.ps1
param(
    [string]$AppPath = "./src/app",
    [string]$OutputPath = "./e2e"
)

Write-Host "Generating Playwright tests for: $AppPath"

$result = & playwright-pom-gen app $AppPath --output $OutputPath

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Generation successful!" -ForegroundColor Green
} else {
    Write-Host "✗ Generation failed!" -ForegroundColor Red
    exit 1
}
```

**Bash script:**
```bash
#!/bin/bash
# generate-tests.sh

APP_PATH="${1:-./src/app}"
OUTPUT_PATH="${2:-./e2e}"

echo "Generating Playwright tests for: $APP_PATH"

if playwright-pom-gen app "$APP_PATH" --output "$OUTPUT_PATH"; then
    echo "✓ Generation successful!"
else
    echo "✗ Generation failed!"
    exit 1
fi
```

## Generated File Structure

When you run the `app` command, the following structure is created:

```
<output-directory>/
├── page-objects/
│   ├── home.page.ts              # Page object for HomeComponent
│   ├── login.page.ts             # Page object for LoginComponent
│   ├── dashboard.page.ts         # Page object for DashboardComponent
│   └── ...                       # One per component
│
├── fixtures/
│   └── base.fixture.ts           # Base test fixture
│
├── selectors/
│   └── app.selectors.ts          # Centralized selectors
│
├── helpers/
│   └── test.helpers.ts           # Testing utilities
│
├── tests/
│   ├── home.spec.ts              # Sample test for home page
│   ├── login.spec.ts             # Sample test for login page
│   ├── dashboard.spec.ts         # Sample test for dashboard
│   └── ...                       # One per component
│
└── playwright.config.ts          # Playwright configuration
```

## Success Criteria

The command succeeds when:
- ✅ Path points to a valid Angular application
- ✅ At least one Angular component is found
- ✅ All files generate without errors
- ✅ Output directory is created/writable
- ✅ Exit code is 0

## Error Handling

### Error: Not a Valid Angular Application

```
Error: '/path' is not a valid Angular application
Exit code: 1
```

**Causes:**
- Path doesn't contain Angular files
- Path doesn't exist
- Path is to a workspace, not an application

**Solutions:**
- Verify the path: `ls <path>`
- Check for Angular files: `ls <path>/**/*.component.ts`
- Use `workspace` command if this is an Angular workspace

### Error: Output Directory Not Writable

```
Error: Failed to write to output directory
Exit code: 1
```

**Causes:**
- No write permissions
- Directory is read-only
- Disk is full

**Solutions:**
- Check permissions: `ls -la <output-directory>`
- Use a different output directory: `--output ./different-path`
- Free up disk space

### Error: No Components Found

```
Warning: Found 0 components in Application
```

**Causes:**
- Path doesn't contain `*.component.ts` files
- Components are in an unexpected location
- TypeScript files are malformed

**Solutions:**
- Verify components exist: `find <path> -name "*.component.ts"`
- Check component file structure
- Review Angular application structure

### Error: Generation Failed

```
Generation failed:
  - Error message 1
  - Error message 2
Exit code: 1
```

**Causes:**
- Component parsing errors
- Template analysis failures
- File system errors

**Solutions:**
- Review error messages in output
- Check component TypeScript syntax
- Verify template files are accessible
- Enable verbose logging: `"Logging": { "LogLevel": { "Default": "Debug" } }`

## Performance Considerations

### Large Applications

For applications with many components (50+):
- Generation may take 10-30 seconds
- Output will contain many files
- Consider using `artifacts` command to generate specific artifacts first

**Optimization tips:**
```bash
# Generate only essential artifacts first
playwright-pom-gen artifacts ./src/my-app --configs --fixtures

# Then generate page objects separately
playwright-pom-gen artifacts ./src/my-app --page-objects
```

### Incremental Generation

Currently, the tool regenerates all files on each run:
- Existing files are overwritten
- Manual changes to generated files will be lost
- Consider version control before regenerating

**Best practice:**
```bash
# Commit before regeneration
git add e2e/
git commit -m "Before POM regeneration"

# Regenerate
playwright-pom-gen app ./src/my-app

# Review changes
git diff
```

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: Generate Playwright Tests

on:
  push:
    paths:
      - 'src/**/*.component.ts'

jobs:
  generate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Generate Playwright POMs
        run: |
          dotnet run --project tools/PlaywrightPomGenerator.Cli -- \
            app ./src/my-app --output ./e2e
      
      - name: Commit generated files
        run: |
          git config user.name "GitHub Actions"
          git config user.email "actions@github.com"
          git add e2e/
          git commit -m "Auto-generate Playwright POMs" || echo "No changes"
          git push
```

### Azure DevOps Example

```yaml
trigger:
  paths:
    include:
      - src/**/*.component.ts

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '10.0.x'

- script: |
    dotnet run --project tools/PlaywrightPomGenerator.Cli -- \
      app ./src/my-app --output ./e2e
  displayName: 'Generate Playwright POMs'

- script: |
    git add e2e/
    git commit -m "Auto-generate POMs" || echo "No changes"
  displayName: 'Commit generated files'
```

## Troubleshooting

See the [Troubleshooting Guide](09-troubleshooting.md) for detailed solutions to common issues.

**Quick diagnostics:**
```bash
# Check Angular application structure
ls -la <app-path>

# Count components
find <app-path> -name "*.component.ts" | wc -l

# Verify output directory permissions
mkdir -p <output-path> && touch <output-path>/test.txt && rm <output-path>/test.txt

# Run with verbose logging
# (Set in appsettings.json: "LogLevel": { "Default": "Debug" })
```

## Next Steps

- Learn about [Generate Workspace Command](03-generate-workspace.md) for multi-project workspaces
- Explore [Generate Artifacts Command](04-generate-artifacts.md) for selective generation
- Understand [Configuration Options](06-configuration.md) to customize output
- Review [Output Structure](08-output-structure.md) to understand generated files
