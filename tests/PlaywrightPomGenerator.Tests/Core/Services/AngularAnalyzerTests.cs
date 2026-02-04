using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Core.Services;
using PlaywrightPomGenerator.Tests.TestUtilities;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class AngularAnalyzerTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly ILogger<AngularAnalyzer> _logger;
    private readonly AngularAnalyzer _analyzer;

    public AngularAnalyzerTests()
    {
        _fileSystem = new MockFileSystem();
        _logger = Substitute.For<ILogger<AngularAnalyzer>>();
        _analyzer = new AngularAnalyzer(_fileSystem, _logger);
    }

    [Fact]
    public void IsWorkspace_WhenAngularJsonExists_ShouldReturnTrue()
    {
        // Arrange
        _fileSystem.AddFile("/workspace/angular.json", "{}");

        // Act
        var result = _analyzer.IsWorkspace("/workspace");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWorkspace_WhenAngularJsonDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _fileSystem.AddDirectory("/workspace");

        // Act
        var result = _analyzer.IsWorkspace("/workspace");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsApplication_WhenAngularJsonExists_ShouldReturnTrue()
    {
        // Arrange
        _fileSystem.AddFile("/app/angular.json", "{}");

        // Act
        var result = _analyzer.IsApplication("/app");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsApplication_WhenPackageJsonWithAngularCore_ShouldReturnTrue()
    {
        // Arrange
        _fileSystem.AddFile("/app/package.json", """
            {
                "dependencies": {
                    "@angular/core": "^17.0.0"
                }
            }
            """);

        // Act
        var result = _analyzer.IsApplication("/app");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsApplication_WhenNoAngularIndicators_ShouldReturnFalse()
    {
        // Arrange
        _fileSystem.AddDirectory("/app");

        // Act
        var result = _analyzer.IsApplication("/app");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeWorkspaceAsync_WithValidWorkspace_ShouldReturnWorkspaceInfo()
    {
        // Arrange
        _fileSystem.AddFile("/workspace/angular.json", """
            {
                "version": 1,
                "defaultProject": "my-app",
                "projects": {
                    "my-app": {
                        "projectType": "application",
                        "root": "projects/my-app",
                        "sourceRoot": "projects/my-app/src"
                    }
                }
            }
            """);
        _fileSystem.AddDirectory("/workspace/projects/my-app/src");

        // Act
        var result = await _analyzer.AnalyzeWorkspaceAsync("/workspace");

        // Assert
        result.Should().NotBeNull();
        result.RootPath.Should().Contain("workspace");
        result.DefaultProject.Should().Be("my-app");
        result.Projects.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyzeWorkspaceAsync_WithLibraryProject_ShouldIncludeLibrary()
    {
        // Arrange
        _fileSystem.AddFile("/workspace/angular.json", """
            {
                "version": 1,
                "projects": {
                    "my-app": {
                        "projectType": "application",
                        "root": "",
                        "sourceRoot": "src"
                    },
                    "my-lib": {
                        "projectType": "library",
                        "root": "projects/my-lib",
                        "sourceRoot": "projects/my-lib/src"
                    }
                }
            }
            """);
        _fileSystem.AddDirectory("/workspace/src");
        _fileSystem.AddDirectory("/workspace/projects/my-lib/src");

        // Act
        var result = await _analyzer.AnalyzeWorkspaceAsync("/workspace");

        // Assert
        result.Projects.Should().HaveCount(2);
        result.Projects.Should().Contain(p => p.Name == "my-app" && p.ProjectType == PlaywrightPomGenerator.Core.Models.AngularProjectType.Application);
        result.Projects.Should().Contain(p => p.Name == "my-lib" && p.ProjectType == PlaywrightPomGenerator.Core.Models.AngularProjectType.Library);
    }

    [Fact]
    public async Task AnalyzeWorkspaceAsync_WhenNoAngularJson_ShouldThrow()
    {
        // Arrange
        _fileSystem.AddDirectory("/workspace");

        // Act
        var act = () => _analyzer.AnalyzeWorkspaceAsync("/workspace");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*angular.json*");
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_WithWorkspace_ShouldReturnDefaultProject()
    {
        // Arrange
        _fileSystem.AddFile("/workspace/angular.json", """
            {
                "version": 1,
                "defaultProject": "my-app",
                "projects": {
                    "my-app": {
                        "projectType": "application",
                        "root": "",
                        "sourceRoot": "src"
                    }
                }
            }
            """);
        _fileSystem.AddDirectory("/workspace/src");

        // Act
        var result = await _analyzer.AnalyzeApplicationAsync("/workspace");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("my-app");
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_WithComponents_ShouldParseComponents()
    {
        // Arrange
        _fileSystem.AddFile("/app/angular.json", """
            {
                "version": 1,
                "projects": {
                    "app": {
                        "projectType": "application",
                        "root": "",
                        "sourceRoot": "src"
                    }
                }
            }
            """);

        _fileSystem.AddFile("/app/src/app/login/login.component.ts", """
            import { Component } from '@angular/core';

            @Component({
                selector: 'app-login',
                templateUrl: './login.component.html'
            })
            export class LoginComponent {
                @Input() username: string;
                @Output() loginSuccess = new EventEmitter();
            }
            """);

        _fileSystem.AddFile("/app/src/app/login/login.component.html", """
            <form>
                <input data-testid="username" formControlName="username" />
                <input data-testid="password" type="password" />
                <button id="submit-btn">Login</button>
            </form>
            """);

        // Act
        var result = await _analyzer.AnalyzeApplicationAsync("/app");

        // Assert
        result.Components.Should().HaveCount(1);
        var component = result.Components[0];
        component.Name.Should().Be("LoginComponent");
        component.Selector.Should().Be("app-login");
        component.Inputs.Should().Contain("username");
        component.Outputs.Should().Contain("loginSuccess");
        component.Selectors.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullFileSystem_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AngularAnalyzer(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AngularAnalyzer(_fileSystem, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsWorkspace_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _analyzer.IsWorkspace(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsApplication_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _analyzer.IsApplication(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsLibrary_WhenNgPackageJsonExists_ShouldReturnTrue()
    {
        // Arrange
        _fileSystem.AddFile("/lib/ng-package.json", "{}");

        // Act
        var result = _analyzer.IsLibrary("/lib");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsLibrary_WhenNgPackageJsonDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _fileSystem.AddDirectory("/lib");

        // Act
        var result = _analyzer.IsLibrary("/lib");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsLibrary_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _analyzer.IsLibrary(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AnalyzeLibraryAsync_WithStandaloneLibrary_ShouldReturnLibraryInfo()
    {
        // Arrange
        _fileSystem.AddFile("/my-lib/ng-package.json", "{}");
        _fileSystem.AddDirectory("/my-lib/src/lib");
        _fileSystem.AddFile("/my-lib/src/lib/my.component.ts", """
            import { Component } from '@angular/core';

            @Component({
                selector: 'lib-my',
                template: '<div>Hello</div>'
            })
            export class MyComponent {}
            """);

        // Act
        var result = await _analyzer.AnalyzeLibraryAsync("/my-lib");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("my-lib");
        result.ProjectType.Should().Be(PlaywrightPomGenerator.Core.Models.AngularProjectType.Library);
        result.Components.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyzeLibraryAsync_WithWorkspace_ShouldReturnFirstLibrary()
    {
        // Arrange
        _fileSystem.AddFile("/workspace/angular.json", """
            {
                "version": 1,
                "projects": {
                    "my-app": {
                        "projectType": "application",
                        "root": "",
                        "sourceRoot": "src"
                    },
                    "my-lib": {
                        "projectType": "library",
                        "root": "projects/my-lib",
                        "sourceRoot": "projects/my-lib/src"
                    }
                }
            }
            """);
        _fileSystem.AddDirectory("/workspace/src");
        _fileSystem.AddDirectory("/workspace/projects/my-lib/src");

        // Act
        var result = await _analyzer.AnalyzeLibraryAsync("/workspace");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("my-lib");
        result.ProjectType.Should().Be(PlaywrightPomGenerator.Core.Models.AngularProjectType.Library);
    }

    [Fact]
    public async Task AnalyzeLibraryAsync_WithWorkspaceNoLibrary_ShouldThrow()
    {
        // Arrange
        _fileSystem.AddFile("/workspace/angular.json", """
            {
                "version": 1,
                "projects": {
                    "my-app": {
                        "projectType": "application",
                        "root": "",
                        "sourceRoot": "src"
                    }
                }
            }
            """);
        _fileSystem.AddDirectory("/workspace/src");

        // Act
        var act = () => _analyzer.AnalyzeLibraryAsync("/workspace");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No library project found*");
    }

    [Fact]
    public async Task AnalyzeApplicationAsync_WithLibraryPath_ShouldReturnLibraryType()
    {
        // Arrange
        _fileSystem.AddFile("/my-lib/ng-package.json", "{}");
        _fileSystem.AddDirectory("/my-lib/src");

        // Act
        var result = await _analyzer.AnalyzeApplicationAsync("/my-lib");

        // Assert
        result.ProjectType.Should().Be(PlaywrightPomGenerator.Core.Models.AngularProjectType.Library);
    }
}
