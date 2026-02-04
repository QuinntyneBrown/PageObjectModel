# Generate Workspace Command

The `workspace` command generates Playwright Page Object Model test files for an **entire Angular workspace** or specific projects within it.

## Command Syntax

```bash
playwright-pom-gen workspace <path> [options]
```

## Arguments

### `path` (Required)
The path to the Angular workspace directory.

- **Type:** String (positional argument)
- **Description:** Root directory of the Angular workspace
- **Must contain:** `angular.json` file (workspace configuration)

**Examples:**
```bash
# Current directory
playwright-pom-gen workspace .

# Specific path
playwright-pom-gen workspace ./my-workspace

# Absolute path (Windows)
playwright-pom-gen workspace C:\projects\my-angular-workspace

# Absolute path (Linux/macOS)
playwright-pom-gen workspace /home/user/projects/my-workspace
```

## Options

### `-o, --output <directory>`
Specifies the output directory for generated files.

- **Type:** String (optional)
- **Default:** `<path>` (workspace root)
- **Description:** Base directory where project test files will be generated

**Examples:**
```bash
# Default output (workspace root)
playwright-pom-gen workspace ./my-workspace

# Custom output directory
playwright-pom-gen workspace ./my-workspace --output ./e2e-tests

# Output to sibling directory
playwright-pom-gen workspace ./my-workspace --output ../workspace-tests
```

### `-p, --project <name>`
Generate tests for a specific project only.

- **Type:** String (optional)
- **Default:** All application projects in the workspace
- **Description:** Name of the project as defined in `angular.json`

**Examples:**
```bash
# Generate for all projects
playwright-pom-gen workspace ./my-workspace

# Generate for specific project
playwright-pom-gen workspace ./my-workspace --project my-app

# Generate for admin portal only
playwright-pom-gen workspace ./my-workspace --project admin-portal
```

### Global Options

See [Global Options Guide](07-global-options.md) for options available to all commands:
- `--header` - Custom file header template
- `--test-suffix` - Test file suffix (default: "spec")

## When to Use

Use the `workspace` command when:

- ✅ You have an **Angular workspace** with `angular.json`
- ✅ Your workspace contains **multiple applications**
- ✅ You want to generate tests for **all or specific projects**
- ✅ You need **consistent test structure across projects**

**Don't use when:**
- ❌ You have a standalone Angular app without `angular.json` → Use [`app` command](02-generate-app.md)
- ❌ You only need specific artifacts → Use [`artifacts` command](04-generate-artifacts.md)

## How It Works

### Step 1: Workspace Analysis
1. Validates the path contains `angular.json`
2. Parses workspace configuration
3. Identifies all projects and their types (application, library)
4. Filters for application projects (or specific project if `--project` specified)

### Step 2: Project Analysis
For each application project:
1. Locates project source directory
2. Scans for Angular components
3. Parses component metadata
4. Builds component hierarchy

### Step 3: Generation
For each application:
1. Generates Page Object Models
2. Creates test fixtures
3. Generates Playwright configuration
4. Creates selector files
5. Generates helper utilities
6. Creates sample tests

### Step 4: Output
Displays summary grouped by project with file counts and warnings.

## Examples

### Example 1: Generate for All Projects

```bash
playwright-pom-gen workspace ./my-workspace
```

**angular.json:**
```json
{
  "projects": {
    "main-app": { "projectType": "application", ... },
    "admin-portal": { "projectType": "application", ... },
    "shared-lib": { "projectType": "library", ... }
  }
}
```

**Output:**
```
info: Analyzing Angular workspace at ./my-workspace
info: Found 3 projects in workspace
Successfully generated 48 files

Project: main-app
  - main-app/e2e/page-objects/home.page.ts
  - main-app/e2e/page-objects/login.page.ts
  - main-app/e2e/fixtures/base.fixture.ts
  - main-app/e2e/playwright.config.ts

Project: admin-portal
  - admin-portal/e2e/page-objects/dashboard.page.ts
  - admin-portal/e2e/page-objects/users.page.ts
  - admin-portal/e2e/fixtures/base.fixture.ts
  - admin-portal/e2e/playwright.config.ts
```

**Note:** The `shared-lib` library project is skipped (only applications are processed).

**Exit code:** 0 (success)

### Example 2: Generate for Specific Project

```bash
playwright-pom-gen workspace ./my-workspace --project main-app
```

**Output:**
```
info: Analyzing Angular workspace at ./my-workspace
info: Found 3 projects in workspace
Successfully generated 24 files

Project: main-app
  - main-app/e2e/page-objects/home.page.ts
  - main-app/e2e/page-objects/login.page.ts
  - main-app/e2e/page-objects/dashboard.page.ts
  - main-app/e2e/fixtures/base.fixture.ts
  - main-app/e2e/playwright.config.ts
```

### Example 3: Custom Output Directory

```bash
playwright-pom-gen workspace ./my-workspace --output ./tests
```

**Result:**
```
./tests/
├── main-app/
│   └── e2e/
│       ├── page-objects/
│       ├── fixtures/
│       └── playwright.config.ts
└── admin-portal/
    └── e2e/
        ├── page-objects/
        ├── fixtures/
        └── playwright.config.ts
```

