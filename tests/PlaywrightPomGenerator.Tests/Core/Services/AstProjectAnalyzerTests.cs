using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class AstProjectAnalyzerTests
{
    private readonly ISidecarTransport _transport = Substitute.For<ISidecarTransport>();
    private readonly AstProjectAnalyzer _analyzer;

    public AstProjectAnalyzerTests()
    {
        _analyzer = new AstProjectAnalyzer(_transport, Substitute.For<ILogger<AstProjectAnalyzer>>());
    }

    private void SetupResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var element = document.RootElement.Clone();
        _transport.InvokeAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(element);
    }

    [Fact]
    public async Task AnalyzeProjectAsync_ShouldMapComponentsTemplatesAndRoutes()
    {
        // Arrange — a representative slice of the schemaVersion 1 contract.
        SetupResponse("""
            {
              "schemaVersion": 1,
              "engine": { "node": "20.11.1", "typescript": "5.4.5", "angularCompiler": "17.3.2", "compilerSource": "/app/node_modules/@angular/compiler" },
              "projects": [
                {
                  "name": "shop",
                  "components": [
                    {
                      "className": "CheckoutComponent",
                      "filePath": "/app/src/checkout/checkout.component.ts",
                      "selector": "app-checkout",
                      "selectors": ["app-checkout"],
                      "standalone": true,
                      "templateSource": "external",
                      "templateUrl": "/app/src/checkout/checkout.component.html",
                      "templateParsed": true,
                      "templateErrors": [],
                      "inputs": [ { "name": "orderId", "kind": "signal", "alias": null, "required": true } ],
                      "outputs": [ { "name": "submitted", "kind": "decorator", "alias": "onSubmitted", "required": false } ],
                      "queries": [ { "name": "form", "kind": "viewChild", "signal": true, "target": "PaymentForm" } ],
                      "importsIdentifiers": ["MatButtonModule"],
                      "dialogOpens": [ { "handler": "openConfirm", "componentName": "ConfirmDialogComponent" } ],
                      "template": {
                        "elements": [
                          {
                            "tag": "button",
                            "testId": "submit-order",
                            "id": null, "classes": [], "role": null,
                            "aria": { "label": null, "labelledBy": null, "labelledByText": null, "describedBy": null },
                            "labels": { "wrappingLabel": null, "labelFor": null, "matLabel": null, "placeholder": null },
                            "text": { "value": "Submit", "interpolated": false },
                            "attributes": { "type": "submit" },
                            "events": ["click"], "handlers": { "click": "submit" },
                            "twoWay": [],
                            "form": { "formControlName": null, "formGroupName": null, "formGroup": "checkoutForm", "ngModel": null, "inputType": "submit" },
                            "structure": { "conditional": true, "condition": "canSubmit", "repeated": false, "repeatAlias": null, "trackBy": null, "switchCase": null, "projected": false, "depth": 2 },
                            "ancestry": { "landmark": { "label": "form(checkoutForm)", "testId": null, "role": "form", "accessibleName": null, "selectorValue": "form" }, "headingText": "Checkout" },
                            "widget": null,
                            "isRouterLink": false, "routerLinkValue": null,
                            "table": null, "hasNgContent": false, "line": 12
                          },
                          {
                            "tag": "table",
                            "testId": "orders-table",
                            "id": null, "classes": [], "role": null,
                            "aria": { "label": null, "labelledBy": null, "labelledByText": null, "describedBy": null },
                            "labels": { "wrappingLabel": null, "labelFor": null, "matLabel": null, "placeholder": null },
                            "text": { "value": null, "interpolated": false },
                            "attributes": { "mat-table": "" },
                            "events": [], "handlers": {}, "twoWay": [],
                            "form": { "formControlName": null, "formGroupName": null, "formGroup": null, "ngModel": null, "inputType": null },
                            "structure": { "conditional": false, "condition": null, "repeated": false, "repeatAlias": null, "trackBy": null, "switchCase": null, "projected": false, "depth": 1 },
                            "ancestry": { "landmark": null, "headingText": null },
                            "widget": "matTable",
                            "isRouterLink": false, "routerLinkValue": null,
                            "table": { "isTable": true, "isMatTable": true, "columns": [ { "id": "name", "headerText": "Name" } ] },
                            "hasNgContent": false, "line": 30
                          }
                        ],
                        "forms": [ { "formGroup": "checkoutForm", "submitHandler": "submit", "controls": [ { "name": "email", "inputType": "email", "widget": null, "tag": "input" } ] } ]
                      },
                      "templateContent": null,
                      "childComponents": [
                        { "selector": "app-kpi-card", "componentClassName": "KpiCardComponent", "componentFilePath": "/app/src/kpi/kpi-card.component.ts", "count": 3, "conditional": false, "repeated": true, "library": null }
                      ]
                    }
                  ],
                  "routes": {
                    "tree": [
                      { "path": "checkout", "fullPath": "/checkout", "component": "CheckoutComponent", "componentFilePath": "/app/src/checkout/checkout.component.ts",
                        "redirectTo": null, "pathMatch": null, "title": "Checkout", "outlet": null,
                        "pathParams": [], "wildcard": false, "guards": ["authGuard"], "dataKeys": ["role"],
                        "loadComponent": null, "loadChildren": null, "isLazy": false, "isRoot": true,
                        "sourceFile": "/app/src/app.routes.ts", "children": [] }
                    ],
                    "componentRoutes": [
                      { "componentFilePath": "/app/src/checkout/checkout.component.ts", "componentClassName": "CheckoutComponent", "fullPaths": ["/checkout"], "titles": ["Checkout"] }
                    ]
                  },
                  "warnings": ["a project warning"]
                }
              ],
              "warnings": ["a top-level warning"]
            }
            """);

        // Act
        var result = await _analyzer.AnalyzeProjectAsync("/app", [new AstProjectTarget("shop", "/app/src", "app")]);

        // Assert
        result.SchemaVersion.Should().Be(1);
        result.Engine!.TypeScript.Should().Be("5.4.5");
        result.Engine.AngularCompiler.Should().Be("17.3.2");
        result.Warnings.Should().ContainSingle(w => w == "a top-level warning");

        var project = result.Projects.Should().ContainSingle().Subject;
        project.Name.Should().Be("shop");
        project.Warnings.Should().ContainSingle(w => w == "a project warning");

        var component = project.Components.Should().ContainSingle().Subject;
        component.ClassName.Should().Be("CheckoutComponent");
        component.Standalone.Should().BeTrue();
        component.Inputs.Should().ContainSingle(p => p.Name == "orderId" && p.Kind == "signal" && p.Required);
        component.Outputs.Should().ContainSingle(p => p.Alias == "onSubmitted");
        component.DialogOpens.Should().ContainSingle(d => d.ComponentName == "ConfirmDialogComponent");
        component.ChildComponents.Should().ContainSingle(c => c.Selector == "app-kpi-card" && c.Repeated && c.Count == 3);

        var button = component.Template!.Elements[0];
        button.TestId.Should().Be("submit-order");
        button.Structure.Conditional.Should().BeTrue();
        button.Structure.Condition.Should().Be("canSubmit");
        button.Ancestry.Landmark!.Label.Should().Be("form(checkoutForm)");
        button.Handlers["click"].Should().Be("submit");

        var table = component.Template.Elements[1];
        table.Table!.IsMatTable.Should().BeTrue();
        table.Table.Columns.Should().ContainSingle(c => c.Id == "name" && c.HeaderText == "Name");

        component.Template.Forms.Should().ContainSingle(f => f.FormGroup == "checkoutForm" && f.SubmitHandler == "submit");

        var route = project.Routes!.Tree.Should().ContainSingle().Subject;
        route.FullPath.Should().Be("/checkout");
        route.Guards.Should().ContainSingle(g => g == "authGuard");
        project.Routes.ComponentRoutes.Should().ContainSingle(r => r.ComponentClassName == "CheckoutComponent");
    }

    [Fact]
    public async Task AnalyzeProjectAsync_ShouldInvokeAnalyzeProjectWithRootParameter()
    {
        // Arrange
        SetupResponse("""{ "schemaVersion": 1, "engine": null, "projects": [], "warnings": [] }""");

        // Act
        await _analyzer.AnalyzeProjectAsync("/workspace", [new AstProjectTarget("a", "/workspace/src", null)]);

        // Assert — the `root` property is the NODE_PATH convention with the transport.
        await _transport.Received(1).InvokeAsync(
            "analyzeProject",
            Arg.Is<object>(p => p.GetType().GetProperty("root") != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeProjectAsync_WithUnsupportedSchemaVersion_ShouldThrow()
    {
        // Arrange
        SetupResponse("""{ "schemaVersion": 99, "engine": null, "projects": [], "warnings": [] }""");

        // Act
        var act = () => _analyzer.AnalyzeProjectAsync("/app", []);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*schema version 99*");
    }

    [Fact]
    public async Task AnalyzeProjectAsync_WithNullArguments_ShouldThrowArgumentNullException()
    {
        var actPath = () => _analyzer.AnalyzeProjectAsync(null!, []);
        await actPath.Should().ThrowAsync<ArgumentNullException>();

        var actProjects = () => _analyzer.AnalyzeProjectAsync("/app", null!);
        await actProjects.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullTransport_ShouldThrowArgumentNullException()
    {
        var act = () => new AstProjectAnalyzer(null!, Substitute.For<ILogger<AstProjectAnalyzer>>());
        act.Should().Throw<ArgumentNullException>();
    }
}
