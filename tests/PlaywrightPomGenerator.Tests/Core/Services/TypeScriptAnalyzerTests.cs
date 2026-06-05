using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PlaywrightPomGenerator.Core.Abstractions;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class TypeScriptAnalyzerTests
{
    [Fact]
    public async Task DiscoverInjectionTokenInterfacesAsync_ShouldMapSidecarResult()
    {
        // Arrange - the JSON the sidecar emits for a token-backed interface
        const string json = """
            {
              "tokens": [
                {
                  "tokenName": "LOCAL_STORAGE",
                  "interfaceName": "ILocalStorage",
                  "description": "ILocalStorage",
                  "tokenFile": "C:/app/local-storage.token.ts",
                  "interfaceFile": "C:/app/local-storage.token.ts",
                  "members": [
                    { "name": "setItem", "isMethod": true, "parameterNames": ["key", "value"], "parametersText": "key: string, value: string", "returnType": "void", "isObservable": false, "returnsVoid": true },
                    { "name": "changes$", "isMethod": false, "parameterNames": [], "parametersText": "", "returnType": "Observable<string>", "isObservable": true, "returnsVoid": false },
                    { "name": "getItem", "isMethod": true, "parameterNames": ["key"], "parametersText": "key: string", "returnType": "string | null", "isObservable": false, "returnsVoid": false }
                  ]
                }
              ],
              "warnings": []
            }
            """;
        var analyzer = CreateAnalyzer(json);

        // Act
        var result = await analyzer.DiscoverInjectionTokenInterfacesAsync("/app");

        // Assert
        result.Should().ContainSingle();
        var iface = result[0];
        iface.TokenName.Should().Be("LOCAL_STORAGE");
        iface.InterfaceName.Should().Be("ILocalStorage");
        iface.Description.Should().Be("ILocalStorage");
        iface.InterfaceFilePath.Should().Be("C:/app/local-storage.token.ts");
        iface.Members.Should().HaveCount(3);
        iface.Members.Should().Contain(m => m.Name == "setItem" && m.IsMethod && m.ReturnsVoid);
        iface.Members.Should().Contain(m => m.Name == "changes$" && !m.IsMethod && m.IsObservable);
        iface.Members.Should().Contain(m => m.Name == "getItem" && m.ParameterNames.Contains("key") && m.ReturnType == "string | null");
    }

    [Fact]
    public async Task DiscoverInjectionTokenInterfacesAsync_WithNoTokens_ShouldReturnEmpty()
    {
        // Arrange
        var analyzer = CreateAnalyzer("""{ "tokens": [], "warnings": ["nothing found"] }""");

        // Act
        var result = await analyzer.DiscoverInjectionTokenInterfacesAsync("/app");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverInjectionTokenInterfacesAsync_ShouldInvokeDiscoverMethod()
    {
        // Arrange
        var transport = Substitute.For<ISidecarTransport>();
        using var doc = JsonDocument.Parse("""{ "tokens": [], "warnings": [] }""");
        transport.InvokeAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(doc.RootElement.Clone());
        var analyzer = new TypeScriptAnalyzer(transport, NullLogger<TypeScriptAnalyzer>.Instance);

        // Act
        await analyzer.DiscoverInjectionTokenInterfacesAsync("/app");

        // Assert
        await transport.Received(1).InvokeAsync("discoverInjectionTokens", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverInjectionTokenInterfacesAsync_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var analyzer = CreateAnalyzer("""{ "tokens": [], "warnings": [] }""");

        // Act
        var act = () => analyzer.DiscoverInjectionTokenInterfacesAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullTransport_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TypeScriptAnalyzer(null!, NullLogger<TypeScriptAnalyzer>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static TypeScriptAnalyzer CreateAnalyzer(string resultJson)
    {
        var transport = Substitute.For<ISidecarTransport>();
        using var doc = JsonDocument.Parse(resultJson);
        transport.InvokeAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(doc.RootElement.Clone());
        return new TypeScriptAnalyzer(transport, NullLogger<TypeScriptAnalyzer>.Instance);
    }
}
