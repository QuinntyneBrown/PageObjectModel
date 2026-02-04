using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlaywrightPomGenerator.Cli;
using PlaywrightPomGenerator.Cli.Commands;
using PlaywrightPomGenerator.Core.Abstractions;

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
        rootCommand.Subcommands.Should().HaveCount(5);
        rootCommand.Subcommands.Should().Contain(c => c.Name == "app");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "workspace");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "lib");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "artifacts");
        rootCommand.Subcommands.Should().Contain(c => c.Name == "signalr-mock");
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
    }
}
