using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Cli;

/// <summary>
/// Entry point for the Playwright POM Generator CLI.
/// </summary>
public static class Program
{
    /// <summary>
    /// Global option for file header template.
    /// </summary>
    public static Option<string?> HeaderOption { get; } = new("--header",
        "Header template for generated files. Supports placeholders: {FileName}, {GeneratedDate}, {ToolVersion}. By default, no header is included.");

    /// <summary>
    /// Global option for test file suffix.
    /// </summary>
    public static Option<string?> TestSuffixOption { get; } = new("--test-suffix",
        "Suffix for test files (default: 'spec'). For example, 'test' produces 'component.test.ts'.");

    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Pre-parse to extract global options for service configuration
        var preParseCommand = new RootCommand();
        preParseCommand.AddGlobalOption(HeaderOption);
        preParseCommand.AddGlobalOption(TestSuffixOption);
        var preParseResult = preParseCommand.Parse(args);
        var headerValue = preParseResult.GetValueForOption(HeaderOption);
        var testSuffixValue = preParseResult.GetValueForOption(TestSuffixOption);

        using var host = CreateHost(args, headerValue, testSuffixValue);
        var rootCommand = BuildRootCommand(host.Services);

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the host with DI, configuration, and logging.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="headerOverride">Optional header template override from CLI.</param>
    /// <param name="testSuffixOverride">Optional test suffix override from CLI.</param>
    /// <returns>The configured host.</returns>
    public static IHost CreateHost(string[] args, string? headerOverride = null, string? testSuffixOverride = null)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables("POMGEN_");
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                logging.AddConsole();
            })
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(services, context.Configuration, headerOverride, testSuffixOverride);
            })
            .Build();
    }

    /// <summary>
    /// Builds the root command with all subcommands.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>The root command.</returns>
    public static RootCommand BuildRootCommand(IServiceProvider services)
    {
        var appCommand = new GenerateAppCommand();
        appCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(appCommand.PathArgument);
            var output = context.ParseResult.GetValueForOption(appCommand.OutputOption);
            var handler = services.GetRequiredService<GenerateAppCommandHandler>();
            context.ExitCode = await handler.ExecuteAsync(path, output, context.GetCancellationToken()).ConfigureAwait(false);
        });

        var workspaceCommand = new GenerateWorkspaceCommand();
        workspaceCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(workspaceCommand.PathArgument);
            var output = context.ParseResult.GetValueForOption(workspaceCommand.OutputOption);
            var project = context.ParseResult.GetValueForOption(workspaceCommand.ProjectOption);
            var handler = services.GetRequiredService<GenerateWorkspaceCommandHandler>();
            context.ExitCode = await handler.ExecuteAsync(path, output, project, context.GetCancellationToken()).ConfigureAwait(false);
        });

        var artifactsCommand = new GenerateArtifactsCommand();
        artifactsCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(artifactsCommand.PathArgument);
            var output = context.ParseResult.GetValueForOption(artifactsCommand.OutputOption);
            var project = context.ParseResult.GetValueForOption(artifactsCommand.ProjectOption);
            var fixtures = context.ParseResult.GetValueForOption(artifactsCommand.FixturesOption);
            var configs = context.ParseResult.GetValueForOption(artifactsCommand.ConfigsOption);
            var selectors = context.ParseResult.GetValueForOption(artifactsCommand.SelectorsOption);
            var pageObjects = context.ParseResult.GetValueForOption(artifactsCommand.PageObjectsOption);
            var helpers = context.ParseResult.GetValueForOption(artifactsCommand.HelpersOption);
            var all = context.ParseResult.GetValueForOption(artifactsCommand.AllOption);
            var handler = services.GetRequiredService<GenerateArtifactsCommandHandler>();
            context.ExitCode = await handler.ExecuteAsync(
                path, output, project, fixtures, configs, selectors, pageObjects, helpers, all, context.GetCancellationToken())
                .ConfigureAwait(false);
        });

        var libraryCommand = new GenerateLibraryCommand();
        libraryCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(libraryCommand.PathArgument);
            var output = context.ParseResult.GetValueForOption(libraryCommand.OutputOption);
            var handler = services.GetRequiredService<GenerateLibraryCommandHandler>();
            context.ExitCode = await handler.ExecuteAsync(path, output, context.GetCancellationToken()).ConfigureAwait(false);
        });

        var signalRMockCommand = new GenerateSignalRMockCommand();
        signalRMockCommand.SetHandler(async (context) =>
        {
            var output = context.ParseResult.GetValueForArgument(signalRMockCommand.OutputArgument);
            var handler = services.GetRequiredService<GenerateSignalRMockCommandHandler>();
            context.ExitCode = await handler.ExecuteAsync(output, context.GetCancellationToken()).ConfigureAwait(false);
        });

        var rootCommand = new RootCommand("Playwright Page Object Model Generator for Angular applications and libraries")
        {
            appCommand,
            workspaceCommand,
            libraryCommand,
            artifactsCommand,
            signalRMockCommand
        };

        // Add global options for header and test suffix
        rootCommand.AddGlobalOption(HeaderOption);
        rootCommand.AddGlobalOption(TestSuffixOption);

        return rootCommand;
    }

    /// <summary>
    /// Configures services for dependency injection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="headerOverride">Optional header template override from CLI.</param>
    /// <param name="testSuffixOverride">Optional test suffix override from CLI.</param>
    public static void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        string? headerOverride = null,
        string? testSuffixOverride = null)
    {
        // Configuration
        services.Configure<GeneratorOptions>(configuration.GetSection(GeneratorOptions.SectionName));

        // Apply CLI overrides if provided
        if (headerOverride is not null || testSuffixOverride is not null)
        {
            services.PostConfigure<GeneratorOptions>(options =>
            {
                if (headerOverride is not null)
                {
                    options.FileHeader = headerOverride;
                }
                if (testSuffixOverride is not null)
                {
                    options.TestFileSuffix = testSuffixOverride;
                }
            });
        }

        // Core services
        services.AddSingleton<IFileSystem, FileSystemService>();
        services.AddSingleton<IAngularAnalyzer, AngularAnalyzer>();
        services.AddSingleton<ITemplateEngine, TemplateEngine>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();

        // Command handlers
        services.AddTransient<GenerateAppCommandHandler>();
        services.AddTransient<GenerateWorkspaceCommandHandler>();
        services.AddTransient<GenerateLibraryCommandHandler>();
        services.AddTransient<GenerateArtifactsCommandHandler>();
        services.AddTransient<GenerateSignalRMockCommandHandler>();
    }
}
