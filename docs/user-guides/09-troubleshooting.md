# Troubleshooting Guide

Common issues and solutions when using the Playwright POM Generator.

## Installation Issues

### .NET SDK Not Found

**Error:**
```
'dotnet' is not recognized as an internal or external command
```

**Solutions:**
1. Install .NET 10.0 SDK from https://dotnet.microsoft.com/download
2. Restart terminal after installation
3. Verify: `dotnet --version`

### Build Fails

**Error:**
```
error MSB1009: Project file does not exist
```

**Solutions:**
1. Ensure you're in the correct directory
2. Verify `PlaywrightPomGenerator.sln` exists
3. Run: `dotnet restore` before building

## Path and Directory Issues

### Not a Valid Angular Application

**Error:**
```
Error: './path' is not a valid Angular application
```

**Causes:**
- Path doesn't contain Angular components
- Path is incorrect
- Using `app` command on workspace

**Solutions:**
```bash
# Verify path contains components
find <path> -name "*.component.ts"

# Check if it's a workspace (has angular.json)
ls <path>/angular.json

# Use correct command:
playwright-pom-gen workspace .  # If workspace
playwright-pom-gen app <path>   # If single app
```

### Not a Valid Angular Workspace

**Error:**
```
Error: './path' is not a valid Angular workspace (no angular.json found)
```

**Solutions:**
```bash
# Verify angular.json exists
ls angular.json

# Use app command if it's a single app
playwright-pom-gen app .
```

### Output Directory Not Writable

**Error:**
```
Error: Failed to write to output directory
```

**Solutions:**
```bash
# Check permissions
ls -la <output-directory>

# Use different directory
playwright-pom-gen app . --output ./different-path

# Create directory first
mkdir -p ./e2e
playwright-pom-gen app . --output ./e2e
```

## Component Detection Issues

### No Components Found

**Warning:**
```
Warning: Found 0 components in Application
```

**Causes:**
- No `*.component.ts` files in path
- Components in excluded directories
- Incorrect path

**Solutions:**
```bash
# Verify components exist
find <path> -name "*.component.ts"

# Count components
find <path> -name "*.component.ts" | wc -l

# Check common locations
ls src/app/**/*.component.ts
ls projects/*/src/**/*.component.ts
```

### Components Skipped

**Warning:**
```
Warning: Skipped 5 components due to parsing errors
```

**Solutions:**
1. Check TypeScript syntax in components
2. Enable debug logging to see which components failed:
   ```json
   {
     "Logging": {
       "LogLevel": { "Default": "Debug" }
     }
   }
   ```
3. Run TypeScript compiler: `tsc --noEmit`

## Configuration Issues

### Configuration Not Loading

**Symptoms:**
- Default values used instead of configuration
- Changes to appsettings.json have no effect

**Solutions:**
```bash
# Verify file exists
ls appsettings.json

# Validate JSON syntax
cat appsettings.json | jq '.'

# Check file location (should be in current directory or CLI directory)
pwd
ls appsettings.json

# Check file permissions
ls -la appsettings.json
```

### Environment Variables Not Working

**Symptoms:**
- Environment variables seem ignored

**Solutions:**
```bash
# Verify variable is set (Linux/macOS)
printenv | grep POMGEN

# Verify variable is set (Windows)
Get-ChildItem Env: | Where-Object Name -like "POMGEN*"

# Check variable name format
# Correct: POMGEN_Generator__TestFileSuffix
# Wrong:   POMGEN_Generator_TestFileSuffix  (single underscore)

# Test with explicit value
POMGEN_Generator__TestFileSuffix="test" playwright-pom-gen app . 
```

### Invalid Configuration Values

**Error:**
```
Configuration error: DefaultTimeout must be between 1000 and 300000
```

**Solutions:**
```json
// Fix invalid values
{
  "Generator": {
    "DefaultTimeout": 30000  // Must be 1000-300000
  }
}
```

## Command-Line Issues

### No Artifact Type Specified

**Error:**
```
Error: At least one artifact type must be specified.
```

**Cause:**
Using `artifacts` command without any artifact flags.

**Solution:**
```bash
# Specify at least one
playwright-pom-gen artifacts . --fixtures

# Or use --all
playwright-pom-gen artifacts . --all
```

### Project Not Found

**Error:**
```
Error: Project 'xyz' not found in workspace
```

**Solutions:**
```bash
# List available projects
cat angular.json | jq '.projects | keys'

# Check project name (case-sensitive)
playwright-pom-gen workspace . --project MyApp  # Not myapp

# Omit --project to generate for all
playwright-pom-gen workspace .
```

### Command Not Recognized

**Error:**
```
'playwright-pom-gen' is not recognized...
```

**Causes:**
- Tool not built/published
- Not in PATH
- Running from source

**Solutions:**
```bash
# Run from source
cd src/PlaywrightPomGenerator.Cli
dotnet run -- app <path>

# Or use full path to executable
/path/to/PlaywrightPomGenerator.Cli.exe app <path>
```

## Generation Issues

### Generation Failed

**Error:**
```
Generation failed:
  - Error message 1
  - Error message 2
```

