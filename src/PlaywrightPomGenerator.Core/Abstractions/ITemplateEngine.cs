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
}
