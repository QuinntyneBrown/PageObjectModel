# Global Options

Global options are available across all commands in the Playwright POM Generator CLI.

## Available Global Options

### --header

Custom file header template for all generated files.

**Syntax:**
```bash
playwright-pom-gen <command> <args> --header "<template>"
```

**Supports Placeholders:**
- `{FileName}` - Name of the generated file
- `{GeneratedDate}` - ISO 8601 formatted date/time of generation
- `{ToolVersion}` - Tool version from configuration

**Examples:**

```bash
# Simple header
playwright-pom-gen app . --header "// Auto-generated file"

# With date
playwright-pom-gen app . --header "// Generated on {GeneratedDate}"

# Multi-line (use \n)
playwright-pom-gen app . --header "/**\n * File: {FileName}\n * Date: {GeneratedDate}\n */"

# Copyright notice
playwright-pom-gen app . --header "// Copyright 2026 ACME Corp"

# Complete JSDoc
playwright-pom-gen workspace . \
  --header "/**\n * @fileoverview {FileName}\n * @generated {GeneratedDate}\n * @version {ToolVersion}\n */"
```

**Result:**
```typescript
// Generated on 2026-02-04T02:35:48.703Z

import { Page } from '@playwright/test';

export class HomePage {
  // ...
}
```

**Override Configuration:**
The `--header` option overrides:
- `appsettings.json` `FileHeader` setting
- `POMGEN_Generator__FileHeader` environment variable

**Default:**
If not specified, uses value from configuration (or empty string if not configured).

### --test-suffix

Suffix for generated test files (before `.ts` extension).

**Syntax:**
```bash
playwright-pom-gen <command> <args> --test-suffix "<suffix>"
```

**Common Values:**
- `"spec"` (default) - Generates `*.spec.ts`
- `"test"` - Generates `*.test.ts`
- `"e2e"` - Generates `*.e2e.ts`
- Any custom suffix

**Examples:**

```bash
# Use .test.ts suffix
playwright-pom-gen app . --test-suffix "test"

# Use .e2e.ts suffix
playwright-pom-gen workspace . --test-suffix "e2e"

# Custom suffix
playwright-pom-gen app . --test-suffix "playwright"
```

**Result:**

With `--test-suffix "test"`:
```
tests/
├── home.test.ts
├── login.test.ts
└── dashboard.test.ts
```

With `--test-suffix "spec"` (default):
```
tests/
├── home.spec.ts
├── login.spec.ts
└── dashboard.spec.ts
```

**Override Configuration:**
The `--test-suffix` option overrides:
- `appsettings.json` `TestFileSuffix` setting
- `POMGEN_Generator__TestFileSuffix` environment variable

**Default:**
If not specified, uses value from configuration (or `"spec"` if not configured).

## Using Global Options

### With Any Command

Global options work with all commands:

```bash
# app command
playwright-pom-gen app ./src/my-app \
  --header "// Copyright 2026" \
  --test-suffix "test"

# workspace command
playwright-pom-gen workspace . \
  --project my-app \
  --header "// Generated: {GeneratedDate}" \
  --test-suffix "e2e"

# artifacts command
playwright-pom-gen artifacts . \
  --fixtures --configs \
  --header "// Auto-generated"

# signalr-mock command
playwright-pom-gen signalr-mock ./fixtures \
  --header "// SignalR Mock v{ToolVersion}"
```

### Combining with Command Options

Global options can be mixed with command-specific options:

```bash
# Global + command-specific options
playwright-pom-gen app ./src/my-app \
  --output ./e2e \
  --header "// Copyright 2026" \
  --test-suffix "test"

playwright-pom-gen workspace . \
  --project my-app \
  --output ./tests \
  --header "// Generated: {GeneratedDate}" \
  --test-suffix "spec"

playwright-pom-gen artifacts . \
  --fixtures --configs \
  --output ./tests \
  --header "// Auto-generated artifacts"
```

### Order Doesn't Matter

Global options can appear before or after other options:

```bash
# Before
playwright-pom-gen --header "// Copyright" app ./src/my-app

# After
playwright-pom-gen app ./src/my-app --header "// Copyright"

# Mixed
playwright-pom-gen --header "// Copyright" app ./src/my-app --test-suffix "test"
```

## Configuration Priority

When the same setting is configured in multiple places:

**Priority Order (highest to lowest):**
1. Command-line global options (`--header`, `--test-suffix`)
2. Environment variables (`POMGEN_Generator__FileHeader`, etc.)
3. Configuration file (`appsettings.json`)
4. Default values

**Example:**

```json
// appsettings.json
{
  "Generator": {
    "FileHeader": "// Config file header",
    "TestFileSuffix": "spec"
  }
}
```

