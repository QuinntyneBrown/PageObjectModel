using System.Diagnostics;

namespace PlaygroundRunner;

class Program
{
    private const string RepoUrl = "https://github.com/QuinntyneBrown/Books";
    private const string AngularWorkspacePath = "src/Ui";
    private const string TargetLibrary = "components"; // Angular library within the workspace
    private const bool GenerateSignalRMock = true; // Generate SignalR mock fixture

    static async Task<int> Main(string[] args)
    {
        var artifactsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts"));
        var cliProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlaywrightPomGenerator.Cli", "PlaywrightPomGenerator.Cli.csproj"));

        Console.WriteLine("=== Playwright POM Generator Playground ===");
        Console.WriteLine();
        Console.WriteLine($"Repository: {RepoUrl}");
        Console.WriteLine($"Angular workspace: {AngularWorkspacePath}");
        Console.WriteLine($"Target library: {TargetLibrary}");
        Console.WriteLine($"Artifacts output: {artifactsPath}");
        Console.WriteLine($"CLI project: {cliProjectPath}");
        Console.WriteLine();

        // Create temp directory for cloning
        var tempDir = Path.Combine(Path.GetTempPath(), $"Books_{Guid.NewGuid():N}");

        try
        {
            // Step 1: Clone the repository
            Console.WriteLine($"Step 1: Cloning {RepoUrl} to {tempDir}...");
            var cloneResult = await RunProcessAsync("git", $"clone --depth 1 {RepoUrl} \"{tempDir}\"");
            if (cloneResult != 0)
            {
                Console.Error.WriteLine("Failed to clone repository");
                return 1;
            }
            Console.WriteLine("Clone completed successfully.");
            Console.WriteLine();

            // Step 2: Verify the Angular workspace exists
            var angularWorkspacePath = Path.Combine(tempDir, AngularWorkspacePath);
            var angularJsonPath = Path.Combine(angularWorkspacePath, "angular.json");

            if (!File.Exists(angularJsonPath))
            {
                Console.Error.WriteLine($"Angular workspace not found at: {angularWorkspacePath}");
                Console.Error.WriteLine("Expected angular.json not found.");
                return 1;
            }

            Console.WriteLine($"Step 2: Found Angular workspace at {angularWorkspacePath}");
            Console.WriteLine();

            // Step 3: Ensure artifacts directory exists (clean if exists)
            if (Directory.Exists(artifactsPath))
            {
                Console.WriteLine("Cleaning existing artifacts...");
                try
                {
                    Directory.Delete(artifactsPath, recursive: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not fully clean artifacts directory: {ex.Message}");
                }
            }
            Directory.CreateDirectory(artifactsPath);

            // Step 4: Run the POM Generator CLI
            Console.WriteLine($"Step 3: Running PlaywrightPomGenerator...");
            Console.WriteLine();

            string cliArgs;
            int generateResult;

            // Use the 'lib' command for Angular library
            var libraryPath = Path.Combine(angularWorkspacePath, "projects", TargetLibrary);
            cliArgs = $"run --project \"{cliProjectPath}\" -- lib \"{libraryPath}\" -o \"{artifactsPath}\"";
            Console.WriteLine($"Executing: dotnet {cliArgs}");
            Console.WriteLine();

            generateResult = await RunProcessAsync("dotnet", cliArgs);
            if (generateResult != 0)
            {
                Console.Error.WriteLine("Generation failed with lib command. Trying workspace command...");

                // Fallback: try workspace command to generate for all projects including libraries
                cliArgs = $"run --project \"{cliProjectPath}\" -- workspace \"{angularWorkspacePath}\" -o \"{artifactsPath}\"";
                Console.WriteLine($"Executing: dotnet {cliArgs}");
                Console.WriteLine();

                generateResult = await RunProcessAsync("dotnet", cliArgs);
                if (generateResult != 0)
                {
                    Console.Error.WriteLine("Generation failed with workspace command as well.");
                    return 1;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Generation completed successfully.");
            Console.WriteLine();

            // Step 4b: Generate SignalR mock fixture if requested
            if (GenerateSignalRMock)
            {
                Console.WriteLine("Step 3b: Generating SignalR mock fixture...");

                // Determine the output path for signalr mock
                var signalrOutputPath = artifactsPath;

                cliArgs = $"run --project \"{cliProjectPath}\" -- signalr-mock \"{signalrOutputPath}\"";
                Console.WriteLine($"Executing: dotnet {cliArgs}");
                Console.WriteLine();

                var signalrResult = await RunProcessAsync("dotnet", cliArgs);
                if (signalrResult != 0)
                {
                    Console.WriteLine("Warning: SignalR mock generation failed, but continuing...");
                }
                else
                {
                    Console.WriteLine("SignalR mock fixture generated successfully.");
                }
                Console.WriteLine();
            }

            // Step 5: List generated files
            Console.WriteLine("Step 4: Generated files:");
            if (Directory.Exists(artifactsPath))
            {
                var files = Directory.GetFiles(artifactsPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(artifactsPath, file);
                    Console.WriteLine($"  - {relativePath}");
                }

                if (files.Length == 0)
                {
                    Console.WriteLine("  (no files generated)");
                }
            }
            Console.WriteLine();

            // Step 6: Initialize npm and install Playwright
            Console.WriteLine("Step 5: Installing Playwright dependencies...");
            var packageJsonPath = Path.Combine(artifactsPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                // Create a basic package.json
                var packageJson = """
                {
                  "name": "generated-playwright-tests",
                  "version": "1.0.0",
                  "description": "Generated Playwright Page Object Model tests",
                  "scripts": {
                    "test": "playwright test",
                    "test:headed": "playwright test --headed",
                    "test:ui": "playwright test --ui",
                    "typecheck": "tsc --noEmit"
                  },
                  "devDependencies": {
                    "@playwright/test": "^1.40.0",
                    "@types/node": "^20.0.0",
                    "rxjs": "^7.8.0",
                    "typescript": "^5.0.0"
                  }
                }
                """;
                await File.WriteAllTextAsync(packageJsonPath, packageJson);
                Console.WriteLine("Created package.json");
            }

            // Run npm install
            var npmInstallResult = await RunProcessAsync("npm", $"install --prefix \"{artifactsPath}\"", artifactsPath);
            if (npmInstallResult != 0)
            {
                Console.Error.WriteLine("Warning: npm install failed. You may need to install dependencies manually.");
            }
            else
            {
                Console.WriteLine("Dependencies installed successfully.");
            }
            Console.WriteLine();

            // Step 7: Verify TypeScript compilation
            Console.WriteLine("Step 6: Verifying TypeScript compilation...");

            // Create a tsconfig.json if it doesn't exist
            var tsconfigPath = Path.Combine(artifactsPath, "tsconfig.json");
            if (!File.Exists(tsconfigPath))
            {
                var tsconfig = """
                {
                  "compilerOptions": {
                    "target": "ES2020",
                    "module": "commonjs",
                    "moduleResolution": "node",
                    "strict": true,
                    "esModuleInterop": true,
                    "skipLibCheck": true,
                    "forceConsistentCasingInFileNames": true,
                    "outDir": "./dist",
                    "rootDir": ".",
                    "declaration": true,
                    "noEmit": true
                  },
                  "include": ["./**/*.ts"],
                  "exclude": ["node_modules", "dist"]
                }
                """;
                await File.WriteAllTextAsync(tsconfigPath, tsconfig);
                Console.WriteLine("Created tsconfig.json");
            }

            // Try to compile TypeScript
            var tscResult = await RunProcessAsync("npx", "tsc --noEmit", artifactsPath);
            if (tscResult != 0)
            {
                Console.WriteLine("Warning: TypeScript compilation had issues. This may be expected if the tests reference the actual application.");
            }
            else
            {
                Console.WriteLine("TypeScript compilation successful!");
            }
            Console.WriteLine();

            // Step 8: Try to run Playwright tests (they will fail without the actual app, but we can verify the test runner works)
            Console.WriteLine("Step 7: Verifying Playwright test runner...");

            // Install Playwright browsers
            var playwrightInstallResult = await RunProcessAsync("npx", "playwright install chromium", artifactsPath);
            if (playwrightInstallResult != 0)
            {
                Console.WriteLine("Warning: Playwright browser installation had issues.");
            }

            // List tests (doesn't run them)
            var listTestsResult = await RunProcessAsync("npx", "playwright test --list", artifactsPath);
            if (listTestsResult == 0)
            {
                Console.WriteLine("Playwright test discovery successful!");
            }
            else
            {
                Console.WriteLine("Note: Test listing may show errors if no test files were generated or if the config is missing.");
            }

            Console.WriteLine();
            Console.WriteLine("=== Playground completed ===");
            Console.WriteLine();
            Console.WriteLine($"Generated tests are available at: {artifactsPath}");
            Console.WriteLine();
            Console.WriteLine("To run the tests:");
            Console.WriteLine($"  cd \"{artifactsPath}\"");
            Console.WriteLine("  npm test");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            // Cleanup temp directory
            Console.WriteLine("Cleaning up temporary files...");
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                    Console.WriteLine("Cleanup completed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not fully clean up temp directory: {ex.Message}");
            }
        }
    }

    static async Task<int> RunProcessAsync(string fileName, string arguments, string? workingDirectory = null)
    {
        // On Windows, npm and npx need to be run via cmd.exe
        var isWindows = OperatingSystem.IsWindows();
        var actualFileName = fileName;
        var actualArguments = arguments;

        if (isWindows && (fileName == "npm" || fileName == "npx"))
        {
            actualFileName = "cmd.exe";
            actualArguments = $"/c {fileName} {arguments}";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = actualFileName,
            Arguments = actualArguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (workingDirectory != null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
