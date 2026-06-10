using FluentAssertions;
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// Repeated/conditional accessors, typed forms, typed tables, locator strategy
/// v2, navigation v2, spec scaffolds, and the degradation guarantee (v1 model
/// produces zero v2 artifacts).
/// </summary>
public sealed class TemplateEngineV2EmissionTests
{
    private readonly TemplateEngine _engine = new(Options.Create(new GeneratorOptions()));

    private static AngularComponentInfo Component(params ElementSelector[] selectors) => new()
    {
        Name = "OrdersComponent",
        Selector = "app-orders",
        FilePath = "/src/orders.component.ts",
        Selectors = selectors
    };

    // --- repeated & conditional ------------------------------------------------

    [Fact]
    public void GeneratePageObject_RepeatedSelector_ShouldEmitPluralAccessorsAndSuppressSingleActions()
    {
        var row = new ElementSelector
        {
            ElementType = "li",
            Strategy = SelectorStrategy.TestId,
            SelectorValue = "[data-testid='order-row']",
            TestIdValue = "order-row",
            PropertyName = "OrderRow",
            IsRepeated = true,
            RepeatItemAlias = "order",
            HasClickHandler = true,
            ClickHandlerName = "open"
        };
        var result = _engine.GeneratePageObject(Component(row));

        result.Should().Contain("orderRowAt(index: number): Locator");
        result.Should().Contain("return this.orderRow.nth(index);");
        result.Should().Contain("orderRowByText(text: string): Locator");
        result.Should().Contain("return this.orderRow.filter({ hasText: text });");
        result.Should().Contain("async orderRowCount(): Promise<number>");
        result.Should().Contain("async clickOrderRowAt(index: number): Promise<void>");
        result.Should().Contain("Matches ALL repeated instances");
        result.Should().NotContain("async clickOrderRow(): Promise<void>", "single-element actions on repeated content violate strict mode");
    }

    [Fact]
    public void GeneratePageObject_ConditionalSelector_ShouldEmitVisibleAndHiddenPair()
    {
        var error = new ElementSelector
        {
            ElementType = "span",
            Strategy = SelectorStrategy.TestId,
            SelectorValue = "[data-testid='error-message']",
            TestIdValue = "error-message",
            PropertyName = "ErrorMessage",
            IsConditional = true,
            ConditionText = "form.invalid"
        };
        var result = _engine.GeneratePageObject(Component(error));

        result.Should().Contain("async expectErrorMessageVisible(): Promise<void>");
        result.Should().Contain("async expectErrorMessageHidden(): Promise<void>");
        result.Should().Contain("await expect(this.errorMessage).toBeHidden();");
        result.Should().Contain("form.invalid");
    }

    [Fact]
    public void GenerateTestSpec_ShouldSkipConditionalsAndUseFirstForRepeated()
    {
        var conditional = new ElementSelector
        {
            ElementType = "span",
            Strategy = SelectorStrategy.Css,
            SelectorValue = "span.error",
            PropertyName = "ErrorMessage",
            IsConditional = true,
            ConditionText = "form.invalid"
        };
        var repeated = new ElementSelector
        {
            ElementType = "li",
            Strategy = SelectorStrategy.Css,
            SelectorValue = "li.row",
            PropertyName = "OrderRow",
            IsRepeated = true
        };
        var result = _engine.GenerateTestSpec(Component(conditional, repeated));

        result.Should().NotContain("test('ErrorMessage should be visible'");
        result.Should().Contain("// Conditional elements not asserted by default");
        result.Should().Contain("errorMessage (form.invalid) -> expectErrorMessageVisible() / expectErrorMessageHidden()");
        result.Should().Contain("await expect(orders.orderRow.first()).toBeVisible();");
    }

    // --- forms ----------------------------------------------------------------

