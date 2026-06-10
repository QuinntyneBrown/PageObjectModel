using System.Text;
using PlaywrightPomGenerator.Core.Models;

namespace PlaywrightPomGenerator.Core.Services;

/// <summary>
/// Child-component composition: typed accessors on page and component objects
/// for the component objects found in their templates. Accessors are emitted
/// ONLY for children whose component object is generated in the same run
/// (<see cref="TemplateContext.ComponentObjectNames"/>) so every import resolves.
/// </summary>
public sealed partial class TemplateEngine
{
    /// <summary>
    /// The children of a component that get composition accessors in this run.
    /// </summary>
    private static List<ChildComponentRef> GetComposableChildren(AngularComponentInfo component, TemplateContext? context)
    {
        if (context is null || component.ChildComponents.Count == 0)
        {
            return [];
        }
        return component.ChildComponents
            .Where(child => child.ComponentName is not null
                && child.ComponentName != component.Name
                && context.ComponentObjectNames.Contains(child.ComponentName))
            .ToList();
    }

    /// <summary>
    /// Emits the import lines for composable children. Page objects import from
    /// ../components/, component objects from ./ (same directory).
    /// </summary>
    private static void AppendCompositionImports(StringBuilder sb, IReadOnlyList<ChildComponentRef> children, string importPrefix)
    {
        foreach (var child in children)
        {
            var childClass = GetComponentObjectClassName(child.ComponentName!);
            sb.AppendLine($"import {{ {childClass} }} from '{importPrefix}{ToKebabCase(child.ComponentName!)}.component';");
        }
    }

    /// <summary>
    /// Emits the typed accessors: a getter for single children, the
    /// at/byText/count trio for repeated ones.
    /// </summary>
    private void AppendChildComponentAccessors(
        StringBuilder sb,
        AngularComponentInfo component,
        IReadOnlyList<ChildComponentRef> children,
        string rootExpression)
    {
        if (children.Count == 0)
        {
            return;
        }

        // Selector properties own their names; colliding accessors get a suffix.
        var usedNames = new HashSet<string>(component.Selectors.Select(s => ToCamelCase(s.PropertyName)), StringComparer.Ordinal);

        foreach (var child in children)
        {
            var childClass = GetComponentObjectClassName(child.ComponentName!);
            var accessorName = ToCamelCase(childClass);
            if (!usedNames.Add(accessorName))
            {
                accessorName += "Component";
                usedNames.Add(accessorName);
            }

            var conditionalNote = child.IsConditional
                ? " Rendered conditionally — use expectVisible()/expectHidden() on the instance."
                : "";

            if (child.IsRepeated || child.Count > 1)
            {
                var pluralName = Pluralize(accessorName);
                usedNames.Add(pluralName);

                Doc(sb, $"All {childClass} host elements rendered here.{conditionalNote}");
                sb.AppendLine($"  {pluralName}(): Locator {{");
                sb.AppendLine($"    return {rootExpression}.locator({childClass}.hostSelector);");
                sb.AppendLine("  }");
                sb.AppendLine();

                Doc(sb, $"The {childClass} at the given index.");
                sb.AppendLine($"  {accessorName}At(index: number): {childClass} {{");
                sb.AppendLine($"    return new {childClass}(this.{pluralName}().nth(index));");
                sb.AppendLine("  }");
                sb.AppendLine();

                Doc(sb, $"The first {childClass} whose content contains the given text.");
                sb.AppendLine($"  {accessorName}ByText(text: string): {childClass} {{");
                sb.AppendLine($"    return new {childClass}(this.{pluralName}().filter({{ hasText: text }}).first());");
                sb.AppendLine("  }");
                sb.AppendLine();

                Doc(sb, $"The number of {childClass} instances currently rendered.");
                sb.AppendLine($"  async {accessorName}Count(): Promise<number> {{");
                sb.AppendLine($"    return this.{pluralName}().count();");
                sb.AppendLine("  }");
                sb.AppendLine();
            }
            else
            {
                Doc(sb, $"The {childClass} component embedded here.{conditionalNote}");
                sb.AppendLine($"  get {accessorName}(): {childClass} {{");
                sb.AppendLine($"    return new {childClass}({rootExpression}.locator({childClass}.hostSelector).first());");
                sb.AppendLine("  }");
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Deterministic pluralization: append "s" unless the name already ends in
    /// s/x/z, then append "Items" (no NLP).
    /// </summary>
    private static string Pluralize(string name)
    {
        var last = name[^1];
        return last is 's' or 'x' or 'z' ? name + "Items" : name + "s";
    }
}
