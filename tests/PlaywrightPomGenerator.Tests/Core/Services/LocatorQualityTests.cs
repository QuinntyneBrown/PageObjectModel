using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// Deterministic locator grading: which generated locators count as weak, what fix is
/// suggested, and how the per-component summary reads.
/// </summary>
public sealed class LocatorQualityTests
{
    private static ElementSelector Selector(
        SelectorStrategy strategy,
        string value,
        string name,
        string elementType = "div",
        bool interpolated = false,
        bool isTable = false,
        LandmarkRef? landmark = null,
        int? line = null) => new()
    {
        ElementType = elementType,
        Strategy = strategy,
        SelectorValue = value,
        PropertyName = name,
        TextIsInterpolated = interpolated,
        IsTable = isTable,
        ParentLandmark = landmark,
        TemplateLine = line
    };

    [Theory]
    [InlineData(SelectorStrategy.TestId, "[data-testid='save']", "SaveButton")]
    [InlineData(SelectorStrategy.Label, "[formControlName='email']", "EmailInput")]
    [InlineData(SelectorStrategy.Role, "button:has-text(\"Save\")", "SaveButton")]
    [InlineData(SelectorStrategy.Id, "#main", "Main")]
    [InlineData(SelectorStrategy.FormControl, "[formControlName='email']", "EmailInput")]
    [InlineData(SelectorStrategy.Css, "[formControlName='email']", "EmailInput")] // regex engine's formControlName
    [InlineData(SelectorStrategy.Placeholder, "input[placeholder='Search']", "SearchInput")]
    public void Assess_StableStrategies_ShouldBeStable(SelectorStrategy strategy, string value, string name)
    {
        LocatorQuality.Assess(Selector(strategy, value, name)).Grade.Should().Be(LocatorGrade.Stable);
    }

    [Fact]
    public void Assess_TableCssSelector_ShouldBeStable()
    {
        var table = Selector(SelectorStrategy.Css, "mat-table, table[mat-table], [mat-table]", "DataTable", "table", isTable: true);

        LocatorQuality.Assess(table).Grade.Should().Be(LocatorGrade.Stable);
    }

    [Theory]
    [InlineData("div", "Container")]
    [InlineData("div.card", "Card")]
    [InlineData("span", "TextSpan")]
    public void Assess_BareCssFallback_ShouldBeWeakWithTestIdFix(string value, string name)
    {
        var assessment = LocatorQuality.Assess(Selector(SelectorStrategy.Css, value, name, "div"));

        assessment.Grade.Should().Be(LocatorGrade.Weak);
        assessment.Reason.Should().Be(LocatorQuality.CssFallbackReason);
        assessment.Fix.Should().Contain("data-testid=\"").And.Contain("<div>");
    }

    [Fact]
    public void Assess_CssFallbackFix_ShouldSuggestKebabCaseTestId()
    {
        var assessment = LocatorQuality.Assess(Selector(SelectorStrategy.Css, "div.summary", "OrderSummary", "div"));

        assessment.Fix.Should().Contain("data-testid=\"order-summary\"");
    }

    [Fact]
    public void Assess_TypedInputCss_ShouldBeWeakWithLabelFix()
    {
        var assessment = LocatorQuality.Assess(Selector(SelectorStrategy.Css, "input[type='email']", "EmailInput", "input"));

        assessment.Grade.Should().Be(LocatorGrade.Weak);
        assessment.Reason.Should().Be(LocatorQuality.TypedInputCssReason);
        assessment.Fix.Should().Contain("label");
    }

    [Theory]
    [InlineData(SelectorStrategy.Text)]
    [InlineData(SelectorStrategy.Role)]
    public void Assess_HasTextBuiltFromInterpolatedText_ShouldBeWeak(SelectorStrategy strategy)
    {
        var assessment = LocatorQuality.Assess(
            Selector(strategy, "h1:has-text(\"Hello !\")", "HelloHeading", "h1", interpolated: true));

        assessment.Grade.Should().Be(LocatorGrade.Weak);
        assessment.Reason.Should().Be(LocatorQuality.InterpolatedTextReason);
    }

    [Fact]
    public void Assess_InterpolatedTextWithTestId_ShouldBeStable()
    {
        var assessment = LocatorQuality.Assess(
            Selector(SelectorStrategy.TestId, "[data-testid='title']", "Title", "h1", interpolated: true));

        assessment.Grade.Should().Be(LocatorGrade.Stable);
    }

    [Theory]
    [InlineData("Container2")]
    [InlineData("TextSpan3")]
    [InlineData("Heading12")]
    [InlineData("Paragraph1")]
    [InlineData("TextInput1")]
    [InlineData("CustomElement2")]
    public void Assess_GenericNumberedName_ShouldBeWeak(string name)
    {
        var assessment = LocatorQuality.Assess(Selector(SelectorStrategy.TestId, "[data-testid='x']", name));

        assessment.Grade.Should().Be(LocatorGrade.Weak);
        assessment.Reason.Should().Be(LocatorQuality.GenericNameReason);
    }

