using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Core.Services;
using PlaywrightPomGenerator.Tests.TestUtilities;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class PackageInspectorTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly PackageInspector _inspector;

    public PackageInspectorTests()
    {
        _inspector = new PackageInspector(_fileSystem, Substitute.For<ILogger<PackageInspector>>());
    }

    [Fact]
    public async Task InspectAsync_WithDependencies_ShouldReportVersions()
    {
        // Arrange
        _fileSystem.AddFile("/app/package.json", """
            {
              "dependencies": {
                "@angular/core": "^17.3.0",
                "@angular/material": "~17.3.1",
                "@angular/cdk": "17.3.1",
                "primeng": "17.0.0"
              },
              "devDependencies": {
                "typescript": "~5.4.2"
              }
            }
            """);

        // Act
        var report = await _inspector.InspectAsync("/app");

        // Assert
        report.AngularCoreVersion.Should().Be("^17.3.0");
        report.AngularMaterialVersion.Should().Be("~17.3.1");
        report.AngularCdkVersion.Should().Be("17.3.1");
        report.TypeScriptVersion.Should().Be("~5.4.2");
        report.HasMaterial.Should().BeTrue();
        report.UiLibraries.Should().ContainKey("primeng");
        report.PackageJsonPath.Should().Be("/app/package.json");
    }

    [Fact]
    public async Task InspectAsync_WithoutPackageJson_ShouldReturnEmptyReport()
    {
        // Arrange
        _fileSystem.AddDirectory("/empty");

        // Act
        var report = await _inspector.InspectAsync("/empty");

        // Assert
        report.AngularCoreVersion.Should().BeNull();
        report.HasMaterial.Should().BeFalse();
        report.Prefixes.Should().BeEmpty();
        report.PackageJsonPath.Should().BeNull();
    }

    [Fact]
    public async Task InspectAsync_WithMalformedPackageJson_ShouldReturnEmptyReportWithoutThrowing()
    {
        // Arrange
        _fileSystem.AddFile("/app/package.json", "{ not valid json");

        // Act
        var report = await _inspector.InspectAsync("/app");

        // Assert
        report.AngularCoreVersion.Should().BeNull();
        report.PackageJsonPath.Should().BeNull();
    }

    [Fact]
    public async Task InspectAsync_WithAngularJson_ShouldCollectWorkspaceAndProjectPrefixes()
    {
        // Arrange
        _fileSystem.AddFile("/app/package.json", """{ "dependencies": { "@angular/core": "17.0.0" } }""");
        _fileSystem.AddFile("/app/angular.json", """
            {
              "schematics": { "@schematics/angular:component": { "prefix": "acme" } },
              "projects": {
                "shop": { "prefix": "shop" },
                "admin": { "prefix": "adm", "schematics": { "@schematics/angular:component": { "prefix": "admin" } } },
                "no-prefix": { }
              }
            }
            """);

        // Act
        var report = await _inspector.InspectAsync("/app");

        // Assert
        report.Prefixes.Should().BeEquivalentTo(["acme", "shop", "adm", "admin"]);
    }

    [Fact]
    public async Task InspectAsync_ShouldReportNodeModulesPresence()
    {
        // Arrange
        _fileSystem.AddFile("/app/package.json", "{}");
        _fileSystem.AddDirectory("/app/node_modules");

        // Act
        var report = await _inspector.InspectAsync("/app");

        // Assert
        report.NodeModulesPresent.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullFileSystem_ShouldThrowArgumentNullException()
    {
        var act = () => new PackageInspector(null!, Substitute.For<ILogger<PackageInspector>>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InspectAsync_WithNullPath_ShouldThrowArgumentNullException()
    {
        var act = () => _inspector.InspectAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
