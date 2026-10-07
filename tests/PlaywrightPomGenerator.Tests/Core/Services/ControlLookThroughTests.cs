using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// Custom-element look-through: a wrapper component resolves to the single value
/// control inside its own template (recursively), and to nothing when ambiguous.
/// </summary>
public sealed class ControlLookThroughTests
{
    private const string HostPath = "/app/src/checkout.component.ts";

    private static AstElement El(string tag, string? widget = null, string? inputType = null, string? testId = null,
        bool repeated = false, bool projected = false) => new()
    {
        Tag = tag,
        Widget = widget,
        TestId = testId,
        Form = new AstFormFacts { InputType = inputType },
        Structure = new AstStructure { Repeated = repeated, Projected = projected }
    };

    private static AstComponent Comp(string className, string path, string selector, bool parsed = true,
        IReadOnlyList<AstElement>? elements = null, IReadOnlyList<AstChildComponent>? children = null) => new()
    {
        ClassName = className,
        FilePath = path,
        Selector = selector,
        Selectors = [selector],
        TemplateParsed = parsed,
        Template = parsed ? new AstTemplate { Elements = elements ?? [] } : null,
        ChildComponents = children ?? []
    };

    private static AstChildComponent Child(string selector, string className, string path, bool repeated = false) => new()
    {
        Selector = selector,
        ComponentClassName = className,
        ComponentFilePath = path,
        Count = 1,
        Repeated = repeated
    };

    private static AstComponent Host(params AstChildComponent[] children) =>
        Comp("CheckoutComponent", HostPath, "app-checkout", elements: [El("ds-dropdown")], children: children);

    private static ControlLookThrough Create(params AstComponent[] components) =>
        ControlLookThrough.Create(new AstProjectAnalysis
        {
            SchemaVersion = 1,
            Projects = [new AstProjectResult { Name = "app", Components = components }]
        });

    [Fact]
    public void Resolve_WrapperAroundSingleMatSelect_ShouldResolveToSelect()
    {
        var dropdown = Comp("DsDropdownComponent", "/app/src/ds-dropdown.component.ts", "ds-dropdown",
            elements: [El("mat-form-field", "matFormField"), El("mat-select", "matSelect"), El("mat-option")]);
        var host = Host(Child("ds-dropdown", "DsDropdownComponent", "/app/src/ds-dropdown.component.ts"));

        var resolved = Create(host, dropdown).Resolve(host);

        var inner = resolved.Should().ContainKey("ds-dropdown").WhoseValue;
        inner.ControlType.Should().Be(ControlType.Select);
        inner.Widget.Should().Be(MaterialWidget.MatSelect);
        inner.InnerCss.Should().Be("mat-select");
        inner.ComponentClassName.Should().Be("DsDropdownComponent");
    }

