using FluentAssertions;
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// Typed control interactions (TemplateEngine.V2): per-ControlType methods on
/// page and component objects, the shared control helpers in both base classes,
/// and the preserved BaseComponent invariants.
/// </summary>
public sealed class TemplateEngineControlInteractionTests
{
    private readonly TemplateEngine _engine = new(Options.Create(new GeneratorOptions()));

    private static ElementSelector Control(string name, ControlType controlType,
        MaterialWidget widget = MaterialWidget.None, string elementType = "input",
        string? inner = null, string? through = null) => new()
    {
        ElementType = elementType,
        Strategy = SelectorStrategy.TestId,
        SelectorValue = $"[data-testid='{name}']",
        TestIdValue = name,
        PropertyName = SelectorNaming.ToPascalCase(name),
        ControlType = controlType,
        MaterialWidget = widget,
        InnerControlSelector = inner,
        ResolvedThroughComponent = through
    };

    private static AngularComponentInfo Component(params ElementSelector[] selectors) => new()
    {
        Name = "SettingsComponent",
        Selector = "app-settings",
        FilePath = "/src/settings.component.ts",
        Selectors = selectors
    };

    [Fact]
    public void GeneratePageObject_WithCheckbox_ShouldEmitCheckUncheckAndExpectChecked()
    {
        var result = _engine.GeneratePageObject(Component(Control("terms", ControlType.Checkbox, MaterialWidget.MatCheckbox, "mat-checkbox")));

        result.Should().Contain("async checkTerms(): Promise<void>");
        result.Should().Contain("await this.setChecked(this.terms, true);");
        result.Should().Contain("async uncheckTerms(): Promise<void>");
        result.Should().Contain("await this.setChecked(this.terms, false);");
        result.Should().Contain("async expectTermsChecked(checked: boolean = true): Promise<void>");
        result.Should().NotContain("async fillTerms(", "typed controls replace the generic fill");
    }

    [Fact]
    public void GeneratePageObject_WithToggle_ShouldEmitToggleOnOff()
    {
        var result = _engine.GeneratePageObject(Component(Control("darkMode", ControlType.Toggle, MaterialWidget.MatSlideToggle, "mat-slide-toggle")));

        result.Should().Contain("async toggleOnDarkMode(): Promise<void>");
        result.Should().Contain("async toggleOffDarkMode(): Promise<void>");
        result.Should().Contain("async expectDarkModeOn(checked: boolean = true): Promise<void>");
    }

    [Fact]
    public void GeneratePageObject_WithMatSelect_ShouldUseOverlayHelper()
    {
        var result = _engine.GeneratePageObject(Component(Control("country", ControlType.Select, MaterialWidget.MatSelect, "mat-select")));

        result.Should().Contain("async selectCountry(optionText: string): Promise<void>");
        result.Should().Contain("await this.selectMatOption(this.country, optionText);");
        result.Should().Contain("async expectCountrySelected(optionText: string): Promise<void>");
    }

    [Fact]
    public void GeneratePageObject_WithNativeSelect_ShouldUseSelectOption()
    {
        var result = _engine.GeneratePageObject(Component(Control("country", ControlType.Select, MaterialWidget.None, "select")));

        result.Should().Contain("await this.country.selectOption({ label: optionLabel });");
        result.Should().NotContain("selectMatOption(this.country");
    }

