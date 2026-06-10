using System.Text.RegularExpressions;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Maps raw AST template element facts to <see cref="ElementSelector"/> models:
/// locator strategy priority, property naming, collision resolution, and
/// control-type derivation. The sidecar returns facts only — all naming policy
/// lives here so it can evolve without touching JavaScript.
/// </summary>
internal static partial class SelectorNaming
{
    /// <summary>
    /// Strategy priority: testid, role+accessible-name, label, placeholder, id,
    /// formControlName, text, css fallback.
    /// </summary>
    public static List<ElementSelector> MapSelectors(
        IReadOnlyList<AstElement> elements,
        IReadOnlyDictionary<string, string> dialogOpensByHandler)
    {
        var candidates = new List<Candidate>();
        foreach (var element in elements)
        {
            var candidate = BuildCandidate(element, dialogOpensByHandler);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        ResolveNameCollisions(candidates);
        MarkAmbiguousLocators(candidates);

        return candidates
            .Select(c => c.Draft with
            {
                PropertyName = c.Name,
                ParentLandmark = c.AttachLandmark ? c.Landmark : null
            })
            .ToList();
    }

    /// <summary>
    /// Derives the interaction kind from widget classification, tag, input type,
    /// and dialog linkage.
    /// </summary>
    public static ControlType DeriveControlType(string? widget, string? tag, string? inputType, bool opensDialog)
    {
        if (opensDialog)
        {
            return ControlType.DialogTrigger;
        }
        switch (widget)
        {
            case "matSelect": return ControlType.Select;
            case "matCheckbox": return ControlType.Checkbox;
            case "matRadioGroup":
            case "matRadioButton": return ControlType.Radio;
            case "matSlideToggle": return ControlType.Toggle;
            case "matDatepicker": return ControlType.Datepicker;
            case "matAutocomplete": return ControlType.Autocomplete;
            case "matMenuTrigger": return ControlType.MenuTrigger;
            case "matTabGroup": return ControlType.Tabs;
            case "matPaginator": return ControlType.Paginator;
        }
        var lowerTag = tag?.ToLowerInvariant();
        var lowerType = inputType?.ToLowerInvariant();
        return lowerTag switch
        {
            "select" => ControlType.Select,
            "textarea" => ControlType.Textarea,
            "input" => lowerType switch
            {
                "checkbox" => ControlType.Checkbox,
                "radio" => ControlType.Radio,
                "date" or "datetime-local" => ControlType.Datepicker,
                "button" or "submit" or "reset" or "image" or "file" or "range" or "color" or "hidden" => ControlType.None,
                _ => ControlType.TextInput
            },
            _ => ControlType.None
        };
    }

    /// <summary>
    /// Maps the sidecar widget string to the <see cref="MaterialWidget"/> enum.
    /// </summary>
    public static MaterialWidget ParseWidget(string? widget) => widget switch
    {
        "matSelect" => MaterialWidget.MatSelect,
        "matCheckbox" => MaterialWidget.MatCheckbox,
        "matRadioGroup" => MaterialWidget.MatRadioGroup,
        "matRadioButton" => MaterialWidget.MatRadioButton,
        "matSlideToggle" => MaterialWidget.MatSlideToggle,
        "matDatepicker" => MaterialWidget.MatDatepicker,
        "matAutocomplete" => MaterialWidget.MatAutocomplete,
        "matMenu" => MaterialWidget.MatMenu,
        "matMenuTrigger" => MaterialWidget.MatMenuTrigger,
        "matTabGroup" => MaterialWidget.MatTabGroup,
        "matPaginator" => MaterialWidget.MatPaginator,
        "matSort" => MaterialWidget.MatSort,
        "matTable" => MaterialWidget.MatTable,
        "matButton" => MaterialWidget.MatButton,
        "matFormField" => MaterialWidget.MatFormField,
        "matChipGrid" => MaterialWidget.MatChipGrid,
        "matDialogClose" => MaterialWidget.MatDialogClose,
        "cdkDrag" => MaterialWidget.CdkDrag,
        _ => MaterialWidget.None
    };

    /// <summary>
    /// PascalCases arbitrary text (selector values, labels, test ids). Unlike the
    /// legacy regex-path helper, interior capitals of camelCase words survive:
    /// "rememberMe" → "RememberMe", "save-button" → "SaveButton", "DARK MODE" → "DarkMode".
    /// </summary>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        var words = NonAlphanumericRegex().Split(input)
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w =>
            {
                var rest = w[1..];
                var isMixedCase = rest.Any(char.IsUpper) && rest.Any(char.IsLower);
                return char.ToUpperInvariant(w[0]) + (isMixedCase ? rest : rest.ToLowerInvariant());
            });
        return string.Concat(words);
    }

    // --- candidate construction ----------------------------------------------

    private sealed class Candidate
    {
        public required ElementSelector Draft { get; set; }
        public required string Name { get; set; }
        public LandmarkRef? Landmark { get; init; }
        public string? HeadingText { get; init; }
        public bool AttachLandmark { get; set; }
    }

    // Presentation-only Material tags that never need their own locator.
    private static readonly HashSet<string> SkippedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "mat-label", "mat-hint", "mat-icon", "mat-divider", "option", "mat-option"
    };

    private static readonly HashSet<string> TextTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "h1", "h2", "h3", "h4", "h5", "h6", "p", "span", "label", "strong", "em", "small",
        "mark", "sub", "sup", "blockquote", "cite", "q", "code", "pre", "abbr", "address",
        "time", "figcaption", "li", "td", "th"
    };

    private static readonly HashSet<string> AlwaysKeptTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "input", "select", "textarea"
    };

    private static Candidate? BuildCandidate(AstElement element, IReadOnlyDictionary<string, string> dialogOpensByHandler)
    {
        var tag = element.Tag.ToLowerInvariant();
        if (SkippedTags.Contains(tag))
        {
            return null;
        }

        var clickHandler = element.Handlers.TryGetValue("click", out var handler) ? handler : null;
        var simpleClickHandler = SimpleHandlerName(clickHandler);
        var hasClick = element.Events.Contains("click");
        string? opensDialog = simpleClickHandler is not null && dialogOpensByHandler.TryGetValue(simpleClickHandler, out var dialogComponent)
            ? dialogComponent
            : null;

        var widget = ParseWidget(element.Widget);
        var inputType = element.Form.InputType;
        var controlType = DeriveControlType(element.Widget, tag, inputType, opensDialog is not null);
        var labelText = element.Labels.LabelFor
            ?? element.Labels.WrappingLabel
            ?? element.Labels.MatLabel
            ?? element.Aria.LabelledByText;
        var staticText = element.Text.Value;
        var formControlName = element.Form.FormControlName;

        // Low-information elements: keep inputs/selects/textareas (always actionable),
        // widgets, tables, and anything carrying a name source or behavior.
        var hasNameSource = element.TestId is not null || element.Id is not null
            || labelText is not null || element.Aria.Label is not null
            || element.Labels.Placeholder is not null || formControlName is not null
            || staticText is not null || hasClick || element.IsRouterLink
            || element.Text.Interpolated || element.HasNgContent;
        var isWidgetOrTable = widget != MaterialWidget.None || element.Table?.IsTable == true;
        if (!hasNameSource && !isWidgetOrTable && !AlwaysKeptTags.Contains(tag))
        {
            return null;
        }

        var ariaRole = element.Role ?? ImplicitRole(tag, inputType, widget, element);
        var accessibleName = element.Aria.Label ?? labelText ?? staticText;
        var (strategy, selectorValue) = ChooseStrategy(element, tag, ariaRole, accessibleName, labelText, formControlName, staticText);

        var baseName = element.TestId
            ?? labelText
            ?? staticText
            ?? element.Aria.Label
            ?? formControlName
            ?? element.Labels.Placeholder
            ?? element.Id
            ?? HandlerBaseName(clickHandler)
            ?? WidgetBaseName(widget);

        string name;
        if (baseName is null)
        {
            // Tag-derived fallback names are already descriptive — no suffix.
            name = DefaultTagName(tag);
        }
        else
        {
            var suffix = SuffixFor(tag, controlType, widget, element);
            name = ApplySuffix(ToPascalCase(baseName), suffix);
        }

        var isConditional = element.Structure.Conditional || element.Structure.Projected;
        var conditionText = element.Structure.Condition
            ?? (element.Structure.Projected ? "ng-template" : null);

        var draft = new ElementSelector
        {
            ElementType = tag,
            Strategy = strategy,
            SelectorValue = selectorValue,
            PropertyName = name, // finalized after collision resolution
            TextContent = staticText,
            Attributes = element.Attributes,
            HasClickHandler = hasClick,
            IsLink = tag == "a" || element.IsRouterLink,
            IsTable = element.Table?.IsTable == true,
            IsMaterialComponent = widget != MaterialWidget.None || tag.StartsWith("mat-", StringComparison.Ordinal),
            ClickHandlerName = SimpleHandlerName(clickHandler),
            IsTextElement = TextTags.Contains(tag) || element.HasNgContent
                || (element.Text.Interpolated && controlType == ControlType.None && !hasClick),
            AriaRole = ariaRole,
            AriaLabel = element.Aria.Label,
            LabelText = labelText,
            Placeholder = element.Labels.Placeholder,
            TestIdValue = element.TestId,
            TextIsInterpolated = element.Text.Interpolated,
            IsConditional = isConditional,
            ConditionText = conditionText,
            IsRepeated = element.Structure.Repeated,
            RepeatItemAlias = element.Structure.RepeatAlias,
            ControlType = controlType,
            MaterialWidget = widget,
            NearestHeadingText = element.Ancestry.HeadingText,
            FormControlName = formControlName,
            FormGroupName = element.Form.FormGroup ?? element.Form.FormGroupName,
            OpensDialogComponent = opensDialog,
            ColumnDefs = element.Table?.Columns.Select(c => new TableColumnDef { Name = c.Id, HeaderText = c.HeaderText }).ToList() ?? [],
            EventBindings = element.Events,
            TemplateLine = element.Line
        };

        return new Candidate
        {
            Draft = draft,
            Name = name,
            Landmark = MapLandmark(element.Ancestry.Landmark),
            HeadingText = element.Ancestry.HeadingText
        };
    }

    private static (SelectorStrategy Strategy, string SelectorValue) ChooseStrategy(
        AstElement element, string tag, string? ariaRole, string? accessibleName,
        string? labelText, string? formControlName, string? staticText)
    {
        if (element.TestId is not null)
        {
            return (SelectorStrategy.TestId, $"[data-testid='{element.TestId}']");
        }
        // Form controls with a real label get getByLabel (idiomatic for inputs);
        // an explicit aria-label still routes through getByRole below.
        if (labelText is not null && element.Aria.Label is null && IsFormControlTag(tag))
        {
            var css = formControlName is not null
                ? $"[formControlName='{formControlName}']"
                : element.Id is not null ? $"#{element.Id}" : tag;
            return (SelectorStrategy.Label, css);
        }
        if (ariaRole is not null && !string.IsNullOrWhiteSpace(accessibleName))
        {
            var css = element.Aria.Label is not null
                ? $"{tag}[aria-label='{element.Aria.Label}']"
                : $"{tag}:has-text(\"{accessibleName}\")";
            return (SelectorStrategy.Role, css);
        }
        if (element.Labels.Placeholder is not null)
        {
            return (SelectorStrategy.Placeholder, $"{tag}[placeholder='{element.Labels.Placeholder}']");
        }
        if (element.Id is not null)
        {
            return (SelectorStrategy.Id, $"#{element.Id}");
        }
        if (formControlName is not null)
        {
            return (SelectorStrategy.FormControl, $"[formControlName='{formControlName}']");
        }
        if (!string.IsNullOrWhiteSpace(staticText))
        {
            return (SelectorStrategy.Text, $"{tag}:has-text(\"{staticText}\")");
        }
        var fallback = element.Classes.Count > 0 ? $"{tag}.{element.Classes[0]}" : tag;
        return (SelectorStrategy.Css, fallback);
    }

    private static bool IsFormControlTag(string tag) =>
        tag is "input" or "select" or "textarea" or "mat-select" or "mat-checkbox"
            or "mat-radio-group" or "mat-slide-toggle" or "mat-form-field";

    private static string? ImplicitRole(string tag, string? inputType, MaterialWidget widget, AstElement element)
    {
        switch (widget)
        {
            case MaterialWidget.MatSelect: return "combobox";
            case MaterialWidget.MatCheckbox: return "checkbox";
            case MaterialWidget.MatRadioGroup: return "radiogroup";
            case MaterialWidget.MatRadioButton: return "radio";
            case MaterialWidget.MatSlideToggle: return "switch";
            case MaterialWidget.MatButton: return "button";
            case MaterialWidget.MatTable: return "table";
        }
        return tag switch
        {
            "button" => "button",
            "a" when element.IsRouterLink || element.Attributes.ContainsKey("href") => "link",
            "select" => "combobox",
            "textarea" => "textbox",
            "input" => inputType?.ToLowerInvariant() switch
            {
                "checkbox" => "checkbox",
                "radio" => "radio",
                "button" or "submit" or "reset" => "button",
                "search" => "searchbox",
                _ => "textbox"
            },
            "table" => "table",
            "nav" => "navigation",
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "heading",
            "summary" => "button",
            _ => null
        };
    }

    private static string? HandlerBaseName(string? handlerSource)
    {
        var simple = SimpleHandlerName(handlerSource);
        if (simple is null)
        {
            return null;
        }
        return simple.StartsWith("on", StringComparison.OrdinalIgnoreCase) && simple.Length > 2
            ? simple[2..]
            : simple;
    }

    private static string? SimpleHandlerName(string? handlerSource)
    {
        if (string.IsNullOrWhiteSpace(handlerSource))
        {
            return null;
        }
        var match = HandlerNameRegex().Match(handlerSource);
        return match.Success ? match.Groups[1].Value : handlerSource;
    }

    private static string? WidgetBaseName(MaterialWidget widget) => widget switch
    {
        MaterialWidget.None => null,
        MaterialWidget.MatPaginator => "Paginator",
        MaterialWidget.MatTabGroup => "Tabs",
        MaterialWidget.MatTable => "Data",
        _ => widget.ToString().Replace("Mat", "", StringComparison.Ordinal)
    };

    private static string DefaultTagName(string tag) => tag switch
    {
        "h1" => "Heading1",
        "h2" => "Heading2",
        "h3" => "Heading3",
        "h4" => "Heading4",
        "h5" => "Heading5",
        "h6" => "Heading6",
        "p" => "Paragraph",
        "div" => "Container",
        "span" => "TextSpan",
        "li" => "ListItem",
        "td" => "TableCell",
        "th" => "TableHeader",
        "input" => "Text",
        _ => ToPascalCase(tag)
    };

    private static string SuffixFor(string tag, ControlType controlType, MaterialWidget widget, AstElement element)
    {
        if (element.Table?.IsTable == true)
        {
            return "Table";
        }
        switch (controlType)
        {
            case ControlType.Checkbox: return "Checkbox";
            case ControlType.Radio: return widget == MaterialWidget.MatRadioButton ? "Radio" : "RadioGroup";
            case ControlType.Select: return "Select";
            case ControlType.Toggle: return "Toggle";
            case ControlType.Datepicker:
            case ControlType.Autocomplete:
            case ControlType.TextInput:
            case ControlType.Textarea: return "Input";
            case ControlType.MenuTrigger: return "Button";
            case ControlType.DialogTrigger: return "Button";
            case ControlType.Tabs: return "Tabs";
            case ControlType.Paginator: return "Paginator";
        }
        if (widget == MaterialWidget.MatFormField)
        {
            return "Field";
        }
        return tag switch
        {
            "button" => "Button",
            "a" => "Link",
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "Heading",
            "label" => "Label",
            "blockquote" or "q" => "Quote",
            "code" or "pre" => "Code",
            "time" => "Time",
            "li" or "td" or "th" or "p" or "span" or "strong" or "em" or "small" or "mark"
                or "sub" or "sup" or "cite" or "abbr" or "address" or "figcaption" => "Text",
            _ when element.IsRouterLink => "Link",
            _ => ""
        };
    }

    private static string ApplySuffix(string pascalBase, string suffix)
    {
        if (string.IsNullOrEmpty(pascalBase))
        {
            return suffix.Length > 0 ? suffix : "Element";
        }
        if (suffix.Length == 0 || pascalBase.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return pascalBase;
        }
        return pascalBase + suffix;
    }

    private static LandmarkRef? MapLandmark(AstLandmark? landmark)
    {
        if (landmark is null || landmark.SelectorValue is null)
        {
            return null;
        }
        return new LandmarkRef
        {
            Label = landmark.Label ?? landmark.TestId ?? landmark.SelectorValue,
            TestId = landmark.TestId,
            Role = landmark.Role,
            AccessibleName = landmark.AccessibleName,
            SelectorValue = landmark.SelectorValue
        };
    }

    // --- collision resolution --------------------------------------------------

    private static void ResolveNameCollisions(List<Candidate> candidates)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (used.Add(candidate.Name))
            {
                continue;
            }

            // Prefer a landmark/heading prefix over a bare ordinal.
            var prefixSource = candidate.Landmark?.Label ?? candidate.HeadingText;
            if (prefixSource is not null)
            {
                var prefixed = ToPascalCase(prefixSource) + candidate.Name;
                if (used.Add(prefixed))
                {
                    candidate.Name = prefixed;
                    continue;
                }
            }

            var baseName = candidate.Name;
            var counter = 2;
            string numbered;
            do
            {
                numbered = baseName + counter++;
            }
            while (!used.Add(numbered));
            candidate.Name = numbered;
        }
    }

    private static void MarkAmbiguousLocators(List<Candidate> candidates)
    {
        var groups = candidates
            .GroupBy(c => (c.Draft.Strategy, c.Draft.SelectorValue))
            .Where(g => g.Count() > 1);
        foreach (var group in groups)
        {
            foreach (var candidate in group)
            {
                if (candidate.Landmark is not null)
                {
                    candidate.AttachLandmark = true;
                }
            }
        }
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"^\s*(?:this\.)?([\w$]+)\s*\(")]
    private static partial Regex HandlerNameRegex();
}
