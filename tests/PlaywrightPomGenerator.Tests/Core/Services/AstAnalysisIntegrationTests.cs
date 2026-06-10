using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// End-to-end checks of the AST analysis engine: real <see cref="AngularAnalyzer"/> +
/// real <see cref="AstProjectAnalyzer"/> + real Node sidecar against fixture apps on
/// disk. Template analysis resolves @angular/compiler from the sidecar's own
/// devDependency (fixtures have no node_modules). Skipped when Node or the sidecar
/// dependencies are absent — except the fallback test, which runs everywhere.
/// </summary>
public sealed class AstAnalysisIntegrationTests
{
    private static readonly string SidecarPath = FindRepoSidecar();

    private static bool SidecarWithCompilerAvailable =>
        File.Exists(SidecarPath)
        && File.Exists(Path.Combine(Path.GetDirectoryName(SidecarPath)!, "node_modules", "typescript", "package.json"))
        && File.Exists(Path.Combine(Path.GetDirectoryName(SidecarPath)!, "node_modules", "@angular", "compiler", "package.json"))
        && CanRunNode();

    private static string FindRepoSidecar()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PlaywrightPomGenerator.Sidecar.Node", "sidecar.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return "sidecar.js";
    }

    private static bool CanRunNode()
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "node",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process!.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static AngularAnalyzer CreateRealAnalyzer(string nodeExecutable = "node")
    {
        var fileSystem = new FileSystemService();
        var transport = new NodeSidecarTransport(nodeExecutable, SidecarPath, NullLogger<NodeSidecarTransport>.Instance);
        var astAnalyzer = new AstProjectAnalyzer(transport, NullLogger<AstProjectAnalyzer>.Instance);
        var packageInspector = new PackageInspector(fileSystem, NullLogger<PackageInspector>.Instance);
        return new AngularAnalyzer(
            fileSystem,
            NullLogger<AngularAnalyzer>.Instance,
            astAnalyzer,
            packageInspector,
            Options.Create(new GeneratorOptions { AnalysisEngine = AnalysisEngine.Auto }));
    }

    private static string CreateFixtureDir() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PpgAstIT_" + Guid.NewGuid().ToString("N"))).FullName;

    private static void Write(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static void Cleanup(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public async Task AnalyzeApplication_StandaloneComponentWithControlFlow_ShouldProduceEnrichedModel()
    {
        if (!SidecarWithCompilerAvailable)
        {
            return; // Node or sidecar dependencies unavailable in this environment.
        }

        var root = CreateFixtureDir();
        try
        {
            Write(root, "package.json", """{ "dependencies": { "@angular/core": "^19.0.0" } }""");
            Write(root, "src/app/dashboard.component.ts", """
                import { Component, input, output } from '@angular/core';
                @Component({
                  selector: 'app-dashboard',
                  template: `
                    <h1 data-testid="title">Dashboard</h1>
                    @if (isAdmin) {
                      <button data-testid="admin-panel" (click)="openAdmin()">Admin</button>
                    }
                    @for (kpi of kpis; track kpi.id) {
                      <app-kpi-card [kpi]="kpi"></app-kpi-card>
                    }
                  `,
                })
                export class DashboardComponent {
                  refreshRate = input.required<number>();
                  refreshed = output<void>();
                }
                """);
            Write(root, "src/app/kpi-card.component.ts", """
                import { Component } from '@angular/core';
                @Component({ selector: 'app-kpi-card', template: '<div data-testid="kpi-value">{{ v }}</div>' })
                export class KpiCardComponent {}
                """);

            var project = await CreateRealAnalyzer().AnalyzeApplicationAsync(root);

            project.Analysis!.EngineUsed.Should().Be(AnalysisEngineUsed.Ast);
            project.Components.Should().HaveCount(2);

            var dashboard = project.Components.Single(c => c.Name == "DashboardComponent");
            dashboard.InputsDetailed.Should().ContainSingle(p =>
                p.Name == "refreshRate" && p.Kind == ComponentPortKind.Signal && p.Required);
            dashboard.OutputsDetailed.Should().ContainSingle(p => p.Name == "refreshed");

            var adminButton = dashboard.Selectors.Single(s => s.TestIdValue == "admin-panel");
            adminButton.IsConditional.Should().BeTrue();
            adminButton.ConditionText.Should().Be("isAdmin");

            var kpiChild = dashboard.ChildComponents.Should().ContainSingle().Subject;
            kpiChild.ComponentName.Should().Be("KpiCardComponent");
            kpiChild.IsRepeated.Should().BeTrue();
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task AnalyzeApplication_RouteTreeWithLazyAlias_ShouldLinkComponentsToFullPaths()
    {
        if (!SidecarWithCompilerAvailable)
        {
            return;
        }

        var root = CreateFixtureDir();
        try
        {
            Write(root, "package.json", """{ "dependencies": { "@angular/core": "^19.0.0" } }""");
            Write(root, "tsconfig.json", """
                { "compilerOptions": { "baseUrl": ".", "paths": { "@features/*": ["src/features/*"] } } }
                """);
            Write(root, "src/app/app.config.ts", """
                import { provideRouter } from '@angular/router';
                import { routes } from './app.routes';
                export const appConfig = { providers: [provideRouter(routes)] };
                """);
            Write(root, "src/app/app.routes.ts", """
                import { Routes } from '@angular/router';
                import { HomePageComponent } from './home-page.component';
                export const routes: Routes = [
                  { path: '', component: HomePageComponent, title: 'Home' },
                  { path: 'orders/:orderId', loadComponent: () => import('./order-page.component').then(m => m.OrderPageComponent) },
                  { path: 'reports', loadChildren: () => import('@features/reports/reports.routes').then(m => m.REPORT_ROUTES) },
                ];
                """);
            Write(root, "src/app/home-page.component.ts", """
                import { Component } from '@angular/core';
                @Component({ selector: 'app-home-page', template: '<h1>Home</h1>' })
                export class HomePageComponent {}
                """);
            Write(root, "src/app/order-page.component.ts", """
                import { Component } from '@angular/core';
                @Component({ selector: 'app-order-page', template: '<h1>Order</h1>' })
                export class OrderPageComponent {}
                """);
            Write(root, "src/features/reports/reports.routes.ts", """
                import { Routes } from '@angular/router';
                import { ReportListComponent } from './report-list.component';
                export const REPORT_ROUTES: Routes = [
                  { path: '', component: ReportListComponent },
                ];
                """);
            Write(root, "src/features/reports/report-list.component.ts", """
                import { Component } from '@angular/core';
                @Component({ selector: 'app-report-list', template: '<h1>Reports</h1>' })
                export class ReportListComponent {}
                """);

            var project = await CreateRealAnalyzer().AnalyzeApplicationAsync(root);

            var orderPage = project.Components.Single(c => c.Name == "OrderPageComponent");
            orderPage.RouteEvidence.Should().BeTrue();
            orderPage.RoutePath.Should().Be("orders/:orderId");
            orderPage.RouteParams.Should().BeEquivalentTo(["orderId"]);
            orderPage.IsRoutable.Should().BeTrue();

            var reportList = project.Components.Single(c => c.Name == "ReportListComponent");
            reportList.RouteEvidence.Should().BeTrue("lazy loadChildren via a tsconfig alias should resolve");
            reportList.RoutePath.Should().Be("reports");

            project.Routes.Should().NotBeEmpty();
            var ordersRoute = project.Routes.Single(r => r.Path == "orders/:orderId");
            ordersRoute.FullPath.Should().Be("/orders/:orderId");
            ordersRoute.IsLazyLoaded.Should().BeTrue();
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task AnalyzeApplication_MaterialTemplate_ShouldClassifyWidgetsAndColumns()
    {
        if (!SidecarWithCompilerAvailable)
        {
            return;
        }

        var root = CreateFixtureDir();
        try
        {
            Write(root, "package.json", """
                { "dependencies": { "@angular/core": "^17.0.0", "@angular/material": "^17.0.0" } }
                """);
            Write(root, "src/app/settings.component.ts", """
                import { Component } from '@angular/core';
                @Component({
                  selector: 'app-settings',
                  template: `
                    <form [formGroup]="settingsForm" (ngSubmit)="save()">
                      <mat-form-field>
                        <mat-label>Region</mat-label>
                        <mat-select formControlName="region"></mat-select>
                      </mat-form-field>
                      <mat-slide-toggle formControlName="darkMode" data-testid="dark-mode">Dark mode</mat-slide-toggle>
                      <button type="submit" data-testid="save-settings">Save</button>
                    </form>
                    <table mat-table [dataSource]="rows" data-testid="audit-table">
                      <ng-container matColumnDef="when">
                        <th mat-header-cell *matHeaderCellDef>When</th>
                        <td mat-cell *matCellDef="let row">{{ row.when }}</td>
                      </ng-container>
                    </table>
                  `,
                })
                export class SettingsComponent {}
                """);

            var project = await CreateRealAnalyzer().AnalyzeApplicationAsync(root);

            var settings = project.Components.Single(c => c.Name == "SettingsComponent");

            var region = settings.Selectors.Single(s => s.FormControlName == "region");
            region.ControlType.Should().Be(ControlType.Select);
            region.MaterialWidget.Should().Be(MaterialWidget.MatSelect);
            region.LabelText.Should().Be("Region");

            var darkMode = settings.Selectors.Single(s => s.TestIdValue == "dark-mode");
            darkMode.ControlType.Should().Be(ControlType.Toggle);

            var table = settings.Selectors.Single(s => s.TestIdValue == "audit-table");
            table.IsTable.Should().BeTrue();
            table.ColumnDefs.Should().ContainSingle(c => c.Name == "when" && c.HeaderText == "When");

            var form = settings.Forms.Should().ContainSingle().Subject;
            form.FormGroupName.Should().Be("settingsForm");
            form.SubmitHandlerName.Should().Be("save");
            form.Controls.Select(c => c.ControlName).Should().ContainInOrder("region", "darkMode");
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task AnalyzeApplication_WithUnusableNodeExecutable_ShouldFallBackToRegex()
    {
        // No skip guard: this scenario must hold in every environment.
        var root = CreateFixtureDir();
        try
        {
            Write(root, "package.json", """{ "dependencies": { "@angular/core": "^17.0.0" } }""");
            Write(root, "src/app/login.component.ts", """
                import { Component } from '@angular/core';
                @Component({
                  selector: 'app-login',
                  template: '<button data-testid="login-button">Login</button>'
                })
                export class LoginComponent {}
                """);

            var project = await CreateRealAnalyzer(nodeExecutable: "definitely-not-a-node-executable-xyz")
                .AnalyzeApplicationAsync(root);

            project.Analysis!.EngineUsed.Should().Be(AnalysisEngineUsed.Regex);
            project.Analysis.FallbackReason.Should().NotBeNullOrEmpty();
            project.Components.Should().ContainSingle(c => c.Name == "LoginComponent");
            project.Components[0].Selectors.Should().Contain(s => s.SelectorValue == "[data-testid='login-button']");
        }
        finally
        {
            Cleanup(root);
        }
    }
}