    [Fact]
    public void GeneratePageObject_WithDatepickerAutocompleteMenuTabsPaginator_ShouldEmitTypedMethods()
    {
        var result = _engine.GeneratePageObject(Component(
            Control("dueDate", ControlType.Datepicker, MaterialWidget.MatDatepicker),
            Control("city", ControlType.Autocomplete, MaterialWidget.MatAutocomplete),
            Control("more", ControlType.MenuTrigger, MaterialWidget.MatMenuTrigger, "button"),
            Control("views", ControlType.Tabs, MaterialWidget.MatTabGroup, "mat-tab-group"),
            Control("pager", ControlType.Paginator, MaterialWidget.MatPaginator, "mat-paginator")));

        result.Should().Contain("async fillDueDateDate(date: string): Promise<void>");
        result.Should().Contain("await this.dueDate.blur();");
        result.Should().Contain("async fillCityAndPick(text: string, optionText?: string): Promise<void>");
        result.Should().Contain("async openMoreMenu(): Promise<void>");
        result.Should().Contain("async pickMoreMenuItem(itemText: string): Promise<void>");
        result.Should().Contain("async selectViewsTab(label: string): Promise<void>");
        result.Should().Contain("async nextPagerPage(): Promise<void>");
        result.Should().Contain("async prevPagerPage(): Promise<void>");
        result.Should().Contain("async setPagerPageSize(size: string): Promise<void>");
    }

    [Fact]
    public void GeneratePageObject_WithDialogTrigger_ShouldOpenAndReturnContainer()
    {
        var trigger = Control("settings", ControlType.DialogTrigger, elementType: "button") with
        {
            OpensDialogComponent = "SettingsDialogComponent"
        };
        var result = _engine.GeneratePageObject(Component(trigger));

        result.Should().Contain("async openSettingsDialog(): Promise<Locator>");
        result.Should().Contain("const container = this.page.locator('mat-dialog-container').last();");
        result.Should().Contain("await container.waitFor({ state: 'visible' });");
    }

    [Fact]
    public void GenerateComponentObject_DialogTrigger_ShouldReachThePageViaRootPage()
    {
        var trigger = Control("settings", ControlType.DialogTrigger, elementType: "button");
        var result = _engine.GenerateComponentObject(Component(trigger));

        // Overlays render outside the component root; Locator.page() reaches them
        // without taking a Page dependency.
        result.Should().Contain("const container = this.root.page().locator('mat-dialog-container').last();");
        result.Should().NotContain("this.page.");
    }

    [Fact]
    public void GenerateBasePage_ShouldContainSharedControlHelpers()
    {
        var result = _engine.GenerateBasePage();

        result.Should().Contain("protected async setChecked(host: Locator, checked: boolean): Promise<void>");
        result.Should().Contain("protected async expectCheckedState(host: Locator, checked: boolean): Promise<void>");
        result.Should().Contain("protected async selectMatOption(trigger: Locator, optionText: string): Promise<void>");
        result.Should().Contain("protected async pickAutocompleteOption(input: Locator, text: string, optionText?: string): Promise<void>");
        result.Should().Contain("protected async pickMenuItem(trigger: Locator, itemText: string): Promise<void>");
        result.Should().Contain("protected async waitForOverlayClosed(anchor: Locator): Promise<void>");
        result.Should().Contain("import { Page, Locator, expect } from '@playwright/test';");
        result.Should().Contain("abstract navigate(params?: unknown): Promise<void>;");
    }

    [Fact]
    public void GenerateBaseComponent_ShouldContainSharedHelpersAndKeepItsInvariants()
    {
        var result = _engine.GenerateBaseComponent();

        // Same helper set as BasePage (single emitter prevents drift).
        result.Should().Contain("protected async setChecked(host: Locator, checked: boolean): Promise<void>");
        result.Should().Contain("protected async selectMatOption(trigger: Locator, optionText: string): Promise<void>");
        result.Should().Contain("trigger.page().locator('.cdk-overlay-container')");

        // The pinned invariants must survive the new helpers.
        result.Should().NotContain("navigate");
        result.Should().NotContain("goto");
        result.Should().NotContain("import { Page");
        result.Should().NotContain(": Page");
        result.Should().NotContain("this.page");
    }

    [Fact]
    public void GeneratePageObject_TypedControl_ShouldStillEmitVisibilityAssertion()
    {
        var result = _engine.GeneratePageObject(Component(Control("country", ControlType.Select, MaterialWidget.MatSelect, "mat-select")));

        result.Should().Contain("async expectCountryVisible(): Promise<void>");
    }