    [Theory]
    [InlineData("Heading1")]
    [InlineData("Container")]
    [InlineData("Step2Button")]
    [InlineData("Address2Input")]
    public void Assess_MeaningfulNames_ShouldNotBeFlaggedAsGeneric(string name)
    {
        LocatorQuality.Assess(Selector(SelectorStrategy.TestId, "[data-testid='x']", name))
            .Grade.Should().Be(LocatorGrade.Stable);
    }

    [Fact]
    public void Assess_DuplicateSelectorWithoutLandmark_ShouldBeWeak()
    {
        var a = Selector(SelectorStrategy.Role, "button:has-text(\"Save\")", "SaveButton", "button");
        var b = Selector(SelectorStrategy.Role, "button:has-text(\"Save\")", "SaveButton2", "button");

        var assessment = LocatorQuality.Assess(a, [a, b]);

        assessment.Grade.Should().Be(LocatorGrade.Weak);
        assessment.Reason.Should().Be(LocatorQuality.DuplicateSelectorReason);
    }

    [Fact]
    public void Assess_DuplicateSelectorScopedByLandmark_ShouldBeStable()
    {
        var landmark = new LandmarkRef { Label = "Billing", SelectorValue = "[data-testid='billing']", TestId = "billing" };
        var a = Selector(SelectorStrategy.Role, "button:has-text(\"Save\")", "BillingSaveButton", "button", landmark: landmark);
        var b = Selector(SelectorStrategy.Role, "button:has-text(\"Save\")", "ShippingSaveButton", "button",
            landmark: new LandmarkRef { Label = "Shipping", SelectorValue = "[data-testid='shipping']" });

        LocatorQuality.Assess(a, [a, b]).Grade.Should().Be(LocatorGrade.Stable);
    }

    [Fact]
    public void AppendWarnings_ShouldEmitOneSummaryPerComponentWithWeakLocators()
    {
        var weak = Component("LoginComponent",
            Selector(SelectorStrategy.TestId, "[data-testid='login']", "LoginButton", "button"),
            Selector(SelectorStrategy.Css, "div", "Container"),
            Selector(SelectorStrategy.Css, "span", "TextSpan2", "span"),
            Selector(SelectorStrategy.Text, "h1:has-text(\"Hi \")", "Title", "h1", interpolated: true));
        var clean = Component("CleanComponent",
            Selector(SelectorStrategy.TestId, "[data-testid='ok']", "OkButton", "button"));
        var warnings = new List<string>();

        LocatorQuality.AppendWarnings([weak, clean], warnings, debug: false);

        var summary = warnings.Should().ContainSingle().Subject;
        summary.Should().StartWith("LoginComponent: 3 of 4 locators are weak — ");
        summary.Should().Contain("CSS fallback: container");
        summary.Should().Contain("generic name: textSpan2");
        summary.Should().Contain("interpolated text: title");
        summary.Should().EndWith("Add data-testid attributes to stabilize.");
    }

    [Fact]
    public void AppendWarnings_ShouldCapExamplesAtThree()
    {
        var component = Component("BigComponent",
            Selector(SelectorStrategy.Css, "div", "A"),
            Selector(SelectorStrategy.Css, "div", "B"),
            Selector(SelectorStrategy.Css, "div", "C"),
            Selector(SelectorStrategy.Css, "div", "D"),
            Selector(SelectorStrategy.Css, "div", "E"));
        var warnings = new List<string>();

        LocatorQuality.AppendWarnings([component], warnings, debug: false);

        warnings.Should().ContainSingle().Which.Should().Contain("a, b, c (+2 more)");
    }

    [Fact]
    public void AppendWarnings_InDebugMode_ShouldAddOneLinePerWeakLocator()
    {
        var component = Component("LoginComponent",
            Selector(SelectorStrategy.Css, "div", "Container", line: 12),
            Selector(SelectorStrategy.TestId, "[data-testid='ok']", "OkButton", "button"));
        var warnings = new List<string>();

        LocatorQuality.AppendWarnings([component], warnings, debug: true);

        warnings.Should().HaveCount(2);
        warnings[1].Should().Contain("LoginComponent.container").And.Contain("line 12").And.Contain("CSS fallback");
    }

    [Fact]
    public void AppendWarnings_ComponentWithoutSelectors_ShouldStaySilent()
    {
        var warnings = new List<string>();

        LocatorQuality.AppendWarnings([Component("EmptyComponent")], warnings, debug: true);

        warnings.Should().BeEmpty();
    }

    private static AngularComponentInfo Component(string name, params ElementSelector[] selectors) => new()
    {
        Name = name,
        Selector = "app-" + name.ToLowerInvariant(),
        FilePath = $"/app/src/{name}.ts",
        Selectors = selectors
    };
}