```bash
# Environment variable
export POMGEN_Generator__FileHeader="// Environment header"
export POMGEN_Generator__TestFileSuffix="test"

# Command-line (wins)
playwright-pom-gen app . \
  --header "// Command-line header" \
  --test-suffix "e2e"

# Result:
# - FileHeader: "// Command-line header"
# - TestFileSuffix: "e2e"
```

## Examples by Use Case

### Use Case 1: Corporate Standard

```bash
# All generated files include copyright notice
playwright-pom-gen app ./src/my-app \
  --header "// Copyright 2026 ACME Corporation\n// Confidential and Proprietary"
```

### Use Case 2: CI/CD Pipeline

```bash
# Include build information in generated files
BUILD_DATE=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
BUILD_NUMBER="1234"

playwright-pom-gen workspace . \
  --header "// CI Build #${BUILD_NUMBER}\n// Generated: ${BUILD_DATE}"
```

### Use Case 3: Team Convention

```bash
# Team uses .test.ts suffix
playwright-pom-gen app ./src/my-app --test-suffix "test"
```

### Use Case 4: Multiple Projects, Different Standards

```bash
# Project A: .spec.ts with minimal header
playwright-pom-gen app ./projects/app-a \
  --test-suffix "spec" \
  --header "// Auto-generated"

# Project B: .test.ts with detailed header
playwright-pom-gen app ./projects/app-b \
  --test-suffix "test" \
  --header "/**\n * @generated {GeneratedDate}\n * @version {ToolVersion}\n */"
```

### Use Case 5: No Header

```bash
# Generate without any header
playwright-pom-gen app . --header ""
```

## Best Practices

### 1. Use Configuration Files for Defaults

Don't use global options for every invocation. Set defaults in `appsettings.json`:

```json
{
  "Generator": {
    "FileHeader": "// Auto-generated by PlaywrightPomGenerator",
    "TestFileSuffix": "spec"
  }
}
```

Use global options only when you need to override.

### 2. Escape Special Characters

When using headers with special characters or newlines:

**Windows PowerShell:**
```powershell
playwright-pom-gen app . --header "// Line 1`n// Line 2"
```

**Linux/macOS Bash:**
```bash
playwright-pom-gen app . --header "// Line 1\n// Line 2"
```

### 3. Document Team Standards

Create a script for team members:

```bash
#!/bin/bash
# generate-pom.sh - Team standard for generating POMs

playwright-pom-gen "$@" \
  --header "// Copyright 2026 ACME Corp" \
  --test-suffix "spec"
```

Usage:
```bash
./generate-pom.sh app ./src/my-app
```

### 4. Environment-Specific Headers

```bash
# Development
if [ "$ENV" = "dev" ]; then
  HEADER="// Development build - {GeneratedDate}"
elif [ "$ENV" = "prod" ]; then
  HEADER="// Production build - {GeneratedDate}\n// Version: {ToolVersion}"
fi

playwright-pom-gen app . --header "$HEADER"
```

### 5. Use Placeholders

Always use placeholders for dynamic values:

```bash
# Good: Uses placeholders
playwright-pom-gen app . --header "// Generated: {GeneratedDate}"

# Bad: Hardcoded date
playwright-pom-gen app . --header "// Generated: 2026-02-04"
```

## Common Patterns

### Pattern 1: Minimal Header

```bash
--header "// Auto-generated"
```

### Pattern 2: With Date

```bash
--header "// Generated on {GeneratedDate}"
```

### Pattern 3: JSDoc Style

```bash
--header "/**\n * @fileoverview {FileName}\n * @generated {GeneratedDate}\n */"
```

### Pattern 4: Copyright Notice

```bash
--header "// Copyright 2026 ACME Corporation\n// All rights reserved"
```

### Pattern 5: Comprehensive

```bash
--header "/**\n * File: {FileName}\n * Generated: {GeneratedDate}\n * Tool Version: {ToolVersion}\n * \n * This file was automatically generated.\n * DO NOT EDIT - changes will be overwritten.\n */"
```

## Troubleshooting

### Issue: Header Not Applied

**Check:**
```bash
# Verify option is being passed
playwright-pom-gen app . --header "// Test" -v

# Check generated file
cat e2e/page-objects/home.page.ts | head -n 5
```

### Issue: Newlines Not Working

**Windows PowerShell:**
Use backtick for newlines:
```powershell
--header "// Line 1`n// Line 2"
```

**Linux/macOS:**
Use backslash-n:
```bash
--header "// Line 1\n// Line 2"
```

### Issue: Test Suffix Not Applied

**Check file names:**
```bash
ls e2e/tests/

# Should see *.test.ts if --test-suffix "test" was used
```

## Next Steps

- Learn about [Configuration Files](06-configuration.md) for persistent settings
- Understand [Output Structure](08-output-structure.md) affected by these options
- Review [Best Practices](10-best-practices.md) for option management
- See command-specific guides:
  - [Generate App](02-generate-app.md)
  - [Generate Workspace](03-generate-workspace.md)
  - [Generate Artifacts](04-generate-artifacts.md)
