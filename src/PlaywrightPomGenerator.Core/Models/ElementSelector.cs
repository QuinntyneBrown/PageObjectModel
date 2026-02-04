namespace PlaywrightPomGenerator.Core.Models;

/// <summary>
/// Represents a selectable element found in an Angular component template.
/// </summary>
public sealed record ElementSelector
{
    /// <summary>
    /// Gets the element type (e.g., "button", "input", "a", "div").
    /// </summary>
    public required string ElementType { get; init; }

    /// <summary>
    /// Gets the best selector strategy for this element.
    /// </summary>
    public required SelectorStrategy Strategy { get; init; }

    /// <summary>
    /// Gets the selector value (e.g., "[data-testid='submit']", "#login-button").
    /// </summary>
    public required string SelectorValue { get; init; }

    /// <summary>
    /// Gets the suggested property name for this selector in the page object.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the element's text content if available.
    /// </summary>
    public string? TextContent { get; init; }

    /// <summary>
    /// Gets additional attributes found on the element.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Defines the selector strategy used to locate an element.
/// </summary>
public enum SelectorStrategy
{
    /// <summary>
    /// Uses data-testid attribute (preferred).
    /// </summary>
    TestId,

    /// <summary>
    /// Uses element ID attribute.
    /// </summary>
    Id,

    /// <summary>
    /// Uses CSS class selector.
    /// </summary>
    Class,

    /// <summary>
    /// Uses element role (ARIA).
    /// </summary>
    Role,

    /// <summary>
    /// Uses text content.
    /// </summary>
    Text,

    /// <summary>
    /// Uses placeholder attribute.
    /// </summary>
    Placeholder,

    /// <summary>
    /// Uses label text.
    /// </summary>
    Label,

    /// <summary>
    /// Uses CSS selector as fallback.
    /// </summary>
    Css
}
