# Playwright POM Generator Core

`PlaywrightPomGenerator.Core` is the reusable .NET library behind the
[`PlaywrightPomGenerator`](https://www.nuget.org/packages/PlaywrightPomGenerator) CLI. It analyzes
Angular applications, libraries, and workspaces and generates typed Playwright page objects,
component objects, selectors, fixtures, configuration, and test specifications.

## Installation

```bash
dotnet add package PlaywrightPomGenerator.Core --version 2.0.0
```

The package targets .NET 8 and can be referenced by .NET 8 or later applications and libraries.

## Basic usage

All analyzers and generators are exposed through interfaces in
`PlaywrightPomGenerator.Core.Abstractions`. Models and options are in
`PlaywrightPomGenerator.Core.Models`, with the default implementations in
`PlaywrightPomGenerator.Core.Services`.

For example, templates can be generated directly:

```csharp
using Microsoft.Extensions.Options;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

var options = Options.Create(new GeneratorOptions
{
    ToolVersion = "2.0.0",
});

var templates = new TemplateEngine(options);
var basePageSource = templates.GenerateBasePage();
```

The CLI's service registrations in `PlaywrightPomGenerator.Cli.Program.ConfigureServices` are a
reference composition root for applications that want the full analysis and generation pipeline.

## AST sidecar

AST-powered analysis uses a Node.js sidecar. The sidecar files are included in this package and are
copied to `sidecar/` in the consuming application's output and publish directories, including when
Core is referenced transitively through another library. `SidecarLocator.Locate()` finds that
packaged copy automatically.

Node.js 18 or later is required for AST analysis. The Angular workspace being analyzed must provide
compatible `typescript` and `@angular/compiler` packages in its `node_modules`. Set
`POMGEN_SIDECAR` to an explicit `sidecar.js` path or supply your own `ISidecarTransport` when custom
process management is required. Regex analysis remains available without Node.js.

## License

Playwright POM Generator Core is licensed under the MIT License.