    private static AngularComponentInfo FormComponent()
    {
        var username = new ElementSelector
        {
            ElementType = "input",
            Strategy = SelectorStrategy.FormControl,
            SelectorValue = "[formControlName='username']",
            PropertyName = "UsernameInput",
            FormControlName = "username",
            ControlType = ControlType.TextInput
        };
        var remember = new ElementSelector
        {
            ElementType = "mat-checkbox",
            Strategy = SelectorStrategy.FormControl,
            SelectorValue = "[formControlName='rememberMe']",
            PropertyName = "RememberMeCheckbox",
            FormControlName = "rememberMe",
            ControlType = ControlType.Checkbox,
            MaterialWidget = MaterialWidget.MatCheckbox
        };
        var country = new ElementSelector
        {
            ElementType = "mat-select",
            Strategy = SelectorStrategy.FormControl,
            SelectorValue = "[formControlName='country']",
            PropertyName = "CountrySelect",
            FormControlName = "country",
            ControlType = ControlType.Select,
            MaterialWidget = MaterialWidget.MatSelect
        };
        var submit = new ElementSelector
        {
            ElementType = "button",
            Strategy = SelectorStrategy.Role,
            SelectorValue = "button:has-text(\"Log in\")",
            PropertyName = "LogInButton",
            TextContent = "Log in",
            Attributes = new Dictionary<string, string> { ["type"] = "submit" }
        };

        return new AngularComponentInfo
        {
            Name = "LoginComponent",
            Selector = "app-login",
            FilePath = "/src/login.component.ts",
            Selectors = [username, remember, country, submit],
            Forms =
            [
                new FormInfo
                {
                    FormGroupName = "loginForm",
                    SubmitHandlerName = "onSubmit",
                    Controls =
                    [
                        new FormControlInfo { ControlName = "username", ControlType = ControlType.TextInput, Required = true },
                        new FormControlInfo { ControlName = "rememberMe", ControlType = ControlType.Checkbox },
                        new FormControlInfo { ControlName = "country", ControlType = ControlType.Select },
                        new FormControlInfo { ControlName = "notes", ControlType = ControlType.TextInput }
                    ]
                }
            ]
        };
    }

    [Fact]
    public void GeneratePageObject_WithForm_ShouldEmitTypedFormDataInterface()
    {
        var result = _engine.GeneratePageObject(FormComponent());

        result.Should().Contain("export interface LoginFormData {");
        result.Should().Contain("username?: string;");
        result.Should().Contain("rememberMe?: boolean;");
        result.Should().Contain("country?: string;");
        result.Should().Contain("/** Required control. */");

        // Interface is declared before the class.
        result.IndexOf("export interface LoginFormData").Should()
            .BeLessThan(result.IndexOf("export class Login extends BasePage"));
    }

    [Fact]
    public void GeneratePageObject_WithForm_ShouldEmitFillAndSubmitMethods()
    {
        var result = _engine.GeneratePageObject(FormComponent());

        // No doubled "Form" suffix: loginForm -> fillLoginForm.
        result.Should().Contain("async fillLoginForm(data: LoginFormData): Promise<void>");
        result.Should().Contain("if (data.username !== undefined) {");
        result.Should().Contain("await this.usernameInput.fill(data.username);");
        result.Should().Contain("await this.setChecked(this.rememberMeCheckbox, data.rememberMe);");
        result.Should().Contain("await this.selectMatOption(this.countrySelect, data.country);");

        // Control without a generated property falls back to an inline locator.
        result.Should().Contain("this.page.locator('[formControlName=\"notes\"]').fill(data.notes);");

        // Submit resolves via the type="submit" button.
        result.Should().Contain("async submitLoginForm(): Promise<void>");
        result.Should().Contain("await this.logInButton.click();");
    }

    [Fact]
    public void GenerateComponentObject_WithForm_ShouldRootFallbacksOnThisRoot()
    {
        var result = _engine.GenerateComponentObject(FormComponent());

        result.Should().Contain("async fillLoginForm(data: LoginFormData): Promise<void>");
        result.Should().Contain("this.root.locator('[formControlName=\"notes\"]').fill(data.notes);");
        result.Should().NotContain("this.page");
    }

    // --- tables v2 ---------------------------------------------------------------

    [Fact]
    public void GeneratePageObject_TableWithColumnDefs_ShouldEmitTypedColumnAccessors()
    {
        var table = new ElementSelector
        {
            ElementType = "mat-table",
            Strategy = SelectorStrategy.TestId,
            SelectorValue = "[data-testid='users-table']",
            TestIdValue = "users-table",
            PropertyName = "UsersTable",
            IsTable = true,
            IsMaterialComponent = true,
            MaterialWidget = MaterialWidget.MatTable,
            ColumnDefs =
            [
                new TableColumnDef { Name = "name", HeaderText = "Name" },
                new TableColumnDef { Name = "status", HeaderText = "Status" }
            ]
        };
        var result = _engine.GeneratePageObject(Component(table));

        result.Should().Contain("getUsersTableCell(rowIndex: number, column: 'name' | 'status'): Locator");
        result.Should().Contain("return this.getUsersTableRow(rowIndex).locator(`.mat-column-${column}`);");
        result.Should().Contain("getUsersTableColumnCells(column: 'name' | 'status'): Locator");
        result.Should().Contain("getUsersTableRowByText(text: string): Locator");
        result.Should().Contain("async expectUsersTableColumnHeaders(): Promise<void>");
        result.Should().Contain("toContainText(['Name', 'Status']);");

        // The v1 generic helpers stay.
        result.Should().Contain("getUsersTableRows(): Locator");
        result.Should().Contain("getUsersTableRow(index: number): Locator");
    }

