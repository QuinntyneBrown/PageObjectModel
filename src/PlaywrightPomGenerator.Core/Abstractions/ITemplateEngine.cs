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
}
