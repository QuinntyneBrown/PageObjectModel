using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Abstractions;

/// <summary>
/// Defines the contract for rendering code templates.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Generates a page object class for a component.
    /// </summary>
    /// <param name="component">The component information.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GeneratePageObject(AngularComponentInfo component);

    /// <summary>
    /// Generates a selectors file for a component.
    /// </summary>
    /// <param name="component">The component information.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateSelectors(AngularComponentInfo component);

    /// <summary>
    /// Generates a test fixture file.
    /// </summary>
    /// <param name="project">The project information.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateFixture(AngularProjectInfo project);

    /// <summary>
    /// Generates the Playwright configuration file.
    /// </summary>
    /// <param name="project">The project information.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateConfig(AngularProjectInfo project);

    /// <summary>
    /// Generates helper utilities.
    /// </summary>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateHelpers();

    /// <summary>
    /// Generates a test specification file for a component.
    /// </summary>
    /// <param name="component">The component information.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateTestSpec(AngularComponentInfo component);

    /// <summary>
    /// Generates the SignalR mock fixture using RxJS.
    /// </summary>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateSignalRMock();

    /// <summary>
    /// Generates the file header with placeholders replaced.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The generated header.</returns>
    string GenerateFileHeader(string fileName);

    /// <summary>
    /// Generates the timeout configuration file with standard timeouts.
    /// </summary>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateTimeoutConfig();

    /// <summary>
    /// Generates the URLs configuration file with URL constants for routable pages.
    /// </summary>
    /// <param name="project">The project information containing routable components.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateUrlsConfig(AngularProjectInfo project);

    /// <summary>
    /// Generates the base page class that all page objects extend from.
    /// </summary>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateBasePage();

    /// <summary>
    /// Generates a component object class for a component. Unlike a page object, a component object
    /// is scoped to a root <c>Locator</c> (the component host element) rather than to a <c>Page</c>,
    /// so it works wherever — and however many times — the component renders.
    /// </summary>
    /// <param name="component">The component information.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateComponentObject(AngularComponentInfo component);

    /// <summary>
    /// Generates the abstract base component class that all component objects extend from.
    /// It is the root-scoped analog of the base page and declares no <c>navigate()</c>.
    /// </summary>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateBaseComponent();

    /// <summary>
    /// Generates a test specification file for a component object. The spec composes the component
    /// object from its host page rather than navigating to it directly.
    /// </summary>
    /// <param name="component">The component information.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateComponentObjectTestSpec(AngularComponentInfo component);

    /// <summary>
    /// Generates the interface-mock registry runtime: the call recorder, stub store, observable
    /// subjects, and the <c>window.__interfaceMocks</c> control API used by Playwright.
    /// </summary>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateInterfaceMockRegistry();

    /// <summary>
    /// Generates a recording mock class for an injection-token-backed service interface.
    /// </summary>
    /// <param name="interfaceInfo">The injection-token interface (enriched with import paths and names).</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateInterfaceMock(InjectionTokenInterface interfaceInfo);

    /// <summary>
    /// Generates the Angular providers (<c>provideInterfaceMocks()</c>) that install the interface
    /// mocks and wire each token to its mock.
    /// </summary>
    /// <param name="interfaces">The injection-token interfaces to wire.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GenerateInterfaceMockProviders(IReadOnlyList<InjectionTokenInterface> interfaces);

    /// <summary>
    /// Generates the Playwright-side typed client (<c>InterfaceMocks</c>) that talks to the window
    /// control API via <c>page.evaluate</c>.
    /// </summary>
    /// <param name="interfaces">The injection-token interfaces to expose.</param>
    /// <returns>The generated TypeScript code.</returns>
    string GeneratePlaywrightInterfaceMocks(IReadOnlyList<InjectionTokenInterface> interfaces);
}
