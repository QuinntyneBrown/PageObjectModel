namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents an Angular service interface that is registered via an <c>InjectionToken</c>
/// (e.g. <c>export const LOCAL_STORAGE = new InjectionToken&lt;ILocalStorage&gt;('ILocalStorage')</c>).
/// These are the seams the Playwright bridge mocks, records, and drives.
/// </summary>
public sealed record InjectionTokenInterface
{
    /// <summary>
    /// Gets the injection token constant name (e.g. "LOCAL_STORAGE").
    /// </summary>
    public required string TokenName { get; init; }

    /// <summary>
    /// Gets the interface name the token is typed with (e.g. "ILocalStorage"). This is the key
    /// used to address the mock from tests via the window bridge.
    /// </summary>
    public required string InterfaceName { get; init; }

    /// <summary>
    /// Gets the optional description string passed to the InjectionToken constructor.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the absolute path to the file declaring the injection token.
    /// </summary>
    public required string TokenFilePath { get; init; }

    /// <summary>
    /// Gets the absolute path to the file declaring the interface, if it was resolved.
    /// </summary>
    public string? InterfaceFilePath { get; init; }

    /// <summary>
    /// Gets the parsed members (methods and properties) of the interface.
    /// </summary>
    public IReadOnlyList<InterfaceMember> Members { get; init; } = [];

    // --- Values computed by the code generator before templating ---

    /// <summary>
    /// Gets the generated mock class name (e.g. "LocalStorageMock").
    /// </summary>
    public string? MockClassName { get; init; }

    /// <summary>
    /// Gets the generated mock file stem without extension (e.g. "local-storage.mock").
    /// </summary>
    public string? MockFileStem { get; init; }

    /// <summary>
    /// Gets the camelCase accessor name used on the Playwright bridge (e.g. "localStorage").
    /// </summary>
    public string? PlaywrightAccessor { get; init; }

    /// <summary>
    /// Gets the best-effort relative import path (no extension) from the bridge directory to the
    /// token's source file.
    /// </summary>
    public string? TokenImportPath { get; init; }

    /// <summary>
    /// Gets the best-effort relative import path (no extension) from the mocks directory to the
    /// interface's source file.
    /// </summary>
    public string? InterfaceImportPath { get; init; }
}

/// <summary>
/// Represents a member (method or property) of a service interface.
/// </summary>
public sealed record InterfaceMember
{
    /// <summary>
    /// Gets the member name (e.g. "getItem").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets whether the member is a method (true) or a property (false).
    /// </summary>
    public required bool IsMethod { get; init; }

    /// <summary>
    /// Gets the raw parameter list text for a method (e.g. "key: string, value: string").
    /// </summary>
    public string ParametersText { get; init; } = "";

    /// <summary>
    /// Gets the parameter names for a method (e.g. ["key", "value"]).
    /// </summary>
    public IReadOnlyList<string> ParameterNames { get; init; } = [];

    /// <summary>
    /// Gets the return type for a method, or the property type for a property.
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Gets whether the return/property type is an RxJS <c>Observable&lt;T&gt;</c>.
    /// </summary>
    public bool IsObservable { get; init; }

    /// <summary>
    /// Gets whether the method returns <c>void</c>.
    /// </summary>
    public bool ReturnsVoid { get; init; }
}