    [Fact]
    public void GeneratePageObject_TableWithoutColumnDefs_ShouldOnlyEmitGenericHelpers()
    {
        var table = new ElementSelector
        {
            ElementType = "mat-table",
            Strategy = SelectorStrategy.Css,
            SelectorValue = "mat-table, table[mat-table], [mat-table]",
            PropertyName = "DataTable",
            IsTable = true,
            IsMaterialComponent = true
        };
        var result = _engine.GeneratePageObject(Component(table));

        result.Should().Contain("getDataTableRows(): Locator");
        result.Should().NotContain("getDataTableCell(");
        result.Should().NotContain(".mat-column-");
    }

    // --- locator strategy v2 -------------------------------------------------------

    [Fact]
    public void GeneratePageObject_RoleWithAriaRole_ShouldEmitGeneralizedGetByRole()
    {
        var combo = new ElementSelector
        {
            ElementType = "mat-select",
            Strategy = SelectorStrategy.Role,
            SelectorValue = "mat-select[aria-label='Country']",
            PropertyName = "CountrySelect",
            AriaRole = "combobox",
            AriaLabel = "Country"
        };
        var result = _engine.GeneratePageObject(Component(combo));

        result.Should().Contain("this.countrySelect = page.getByRole('combobox', { name: 'Country' });");
    }

    [Fact]
    public void GeneratePageObject_LabelStrategy_ShouldUseLabelText()
    {
        var email = new ElementSelector
        {
            ElementType = "input",
            Strategy = SelectorStrategy.Label,
            SelectorValue = "[formControlName='email']",
            PropertyName = "EmailAddressInput",
            LabelText = "Email address"
        };
        var result = _engine.GeneratePageObject(Component(email));

        result.Should().Contain("this.emailAddressInput = page.getByLabel('Email address');");
    }

    [Fact]
    public void GeneratePageObject_FormControlStrategy_ShouldEmitAttributeLocator()
    {
        var input = new ElementSelector
        {
            ElementType = "input",
            Strategy = SelectorStrategy.FormControl,
            SelectorValue = "[formControlName='username']",
            PropertyName = "UsernameInput",
            FormControlName = "username"
        };
        var result = _engine.GeneratePageObject(Component(input));

        result.Should().Contain("this.usernameInput = page.locator(\"[formControlName='username']\");");
    }

    [Fact]
    public void GeneratePageObject_WithParentLandmark_ShouldChainTheLocator()
    {
        var save = new ElementSelector
        {
            ElementType = "button",
            Strategy = SelectorStrategy.Role,
            SelectorValue = "button:has-text(\"Save\")",
            PropertyName = "BillingSaveButton",
            AriaRole = "button",
            TextContent = "Save",
            ParentLandmark = new LandmarkRef
            {
                Label = "billing",
                TestId = "billing",
                SelectorValue = "[data-testid=\"billing\"]"
            }
        };
        var result = _engine.GeneratePageObject(Component(save));

        result.Should().Contain("this.billingSaveButton = page.getByTestId('billing').getByRole('button', { name: 'Save' });");
    }

    // --- navigation v2 ---------------------------------------------------------------

    private static AngularComponentInfo RoutableComponent(string name, string routePath, params string[] routeParams) => new()
    {
        Name = name,
        Selector = $"app-{name.ToLowerInvariant()}",
        FilePath = $"/src/{name}.ts",
        IsRoutable = true,
        RoutePath = routePath,
        RouteParams = routeParams,
        RouteEvidence = true
    };

