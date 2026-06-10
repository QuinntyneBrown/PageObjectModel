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

    /// <summary>
    /// Gets whether the element has a click handler.
    /// </summary>
    public bool HasClickHandler { get; init; }

    /// <summary>
    /// Gets whether the element is a link (href, routerLink).
    /// </summary>
    public bool IsLink { get; init; }

    /// <summary>
    /// Gets whether the element is a table or mat-table.
    /// </summary>
    public bool IsTable { get; init; }

    /// <summary>
    /// Gets whether the element is an Angular Material component.
    /// </summary>
    public bool IsMaterialComponent { get; init; }

    /// <summary>
    /// Gets the click handler method name if available.
    /// </summary>
    public string? ClickHandlerName { get; init; }

    /// <summary>
    /// Gets whether the element is a text element (h1-h6, p, span, etc.).
    /// </summary>
    public bool IsTextElement { get; init; }

    // --- Enrichment (populated by the AST analysis engine; defaults keep the
    // --- regex engine's output unchanged) ------------------------------------

    /// <summary>
    /// Gets the ARIA role for role-based locators (explicit role attribute or
    /// implicit from the element kind).
    /// </summary>
    public string? AriaRole { get; init; }

    /// <summary>
    /// Gets the aria-label value, if present.
    /// </summary>
    public string? AriaLabel { get; init; }

    /// <summary>
    /// Gets the best associated label text (label[for], wrapping label,
    /// mat-label, aria-labelledby target text).
    /// </summary>
    public string? LabelText { get; init; }

    /// <summary>
    /// Gets the placeholder attribute value, if present.
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// Gets the raw data-testid value (unbracketed), if present.
    /// </summary>
    public string? TestIdValue { get; init; }

    /// <summary>
    /// Gets whether the element's text content contains interpolation.
    /// </summary>
    public bool TextIsInterpolated { get; init; }

    /// <summary>
    /// Gets whether the element renders conditionally (@if / *ngIf / @switch case).
    /// </summary>
    public bool IsConditional { get; init; }

    /// <summary>
    /// Gets the condition expression text when <see cref="IsConditional"/> is true.
    /// </summary>
    public string? ConditionText { get; init; }

    /// <summary>
    /// Gets whether the element repeats (@for / *ngFor).
    /// </summary>
    public bool IsRepeated { get; init; }

    /// <summary>
    /// Gets the loop item alias when <see cref="IsRepeated"/> is true.
    /// </summary>
    public string? RepeatItemAlias { get; init; }

    /// <summary>
    /// Gets the interaction kind used to emit typed action methods.
    /// </summary>
    public ControlType ControlType { get; init; } = ControlType.None;

    /// <summary>
    /// Gets the Material/CDK widget classification.
    /// </summary>
    public MaterialWidget MaterialWidget { get; init; } = MaterialWidget.None;

    /// <summary>
    /// Gets the nearest ancestor landmark used for locator chaining and
    /// property-name disambiguation.
    /// </summary>
    public LandmarkRef? ParentLandmark { get; init; }

    /// <summary>
    /// Gets the closest preceding heading text within the current landmark
    /// (naming disambiguation).
    /// </summary>
    public string? NearestHeadingText { get; init; }

    /// <summary>
    /// Gets the formControlName, if the element is a reactive form control.
    /// </summary>
    public string? FormControlName { get; init; }

    /// <summary>
    /// Gets the formGroup expression (or formGroupName) the control belongs to.
    /// </summary>
    public string? FormGroupName { get; init; }

    /// <summary>
    /// Gets the dialog component class this element opens (MatDialog linkage).
    /// </summary>
    public string? OpensDialogComponent { get; init; }

    /// <summary>
    /// Gets the table column definitions (matColumnDef) when the element is a table.
    /// </summary>
    public IReadOnlyList<TableColumnDef> ColumnDefs { get; init; } = [];

    /// <summary>
    /// Gets the event binding names found on the element.
    /// </summary>
    public IReadOnlyList<string> EventBindings { get; init; } = [];

    /// <summary>
    /// Gets the 1-based template line of the element, when known.
    /// </summary>
    public int? TemplateLine { get; init; }
}

