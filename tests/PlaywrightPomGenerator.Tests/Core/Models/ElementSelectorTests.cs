using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Tests.Core.Models;

public sealed class ElementSelectorTests
{
    [Fact]
    public void ElementSelector_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var selector = new ElementSelector
        {
            ElementType = "button",
            Strategy = SelectorStrategy.TestId,
            SelectorValue = "[data-testid='submit']",
            PropertyName = "SubmitButton",
            TextContent = "Submit"
        };

        // Assert
        selector.ElementType.Should().Be("button");
        selector.Strategy.Should().Be(SelectorStrategy.TestId);
        selector.SelectorValue.Should().Be("[data-testid='submit']");
        selector.PropertyName.Should().Be("SubmitButton");
        selector.TextContent.Should().Be("Submit");
        selector.Attributes.Should().BeEmpty();
    }

    [Fact]
    public void ElementSelector_WithAttributes_ShouldStoreAttributes()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["class"] = "btn btn-primary",
            ["type"] = "submit"
        };

        // Act
        var selector = new ElementSelector
        {
            ElementType = "button",
            Strategy = SelectorStrategy.Css,
            SelectorValue = ".btn",
            PropertyName = "Button",
            Attributes = attributes
        };

        // Assert
        selector.Attributes.Should().HaveCount(2);
        selector.Attributes["class"].Should().Be("btn btn-primary");
        selector.Attributes["type"].Should().Be("submit");
    }

    [Theory]
    [InlineData(SelectorStrategy.TestId)]
    [InlineData(SelectorStrategy.Id)]
    [InlineData(SelectorStrategy.Class)]
    [InlineData(SelectorStrategy.Role)]
    [InlineData(SelectorStrategy.Text)]
    [InlineData(SelectorStrategy.Placeholder)]
    [InlineData(SelectorStrategy.Label)]
    [InlineData(SelectorStrategy.Css)]
    public void SelectorStrategy_ShouldHaveAllExpectedValues(SelectorStrategy strategy)
    {
        // Assert
        Enum.IsDefined(typeof(SelectorStrategy), strategy).Should().BeTrue();
    }
}
