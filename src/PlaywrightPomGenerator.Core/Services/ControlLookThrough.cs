using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// The control a custom-element wrapper resolves to: its interaction kind, Material
/// widget, the CSS that reaches it from the host element, and the component that
/// supplied it.
/// </summary>
internal sealed record InnerControl(
    ControlType ControlType,
    MaterialWidget Widget,
    string InnerCss,
    string ComponentClassName);

/// <summary>
/// Resolves custom elements that are workspace components (e.g. <c>&lt;ds-dropdown&gt;</c>)
/// to the single value control inside their own template, so the host element gets a
/// typed interaction instead of <see cref="ControlType.None"/>. Pure function over the
/// sidecar's project analysis: no IO, no sidecar changes. Resolution is conservative —
/// exactly one value control (recursively, through nested wrappers) or nothing.
/// </summary>
internal sealed class ControlLookThrough
{
    private const int MaxDepth = 3;

    private static readonly HashSet<ControlType> ValueControls =
    [
        ControlType.TextInput,
        ControlType.Textarea,
        ControlType.Checkbox,
        ControlType.Radio,
        ControlType.Select,
        ControlType.Toggle,
        ControlType.Datepicker,
        ControlType.Autocomplete
    ];

    private readonly Dictionary<string, AstComponent> _components;

    private ControlLookThrough(Dictionary<string, AstComponent> components)
    {
        _components = components;
    }

    /// <summary>
    /// An instance that resolves nothing (regex engine, or no AST result).
    /// </summary>
    public static ControlLookThrough Empty { get; } = new(new Dictionary<string, AstComponent>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Indexes every component of every analyzed project so cross-project (library)
    /// wrappers resolve too.
    /// </summary>
    public static ControlLookThrough Create(AstProjectAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var index = new Dictionary<string, AstComponent>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in analysis.Projects.SelectMany(p => p.Components))
        {
            index[Key(component.FilePath, component.ClassName)] = component;
        }
        return new ControlLookThrough(index);
    }

    /// <summary>
    /// Returns, per lowercased template tag used by <paramref name="host"/>, the inner
    /// control that tag resolves to. Tags that do not resolve are absent.
    /// </summary>
    public IReadOnlyDictionary<string, InnerControl> Resolve(AstComponent host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var result = new Dictionary<string, InnerControl>(StringComparer.OrdinalIgnoreCase);
        if (_components.Count == 0)
        {
            return result;
        }

        foreach (var child in host.ChildComponents)
        {
            if (child.ComponentClassName is null || child.ComponentFilePath is null)
            {
                continue;
            }
            if (!_components.TryGetValue(Key(child.ComponentFilePath, child.ComponentClassName), out var component))
            {
                continue;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Key(host.FilePath, host.ClassName) };
            var inner = ResolveComponent(component, depth: 1, visited);
            if (inner is not null)
            {
                // Report the component the host actually embeds, not the innermost wrapper.
                result[child.Selector.ToLowerInvariant()] = inner with { ComponentClassName = component.ClassName };
            }
        }
        return result;
    }

    private InnerControl? ResolveComponent(AstComponent component, int depth, HashSet<string> visited)
    {
        if (depth > MaxDepth || !component.TemplateParsed || component.Template is null)
        {
            return null;
        }
        if (!visited.Add(Key(component.FilePath, component.ClassName)))
        {
            return null; // cycle
        }

        var candidates = new List<InnerControl>();

        var elements = component.Template.Elements
            .Where(e => !e.Structure.Repeated && !e.Structure.Projected)
            .ToList();
        var hasRadioGroup = elements.Any(e => e.Widget == "matRadioGroup");

        foreach (var element in elements)
        {
            if (hasRadioGroup && IsRadioButton(element))
            {
                continue; // the group is the control; its buttons are its options
            }
            var controlType = SelectorNaming.DeriveControlType(element.Widget, element.Tag, element.Form.InputType, opensDialog: false);
            if (!ValueControls.Contains(controlType))
            {
                continue;
            }
            var widget = SelectorNaming.ParseWidget(element.Widget);
            candidates.Add(new InnerControl(controlType, widget, InnerCssFor(element), component.ClassName));
        }

        foreach (var child in component.ChildComponents)
        {
            if (child.Repeated || child.ComponentClassName is null || child.ComponentFilePath is null)
            {
                continue;
            }
            if (_components.TryGetValue(Key(child.ComponentFilePath, child.ComponentClassName), out var nested))
            {
                var inner = ResolveComponent(nested, depth + 1, visited);
                if (inner is not null)
                {
                    candidates.Add(inner);
                }
            }
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool IsRadioButton(AstElement element) =>
        element.Widget == "matRadioButton"
        || (element.Tag.Equals("input", StringComparison.OrdinalIgnoreCase)
            && string.Equals(element.Form.InputType, "radio", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The descendant CSS that reaches the control from the host: testid when present,
    /// the Material tag, a typed input, or the bare tag. Property bindings such as
    /// <c>[matDatepicker]</c> are not DOM attributes, so inputs fall back to their type.
    /// </summary>
    internal static string InnerCssFor(AstElement element)
    {
        if (element.TestId is not null)
        {
            return $"[data-testid='{element.TestId}']";
        }
        var tag = element.Tag.ToLowerInvariant();
        if (tag == "input")
        {
            return element.Form.InputType is { Length: > 0 } type
                ? $"input[type='{type.ToLowerInvariant()}']"
                : "input";
        }
        return tag;
    }

    private static string Key(string filePath, string className) =>
        filePath.Replace('\\', '/') + "::" + className;
}
