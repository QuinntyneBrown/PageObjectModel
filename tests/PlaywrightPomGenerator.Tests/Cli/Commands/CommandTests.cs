using FluentAssertions;
using PlaywrightPomGenerator.Cli.Commands;

namespace PlaywrightPomGenerator.Tests.Cli.Commands;

public sealed class CommandTests
{
    [Fact]
    public void GenerateAppCommand_ShouldHaveCorrectNameAndDescription()
    {
        // Act
        var command = new GenerateAppCommand();

        // Assert
        command.Name.Should().Be("app");
        command.Description.Should().Contain("Angular application");
    }

    [Fact]
    public void GenerateAppCommand_ShouldHaveRequiredArguments()
    {
        // Act
        var command = new GenerateAppCommand();

        // Assert
        command.PathArgument.Should().NotBeNull();
        command.PathArgument.Name.Should().Be("path");
    }

    [Fact]
    public void GenerateAppCommand_ShouldHaveOutputOption()
    {
        // Act
        var command = new GenerateAppCommand();

        // Assert
        command.OutputOption.Should().NotBeNull();
        command.OutputOption.Name.Should().Be("output");
    }

    [Fact]
    public void GenerateWorkspaceCommand_ShouldHaveCorrectNameAndDescription()
    {
        // Act
        var command = new GenerateWorkspaceCommand();

        // Assert
        command.Name.Should().Be("workspace");
        command.Description.Should().Contain("workspace");
    }

    [Fact]
    public void GenerateWorkspaceCommand_ShouldHaveProjectOption()
    {
        // Act
        var command = new GenerateWorkspaceCommand();

        // Assert
        command.ProjectOption.Should().NotBeNull();
        command.ProjectOption.Name.Should().Be("project");
    }

    [Fact]
    public void GenerateArtifactsCommand_ShouldHaveCorrectNameAndDescription()
    {
        // Act
        var command = new GenerateArtifactsCommand();

        // Assert
        command.Name.Should().Be("artifacts");
        command.Description.Should().Contain("artifacts");
    }

    [Fact]
    public void GenerateArtifactsCommand_ShouldHaveAllArtifactOptions()
    {
        // Act
        var command = new GenerateArtifactsCommand();

        // Assert
        command.FixturesOption.Should().NotBeNull();
        command.ConfigsOption.Should().NotBeNull();
        command.SelectorsOption.Should().NotBeNull();
        command.PageObjectsOption.Should().NotBeNull();
        command.HelpersOption.Should().NotBeNull();
        command.AllOption.Should().NotBeNull();
    }

    [Fact]
    public void GenerateSignalRMockCommand_ShouldHaveCorrectNameAndDescription()
    {
        // Act
        var command = new GenerateSignalRMockCommand();

        // Assert
        command.Name.Should().Be("signalr-mock");
        command.Description.Should().Contain("SignalR");
        command.Description.Should().Contain("RxJS");
    }

    [Fact]
    public void GenerateSignalRMockCommand_OutputArgument_ShouldHaveDefaultValue()
    {
        // Act
        var command = new GenerateSignalRMockCommand();

        // Assert
        command.OutputArgument.Should().NotBeNull();
        command.OutputArgument.Name.Should().Be("output");
    }
}
