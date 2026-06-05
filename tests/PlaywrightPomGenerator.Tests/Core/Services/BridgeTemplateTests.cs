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
    public void GenerateInterfaceMockRegistry_ShouldEmitRegistryAndWindowApi()
    {
        // Act
        var result = _engine.GenerateInterfaceMockRegistry();

        // Assert
        result.Should().Contain("import { Observable, ReplaySubject } from 'rxjs'");
        result.Should().Contain("export interface InterfaceCallRecord");
        result.Should().Contain("export interface InterfaceMockApi");
        result.Should().Contain("__interfaceMocks?: InterfaceMockApi;");
        result.Should().Contain("export class InterfaceMockRegistry");
        result.Should().Contain("invoke(interfaceName: string, method: string, args: unknown[]): unknown");
        result.Should().Contain("stream(interfaceName: string, member: string): Observable<unknown>");
        result.Should().Contain("export const interfaceMocks = new InterfaceMockRegistry();");
        result.Should().Contain("export function exposeInterfaceMocks(): void");
        // No stale bridge naming should remain.
        result.Should().NotContain("e2eBridge");
        result.Should().NotContain("E2EBridge");
        result.Should().NotContain("__e2eBridge");
        result.Count(c => c == '{').Should().Be(result.Count(c => c == '}'));
    }

    [Fact]
    public void GenerateInterfaceMockRegistry_Reset_ShouldClearSubjectsAsWellAsCallsAndStubs()
    {
        // Act
        var result = _engine.GenerateInterfaceMockRegistry();

        // Assert - the full reset must complete and clear the observable subjects so stale
        // emissions cannot leak between tests.
        result.Should().Contain("this.subjects.forEach((subject) => subject.complete());");
        result.Should().Contain("this.subjects.clear();");
        // Per-interface reset clears that interface's subjects too.
        result.Should().Contain("for (const [key, subject] of [...this.subjects]) {");
        result.Should().Contain("this.subjects.delete(key);");
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
        result.Should().Contain("import { interfaceMocks } from '../interface-mock-registry'");
        result.Should().Contain("export class LocalStorageMock implements ILocalStorage");
        result.Should().Contain("static readonly interfaceName = 'ILocalStorage';");
        // Methods use indexed-access types so referenced DTO types need not be imported.
        result.Should().Contain("getItem(...args: Parameters<ILocalStorage['getItem']>): ReturnType<ILocalStorage['getItem']>");
        result.Should().Contain("return interfaceMocks.invoke('ILocalStorage', 'getItem', args) as ReturnType<ILocalStorage['getItem']>;");
        // void method records but does not return.
        result.Should().Contain("interfaceMocks.invoke('ILocalStorage', 'setItem', args);");
        result.Should().NotContain("return interfaceMocks.invoke('ILocalStorage', 'setItem'");
        // observable property backed by a stream; value property by a stub.
        result.Should().Contain("readonly changes$: ILocalStorage['changes$'] = interfaceMocks.stream('ILocalStorage', 'changes$')");
        result.Should().Contain("readonly length: ILocalStorage['length'] = interfaceMocks.value('ILocalStorage', 'length')");
        result.Should().NotContain(": any");
        result.Should().NotContain("e2eBridge");
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
        result.Should().Contain("interfaceMocks.invoke('ILocalStorage', 'watch', args);");
        result.Should().Contain("return interfaceMocks.stream('ILocalStorage', 'watch') as ReturnType<ILocalStorage['watch']>;");
    }

    [Fact]
    public void GenerateInterfaceMockProviders_ShouldWireTokensToMocks()
    {
        // Arrange
        var iface = CreateEnrichedInterface();

        // Act
        var result = _engine.GenerateInterfaceMockProviders([iface]);

        // Assert
        result.Should().Contain("import { EnvironmentProviders, makeEnvironmentProviders, ENVIRONMENT_INITIALIZER } from '@angular/core'");
        result.Should().Contain("import { exposeInterfaceMocks } from './interface-mock-registry'");
        result.Should().Contain("import { LOCAL_STORAGE } from '../app/services/local-storage.token'");
        result.Should().Contain("import { LocalStorageMock } from './mocks/local-storage.mock'");
        result.Should().Contain("export function provideInterfaceMocks(): EnvironmentProviders");
        result.Should().Contain("{ provide: ENVIRONMENT_INITIALIZER, multi: true, useValue: () => exposeInterfaceMocks() }");
        result.Should().Contain("{ provide: LOCAL_STORAGE, useClass: LocalStorageMock },");
        result.Should().NotContain("provideE2EBridge");
        result.Should().NotContain("installE2EBridge");
    }

    [Fact]
    public void GeneratePlaywrightInterfaceMocks_ShouldExposeTypedAccessors()
    {
        // Arrange
        var iface = CreateEnrichedInterface();

        // Act
        var result = _engine.GeneratePlaywrightInterfaceMocks([iface]);

        // Assert
        result.Should().Contain("import { Page } from '@playwright/test'");
        result.Should().Contain("export class InterfaceMockHandle");
        result.Should().Contain("async expectCalled(method: string): Promise<void>");
        result.Should().Contain("emit(member: string, value: unknown): Promise<void>");
        result.Should().Contain("export class InterfaceMocks {");
        result.Should().Contain("readonly localStorage = new InterfaceMockHandle(this.page, 'ILocalStorage');");
        result.Should().Contain("__interfaceMocks");
        result.Should().NotContain("as any");
        result.Should().NotContain("PlaywrightBridge");
        result.Should().NotContain("InterfaceBridge ");
        result.Count(c => c == '{').Should().Be(result.Count(c => c == '}'));
    }

    [Fact]
    public void GenerateInterfaceMock_WithNull_ShouldThrowArgumentNullException()
    {
        var act = () => _engine.GenerateInterfaceMock(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateInterfaceMockProviders_WithNull_ShouldThrowArgumentNullException()
    {
        var act = () => _engine.GenerateInterfaceMockProviders(null!);
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