### Example 4: With Global Options

```bash
playwright-pom-gen workspace ./my-workspace \
  --project main-app \
  --test-suffix "test" \
  --header "// Company: ACME Corp\n// Generated: {GeneratedDate}"
```

**Result:**
- Tests named `*.test.ts` instead of `*.spec.ts`
- All files have custom header with company name and generation date

### Example 5: Monorepo with Custom Structure

For Nx/Nrwl workspaces or custom monorepos:

```bash
# Generate for all apps in projects/ directory
playwright-pom-gen workspace . --output ./e2e

# Generate for specific app
playwright-pom-gen workspace . --project store-frontend
```

### Example 6: CI/CD Pipeline Integration

**GitHub Actions:**
```yaml
name: Generate Workspace Tests

on:
  schedule:
    - cron: '0 2 * * 0'  # Weekly on Sunday at 2 AM

jobs:
  generate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Generate tests for all projects
        run: |
          playwright-pom-gen workspace . --output ./e2e-tests
      
      - name: Create Pull Request
        uses: peter-evans/create-pull-request@v5
        with:
          commit-message: Update generated Playwright tests
          title: 'chore: Update Playwright POM tests'
          body: Auto-generated Playwright Page Object Models
```

## Generated File Structure

When you run the `workspace` command, the following structure is created for each application project:

```
<output-directory>/
├── <project-1>/
│   └── e2e/
│       ├── page-objects/
│       │   ├── home.page.ts
│       │   ├── login.page.ts
│       │   └── ...
│       ├── fixtures/
│       │   └── base.fixture.ts
│       ├── selectors/
│       │   └── app.selectors.ts
│       ├── helpers/
│       │   └── test.helpers.ts
│       ├── tests/
│       │   ├── home.spec.ts
│       │   └── ...
│       └── playwright.config.ts
│
├── <project-2>/
│   └── e2e/
│       ├── page-objects/
│       ├── fixtures/
│       ├── selectors/
│       ├── helpers/
│       ├── tests/
│       └── playwright.config.ts
│
└── ...
```

### Project Isolation

Each project gets its own:
- Complete test structure
- Independent Playwright configuration
- Isolated fixtures and helpers
- Separate page objects

This allows:
- Projects to run tests independently
- Different Playwright configurations per project
- Selective test execution
- Project-specific customization

## Success Criteria

The command succeeds when:
- ✅ Path points to a valid Angular workspace with `angular.json`
- ✅ At least one application project is found (or specified project exists)
- ✅ All files generate without errors for each project
- ✅ Output directories are created/writable
- ✅ Exit code is 0

## Error Handling

### Error: Not a Valid Angular Workspace

```
Error: './path' is not a valid Angular workspace (no angular.json found)
Exit code: 1
```

**Causes:**
- `angular.json` file doesn't exist in the specified path
- Path is incorrect
- Path points to an application, not a workspace

**Solutions:**
```bash
# Verify angular.json exists
ls -la <path>/angular.json

# Check if it's an application instead
playwright-pom-gen app <path>

# Use correct workspace path
playwright-pom-gen workspace <correct-path>
```

### Error: Project Not Found

```
Error: Project 'xyz' not found in workspace
Exit code: 1
```

**Causes:**
- Project name doesn't exist in `angular.json`
- Typo in project name
- Project name is case-sensitive

**Solutions:**
```bash
# List all projects in workspace
cat angular.json | grep -A 1 '"projects"'

# Or use jq for better formatting
cat angular.json | jq '.projects | keys'

# Use correct project name (case-sensitive)
playwright-pom-gen workspace . --project MyApp
```

### Error: No Application Projects Found

```
Warning: No application projects found in workspace
```

**Causes:**
- Workspace only contains library projects
- All projects have `"projectType": "library"`
- `angular.json` structure is unexpected

**Solutions:**
```bash
# Verify project types
cat angular.json | jq '.projects[] | {name: .name, type: .projectType}'

# If you need to generate for a library, use app command instead
playwright-pom-gen app ./projects/my-lib
```

### Error: Generation Failed for Some Projects

```
Successfully generated 24 files

Warnings:
  - Failed to generate for project 'broken-app': Component parsing error
```

**Causes:**
- Malformed components in one project
- Missing files or dependencies
- TypeScript syntax errors

**Solutions:**
```bash
# Generate for working projects only
playwright-pom-gen workspace . --project working-app

# Fix the broken project
cd projects/broken-app
# ... fix issues ...

# Regenerate
playwright-pom-gen workspace . --project broken-app
```

## Performance Considerations

### Large Workspaces

For workspaces with many projects or components:
- **Generation time:** 5-10 seconds per project (varies by project size)
- **Output size:** Can be hundreds of files across projects
- **Memory usage:** Proportional to total component count

**Optimization strategies:**

1. **Generate specific projects:**
```bash
# Instead of all projects
playwright-pom-gen workspace .

# Generate one at a time
playwright-pom-gen workspace . --project app1
playwright-pom-gen workspace . --project app2
```