**Solutions:**
1. **Enable debug logging:**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug",
         "PlaywrightPomGenerator": "Trace"
       }
     }
   }
   ```

2. **Check error messages** in output for specific issues

3. **Verify file system permissions**

4. **Check disk space:** `df -h` (Linux/macOS) or `Get-PSDrive` (Windows)

### Partial Generation

**Symptoms:**
- Some files generated, others missing
- Warnings in output

**Solutions:**
1. Check warnings in output
2. Review generated file list
3. Regenerate specific artifacts:
   ```bash
   playwright-pom-gen artifacts . --page-objects
   ```

### Generated Files Are Empty

**Causes:**
- Template engine error
- Component parsing failed

**Solutions:**
1. Enable debug logging
2. Check component TypeScript syntax
3. Verify templates exist in CLI directory

## Runtime Issues

### Tests Don't Run

**Error:**
```
Error: Cannot find module './page-objects/home.page'
```

**Solutions:**
```bash
# Install dependencies
npm install @playwright/test

# Verify imports in test files
cat e2e/tests/home.spec.ts

# Check file paths and names
ls e2e/page-objects/
```

### Type Errors in Generated Files

**Error:**
```
error TS2304: Cannot find name 'Page'
```

**Solutions:**
```bash
# Install Playwright
npm install -D @playwright/test

# Install browser binaries
npx playwright install

# Verify TypeScript configuration
cat tsconfig.json
```

### Selectors Not Found

**Error in test:**
```
Error: Locator 'data-testid=home-title' not found
```

**Solutions:**
1. Verify selectors match your HTML
2. Update generated selectors if needed
3. Check component templates for actual attributes

## Platform-Specific Issues

### Windows: Path Separators

**Issue:**
Forward slashes not working in paths.

**Solution:**
Use backslashes on Windows:
```powershell
playwright-pom-gen app .\src\my-app
```

### Linux/macOS: Permission Denied

**Error:**
```
Permission denied: ./e2e
```

**Solutions:**
```bash
# Fix permissions
chmod +w ./e2e

# Use different directory
playwright-pom-gen app . --output ~/tests
```

### Windows: Long Path Names

**Error:**
```
Error: The specified path is too long
```

**Solutions:**
1. Enable long paths in Windows
2. Use shorter output directory
3. Move project closer to root: `C:\proj\`

## Performance Issues

### Generation Takes Too Long

**Symptoms:**
- Generation takes > 1 minute for small project
- Tool appears hung

**Solutions:**
1. Check CPU/memory usage
2. Reduce component count (if testing)
3. Use `artifacts` command for selective generation:
   ```bash
   playwright-pom-gen artifacts . --fixtures --configs
   ```

### Large Output Size

**Symptoms:**
- Hundreds of files generated
- Disk space warning

**Solutions:**
1. Generate for specific project only:
   ```bash
   playwright-pom-gen workspace . --project my-app
   ```

2. Generate only needed artifacts:
   ```bash
   playwright-pom-gen artifacts . --page-objects
   ```

## Debugging Techniques

### Enable Verbose Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Trace",
      "PlaywrightPomGenerator": "Trace"
    }
  }
}
```

### Check Version

```bash
dotnet --version  # .NET version
playwright-pom-gen --version  # Tool version (if supported)
```

### Validate Environment

```bash
# Check .NET
dotnet --info

# Check Node.js (if running tests)
node --version
npm --version

# Check Playwright
npx playwright --version
```

### Test with Minimal Config

```json
{
  "Generator": {
    "TestFileSuffix": "spec"
  }
}
```

### Clean and Rebuild

```bash
# Clean output
rm -rf ./e2e

# Regenerate
playwright-pom-gen app .
```

### Verify File System

```bash
# Check available space
df -h .  # Linux/macOS
Get-PSDrive C  # Windows

# Check permissions
ls -la .
ls -la ./e2e

# Check for file locks
lsof | grep e2e  # Linux/macOS
```

## Getting Help

### Collect Information

When reporting issues, include:

1. **Command used:**
   ```bash
   playwright-pom-gen app ./src/my-app --output ./e2e
   ```

2. **Error message:**
   ```
   Error: Not a valid Angular application
   ```

3. **Environment:**
   ```
   OS: Windows 11
   .NET: 10.0.100
   Project type: Angular 17
   ```

4. **Configuration:**
   ```json
   {
     "Generator": { ... }
   }
   ```

5. **Directory structure:**
   ```
   src/
   ├── app/
   │   ├── home/
   │   │   ├── home.component.ts
   │   │   └── home.component.html
   ```

### Check Documentation

- [Getting Started](01-getting-started.md)
- [Configuration Guide](06-configuration.md)
- Command-specific guides:
  - [Generate App](02-generate-app.md)
  - [Generate Workspace](03-generate-workspace.md)
  - [Generate Artifacts](04-generate-artifacts.md)

### Community Support

- Check existing issues on GitHub
- Search documentation
- Ask in community forums

## Quick Reference

```bash
# Verify installation
dotnet --version

# Check path structure
ls <path>
find <path> -name "*.component.ts"

# Validate config
cat appsettings.json | jq '.'

# Test minimal command
playwright-pom-gen app . --output ./temp

# Check output
ls -R ./temp

# Enable debug logging
# (in appsettings.json: "LogLevel": { "Default": "Debug" })

# Clean and retry
rm -rf ./e2e
playwright-pom-gen app .
```