    // --- custom-element look-through: actions descend, host stays the property ------------

    [Fact]
    public void GeneratePageObject_ResolvedMatSelectWrapper_ShouldDescendForActionsOnly()
    {
        var result = _engine.GeneratePageObject(Component(
            Control("country", ControlType.Select, MaterialWidget.MatSelect, "ds-dropdown", inner: "mat-select", through: "DsDropdownComponent")));

        result.Should().Contain("await this.selectMatOption(this.country.locator('mat-select'), optionText);");
        result.Should().Contain("await expect(this.country).toContainText(optionText);", "assertions stay on the host");
        result.Should().Contain("await expect(this.country).toBeVisible();");
        result.Should().Contain("this.country = page.getByTestId('country');", "the property is the host element");
        result.Should().Contain("Control resolved through DsDropdownComponent (mat-select)");
    }

    [Fact]
    public void GeneratePageObject_ResolvedCheckboxWrapper_ShouldDescendIntoSetChecked()
    {
        var result = _engine.GeneratePageObject(Component(
            Control("terms", ControlType.Checkbox, MaterialWidget.MatCheckbox, "ds-check", inner: "mat-checkbox")));

        result.Should().Contain("await this.setChecked(this.terms.locator('mat-checkbox'), true);");
        result.Should().Contain("await this.expectCheckedState(this.terms.locator('mat-checkbox'), checked);");
    }

    [Fact]
    public void GeneratePageObject_ResolvedDatepickerWrapper_ShouldFillTheInnerInput()
    {
        var result = _engine.GeneratePageObject(Component(
            Control("dueDate", ControlType.Datepicker, MaterialWidget.MatDatepicker, "ds-date", inner: "input[type='text']")));

        result.Should().Contain("await this.dueDate.locator('input[type=\\'text\\']').fill(date);");
        result.Should().Contain("await this.dueDate.locator('input[type=\\'text\\']').blur();");
    }

    [Fact]
    public void GeneratePageObject_ResolvedTextWrapper_ShouldFillTheInnerInput()
    {
        var result = _engine.GeneratePageObject(Component(
            Control("email", ControlType.TextInput, elementType: "ds-input", inner: "input")));

        result.Should().Contain("await this.email.locator('input').fill(value);");
    }

    [Fact]
    public void GeneratePageObject_WithoutInnerControl_ShouldNotDescend()
    {
        var result = _engine.GeneratePageObject(Component(Control("country", ControlType.Select, MaterialWidget.MatSelect, "mat-select")));

        result.Should().NotContain(".locator('mat-select')");
        result.Should().NotContain("Control resolved through");
    }

    [Fact]
    public void GeneratePageObject_FormWithResolvedWrapper_ShouldDescendInTheFormFill()
    {
        var country = Control("country", ControlType.Select, MaterialWidget.MatSelect, "ds-dropdown", inner: "mat-select") with
        {
            FormControlName = "country"
        };
        var component = Component(country) with
        {
            Forms =
            [
                new FormInfo
                {
                    FormGroupName = "checkoutForm",
                    Controls = [new FormControlInfo { ControlName = "country", ControlType = ControlType.Select }]
                }
            ]
        };

        var result = _engine.GeneratePageObject(component);

        result.Should().Contain("await this.selectMatOption(this.country.locator('mat-select'), data.country);");
    }

    [Fact]
    public void GenerateComponentObject_ResolvedWrapper_ShouldDescendFromThisRoot()
    {
        var result = _engine.GenerateComponentObject(Component(
            Control("country", ControlType.Select, MaterialWidget.MatSelect, "ds-dropdown", inner: "mat-select")));

        result.Should().Contain("this.country = this.root.getByTestId('country');");
        result.Should().Contain("await this.selectMatOption(this.country.locator('mat-select'), optionText);");
        result.Should().NotContain("this.page");
    }
}