2. **Use artifacts command for selective generation:**
```bash
# Generate only fixtures and configs first
playwright-pom-gen artifacts . --fixtures --configs

# Generate page objects later
playwright-pom-gen artifacts . --page-objects
```

3. **Parallel generation in CI:**
```yaml
# GitHub Actions matrix strategy
jobs:
  generate:
    strategy:
      matrix:
        project: [app1, app2, app3, app4]
    steps:
      - run: playwright-pom-gen workspace . --project ${{ matrix.project }}
```

### Incremental Regeneration

Current behavior:
- All files are regenerated on each run
- Manual edits are overwritten
- No incremental/partial regeneration support

**Best practice workflow:**
```bash
# 1. Version control before regeneration
git status
git add .
git commit -m "Before POM regeneration"

# 2. Regenerate
playwright-pom-gen workspace .

# 3. Review changes per project
git diff -- project1/e2e/
git diff -- project2/e2e/

# 4. Keep or discard changes
git add .
# or
git checkout -- <files-to-discard>
```

## Common Workspace Structures

### Nx Workspace

```
my-nx-workspace/
├── angular.json
├── apps/
│   ├── app1/
│   ├── app2/
│   └── app3/
└── libs/
    └── shared/
```

**Command:**
```bash
playwright-pom-gen workspace . --output ./e2e-tests
```

### Angular CLI Workspace

```
my-workspace/
├── angular.json
└── projects/
    ├── main-app/
    ├── admin-portal/
    └── shared-lib/
```

**Command:**
```bash
playwright-pom-gen workspace .
```

### Custom Monorepo

```
monorepo/
├── angular.json
├── packages/
│   ├── frontend/
│   ├── backend/
│   └── admin/
└── shared/
```

**Command:**
```bash
playwright-pom-gen workspace . --project frontend
playwright-pom-gen workspace . --project admin
```

## Project Selection Logic

The tool processes projects based on:

1. **Project Type Filter:**
   - Only `"projectType": "application"` projects are processed
   - Library projects are automatically skipped

2. **Project Name Filter (if --project specified):**
   - Must match exactly (case-sensitive)
   - If not found, command fails with error

3. **Source Root Detection:**
   - Uses `sourceRoot` from `angular.json`
   - Falls back to `<projectRoot>/src`

**Example angular.json:**
```json
{
  "projects": {
    "my-app": {
      "projectType": "application",
      "sourceRoot": "projects/my-app/src",
      "root": "projects/my-app"
    },
    "shared-lib": {
      "projectType": "library",
      "sourceRoot": "projects/shared-lib/src"
    }
  }
}
```

**Behavior:**
- `my-app` will be processed (application)
- `shared-lib` will be skipped (library)
- Files generated in `projects/my-app/e2e/`

## Integration Patterns

### Separate Test Workspace

Keep tests in a separate directory:

```bash
# Generate tests outside workspace
playwright-pom-gen workspace ./src/workspace --output ./tests/e2e

# Directory structure
project/
├── src/
│   └── workspace/        # Source code
└── tests/
    └── e2e/              # Generated tests
        ├── app1/
        └── app2/
```

### Per-Project E2E Directories

Generate inside each project:

```bash
# Default behavior (generates in project directories)
playwright-pom-gen workspace .

# Result
workspace/
└── projects/
    ├── app1/
    │   ├── src/
    │   └── e2e/          # Generated tests
    └── app2/
        ├── src/
        └── e2e/          # Generated tests
```

### Shared Test Infrastructure

Generate shared test infrastructure once:

```bash
# Generate fixtures and configs once at root
playwright-pom-gen artifacts . --fixtures --configs --output ./shared-tests

# Generate page objects per project
playwright-pom-gen workspace . --project app1
playwright-pom-gen workspace . --project app2
```

## Troubleshooting

### Problem: Generated files in unexpected location

**Check:**
1. Output option: `--output` value
2. Project `sourceRoot` in `angular.json`
3. Current working directory

**Solution:**
```bash
# Use absolute paths for clarity
playwright-pom-gen workspace /full/path/to/workspace \
  --output /full/path/to/output
```

### Problem: Some projects not generating

**Check:**
1. Project type (must be "application")
2. Component files exist
3. Permissions on project directory

**Debug:**
```bash
# List application projects
cat angular.json | jq '.projects | to_entries[] | select(.value.projectType == "application") | .key'

# Check for components in each project
find projects/app1 -name "*.component.ts"
```

### Problem: Workspace detection fails

**Check:**
1. `angular.json` exists and is valid JSON
2. File permissions
3. Path is correct

**Verify:**
```bash
# Validate angular.json
cat angular.json | jq '.' > /dev/null && echo "Valid JSON" || echo "Invalid JSON"

# Check file exists
test -f angular.json && echo "Found" || echo "Not found"
```

## Next Steps

- Learn about [Generate Artifacts Command](04-generate-artifacts.md) for selective generation
- Understand [Configuration Options](06-configuration.md) to customize per-project settings
- Review [Output Structure](08-output-structure.md) for workspace-specific patterns
- Check [Best Practices](10-best-practices.md) for monorepo strategies
