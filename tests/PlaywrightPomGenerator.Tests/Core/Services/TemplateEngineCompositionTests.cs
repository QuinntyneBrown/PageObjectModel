using FluentAssertions;
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// Child-component composition: typed accessors gated on the run context, with
/// resolvable imports and preserved rooting invariants.
/// </summary>
public sealed class TemplateEngineCompositionTests
{
    private readonly TemplateEngine _engine = new(Options.Create(new GeneratorOptions()));

    private static AngularComponentInfo Dashboard(params ChildComponentRef[] children) => new()
    {
        Name = "DashboardComponent",
        Selector = "app-dashboard",
        FilePath = "/src/dashboard.component.ts",
        IsRoutable = true,
        RoutePath = "dashboard",
        ChildComponents = children
    };

    private static ChildComponentRef Child(string name, bool repeated = false, bool conditional = false, int count = 1) => new()
    {
        Selector = "app-" + name.ToLowerInvariant(),
        ComponentName = name + "Component",
        ComponentFilePath = $"/src/{name}.component.ts",
        Count = count,
        IsRepeated = repeated,
        IsConditional = conditional
    };

    private static TemplateContext Context(params string[] componentObjectNames) => new()
    {
        ComponentObjectNames = componentObjectNames.ToHashSet(StringComparer.Ordinal)
    };

    [Fact]
    public void GeneratePageObject_SingleChild_ShouldEmitGetterAndImport()
    {
        var result = _engine.GeneratePageObject(
            Dashboard(Child("FilterBar")), Context("FilterBarComponent"));

        result.Should().Contain("import { FilterBar } from '../components/filter-bar.component';");
        result.Should().Contain("get filterBar(): FilterBar {");
        result.Should().Contain("return new FilterBar(this.page.locator(FilterBar.hostSelector).first());");
    }

    [Fact]
    public void GeneratePageObject_RepeatedChild_ShouldEmitTheAccessorQuartet()
    {
        var result = _engine.GeneratePageObject(
            Dashboard(Child("KpiCard", repeated: true, count: 3)), Context("KpiCardComponent"));

        result.Should().Contain("kpiCards(): Locator {");
        result.Should().Contain("return this.page.locator(KpiCard.hostSelector);");
        result.Should().Contain("kpiCardAt(index: number): KpiCard {");
        result.Should().Contain("return new KpiCard(this.kpiCards().nth(index));");
        result.Should().Contain("kpiCardByText(text: string): KpiCard {");
        result.Should().Contain("filter({ hasText: text }).first()");
        result.Should().Contain("async kpiCardCount(): Promise<number> {");
    }

    [Fact]
    public void GeneratePageObject_ChildNotInContext_ShouldEmitNothingForIt()
    {
        var result = _engine.GeneratePageObject(
            Dashboard(Child("FilterBar"), Child("KpiCard")), Context("KpiCardComponent"));

        result.Should().NotContain("FilterBar", "its component object is not generated in this run");
        result.Should().Contain("import { KpiCard } from '../components/kpi-card.component';");
    }

    [Fact]
    public void GeneratePageObject_WithoutContext_ShouldEmitNoComposition()
    {
        var result = _engine.GeneratePageObject(Dashboard(Child("KpiCard")));

        result.Should().NotContain("KpiCard");
        result.Should().NotContain("../components/");
    }

    [Fact]
    public void GeneratePageObject_ConditionalChild_ShouldNoteTheCondition()
    {
        var result = _engine.GeneratePageObject(
            Dashboard(Child("Banner", conditional: true)), Context("BannerComponent"));

        result.Should().Contain("get banner(): Banner {");
        result.Should().Contain("Rendered conditionally");
    }

    [Fact]
    public void GenerateComponentObject_WithChild_ShouldRootOnThisRootAndImportSameDirectory()
    {
        var card = new AngularComponentInfo
        {
            Name = "KpiCardComponent",
            Selector = "app-kpi-card",
            FilePath = "/src/kpi-card.component.ts",
            ChildComponents = [Child("TrendIcon")]
        };
        var result = _engine.GenerateComponentObject(card, Context("TrendIconComponent"));

        result.Should().Contain("import { TrendIcon } from './trend-icon.component';");
        result.Should().Contain("return new TrendIcon(this.root.locator(TrendIcon.hostSelector).first());");
        result.Should().NotContain("this.page");
    }

