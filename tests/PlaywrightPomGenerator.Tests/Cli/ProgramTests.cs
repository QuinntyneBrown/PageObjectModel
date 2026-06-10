using System.CommandLine.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Cli;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Cli;

public sealed class ProgramTests
{
    [Fact]
    public void CreateHost_ShouldCreateValidHost()
    {
        // Act
        using var host = Program.CreateHost([]);

        // Assert
        host.Should().NotBeNull();
        host.Services.Should().NotBeNull();
    }

    [Fact]
    public void CreateHost_ShouldRegisterCoreServices()
    {
        // Act
        using var host = Program.CreateHost([]);

        // Assert
        host.Services.GetService<IFileSystem>().Should().NotBeNull();
        host.Services.GetService<IAngularAnalyzer>().Should().NotBeNull();
        host.Services.GetService<ITemplateEngine>().Should().NotBeNull();
        host.Services.GetService<ICodeGenerator>().Should().NotBeNull();
    }

    [Fact]
    public void CreateHost_ShouldRegisterCommandHandlers()
    {
        // Act
        using var host = Program.CreateHost([]);

        // Assert
        host.Services.GetService<GenerateAppCommandHandler>().Should().NotBeNull();
        host.Services.GetService<GenerateWorkspaceCommandHandler>().Should().NotBeNull();
        host.Services.GetService<GenerateLibraryCommandHandler>().Should().NotBeNull();
        host.Services.GetService<GenerateComponentCommandHandler>().Should().NotBeNull();
        host.Services.GetService<GenerateBridgeCommandHandler>().Should().NotBeNull();
        host.Services.GetService<GenerateArtifactsCommandHandler>().Should().NotBeNull();
        host.Services.GetService<GenerateSignalRMockCommandHandler>().Should().NotBeNull();
    }

    [Fact]
    public void BuildRootCommand_ShouldHaveAllSubcommands()
    {
        // Arrange
        using var host = Program.CreateHost([]);

        // Act
        var rootCommand = Program.BuildRootCommand(host.Services);

        // Assert
        rootCommand.Should().NotBeNull();
        rootCommand.Subcommands.Should().HaveCount(8);
        rootCommand.Subcommands.Should().Contain(c => c.Name == "app");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "workspace");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "lib");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "component");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "bridge");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "artifacts");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "signalr-mock");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "remote");
    }

    [Fact]
    public void ConfigureServices_ShouldConfigureAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        Program.ConfigureServices(services, configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IFileSystem>().Should().NotBeNull();
        provider.GetService<IAngularAnalyzer>().Should().NotBeNull();
        provider.GetService<ITemplateEngine>().Should().NotBeNull();
        provider.GetService<ICodeGenerator>().Should().NotBeNull();
        provider.GetService<ISidecarTransport>().Should().NotBeNull();
        provider.GetService<ITypeScriptAnalyzer>().Should().NotBeNull();
        provider.GetService<IAstProjectAnalyzer>().Should().NotBeNull();
        provider.GetService<IPackageInspector>().Should().NotBeNull();
    }

    [Fact]
    public void BuildRootCommand_ShouldExposeEngineGlobalOption()
    {
        // Arrange
        using var host = Program.CreateHost([]);

        // Act
        var rootCommand = Program.BuildRootCommand(host.Services);

        // Assert — global options apply to every subcommand.
        var parser = new Parser(rootCommand);
        var parsed = parser.Parse("app . --engine regex");
        parsed.Errors.Should().BeEmpty();
        parsed.GetValueForOption(Program.EngineOption).Should().Be("regex");

        var invalid = parser.Parse("app . --engine bogus");
        invalid.Errors.Should().NotBeEmpty("--engine only accepts auto|ast|regex");
    }

    [Fact]
    public void ConfigureServices_WithEngineOverride_ShouldSetAnalysisEngine()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        Program.ConfigureServices(services, configuration, engineOverride: "regex");
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GeneratorOptions>>().Value;

        // Assert
        options.AnalysisEngine.Should().Be(AnalysisEngine.Regex);
    }

    [Fact]
    public void ConfigureServices_WithNoComponentObjects_ShouldDisableComponentObjectsAndComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        Program.ConfigureServices(services, configuration, noComponentObjects: true);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GeneratorOptions>>().Value;

        // Assert — the 1.x layout escape hatch implies no composition.
        options.EmitComponentObjects.Should().BeFalse();
        options.EmitComposition.Should().BeFalse();
    }

    [Fact]
    public void ConfigureServices_WithNoComposition_ShouldOnlyDisableComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        Program.ConfigureServices(services, configuration, noComposition: true);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GeneratorOptions>>().Value;

        // Assert
        options.EmitComponentObjects.Should().BeTrue();
        options.EmitComposition.Should().BeFalse();
    }

    [Fact]
    public void BuildRootCommand_ShouldExposeOutputShapeGlobalFlags()
    {
        // Arrange
        using var host = Program.CreateHost([]);
        var rootCommand = Program.BuildRootCommand(host.Services);

        // Act
        var parsed = new Parser(rootCommand).Parse("app . --no-component-objects --no-composition");

        // Assert
        parsed.Errors.Should().BeEmpty();
        parsed.GetValueForOption(Program.NoComponentObjectsOption).Should().BeTrue();
        parsed.GetValueForOption(Program.NoCompositionOption).Should().BeTrue();
    }

    [Fact]
    public void ConfigureServices_ShouldDeriveToolVersionFromAssembly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        Program.ConfigureServices(services, configuration);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GeneratorOptions>>().Value;

        // Assert — the runtime value tracks the Cli assembly version, ending the
        // hand-synced ToolVersion default.
        var assemblyVersion = typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .First().InformationalVersion;
        var plusIndex = assemblyVersion.IndexOf('+');
        var expected = plusIndex > 0 ? assemblyVersion[..plusIndex] : assemblyVersion;
        options.ToolVersion.Should().Be(expected);
    }
}