/// <summary>
/// The interaction kind of a control, driving which typed action methods are emitted.
/// </summary>
public enum ControlType
{
    /// <summary>No special interaction; generic click/fill emission applies.</summary>
    None,

    /// <summary>Single-line text input.</summary>
    TextInput,

    /// <summary>Multi-line text input.</summary>
    Textarea,

    /// <summary>Checkbox (native or mat-checkbox).</summary>
    Checkbox,

    /// <summary>Radio group or radio button.</summary>
    Radio,

    /// <summary>Select (native or mat-select).</summary>
    Select,

    /// <summary>Slide toggle / switch.</summary>
    Toggle,

    /// <summary>Date input backed by a datepicker.</summary>
    Datepicker,

    /// <summary>Text input backed by an autocomplete overlay.</summary>
    Autocomplete,

    /// <summary>Element that opens a menu.</summary>
    MenuTrigger,

    /// <summary>Element that opens a dialog.</summary>
    DialogTrigger,

    /// <summary>Tab group header interaction.</summary>
    Tabs,

    /// <summary>Paginator interaction.</summary>
    Paginator
}

/// <summary>
/// Material / CDK widget classification of an element.
/// </summary>
public enum MaterialWidget
{
    /// <summary>Not a recognized Material/CDK widget.</summary>
    None,

    /// <summary>mat-select.</summary>
    MatSelect,

    /// <summary>mat-checkbox.</summary>
    MatCheckbox,

    /// <summary>mat-radio-group.</summary>
    MatRadioGroup,

    /// <summary>mat-radio-button.</summary>
    MatRadioButton,

    /// <summary>mat-slide-toggle.</summary>
    MatSlideToggle,

    /// <summary>Input bound to a mat-datepicker.</summary>
    MatDatepicker,

    /// <summary>Input bound to a mat-autocomplete.</summary>
    MatAutocomplete,

    /// <summary>mat-menu panel.</summary>
    MatMenu,

    /// <summary>Element with matMenuTriggerFor.</summary>
    MatMenuTrigger,

    /// <summary>mat-tab-group.</summary>
    MatTabGroup,

    /// <summary>mat-paginator.</summary>
    MatPaginator,

    /// <summary>Table with matSort.</summary>
    MatSort,

    /// <summary>mat-table or table[mat-table].</summary>
    MatTable,

    /// <summary>Button with a mat-button variant attribute.</summary>
    MatButton,

    /// <summary>mat-form-field.</summary>
    MatFormField,

    /// <summary>mat-chip-grid / mat-chip-listbox.</summary>
    MatChipGrid,

    /// <summary>Element with mat-dialog-close.</summary>
    MatDialogClose,

    /// <summary>Element with cdkDrag.</summary>
    CdkDrag
}

/// <summary>
/// A landmark ancestor (testid container, form, or semantic region) used to
/// scope locators and disambiguate property names.
/// </summary>
public sealed record LandmarkRef
{
    /// <summary>
    /// Gets the display label used for property-name disambiguation.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the landmark's data-testid, if that is what anchors it.
    /// </summary>
    public string? TestId { get; init; }

    /// <summary>
    /// Gets the landmark's ARIA role, if anchored by a semantic tag.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Gets the landmark's accessible name (aria-label, legend, card title).
    /// </summary>
    public string? AccessibleName { get; init; }

    /// <summary>
    /// Gets a CSS selector fallback for the landmark.
    /// </summary>
    public required string SelectorValue { get; init; }
}

/// <summary>
/// A Material table column definition (matColumnDef).
/// </summary>
public sealed record TableColumnDef
{
    /// <summary>
    /// Gets the column id (the matColumnDef value, also the .mat-column-* class suffix).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the static header text, when known.
    /// </summary>
    public string? HeaderText { get; init; }
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
    Css,

    /// <summary>
    /// Uses the formControlName attribute.
    /// </summary>
    FormControl
}
