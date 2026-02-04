using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Core.Models;

public sealed class AngularComponentInfoTests
{
    [Fact]
    public void AngularComponentInfo_ShouldStoreBasicProperties()
    {
        // Arrange & Act
        var component = new AngularComponentInfo
        {
            Name = "LoginComponent",
            Selector = "app-login",
            FilePath = "/src/app/login/login.component.ts"
        };

        // Assert
        component.Name.Should().Be("LoginComponent");
        component.Selector.Should().Be("app-login");
        component.FilePath.Should().Be("/src/app/login/login.component.ts");
    }

    [Fact]
    public void AngularComponentInfo_WithOptionalProperties_ShouldStoreAll()
    {
        // Arrange
        var selectors = new List<ElementSelector>
        {
            new()
            {
                ElementType = "button",
                Strategy = SelectorStrategy.TestId,
                SelectorValue = "[data-testid='login']",
                PropertyName = "LoginButton"
            }
        };

        // Act
        var component = new AngularComponentInfo
        {
            Name = "LoginComponent",
            Selector = "app-login",
            FilePath = "/src/app/login/login.component.ts",
            TemplatePath = "/src/app/login/login.component.html",
            Selectors = selectors,
            Inputs = ["username", "password"],
            Outputs = ["loginSuccess", "loginFailed"],
            RoutePath = "login"
        };

        // Assert
        component.TemplatePath.Should().Be("/src/app/login/login.component.html");
        component.Selectors.Should().HaveCount(1);
        component.Inputs.Should().HaveCount(2);
        component.Outputs.Should().HaveCount(2);
        component.RoutePath.Should().Be("login");
    }

    [Fact]
    public void AngularComponentInfo_DefaultCollections_ShouldBeEmpty()
    {
        // Arrange & Act
        var component = new AngularComponentInfo
        {
            Name = "TestComponent",
            Selector = "app-test",
            FilePath = "/test.ts"
        };

        // Assert
        component.Selectors.Should().BeEmpty();
        component.Inputs.Should().BeEmpty();
        component.Outputs.Should().BeEmpty();
        component.TemplatePath.Should().BeNull();
        component.RoutePath.Should().BeNull();
    }
}
