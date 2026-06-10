using System.Text;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Enrichment-driven emission: typed control interactions (Material + native),
/// repeated/conditional content accessors, typed form fill helpers, typed table
/// column accessors, and the shared control helpers for the base classes.
/// Everything here is gated on enrichment data, so a v1-shaped model produces
/// v1-shaped output.
/// </summary>
public sealed partial class TemplateEngine
{
    // ------------------------------------------------------------------
    // Shared control helpers, emitted IDENTICALLY into base.page.ts and
    // base.component.ts (BaseComponent cannot import BasePage; one emitter
    // prevents drift). Wording must avoid the BaseComponent banned substrings:
    // "navigate", "goto", "import { Page", ": Page", "this.page".
    // ------------------------------------------------------------------
    private void AppendSharedControlHelpers(StringBuilder sb)
    {
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /** Sets a checkbox-like control, resolving inner inputs of mat-checkbox and the switch of mat-slide-toggle. */");
        }
        sb.AppendLine("  protected async setChecked(host: Locator, checked: boolean): Promise<void> {");
        sb.AppendLine("    const inner = host.locator('input[type=\"checkbox\"], input[type=\"radio\"]');");
        sb.AppendLine("    if ((await inner.count()) > 0) {");
        sb.AppendLine("      await inner.first().setChecked(checked);");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine("    const switchControl = host.locator('button[role=\"switch\"]');");
        sb.AppendLine("    if ((await switchControl.count()) > 0) {");
        sb.AppendLine("      const isOn = (await switchControl.first().getAttribute('aria-checked')) === 'true';");
        sb.AppendLine("      if (isOn !== checked) {");
        sb.AppendLine("        await switchControl.first().click();");
        sb.AppendLine("      }");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine("    await host.setChecked(checked);");
        sb.AppendLine("  }");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /** Asserts the checked state of a checkbox-like control. */");
        }
        sb.AppendLine("  protected async expectCheckedState(host: Locator, checked: boolean): Promise<void> {");
        sb.AppendLine("    const inner = host.locator('input[type=\"checkbox\"], input[type=\"radio\"], button[role=\"switch\"]');");
        sb.AppendLine("    const target = (await inner.count()) > 0 ? inner.first() : host;");
        sb.AppendLine("    if (checked) {");
        sb.AppendLine("      await expect(target).toBeChecked();");
        sb.AppendLine("    } else {");
        sb.AppendLine("      await expect(target).not.toBeChecked();");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /** Opens a mat-select and picks an option from the CDK overlay (rendered at the document root, outside any component). */");
        }
        sb.AppendLine("  protected async selectMatOption(trigger: Locator, optionText: string): Promise<void> {");
        sb.AppendLine("    await trigger.click();");
        sb.AppendLine("    const overlay = trigger.page().locator('.cdk-overlay-container');");
        sb.AppendLine("    await overlay.getByRole('option', { name: optionText }).first().click();");
        sb.AppendLine("    await this.waitForOverlayClosed(trigger);");
        sb.AppendLine("  }");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /** Types into an autocomplete input and picks the matching overlay option. */");
        }
        sb.AppendLine("  protected async pickAutocompleteOption(input: Locator, text: string, optionText?: string): Promise<void> {");
        sb.AppendLine("    await input.fill(text);");
        sb.AppendLine("    const overlay = input.page().locator('.cdk-overlay-container');");
        sb.AppendLine("    await overlay.getByRole('option', { name: optionText ?? text }).first().click();");
        sb.AppendLine("  }");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /** Opens a menu trigger and clicks the named item in the CDK overlay. */");
        }
        sb.AppendLine("  protected async pickMenuItem(trigger: Locator, itemText: string): Promise<void> {");
        sb.AppendLine("    await trigger.click();");
        sb.AppendLine("    await trigger.page().locator('.cdk-overlay-container').getByRole('menuitem', { name: itemText }).click();");
        sb.AppendLine("  }");
        sb.AppendLine();

        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine("  /** Waits until no CDK overlay backdrop remains attached. */");
        }
        sb.AppendLine("  protected async waitForOverlayClosed(anchor: Locator): Promise<void> {");
        sb.AppendLine("    await anchor.page().locator('.cdk-overlay-backdrop').waitFor({ state: 'detached' }).catch(() => undefined);");
        sb.AppendLine("  }");
    }

    // ------------------------------------------------------------------
    // Typed control interactions (ControlType != None).
    // ------------------------------------------------------------------
    private void AppendTypedInteractionMethods(StringBuilder sb, IReadOnlyList<ElementSelector> typed, string rootExpression)
    {
        foreach (var selector in typed)
        {
            if (selector.IsRepeated)
            {
                AppendRepeatedAccessors(sb, selector);
                continue;
            }

            switch (selector.ControlType)
            {
                case ControlType.Checkbox:
                    AppendCheckboxMethods(sb, selector, "check", "uncheck", "Checked");
                    break;
                case ControlType.Toggle:
                    AppendCheckboxMethods(sb, selector, "toggleOn", "toggleOff", "On");
                    break;
                case ControlType.Select:
                    AppendSelectMethods(sb, selector);
                    break;
                case ControlType.Radio:
                    AppendRadioMethods(sb, selector);
                    break;
                case ControlType.Datepicker:
                    AppendDatepickerMethods(sb, selector);
                    break;
                case ControlType.Autocomplete:
                    AppendAutocompleteMethods(sb, selector);
                    break;
                case ControlType.MenuTrigger:
                    AppendMenuMethods(sb, selector);
                    break;
                case ControlType.DialogTrigger:
                    AppendDialogTriggerMethods(sb, selector, rootExpression);
                    break;
                case ControlType.Tabs:
                    AppendTabsMethods(sb, selector);
                    break;
                case ControlType.Paginator:
                    AppendPaginatorMethods(sb, selector);
                    break;
                case ControlType.TextInput:
                case ControlType.Textarea:
                    AppendTypedFillMethods(sb, selector);
                    break;
            }

            AppendVisibilityPair(sb, selector);
        }
    }

    private void AppendCheckboxMethods(StringBuilder sb, ElementSelector selector, string onName, string offName, string stateWord)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Turns the {selector.PropertyName} control on.");
        sb.AppendLine($"  async {onName}{selector.PropertyName}(): Promise<void> {{");
        sb.AppendLine($"    await this.setChecked(this.{prop}, true);");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"Turns the {selector.PropertyName} control off.");
        sb.AppendLine($"  async {offName}{selector.PropertyName}(): Promise<void> {{");
        sb.AppendLine($"    await this.setChecked(this.{prop}, false);");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"Asserts the checked state of {selector.PropertyName}.");
        sb.AppendLine($"  async expect{selector.PropertyName}{stateWord}(checked: boolean = true): Promise<void> {{");
        sb.AppendLine($"    await this.expectCheckedState(this.{prop}, checked);");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendSelectMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        if (selector.MaterialWidget == MaterialWidget.MatSelect)
        {
            Doc(sb, $"Selects an option of {selector.PropertyName} by its visible text (CDK overlay aware).");
            sb.AppendLine($"  async select{selector.PropertyName}(optionText: string): Promise<void> {{");
            sb.AppendLine($"    await this.selectMatOption(this.{prop}, optionText);");
            sb.AppendLine("  }");
            sb.AppendLine();

            Doc(sb, $"Asserts the selected value displayed by {selector.PropertyName}.");
            sb.AppendLine($"  async expect{selector.PropertyName}Selected(optionText: string): Promise<void> {{");
            sb.AppendLine($"    await expect(this.{prop}).toContainText(optionText);");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
        else
        {
            Doc(sb, $"Selects an option of {selector.PropertyName} by its label.");
            sb.AppendLine($"  async select{selector.PropertyName}(optionLabel: string): Promise<void> {{");
            sb.AppendLine($"    await this.{prop}.selectOption({{ label: optionLabel }});");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
    }

    private void AppendRadioMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Checks the radio option of {selector.PropertyName} with the given label.");
        sb.AppendLine($"  async select{selector.PropertyName}Option(label: string): Promise<void> {{");
        sb.AppendLine($"    await this.{prop}.getByRole('radio', {{ name: label }}).check();");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendDatepickerMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Types a date into {selector.PropertyName} (in the app's display format) without opening the calendar.");
        sb.AppendLine($"  async fill{selector.PropertyName}Date(date: string): Promise<void> {{");
        sb.AppendLine($"    await this.{prop}.fill(date);");
        sb.AppendLine($"    await this.{prop}.blur();");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendAutocompleteMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Types into {selector.PropertyName} and picks the matching autocomplete option.");
        sb.AppendLine($"  async fill{selector.PropertyName}AndPick(text: string, optionText?: string): Promise<void> {{");
        sb.AppendLine($"    await this.pickAutocompleteOption(this.{prop}, text, optionText);");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendMenuMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Opens the menu behind {selector.PropertyName}.");
        sb.AppendLine($"  async open{selector.PropertyName}Menu(): Promise<void> {{");
        sb.AppendLine($"    await this.{prop}.click();");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"Opens the menu behind {selector.PropertyName} and picks the named item.");
        sb.AppendLine($"  async pick{selector.PropertyName}MenuItem(itemText: string): Promise<void> {{");
        sb.AppendLine($"    await this.pickMenuItem(this.{prop}, itemText);");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendDialogTriggerMethods(StringBuilder sb, ElementSelector selector, string rootExpression)
    {
        var prop = ToCamelCase(selector.PropertyName);
        var pageExpression = GetPageExpression(rootExpression);
        Doc(sb, $"Clicks {selector.PropertyName} and waits for the opened dialog container."
            + (selector.OpensDialogComponent is not null ? $" Opens {selector.OpensDialogComponent}." : ""));
        sb.AppendLine($"  async open{selector.PropertyName}Dialog(): Promise<Locator> {{");
        sb.AppendLine($"    await this.{prop}.click();");
        sb.AppendLine($"    const container = {pageExpression}.locator('mat-dialog-container').last();");
        sb.AppendLine("    await container.waitFor({ state: 'visible' });");
        sb.AppendLine("    return container;");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendTabsMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Selects the tab of {selector.PropertyName} with the given label.");
        sb.AppendLine($"  async select{selector.PropertyName}Tab(label: string): Promise<void> {{");
        sb.AppendLine($"    await this.{prop}.getByRole('tab', {{ name: label }}).click();");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendPaginatorMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Advances {selector.PropertyName} to the next page of results.");
        sb.AppendLine($"  async next{selector.PropertyName}Page(): Promise<void> {{");
        sb.AppendLine($"    await this.{prop}.locator('.mat-mdc-paginator-navigation-next, .mat-paginator-navigation-next').click();");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"Moves {selector.PropertyName} back to the previous page of results.");
        sb.AppendLine($"  async prev{selector.PropertyName}Page(): Promise<void> {{");
        sb.AppendLine($"    await this.{prop}.locator('.mat-mdc-paginator-navigation-previous, .mat-paginator-navigation-previous').click();");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"Sets the page size of {selector.PropertyName}.");
        sb.AppendLine($"  async set{selector.PropertyName}PageSize(size: string): Promise<void> {{");
        sb.AppendLine($"    await this.selectMatOption(this.{prop}.locator('mat-select'), size);");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private void AppendTypedFillMethods(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Fills the {selector.PropertyName} input with the specified value.");
        sb.AppendLine($"  async fill{selector.PropertyName}(value: string): Promise<void> {{");
        sb.AppendLine($"    await this.{prop}.fill(value);");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    /// <summary>
    /// expectXVisible always; expectXHidden additionally when the element renders
    /// conditionally.
    /// </summary>
    private void AppendVisibilityPair(StringBuilder sb, ElementSelector selector)
    {
        var prop = ToCamelCase(selector.PropertyName);
        Doc(sb, $"Asserts that {selector.PropertyName} is visible."
            + (selector.IsConditional && selector.ConditionText is not null ? $" Rendered conditionally ({selector.ConditionText})." : ""));
        sb.AppendLine($"  async expect{selector.PropertyName}Visible(): Promise<void> {{");
        sb.AppendLine($"    await expect(this.{prop}).toBeVisible();");
        sb.AppendLine("  }");
        sb.AppendLine();

        if (selector.IsConditional)
        {
            Doc(sb, $"Asserts that {selector.PropertyName} is hidden (its condition is not met).");
            sb.AppendLine($"  async expect{selector.PropertyName}Hidden(): Promise<void> {{");
            sb.AppendLine($"    await expect(this.{prop}).toBeHidden();");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
    }

    // ------------------------------------------------------------------
    // Repeated content: the readonly field already matches ALL instances;
    // these accessors address individual items. Single-element actions are
    // suppressed for repeated selectors (Playwright strict mode would throw).
    // ------------------------------------------------------------------
    private void AppendRepeatedAccessors(StringBuilder sb, ElementSelector selector)
    {
        var name = selector.PropertyName;
        var prop = ToCamelCase(name);
        var aliasNote = selector.RepeatItemAlias is not null ? $" (repeats per '{selector.RepeatItemAlias}')" : "";

        Doc(sb, $"The {name} instance at the given index{aliasNote}.");
        sb.AppendLine($"  {prop}At(index: number): Locator {{");
        sb.AppendLine($"    return this.{prop}.nth(index);");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"The {name} instances whose content contains the given text.");
        sb.AppendLine($"  {prop}ByText(text: string): Locator {{");
        sb.AppendLine($"    return this.{prop}.filter({{ hasText: text }});");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"The number of {name} instances currently rendered.");
        sb.AppendLine($"  async {prop}Count(): Promise<number> {{");
        sb.AppendLine($"    return this.{prop}.count();");
        sb.AppendLine("  }");
        sb.AppendLine();

        if (selector.HasClickHandler || selector.ElementType is "button" or "a")
        {
            Doc(sb, $"Clicks the {name} instance at the given index.");
            sb.AppendLine($"  async click{name}At(index: number): Promise<void> {{");
            sb.AppendLine($"    await this.{prop}At(index).click();");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Conditional elements on the v1 emission path get an expectXHidden partner
    /// (and an expectXVisible when the v1 path did not already produce one).
    /// </summary>
    private void AppendConditionalAssertions(StringBuilder sb, IEnumerable<ElementSelector> conditionals)
    {
        foreach (var selector in conditionals)
        {
            var prop = ToCamelCase(selector.PropertyName);
            if (!ProducesVisibilityAssertion(selector))
            {
                Doc(sb, $"Asserts that {selector.PropertyName} is visible."
                    + (selector.ConditionText is not null ? $" Rendered conditionally ({selector.ConditionText})." : ""));
                sb.AppendLine($"  async expect{selector.PropertyName}Visible(): Promise<void> {{");
                sb.AppendLine($"    await expect(this.{prop}).toBeVisible();");
                sb.AppendLine("  }");
                sb.AppendLine();
            }

            Doc(sb, $"Asserts that {selector.PropertyName} is hidden (its condition is not met).");
            sb.AppendLine($"  async expect{selector.PropertyName}Hidden(): Promise<void> {{");
            sb.AppendLine($"    await expect(this.{prop}).toBeHidden();");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
    }

    // ------------------------------------------------------------------
    // Typed table accessors (additive to the v1 generic helpers).
    // ------------------------------------------------------------------
    private void AppendTypedTableAccessors(StringBuilder sb, ElementSelector table)
    {
        if (table.ColumnDefs.Count == 0 || !table.IsMaterialComponent)
        {
            return;
        }

        var name = table.PropertyName;
        var columnUnion = string.Join(" | ", table.ColumnDefs.Select(c => $"'{EscapeForJsString(c.Name)}'"));

        Doc(sb, $"The cell of {name} at (rowIndex, column), addressed by its matColumnDef class.");
        sb.AppendLine($"  get{name}Cell(rowIndex: number, column: {columnUnion}): Locator {{");
        sb.AppendLine($"    return this.get{name}Row(rowIndex).locator(`.mat-column-${{column}}`);");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"All body cells of one {name} column.");
        sb.AppendLine($"  get{name}ColumnCells(column: {columnUnion}): Locator {{");
        sb.AppendLine($"    return this.get{name}Rows().locator(`.mat-column-${{column}}`);");
        sb.AppendLine("  }");
        sb.AppendLine();

        Doc(sb, $"The {name} rows whose content contains the given text.");
        sb.AppendLine($"  get{name}RowByText(text: string): Locator {{");
        sb.AppendLine($"    return this.get{name}Rows().filter({{ hasText: text }});");
        sb.AppendLine("  }");
        sb.AppendLine();

        var headerTexts = table.ColumnDefs
            .Where(c => !string.IsNullOrEmpty(c.HeaderText))
            .Select(c => $"'{EscapeForJsString(c.HeaderText!)}'")
            .ToList();
        if (headerTexts.Count > 0)
        {
            Doc(sb, $"Asserts the {name} column headers (in order; extra columns are tolerated).");
            sb.AppendLine($"  async expect{name}ColumnHeaders(): Promise<void> {{");
            sb.AppendLine($"    await expect(this.get{name}Headers()).toContainText([{string.Join(", ", headerTexts)}]);");
            sb.AppendLine("  }");
            sb.AppendLine();
        }
    }

    // ------------------------------------------------------------------
    // Forms: exported FormData interface + fill/submit helpers.
    // ------------------------------------------------------------------
    private void AppendFormDataInterfaces(StringBuilder sb, AngularComponentInfo component)
    {
        for (var i = 0; i < component.Forms.Count; i++)
        {
            var form = component.Forms[i];
            if (form.Controls.Count == 0)
            {
                continue;
            }
            var baseName = GetFormMethodBase(form, component, i);

            if (_options.GenerateJsDocComments)
            {
                sb.AppendLine("/**");
                sb.AppendLine($" * Data for filling the {form.FormGroupName ?? baseName} form.");
                sb.AppendLine(" * All fields are optional; only provided fields are filled.");
                sb.AppendLine(" */");
            }
            sb.AppendLine($"export interface {baseName}Data {{");
            foreach (var control in form.Controls)
            {
                var tsType = control.ControlType is ControlType.Checkbox or ControlType.Toggle ? "boolean" : "string";
                if (_options.GenerateJsDocComments && control.Required)
                {
                    sb.AppendLine("  /** Required control. */");
                }
                sb.AppendLine($"  {ToCamelCase(control.ControlName)}?: {tsType};");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }

    private void AppendFormMethods(StringBuilder sb, AngularComponentInfo component, string rootExpression)
    {
        for (var i = 0; i < component.Forms.Count; i++)
        {
            var form = component.Forms[i];
            if (form.Controls.Count == 0)
            {
                continue;
            }
            var baseName = GetFormMethodBase(form, component, i);

            Doc(sb, $"Fills the {form.FormGroupName ?? baseName} form with the provided values, in template order.");
            sb.AppendLine($"  async fill{baseName}(data: {baseName}Data): Promise<void> {{");
            foreach (var control in form.Controls)
            {
                var dataProp = $"data.{ToCamelCase(control.ControlName)}";
                sb.AppendLine($"    if ({dataProp} !== undefined) {{");
                sb.AppendLine($"      {BuildControlFillStatement(component, control, dataProp, rootExpression)}");
                sb.AppendLine("    }");
            }
            sb.AppendLine("  }");
            sb.AppendLine();

            var submitProperty = FindSubmitProperty(component, form);
            if (submitProperty is not null)
            {
                Doc(sb, $"Submits the {form.FormGroupName ?? baseName} form.");
                sb.AppendLine($"  async submit{baseName}(): Promise<void> {{");
                sb.AppendLine($"    await this.{ToCamelCase(submitProperty)}.click();");
                sb.AppendLine("  }");
                sb.AppendLine();
            }
        }
    }

    private static string BuildControlFillStatement(
        AngularComponentInfo component,
        FormControlInfo control,
        string dataProp,
        string rootExpression)
    {
        var selector = component.Selectors.FirstOrDefault(s =>
            string.Equals(s.FormControlName, control.ControlName, StringComparison.Ordinal));
        // Inline fallback keeps the method compiling when the control has no generated property.
        var target = selector is not null
            ? $"this.{ToCamelCase(selector.PropertyName)}"
            : $"{rootExpression}.locator('[formControlName=\"{control.ControlName}\"]')";

        return control.ControlType switch
        {
            ControlType.Checkbox or ControlType.Toggle => $"await this.setChecked({target}, {dataProp});",
            ControlType.Select when selector?.MaterialWidget == MaterialWidget.MatSelect || selector is null
                => $"await this.selectMatOption({target}, {dataProp});",
            ControlType.Select => $"await {target}.selectOption({{ label: {dataProp} }});",
            ControlType.Radio => $"await {target}.getByRole('radio', {{ name: {dataProp} }}).check();",
            ControlType.Autocomplete => $"await this.pickAutocompleteOption({target}, {dataProp});",
            ControlType.Datepicker => $"await {target}.fill({dataProp});",
            _ => $"await {target}.fill({dataProp});"
        };
    }

    private static string? FindSubmitProperty(AngularComponentInfo component, FormInfo form)
    {
        var byTypeAttribute = component.Selectors.FirstOrDefault(s =>
            s.Attributes.TryGetValue("type", out var type) && type == "submit");
        if (byTypeAttribute is not null)
        {
            return byTypeAttribute.PropertyName;
        }
        if (form.SubmitHandlerName is not null)
        {
            var byHandler = component.Selectors.FirstOrDefault(s =>
                string.Equals(s.ClickHandlerName, form.SubmitHandlerName, StringComparison.Ordinal));
            return byHandler?.PropertyName;
        }
        return null;
    }

    /// <summary>
    /// PascalCase form name guaranteed to end in "Form" without doubling it:
    /// "loginForm" → "LoginForm", "checkout" → "CheckoutForm".
    /// </summary>
    private static string GetFormMethodBase(FormInfo form, AngularComponentInfo component, int index)
    {
        var source = form.FormGroupName;
        string pascal;
        if (string.IsNullOrWhiteSpace(source))
        {
            var className = GetPageObjectClassName(component.Name);
            pascal = index == 0 ? className : $"{className}{index + 1}";
        }
        else
        {
            pascal = SelectorNaming.ToPascalCase(source);
        }
        return pascal.EndsWith("Form", StringComparison.Ordinal) ? pascal : pascal + "Form";
    }

    // ------------------------------------------------------------------
    // Navigation v2 helpers.
    // ------------------------------------------------------------------

    /// <summary>
    /// A routable component's URL entry: a string constant for static routes, an
    /// arrow function for parameterized ones (e.g. (userId: string) =&gt; `/users/${userId}`).
    /// </summary>
    private static string BuildUrlEntry(AngularComponentInfo component, string urlKey)
    {
        var routePath = !string.IsNullOrEmpty(component.RoutePath)
            ? component.RoutePath
            : ToKebabCase(component.Name);

        if (component.RouteParams.Count == 0)
        {
            return $"  {urlKey}: '/{routePath}',";
        }

        var parameters = string.Join(", ", component.RouteParams.Select(p => $"{p}: string"));
        var template = string.Join("/", routePath
            .Split('/')
            .Select(segment => segment.StartsWith(':')
                ? "${" + segment[1..].TrimEnd('?') + "}"
                : segment));
        return $"  {urlKey}: ({parameters}) => `/{template}`,";
    }

    private static string GetPageExpression(string rootExpression) =>
        rootExpression == "this.page" ? "this.page" : "this.root.page()";

    /// <summary>
    /// The base URL including the deployed &lt;base href&gt; when dist analysis
    /// found one (e.g. http://localhost:4200/portal/).
    /// </summary>
    private string GetEffectiveBaseUrl(AngularProjectInfo project)
    {
        var baseHref = project.Dist?.BaseHref;
        if (string.IsNullOrEmpty(baseHref) || baseHref == "/")
        {
            return _options.BaseUrlPlaceholder;
        }
        var path = baseHref.StartsWith('/') ? baseHref : "/" + baseHref;
        return _options.BaseUrlPlaceholder.TrimEnd('/') + path;
    }

    /// <summary>
    /// The waitForLoad anchor: the first stable, always-rendered test-id selector.
    /// </summary>
    private static ElementSelector? FindLoadAnchor(AngularComponentInfo component) =>
        component.Selectors.FirstOrDefault(s =>
            s.Strategy == SelectorStrategy.TestId && !s.IsConditional && !s.IsRepeated);

    private void Doc(StringBuilder sb, string text)
    {
        if (_options.GenerateJsDocComments)
        {
            sb.AppendLine($"  /** {text} */");
        }
    }
}