    [Fact]
    public void GenerateComponentObject_SelfReference_ShouldBeSkipped()
    {
        var tree = new AngularComponentInfo
        {
            Name = "TreeNodeComponent",
            Selector = "app-tree-node",
            FilePath = "/src/tree-node.component.ts",
            ChildComponents =
            [
                new ChildComponentRef { Selector = "app-tree-node", ComponentName = "TreeNodeComponent" }
            ]
        };
        var result = _engine.GenerateComponentObject(tree, Context("TreeNodeComponent"));

        result.Should().NotContain("import { TreeNode } from './tree-node.component';");
    }

    [Fact]
    public void GeneratePageObject_AccessorNameCollidingWithSelector_ShouldGetComponentSuffix()
    {
        var component = Dashboard(Child("FilterBar")) with
        {
            Selectors =
            [
                new ElementSelector
                {
                    ElementType = "div",
                    Strategy = SelectorStrategy.TestId,
                    SelectorValue = "[data-testid='filter-bar']",
                    TestIdValue = "filter-bar",
                    PropertyName = "FilterBar"
                }
            ]
        };
        var result = _engine.GeneratePageObject(component, Context("FilterBarComponent"));

        result.Should().Contain("readonly filterBar: Locator;", "selector properties keep their names");
        result.Should().Contain("get filterBarComponent(): FilterBar {");
    }

    [Fact]
    public void GenerateComponentObjectTestSpec_WithHostPageUrl_ShouldUseIt()
    {
        var card = new AngularComponentInfo
        {
            Name = "KpiCardComponent",
            Selector = "app-kpi-card",
            FilePath = "/src/kpi-card.component.ts"
        };
        var context = new TemplateContext
        {
            HostPageUrls = new Dictionary<string, string> { ["KpiCardComponent"] = "/dashboard" }
        };
        var result = _engine.GenerateComponentObjectTestSpec(card, context);

        result.Should().Contain("const HOST_PAGE_URL = '/dashboard';");
        result.Should().Contain("derived from the route tree");
        result.Should().NotContain("TODO: replace with the real host page URL");
    }

    [Fact]
    public void GenerateComponentObjectTestSpec_WithoutHostPageUrl_ShouldKeepThePlaceholder()
    {
        var card = new AngularComponentInfo
        {
            Name = "KpiCardComponent",
            Selector = "app-kpi-card",
            FilePath = "/src/kpi-card.component.ts"
        };
        var result = _engine.GenerateComponentObjectTestSpec(card, TemplateContext.Empty);

        result.Should().Contain("const HOST_PAGE_URL = '/';");
        result.Should().Contain("TODO: replace with the real host page URL");
    }

    [Fact]
    public void GenerateTestSpec_RoutablePage_ShouldNavigateInBeforeEach()
    {
        var page = Dashboard();
        var result = _engine.GenerateTestSpec(page);

        result.Should().Contain("test.beforeEach(async ({ dashboard }) => {");
        result.Should().Contain("await dashboard.navigate();");
    }

    [Fact]
    public void GenerateTestSpec_ParameterizedRoute_ShouldNavigateWithTodoPlaceholders()
    {
        var page = new AngularComponentInfo
        {
            Name = "UserDetailComponent",
            Selector = "app-user-detail",
            FilePath = "/src/user-detail.component.ts",
            IsRoutable = true,
            RoutePath = "users/:userId",
            RouteParams = ["userId"]
        };
        var result = _engine.GenerateTestSpec(page);

        result.Should().Contain("await userDetail.navigate({ userId: 'test-user-id' }); // TODO: provide real route parameter values");
    }

    [Fact]
    public void GenerateTestSpec_NonRoutable_ShouldHaveNoBeforeEach()
    {
        var widget = new AngularComponentInfo
        {
            Name = "KpiCardComponent",
            Selector = "app-kpi-card",
            FilePath = "/src/kpi-card.component.ts",
            IsRoutable = false
        };
        var result = _engine.GenerateTestSpec(widget);

        result.Should().NotContain("test.beforeEach");
    }
}
