using System.CommandLine;
using System.CommandLine.Parsing;
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
    public static Option<string?> HeaderOption { get; } = new Option<string?>("--header")
    {
        Description = "Header template for generated files. Supports placeholders: {FileName}, {GeneratedDate}, {ToolVersion}. By default, no header is included."
    };

    /// <summary>
    /// Global option for test file suffix.
    /// </summary>
    public static Option<string?> TestSuffixOption { get; } = new Option<string?>("--test-suffix")
    {
        Description = "Suffix for test files (default: 'spec'). For example, 'test' produces 'component.test.ts'."
    };

    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Pre-parse to extract global options for service configuration
        var preParseCommand = new RootCommand();
        preParseCommand.Add(HeaderOption);
        preParseCommand.Add(TestSuffixOption);
        var preParseResult = preParseCommand.Parse(args);
        var headerValue = preParseResult.GetValue(HeaderOption);
        var testSuffixValue = preParseResult.GetValue(TestSuffixOption);

        using var host = CreateHost(args, headerValue, testSuffixValue);
        var rootCommand = BuildRootCommand(host.Services);

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync().ConfigureAwait(false);
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
        appCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(appCommand.PathArgument)!;
            var output = parseResult.GetValue(appCommand.OutputOption);
            var handler = services.GetRequiredService<GenerateAppCommandHandler>();
            return await handler.ExecuteAsync(path, output, cancellationToken).ConfigureAwait(false);
        });

        var workspaceCommand = new GenerateWorkspaceCommand();
        workspaceCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(workspaceCommand.PathArgument)!;
            var output = parseResult.GetValue(workspaceCommand.OutputOption);
            var project = parseResult.GetValue(workspaceCommand.ProjectOption);
            var handler = services.GetRequiredService<GenerateWorkspaceCommandHandler>();
            return await handler.ExecuteAsync(path, output, project, cancellationToken).ConfigureAwait(false);
        });

        var artifactsCommand = new GenerateArtifactsCommand();
        artifactsCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(artifactsCommand.PathArgument)!;
            var output = parseResult.GetValue(artifactsCommand.OutputOption);
            var project = parseResult.GetValue(artifactsCommand.ProjectOption);
            var fixtures = parseResult.GetValue(artifactsCommand.FixturesOption);
            var configs = parseResult.GetValue(artifactsCommand.ConfigsOption);
            var selectors = parseResult.GetValue(artifactsCommand.SelectorsOption);
            var pageObjects = parseResult.GetValue(artifactsCommand.PageObjectsOption);
            var helpers = parseResult.GetValue(artifactsCommand.HelpersOption);
            var all = parseResult.GetValue(artifactsCommand.AllOption);
            var handler = services.GetRequiredService<GenerateArtifactsCommandHandler>();
            return await handler.ExecuteAsync(
                path, output, project, fixtures, configs, selectors, pageObjects, helpers, all, cancellationToken)
                .ConfigureAwait(false);
        });

        var signalRMockCommand = new GenerateSignalRMockCommand();
        signalRMockCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var output = parseResult.GetValue(signalRMockCommand.OutputArgument)!;
            var handler = services.GetRequiredService<GenerateSignalRMockCommandHandler>();
            return await handler.ExecuteAsync(output, cancellationToken).ConfigureAwait(false);
        });

        var rootCommand = new RootCommand("Playwright Page Object Model Generator for Angular applications")
        {
            appCommand,
            workspaceCommand,
            artifactsCommand,
            signalRMockCommand
        };

        // Add global options for header and test suffix
        rootCommand.Add(HeaderOption);
        rootCommand.Add(TestSuffixOption);

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
        services.AddTransient<GenerateArtifactsCommandHandler>();
        services.AddTransient<GenerateSignalRMockCommandHandler>();
    }
}