    [Fact]
    public void Resolve_WrapperWithTwoValueControls_ShouldNotResolve()
    {
        var range = Comp("DsRangeComponent", "/lib/ds-range.component.ts", "ds-range",
            elements: [El("input", inputType: "number"), El("input", inputType: "number")]);
        var host = Host(Child("ds-dropdown", "DsRangeComponent", "/lib/ds-range.component.ts"));

        Create(host, range).Resolve(host).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_NestedWrappers_ShouldResolveAndReportTheDirectChild()
    {
        var inner = Comp("DsSelectCoreComponent", "/lib/core.component.ts", "ds-select-core",
            elements: [El("mat-select", "matSelect")]);
        var outer = Comp("DsDropdownComponent", "/lib/dropdown.component.ts", "ds-dropdown",
            elements: [El("ds-select-core"), El("mat-hint")],
            children: [Child("ds-select-core", "DsSelectCoreComponent", "/lib/core.component.ts")]);
        var host = Host(Child("ds-dropdown", "DsDropdownComponent", "/lib/dropdown.component.ts"));

        var resolved = Create(host, outer, inner).Resolve(host);

        resolved["ds-dropdown"].ControlType.Should().Be(ControlType.Select);
        resolved["ds-dropdown"].InnerCss.Should().Be("mat-select", "descendant CSS needs no wrapper prefix");
        resolved["ds-dropdown"].ComponentClassName.Should().Be("DsDropdownComponent");
    }

    [Fact]
    public void Resolve_RepeatedInnerControl_ShouldNotResolve()
    {
        var list = Comp("DsChecklistComponent", "/lib/checklist.component.ts", "ds-checklist",
            elements: [El("mat-checkbox", "matCheckbox", repeated: true)]);
        var host = Host(Child("ds-dropdown", "DsChecklistComponent", "/lib/checklist.component.ts"));

        Create(host, list).Resolve(host).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ProjectedInnerControl_ShouldNotResolve()
    {
        var slot = Comp("DsSlotComponent", "/lib/slot.component.ts", "ds-slot",
            elements: [El("input", inputType: "text", projected: true)]);
        var host = Host(Child("ds-dropdown", "DsSlotComponent", "/lib/slot.component.ts"));

        Create(host, slot).Resolve(host).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_CyclicWrappers_ShouldNotResolveOrLoop()
    {
        var a = Comp("AComponent", "/lib/a.component.ts", "ds-a",
            elements: [El("ds-b")], children: [Child("ds-b", "BComponent", "/lib/b.component.ts")]);
        var b = Comp("BComponent", "/lib/b.component.ts", "ds-b",
            elements: [El("ds-a")], children: [Child("ds-a", "AComponent", "/lib/a.component.ts")]);
        var host = Host(Child("ds-dropdown", "AComponent", "/lib/a.component.ts"));

        Create(host, a, b).Resolve(host).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ChildWithUnparsedTemplate_ShouldNotResolve()
    {
        var broken = Comp("DsBrokenComponent", "/lib/broken.component.ts", "ds-broken", parsed: false);
        var host = Host(Child("ds-dropdown", "DsBrokenComponent", "/lib/broken.component.ts"));

        Create(host, broken).Resolve(host).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_DatepickerInput_ShouldUseTypedInputCss()
    {
        var picker = Comp("DsDateComponent", "/lib/date.component.ts", "ds-date",
            elements: [El("input", "matDatepicker", inputType: "text"), El("mat-datepicker-toggle"), El("mat-datepicker")]);
        var host = Host(Child("ds-dropdown", "DsDateComponent", "/lib/date.component.ts"));

        var inner = Create(host, picker).Resolve(host)["ds-dropdown"];

        inner.ControlType.Should().Be(ControlType.Datepicker);
        inner.InnerCss.Should().Be("input[type='text']");
    }

    [Fact]
    public void Resolve_InnerControlWithTestId_ShouldPreferTheTestId()
    {
        var toggle = Comp("DsToggleComponent", "/lib/toggle.component.ts", "ds-toggle",
            elements: [El("mat-slide-toggle", "matSlideToggle", testId: "the-toggle")]);
        var host = Host(Child("ds-dropdown", "DsToggleComponent", "/lib/toggle.component.ts"));

        var inner = Create(host, toggle).Resolve(host)["ds-dropdown"];

        inner.ControlType.Should().Be(ControlType.Toggle);
        inner.InnerCss.Should().Be("[data-testid='the-toggle']");
    }

    [Fact]
    public void Resolve_RadioGroupWithButtons_ShouldResolveToTheGroup()
    {
        var radios = Comp("DsRadioComponent", "/lib/radio.component.ts", "ds-radio",
            elements: [El("mat-radio-group", "matRadioGroup"), El("mat-radio-button", "matRadioButton"), El("mat-radio-button", "matRadioButton")]);
        var host = Host(Child("ds-dropdown", "DsRadioComponent", "/lib/radio.component.ts"));

        var inner = Create(host, radios).Resolve(host)["ds-dropdown"];

        inner.ControlType.Should().Be(ControlType.Radio);
        inner.InnerCss.Should().Be("mat-radio-group");
    }

    [Fact]
    public void Resolve_NonValueControlsOnly_ShouldNotResolve()
    {
        var menu = Comp("DsMenuComponent", "/lib/menu.component.ts", "ds-menu",
            elements: [El("button", "matMenuTrigger"), El("mat-menu", "matMenu")]);
        var host = Host(Child("ds-dropdown", "DsMenuComponent", "/lib/menu.component.ts"));

        Create(host, menu).Resolve(host).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_MixedPathSeparators_ShouldStillMatch()
    {
        var dropdown = Comp("DsDropdownComponent", @"C:\ws\libs\ui\ds-dropdown.component.ts", "ds-dropdown",
            elements: [El("mat-select", "matSelect")]);
        var host = Host(Child("ds-dropdown", "DsDropdownComponent", "C:/ws/libs/ui/ds-dropdown.component.ts"));

        Create(host, dropdown).Resolve(host).Should().ContainKey("ds-dropdown");
    }

    [Fact]
    public void Resolve_UnknownChild_ShouldNotResolve()
    {
        var host = Host(Child("ds-dropdown", "MissingComponent", "/nowhere.ts"));

        Create(host).Resolve(host).Should().BeEmpty();
    }

    [Fact]
    public void Empty_ShouldResolveNothing()
    {
        var host = Host(Child("ds-dropdown", "DsDropdownComponent", "/lib/x.ts"));

        ControlLookThrough.Empty.Resolve(host).Should().BeEmpty();
    }
}
