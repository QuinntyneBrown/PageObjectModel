using System.Text;
using System.Text.RegularExpressions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// How robust a generated locator is expected to be at runtime.
/// </summary>
internal enum LocatorGrade
{
    /// <summary>User-facing or explicitly keyed: testid, label, role+name, id, formControlName.</summary>
    Stable,

    /// <summary>Likely to break on markup or copy changes; the user should add a stable hook.</summary>
    Weak
}

/// <summary>
/// The grade of one locator with the reason and the suggested fix when it is weak.
/// </summary>
internal sealed record LocatorAssessment(LocatorGrade Grade, string? Reason, string? Fix)
{
    public static readonly LocatorAssessment Stable = new(LocatorGrade.Stable, null, null);
}

/// <summary>
/// Deterministic locator-quality grading. Reads only <see cref="ElementSelector.Strategy"/>,
/// <see cref="ElementSelector.SelectorValue"/> and the naming facts, so it grades both the
/// AST engine's precise strategies and the regex engine's looser ones correctly.
/// </summary>
internal static partial class LocatorQuality
{
    private const int MaxExamples = 3;

    public const string InterpolatedTextReason = "interpolated text";
    public const string GenericNameReason = "generic name";
    public const string TypedInputCssReason = "typed-input CSS";
    public const string CssFallbackReason = "CSS fallback";
    public const string DuplicateSelectorReason = "duplicate selector";

    /// <summary>
    /// Grades one locator. Pass its sibling selectors to also detect duplicate selectors
    /// that nothing disambiguates.
    /// </summary>
    public static LocatorAssessment Assess(ElementSelector selector, IReadOnlyList<ElementSelector>? siblings = null)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (selector.Strategy is SelectorStrategy.Text or SelectorStrategy.Role
            && selector.TextIsInterpolated
            && selector.SelectorValue.Contains(":has-text(", StringComparison.Ordinal))
        {
            return new LocatorAssessment(LocatorGrade.Weak, InterpolatedTextReason,
                "use aria-label or data-testid; the visible text is dynamic");
        }

        if (GenericNumberedNameRegex().IsMatch(selector.PropertyName))
        {
            return new LocatorAssessment(LocatorGrade.Weak, GenericNameReason,
                "name the element with data-testid or aria-label");
        }

        if (selector.Strategy is SelectorStrategy.Css or SelectorStrategy.Class
            && !selector.IsTable
            && !selector.SelectorValue.StartsWith('[')
            && !selector.SelectorValue.StartsWith('#'))
        {
            if (TypedInputCssRegex().IsMatch(selector.SelectorValue))
            {
                return new LocatorAssessment(LocatorGrade.Weak, TypedInputCssReason,
                    "add a label, placeholder or data-testid");
            }
            return new LocatorAssessment(LocatorGrade.Weak, CssFallbackReason,
                $"add data-testid=\"{ToKebabCase(selector.PropertyName)}\" to <{selector.ElementType}>");
        }

        if (siblings is not null && selector.ParentLandmark is null)
        {
            var duplicates = siblings.Count(s =>
                s.Strategy == selector.Strategy
                && string.Equals(s.SelectorValue, selector.SelectorValue, StringComparison.Ordinal)
                && s.ParentLandmark is null);
            if (duplicates > 1)
            {
                return new LocatorAssessment(LocatorGrade.Weak, DuplicateSelectorReason,
                    "wrap in a landmark (<section aria-label>) or add distinct data-testids");
            }
        }

        return LocatorAssessment.Stable;
    }

    /// <summary>
    /// Appends one summary warning per component that has weak locators and, in debug
    /// mode, one detail line per weak locator.
    /// </summary>
    public static void AppendWarnings(IEnumerable<AngularComponentInfo> components, List<string> warnings, bool debug)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(warnings);

        foreach (var component in components)
        {
            var selectors = component.Selectors;
            if (selectors.Count == 0)
            {
                continue;
            }

            var weak = selectors
                .Select(s => (Selector: s, Assessment: Assess(s, selectors)))
                .Where(x => x.Assessment.Grade == LocatorGrade.Weak)
                .ToList();
            if (weak.Count == 0)
            {
                continue;
            }

            warnings.Add(Summarize(component.Name, weak, selectors.Count));

            if (debug)
            {
                foreach (var (selector, assessment) in weak)
                {
                    var line = selector.TemplateLine is { } templateLine ? $" line {templateLine}" : "";
                    warnings.Add(
                        $"  {component.Name}.{ToCamelCase(selector.PropertyName)} ({selector.Strategy} {selector.SelectorValue}{line}): " +
                        $"{assessment.Reason} — {assessment.Fix}");
                }
            }
        }
    }

    private static string Summarize(
        string componentName,
        List<(ElementSelector Selector, LocatorAssessment Assessment)> weak,
        int total)
    {
        var sb = new StringBuilder();
        sb.Append(componentName).Append(": ")
          .Append(weak.Count).Append(" of ").Append(total)
          .Append(weak.Count == 1 && total == 1 ? " locator is weak — " : " locators are weak — ");

        var shown = 0;
        var groups = weak.GroupBy(w => w.Assessment.Reason!).ToList();
        var parts = new List<string>();
        foreach (var group in groups)
        {
            if (shown >= MaxExamples)
            {
                break;
            }
            var names = group.Select(w => ToCamelCase(w.Selector.PropertyName)).Take(MaxExamples - shown).ToList();
            shown += names.Count;
            parts.Add($"{group.Key}: {string.Join(", ", names)}");
        }
        sb.Append(string.Join("; ", parts));
        if (weak.Count > shown)
        {
            sb.Append(" (+").Append(weak.Count - shown).Append(" more)");
        }
        sb.Append(". Add data-testid attributes to stabilize.");
        return sb.ToString();
    }

    private static string ToCamelCase(string input) =>
        string.IsNullOrEmpty(input) ? input : char.ToLowerInvariant(input[0]) + input[1..];

    private static string ToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        var kebab = KebabBoundaryRegex().Replace(input, "$1-$2").ToLowerInvariant();
        return TrailingDigitsRegex().Replace(kebab, "");
    }

    // Numbered variants of the generic names both engines fall back to:
    // Container2, TextSpan3, Heading12, Paragraph1, FooElement2, TextInput1, Checkbox2...
    [GeneratedRegex(@"^(?:Container|TextSpan|Paragraph|ListItem|TableCell|TableHeader|Label|Text|Checkbox|Heading[1-6]|[A-Za-z]+Element|[A-Za-z]+Input)\d+$")]
    private static partial Regex GenericNumberedNameRegex();

    [GeneratedRegex(@"^input\[type=")]
    private static partial Regex TypedInputCssRegex();

    [GeneratedRegex(@"([a-z0-9])([A-Z])")]
    private static partial Regex KebabBoundaryRegex();

    [GeneratedRegex(@"-?\d+$")]
    private static partial Regex TrailingDigitsRegex();
}
