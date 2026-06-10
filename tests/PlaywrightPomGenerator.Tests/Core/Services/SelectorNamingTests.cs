using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class SelectorNamingTests
{
    private static readonly IReadOnlyDictionary<string, string> NoDialogs = new Dictionary<string, string>();

    private static AstElement Element(
        string tag,
        string? testId = null,
        string? id = null,
        string? text = null,
        bool interpolated = false,
        string? widget = null,
        string? formControlName = null,
        string? inputType = null,
        string? labelFor = null,
        string? placeholder = null,
        string? ariaLabel = null,
        string? clickHandler = null,
        AstStructure? structure = null,
        AstLandmark? landmark = null,
        string? headingText = null,
        AstTable? table = null,
        bool isRouterLink = false)
    {
        return new AstElement
        {
            Tag = tag,
            TestId = testId,
            Id = id,
            Text = new AstText { Value = text, Interpolated = interpolated },
            Widget = widget,
            Form = new AstFormFacts { FormControlName = formControlName, InputType = inputType },
            Labels = new AstLabels { LabelFor = labelFor, Placeholder = placeholder },
            Aria = new AstAria { Label = ariaLabel },
            Events = clickHandler is not null ? ["click"] : [],
            Handlers = clickHandler is not null
                ? new Dictionary<string, string> { ["click"] = clickHandler }
                : new Dictionary<string, string>(),
            Structure = structure ?? new AstStructure(),
            Ancestry = new AstAncestry { Landmark = landmark, HeadingText = headingText },
            Table = table,
            IsRouterLink = isRouterLink
        };
    }

    [Fact]
    public void MapSelectors_WithTestId_ShouldUseTestIdStrategyWithoutDoubledSuffix()
    {
        var selectors = SelectorNaming.MapSelectors(
            [Element("button", testId: "save-button", text: "Save")], NoDialogs);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.Strategy.Should().Be(SelectorStrategy.TestId);
        selector.SelectorValue.Should().Be("[data-testid='save-button']");
        selector.TestIdValue.Should().Be("save-button");
        selector.PropertyName.Should().Be("SaveButton"); // not SaveButtonButton
    }

    [Fact]
    public void MapSelectors_ButtonWithText_ShouldUseRoleWithAccessibleName()
    {
        var selectors = SelectorNaming.MapSelectors([Element("button", text: "Log in")], NoDialogs);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.Strategy.Should().Be(SelectorStrategy.Role);
        selector.AriaRole.Should().Be("button");
        selector.TextContent.Should().Be("Log in");
        selector.PropertyName.Should().Be("LogInButton");
    }

    [Fact]
    public void MapSelectors_InputWithLabel_ShouldUseLabelStrategy()
    {
        var selectors = SelectorNaming.MapSelectors(
            [Element("input", labelFor: "Email address", formControlName: "email", inputType: "email")],
            NoDialogs);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.Strategy.Should().Be(SelectorStrategy.Label);
        selector.LabelText.Should().Be("Email address");
        selector.PropertyName.Should().Be("EmailAddressInput");
        selector.ControlType.Should().Be(ControlType.TextInput);
        selector.FormControlName.Should().Be("email");
    }

    [Fact]
    public void MapSelectors_InputWithOnlyFormControlName_ShouldUseFormControlStrategy()
    {
        var selectors = SelectorNaming.MapSelectors(
            [Element("input", formControlName: "username", inputType: "text")], NoDialogs);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.Strategy.Should().Be(SelectorStrategy.FormControl);
        selector.SelectorValue.Should().Be("[formControlName='username']");
        selector.PropertyName.Should().Be("UsernameInput");
    }

    [Theory]
    [InlineData("matSelect", "mat-select", null, ControlType.Select)]
    [InlineData("matCheckbox", "mat-checkbox", null, ControlType.Checkbox)]
    [InlineData("matSlideToggle", "mat-slide-toggle", null, ControlType.Toggle)]
    [InlineData("matDatepicker", "input", "text", ControlType.Datepicker)]
    [InlineData("matAutocomplete", "input", "text", ControlType.Autocomplete)]
    [InlineData("matMenuTrigger", "button", null, ControlType.MenuTrigger)]
    [InlineData("matPaginator", "mat-paginator", null, ControlType.Paginator)]
    [InlineData(null, "select", null, ControlType.Select)]
    [InlineData(null, "textarea", null, ControlType.Textarea)]
    [InlineData(null, "input", "checkbox", ControlType.Checkbox)]
    [InlineData(null, "input", "radio", ControlType.Radio)]
    public void DeriveControlType_ShouldClassifyWidgetsAndNativeControls(
        string? widget, string tag, string? inputType, ControlType expected)
    {
        SelectorNaming.DeriveControlType(widget, tag, inputType, opensDialog: false)
            .Should().Be(expected);
    }

    [Fact]
    public void MapSelectors_WithDialogOpensHandler_ShouldLinkDialogTrigger()
    {
        var dialogOpens = new Dictionary<string, string> { ["openSettings"] = "SettingsDialogComponent" };

        var selectors = SelectorNaming.MapSelectors(
            [Element("button", text: "Settings", clickHandler: "openSettings()")], dialogOpens);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.ControlType.Should().Be(ControlType.DialogTrigger);
        selector.OpensDialogComponent.Should().Be("SettingsDialogComponent");
    }

    [Fact]
    public void MapSelectors_DuplicateNames_ShouldDisambiguateWithLandmarkPrefixAndAttachLandmark()
    {
        var shipping = new AstLandmark { Label = "shipping", TestId = "shipping", SelectorValue = "[data-testid=\"shipping\"]" };
        var billing = new AstLandmark { Label = "billing", TestId = "billing", SelectorValue = "[data-testid=\"billing\"]" };

        var selectors = SelectorNaming.MapSelectors(
            [
                Element("button", text: "Save", landmark: shipping),
                Element("button", text: "Save", landmark: billing)
            ],
            NoDialogs);

        selectors.Should().HaveCount(2);
        selectors[0].PropertyName.Should().Be("SaveButton");
        selectors[1].PropertyName.Should().Be("BillingSaveButton");

        // Identical locators in different landmarks get the landmark chain attached.
        selectors[0].ParentLandmark.Should().NotBeNull();
        selectors[0].ParentLandmark!.TestId.Should().Be("shipping");
        selectors[1].ParentLandmark!.TestId.Should().Be("billing");
    }

    [Fact]
    public void MapSelectors_StructuralContext_ShouldFlowThrough()
    {
        var selectors = SelectorNaming.MapSelectors(
            [
                Element("button", text: "Delete",
                    structure: new AstStructure { Conditional = true, Condition = "isAdmin" }),
                Element("li", text: "Row",
                    structure: new AstStructure { Repeated = true, RepeatAlias = "item" })
            ],
            NoDialogs);

        selectors[0].IsConditional.Should().BeTrue();
        selectors[0].ConditionText.Should().Be("isAdmin");
        selectors[1].IsRepeated.Should().BeTrue();
        selectors[1].RepeatItemAlias.Should().Be("item");
    }

    [Fact]
    public void MapSelectors_MatTableWithColumns_ShouldCarryColumnDefs()
    {
        var selectors = SelectorNaming.MapSelectors(
            [
                Element("table", testId: "users", widget: "matTable",
                    table: new AstTable
                    {
                        IsTable = true,
                        IsMatTable = true,
                        Columns = [new AstTableColumn { Id = "name", HeaderText = "Name" }]
                    })
            ],
            NoDialogs);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.IsTable.Should().BeTrue();
        selector.IsMaterialComponent.Should().BeTrue();
        selector.MaterialWidget.Should().Be(MaterialWidget.MatTable);
        selector.ColumnDefs.Should().ContainSingle(c => c.Name == "name" && c.HeaderText == "Name");
        selector.PropertyName.Should().Be("UsersTable");
    }

    [Fact]
    public void MapSelectors_PresentationTagsAndOptions_ShouldBeSkipped()
    {
        var selectors = SelectorNaming.MapSelectors(
            [
                Element("mat-icon", text: "home"),
                Element("mat-label", text: "Country"),
                Element("option", text: "Canada"),
                Element("div") // no facts at all
            ],
            NoDialogs);

        selectors.Should().BeEmpty();
    }

    [Fact]
    public void MapSelectors_RouterLink_ShouldBeMarkedAsLink()
    {
        var selectors = SelectorNaming.MapSelectors(
            [Element("a", text: "Settings", isRouterLink: true)], NoDialogs);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.IsLink.Should().BeTrue();
        selector.AriaRole.Should().Be("link");
        selector.PropertyName.Should().Be("SettingsLink");
    }

    [Fact]
    public void MapSelectors_HeadingWithInterpolation_ShouldRemainTextElement()
    {
        var selectors = SelectorNaming.MapSelectors(
            [Element("h1", interpolated: true)], NoDialogs);

        var selector = selectors.Should().ContainSingle().Subject;
        selector.IsTextElement.Should().BeTrue();
        selector.TextIsInterpolated.Should().BeTrue();
        selector.PropertyName.Should().Be("Heading1");
    }
}