    [Fact]
    public void GenerateUrlsConfig_ShouldEmitStaticStringsAndParamFunctions()
    {
        var project = new AngularProjectInfo
        {
            Name = "shop",
            RootPath = "/shop",
            SourceRoot = "/shop/src",
            ProjectType = AngularProjectType.Application,
            Components =
            [
                RoutableComponent("DashboardComponent", "dashboard") with { TitleFromRoute = "Dashboard" },
                RoutableComponent("UserDetailComponent", "users/:userId", "userId"),
                RoutableComponent("OrderItemComponent", "orders/:orderId/items/:itemId", "orderId", "itemId")
            ]
        };
        var result = _engine.GenerateUrlsConfig(project);

        result.Should().Contain("dashboard: '/dashboard',");
        result.Should().Contain("/** Route title: \"Dashboard\" */");
        result.Should().Contain("userDetail: (userId: string) => `/users/${userId}`,");
        result.Should().Contain("orderItem: (orderId: string, itemId: string) => `/orders/${orderId}/items/${itemId}`,");
        result.Should().NotContain("API_ENDPOINTS", "the example block is off by default");
    }

    [Fact]
    public void GenerateUrlsConfig_WithApiEndpointsExampleEnabled_ShouldEmitTheBlock()
    {
        var engine = new TemplateEngine(Options.Create(new GeneratorOptions { EmitApiEndpointsExample = true }));
        var project = new AngularProjectInfo
        {
            Name = "shop",
            RootPath = "/shop",
            SourceRoot = "/shop/src",
            ProjectType = AngularProjectType.Application
        };

        engine.GenerateUrlsConfig(project).Should().Contain("export const API_ENDPOINTS = {");
    }

    [Fact]
    public void GeneratePageObject_ParameterizedRoute_ShouldEmitTypedNavigate()
    {
        var component = RoutableComponent("UserDetailComponent", "users/:userId", "userId");
        var result = _engine.GeneratePageObject(component);

        result.Should().Contain("async navigate(params: { userId: string }): Promise<void>");
        result.Should().Contain("await this.page.goto(URLS.userDetail(params.userId));");
        result.Should().Contain("await this.waitForLoad();");
    }

    [Fact]
    public void GeneratePageObject_WithStableTestIdAnchor_ShouldAwaitItInWaitForLoad()
    {
        var anchor = new ElementSelector
        {
            ElementType = "h1",
            Strategy = SelectorStrategy.TestId,
            SelectorValue = "[data-testid='page-title']",
            TestIdValue = "page-title",
            PropertyName = "PageTitle"
        };
        var conditionalFirst = new ElementSelector
        {
            ElementType = "span",
            Strategy = SelectorStrategy.TestId,
            SelectorValue = "[data-testid='maybe']",
            TestIdValue = "maybe",
            PropertyName = "Maybe",
            IsConditional = true
        };
        var result = _engine.GeneratePageObject(Component(conditionalFirst, anchor));

        // The anchor must skip conditional/repeated selectors.
        result.Should().Contain("await this.page.waitForSelector(this.componentSelector);");
        result.Should().Contain("await this.pageTitle.first().waitFor({ state: 'visible' });");
        result.Should().NotContain("await this.maybe.first().waitFor");
    }

    // --- degradation guarantee --------------------------------------------------------

    [Fact]
    public void GeneratePageObject_WithV1ShapedModel_ShouldProduceNoV2Artifacts()
    {
        // A model exactly as the regex engine produces it (no enrichment fields).
        var component = new AngularComponentInfo
        {
            Name = "LoginComponent",
            Selector = "app-login",
            FilePath = "/src/login.component.ts",
            Selectors =
            [
                new ElementSelector
                {
                    ElementType = "input",
                    Strategy = SelectorStrategy.Css,
                    SelectorValue = "[formControlName='username']",
                    PropertyName = "UsernameInput"
                },
                new ElementSelector
                {
                    ElementType = "button",
                    Strategy = SelectorStrategy.Role,
                    SelectorValue = "button:has-text(\"Login\")",
                    PropertyName = "LoginButton",
                    TextContent = "Login"
                }
            ],
            IsRoutable = true
        };
        var result = _engine.GeneratePageObject(component);

        result.Should().NotContain("FormData");
        result.Should().NotContain("setChecked(this.");
        result.Should().NotContain("At(index: number)");
        result.Should().NotContain("expectLoginButtonHidden");
        result.Should().NotContain(".mat-column-");
        result.Should().Contain("async navigate(): Promise<void>", "no route params means the v1 signature");
        result.Should().Contain("async fillUsernameInput(value: string): Promise<void>", "v1 fill emission is unchanged");
        result.Should().Contain("this.loginButton = page.getByRole('button', { name: 'Login' });");
    }
}
