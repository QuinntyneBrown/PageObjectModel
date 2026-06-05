using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// End-to-end checks that spawn the real Node TS-AST sidecar, run discovery through
/// <see cref="TypeScriptAnalyzer"/>, then generate the bridge with the real
/// <see cref="TemplateEngine"/>/<see cref="CodeGenerator"/>/<see cref="FileSystemService"/> and
/// inspect the TypeScript written to disk. Node is required; these are skipped if it is absent.
/// </summary>
public sealed class BridgeIntegrationTests
{
    private static readonly string SidecarPath = FindRepoSidecar();

    // The sidecar needs node, the script, and a resolvable typescript (bundled here via npm install).
    private static bool NodeAndSidecarAvailable =>
        File.Exists(SidecarPath)
        && File.Exists(Path.Combine(Path.GetDirectoryName(SidecarPath)!, "node_modules", "typescript", "package.json"))
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

    [Fact]
    public async Task Bridge_RealSidecarAndServices_DiscoversAndGeneratesValidTypeScript()
    {
        if (!NodeAndSidecarAvailable)
        {
            return; // Node or the sidecar isn't available in this environment.
        }

        var fixtureDir = Path.Combine(Path.GetTempPath(), "PpgBridgeIT_" + Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(Path.GetTempPath(), "PpgBridgeOut_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(fixtureDir, "services"));
            await File.WriteAllTextAsync(Path.Combine(fixtureDir, "services", "local-storage.token.ts"), """
                import { InjectionToken } from '@angular/core';
                import { Observable } from 'rxjs';

                export interface IReadable {
                  getItem(key: string): string | null;
                }

                export interface ILocalStorage extends IReadable {
                  setItem(key: string, value: string): void;
                  clear(): void;
                  readonly length: number;
                  changes$: Observable<string>;
                }

                export const LOCAL_STORAGE = new InjectionToken<ILocalStorage>('ILocalStorage');
                """);

            // --- Discovery via the real sidecar ---
            var transport = new NodeSidecarTransport("node", SidecarPath, NullLogger<NodeSidecarTransport>.Instance);
            var analyzer = new TypeScriptAnalyzer(transport, NullLogger<TypeScriptAnalyzer>.Instance);

            var interfaces = await analyzer.DiscoverInjectionTokenInterfacesAsync(fixtureDir);

            interfaces.Should().ContainSingle();
            var iface = interfaces[0];
            iface.TokenName.Should().Be("LOCAL_STORAGE");
            iface.InterfaceName.Should().Be("ILocalStorage");
            iface.Members.Should().Contain(m => m.Name == "getItem"); // inherited via extends IReadable
            iface.Members.Should().Contain(m => m.Name == "changes$" && m.IsObservable);
            iface.Members.Should().Contain(m => m.Name == "setItem" && m.ReturnsVoid);
            iface.Members.Should().Contain(m => m.Name == "length" && !m.IsMethod);

            // --- Generation via the real services ---
            var options = new GeneratorOptions();
            var optionsWrapper = Substitute.For<IOptions<GeneratorOptions>>();
            optionsWrapper.Value.Returns(options);
            var generator = new CodeGenerator(
                Substitute.For<IAngularAnalyzer>(),
                new TemplateEngine(optionsWrapper),
                new FileSystemService(),
                NullLogger<CodeGenerator>.Instance,
                optionsWrapper);

            var result = await generator.GenerateBridgeAsync(interfaces, outputDir);

            result.Success.Should().BeTrue();

            var registry = await File.ReadAllTextAsync(Path.Combine(outputDir, "bridge", "bridge-registry.ts"));
            registry.Should().Contain("__e2eBridge");
            registry.Should().Contain("export class E2EBridgeRegistry");
            registry.Should().Contain("export function installE2EBridge()");

            var mock = await File.ReadAllTextAsync(Path.Combine(outputDir, "bridge", "mocks", "local-storage.mock.ts"));
            mock.Should().Contain("export class LocalStorageMock implements ILocalStorage");
            mock.Should().Contain("static readonly interfaceName = 'ILocalStorage';");
            mock.Should().Contain("getItem(...args: Parameters<ILocalStorage['getItem']>): ReturnType<ILocalStorage['getItem']>");
            mock.Should().Contain("e2eBridge.invoke('ILocalStorage', 'setItem', args);");
            mock.Should().Contain("e2eBridge.stream('ILocalStorage', 'changes$')");
            mock.Should().Contain("from '../bridge-registry'");
            mock.Count(c => c == '{').Should().Be(mock.Count(c => c == '}'));

            var providers = await File.ReadAllTextAsync(Path.Combine(outputDir, "bridge", "bridge-providers.ts"));
            providers.Should().Contain("export function provideE2EBridge(): EnvironmentProviders");
            providers.Should().Contain("{ provide: LOCAL_STORAGE, useClass: LocalStorageMock }");
            providers.Should().Contain("import { LOCAL_STORAGE }");

            var client = await File.ReadAllTextAsync(Path.Combine(outputDir, "bridge", "playwright-bridge.ts"));
            client.Should().Contain("export class PlaywrightBridge");
            client.Should().Contain("readonly localStorage = new InterfaceBridge(this.page, 'ILocalStorage');");
            client.Should().Contain("async expectCalled(method: string): Promise<void>");
        }
        finally
        {
            TryDelete(fixtureDir);
            TryDelete(outputDir);
        }
    }

    private static bool CanRunNode()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "node",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return false;
            }
            process.WaitForExit(5000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
