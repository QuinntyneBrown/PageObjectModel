using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class BridgeTemplateTests
{
    private readonly TemplateEngine _engine;
    private readonly GeneratorOptions _options;

    public BridgeTemplateTests()
    {
        _options = new GeneratorOptions();
        var optionsWrapper = Substitute.For<IOptions<GeneratorOptions>>();
        optionsWrapper.Value.Returns(_options);
        _engine = new TemplateEngine(optionsWrapper);
    }

    [Fact]
    public void GenerateBridgeRegistry_ShouldEmitRegistryAndWindowApi()
    {
        // Act
        var result = _engine.GenerateBridgeRegistry();

        // Assert
        result.Should().Contain("import { Observable, ReplaySubject } from 'rxjs'");
        result.Should().Contain("export interface BridgeCallRecord");
        result.Should().Contain("export interface E2EBridgeApi");
        result.Should().Contain("__e2eBridge?: E2EBridgeApi;");
        result.Should().Contain("export class E2EBridgeRegistry");
        result.Should().Contain("invoke(interfaceName: string, method: string, args: unknown[]): unknown");
        result.Should().Contain("stream(interfaceName: string, member: string): Observable<unknown>");
        result.Should().Contain("export const e2eBridge = new E2EBridgeRegistry();");
        result.Should().Contain("export function installE2EBridge(): void");
        result.Count(c => c == '{').Should().Be(result.Count(c => c == '}'));
    }

    [Fact]
    public void GenerateInterfaceMock_ShouldImplementInterfaceWithIndexedAccessTypes()
    {
        // Arrange
        var iface = CreateEnrichedInterface();

        // Act
        var result = _engine.GenerateInterfaceMock(iface);

        // Assert
        result.Should().Contain("import { ILocalStorage } from '../../app/services/local-storage.token'");
        result.Should().Contain("import { e2eBridge } from '../bridge-registry'");
        result.Should().Contain("export class LocalStorageMock implements ILocalStorage");
        result.Should().Contain("static readonly interfaceName = 'ILocalStorage';");
        // Methods use indexed-access types so referenced DTO types need not be imported.
        result.Should().Contain("getItem(...args: Parameters<ILocalStorage['getItem']>): ReturnType<ILocalStorage['getItem']>");
        result.Should().Contain("return e2eBridge.invoke('ILocalStorage', 'getItem', args) as ReturnType<ILocalStorage['getItem']>;");
        // void method records but does not return.
        result.Should().Contain("e2eBridge.invoke('ILocalStorage', 'setItem', args);");
        result.Should().NotContain("return e2eBridge.invoke('ILocalStorage', 'setItem'");
        // observable property backed by a stream; value property by a stub.
        result.Should().Contain("readonly changes$: ILocalStorage['changes$'] = e2eBridge.stream('ILocalStorage', 'changes$')");
        result.Should().Contain("readonly length: ILocalStorage['length'] = e2eBridge.value('ILocalStorage', 'length')");
        result.Should().NotContain(": any");
        result.Count(c => c == '{').Should().Be(result.Count(c => c == '}'));
    }

    [Fact]
    public void GenerateInterfaceMock_WithObservableMethod_ShouldRecordAndReturnStream()
    {
        // Arrange
        var iface = CreateEnrichedInterface() with
        {
            Members = [new InterfaceMember { Name = "watch", IsMethod = true, ReturnType = "Observable<string>", IsObservable = true }]
        };

        // Act
        var result = _engine.GenerateInterfaceMock(iface);

        // Assert
        result.Should().Contain("e2eBridge.invoke('ILocalStorage', 'watch', args);");
        result.Should().Contain("return e2eBridge.stream('ILocalStorage', 'watch') as ReturnType<ILocalStorage['watch']>;");
    }

    [Fact]
    public void GenerateBridgeProviders_ShouldWireTokensToMocks()
    {
        // Arrange
        var iface = CreateEnrichedInterface();

        // Act
        var result = _engine.GenerateBridgeProviders([iface]);

        // Assert
        result.Should().Contain("import { EnvironmentProviders, makeEnvironmentProviders, ENVIRONMENT_INITIALIZER } from '@angular/core'");
        result.Should().Contain("import { installE2EBridge } from './bridge-registry'");
        result.Should().Contain("import { LOCAL_STORAGE } from '../app/services/local-storage.token'");
        result.Should().Contain("import { LocalStorageMock } from './mocks/local-storage.mock'");
        result.Should().Contain("export function provideE2EBridge(): EnvironmentProviders");
        result.Should().Contain("{ provide: ENVIRONMENT_INITIALIZER, multi: true, useValue: () => installE2EBridge() }");
        result.Should().Contain("{ provide: LOCAL_STORAGE, useClass: LocalStorageMock },");
    }

    [Fact]
    public void GeneratePlaywrightBridge_ShouldExposeTypedAccessors()
    {
        // Arrange
        var iface = CreateEnrichedInterface();

        // Act
        var result = _engine.GeneratePlaywrightBridge([iface]);

        // Assert
        result.Should().Contain("import { Page } from '@playwright/test'");
        result.Should().Contain("export class InterfaceBridge");
        result.Should().Contain("async expectCalled(method: string): Promise<void>");
        result.Should().Contain("emit(member: string, value: unknown): Promise<void>");
        result.Should().Contain("export class PlaywrightBridge");
        result.Should().Contain("readonly localStorage = new InterfaceBridge(this.page, 'ILocalStorage');");
        result.Should().NotContain("as any");
        result.Count(c => c == '{').Should().Be(result.Count(c => c == '}'));
    }

    [Fact]
    public void GenerateInterfaceMock_WithNull_ShouldThrowArgumentNullException()
    {
        var act = () => _engine.GenerateInterfaceMock(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateBridgeProviders_WithNull_ShouldThrowArgumentNullException()
    {
        var act = () => _engine.GenerateBridgeProviders(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static InjectionTokenInterface CreateEnrichedInterface() => new()
    {
        TokenName = "LOCAL_STORAGE",
        InterfaceName = "ILocalStorage",
        Description = "ILocalStorage",
        TokenFilePath = "/app/src/app/services/local-storage.token.ts",
        InterfaceFilePath = "/app/src/app/services/local-storage.token.ts",
        MockClassName = "LocalStorageMock",
        MockFileStem = "local-storage.mock",
        PlaywrightAccessor = "localStorage",
        TokenImportPath = "../app/services/local-storage.token",
        InterfaceImportPath = "../../app/services/local-storage.token",
        Members =
        [
            new InterfaceMember { Name = "setItem", IsMethod = true, ParameterNames = ["key", "value"], ParametersText = "key: string, value: string", ReturnType = "void", ReturnsVoid = true },
            new InterfaceMember { Name = "getItem", IsMethod = true, ParameterNames = ["key"], ParametersText = "key: string", ReturnType = "string | null" },
            new InterfaceMember { Name = "changes$", IsMethod = false, ReturnType = "Observable<string>", IsObservable = true },
            new InterfaceMember { Name = "length", IsMethod = false, ReturnType = "number" }
        ]
    };
}
