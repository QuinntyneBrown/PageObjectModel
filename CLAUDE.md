# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`PlaywrightPomGenerator` is a .NET 8 CLI tool (`ppg`, packaged as a global dotnet tool) that
statically analyzes Angular source and generates Playwright Page Object Model test
infrastructure: page objects, component objects, selector files, fixtures, configs, a service
bridge, and SignalR mocks. It does **not** run Angular or a browser. Analysis runs on one of
two engines: **AST** (a Node sidecar using the TypeScript compiler plus the analyzed app's own
`@angular/compiler` from its node_modules) or **regex** (the original source-text parsing, no
Node needed). `--engine auto` (default) prefers AST and falls back to regex with a banner.

## Build / test / run

```powershell
dotnet build -c Release
dotnet test                                   # full suite (see SAC caveat below)
dotnet pack src/PlaywrightPomGenerator.Cli -c Release   # produces the ppg nupkg in ./nupkg

# Run the CLI from source
dotnet run --project src/PlaywrightPomGenerator.Cli -- app ./some-angular-app -o ./e2e

# Run a single test / a filtered set
dotnet test --filter "FullyQualifiedName~AngularAnalyzerTests"
dotnet test --filter "FullyQualifiedName~PlaywrightPomGenerator.Tests.Core"

# Sidecar JS tests (node:test; needs `npm install` in the sidecar dir once)
node --test "src/PlaywrightPomGenerator.Sidecar.Node/test/*.test.js"
```

Tests use **xUnit + FluentAssertions + NSubstitute**. The file system is abstracted behind
`IFileSystem`; unit tests inject `TestUtilities/MockFileSystem` so generation can be verified
without touching disk. The sidecar has its own `node:test` suite under
`src/PlaywrightPomGenerator.Sidecar.Node/test/` (zero extra dependencies; `typescript` and
`@angular/compiler` are devDependencies used only by tests and dev runs).

### Smart App Control caveat (this Windows machine only)

Smart App Control is **On** here, so a freshly rebuilt `PlaywrightPomGenerator.Cli.dll` is
OS-blocked from loading (`0x800711C7`) **in the xUnit test host**. This makes `dotnet test` on
the *full* solution fail every test that reflection-loads `Cli.dll` (`ProgramTests`,
`*CommandHandlerTests`, `CommandTests`) with an identical load error — these are **not** real
defects, and `Unblock-File` does not fix it. (`dotnet run` of the CLI itself works fine.)
Verify changes with the Core suite instead, which never loads `Cli.dll` and exercises the whole
generation pipeline:

```powershell
dotnet test --filter "FullyQualifiedName~PlaywrightPomGenerator.Tests.Core"
```

For end-to-end coverage that still runs locally, prefer a Core integration test wiring real
services against a temp dir (see `ComponentObjectIntegrationTests`,
`AstAnalysisIntegrationTests`, `BridgeIntegrationTests` — the latter two spawn the real Node
sidecar and skip themselves when Node or the sidecar's node_modules are absent). The CLI
handler tests still compile and run fine in normal CI.

## Architecture

Two projects, clean dependency direction `Cli -> Core` (Core has no dependency on Cli):

- **`src/PlaywrightPomGenerator.Core`** — all analysis and code generation. Everything is behind
  an interface in `Abstractions/` with a single implementation in `Services/`:
  - `IAngularAnalyzer` / `AngularAnalyzer` — discovers workspaces (`angular.json`), apps, and
    libraries. The engine decision tree lives in `AngularAnalyzer.Ast.cs`: `Regex` skips the
    sidecar; `Auto` tries one batched `analyzeProject` sidecar call and falls back to the regex
    path (in `AngularAnalyzer.cs`) on `SidecarUnavailableException`; `Ast` rethrows. A component
    whose template the sidecar could not parse falls back to regex *for that component only*.
    AST results are mapped to models via `SelectorNaming` (internal static — locator strategy
    priority, property naming, collision resolution; the sidecar returns raw facts only).
    Per-project results carry an `AnalysisReport` (engine used, versions, fallback reason)
    printed by handlers via `Commands/ResultPrinter`.
  - `IAstProjectAnalyzer` / `AstProjectAnalyzer` — invokes the sidecar's `analyzeProject` and
    deserializes the schemaVersion-1 JSON into the wire DTOs in `Models/AstProjectModels.cs`.
  - `ITypeScriptAnalyzer` / `TypeScriptAnalyzer` — the `bridge` command's seam
    (`discoverInjectionTokens`).
  - `ISidecarTransport` / `NodeSidecarTransport` — spawns `node sidecar.js` per request
    (one-shot: write a line, close stdin, read one line). When the params object has a `root`
    property, NODE_PATH is pointed at `{root}/node_modules` so the analyzed app's own
    typescript/@angular/compiler resolve. Failures surface as `SidecarUnavailableException`
    with a `Reason` (`NodeMissing`/`TypeScriptMissing`/`SidecarMissing`/`ProtocolError`).
  - `IPackageInspector` / `PackageInspector` — package.json versions (core/material/cdk/...),
    angular.json component prefixes, node_modules presence. Pure JSON over `IFileSystem`.
  - `IDistAnalyzer` / `DistAnalyzer` — build output facts: `<base href>` from dist index.html
    (flows into the generated baseURL) and prerendered route directories.
  - `ICodeGenerator` / `CodeGenerator` — orchestrates: creates the output tree, dedupes
    components, builds the `TemplateContext` (which component objects exist in this run →
    gates composition; host-page URLs from the route tree), and calls the template engine per
    file. The `app`/`workspace`/`remote` flow also emits `components/` for components that are
    embedded by others or non-routable (`EmitComponentObjects`).
  - `ITemplateEngine` / `TemplateEngine` — produces the actual TypeScript text. Partial class:
    `TemplateEngine.cs` (v1 emission + integration points), `TemplateEngine.V2.cs`
    (enrichment-driven: typed control interactions, forms, repeated/conditional accessors,
    typed table columns, shared control helpers emitted identically into BasePage and
    BaseComponent), `TemplateEngine.Composition.cs` (child component accessors). **Every v2
    feature is gated on enrichment data**, so a regex-engine (v1-shaped) model produces
    v1-shaped output — pinned by `TemplateEngineDegradationTests`-style tests.
  - `IGitService` / `GitService` + `GitUrlParser` — clone-and-analyze for the `remote` command.
  - `IFileSystem` / `FileSystemService` — the disk seam that makes generation testable.

- **`src/PlaywrightPomGenerator.Cli`** — `Program.cs` (System.CommandLine + generic host DI) and
  one `Commands/Generate*Command.cs` file per subcommand. Each file holds both the `Command`
  (argument/option definitions) and its `*CommandHandler` (thin: validate path → analyzer →
  generator → print results, return exit code). `Program.BuildRootCommand` wires command parse
  results to handlers resolved from DI; `Program.ConfigureServices` registers everything as
  singletons (services) / transients (handlers).

- **`src/PlaywrightPomGenerator.Sidecar.Node`** — a newline-delimited JSON-RPC server over stdio
  (`sidecar.js` dispatch + `lib/*.js` CJS modules). Methods: `ping`, `discoverInjectionTokens`
  (bridge), `analyzeProject` (components via TS AST in `lib/project-scanner.js`, templates via
  the app's @angular/compiler in `lib/template-visitor.js` + `lib/compiler-loader.js`, route
  tree with lazy-import and tsconfig-alias resolution in `lib/route-resolver.js` +
  `lib/tsconfig-paths.js`, assembled by `lib/analyze-project.js`). Shipped inside the nupkg
  under `tools/net8.0/any/sidecar/` (`<None Include>` items + a `lib/**` glob in the Cli
  csproj; `test/` is not shipped, and node_modules is never shipped — production resolution
  comes from the analyzed app). Located at runtime by `SidecarLocator` (env `POMGEN_SIDECAR` →
  `sidecar/` next to the tool → walk up to the source copy for dev/test).

### Sidecar conventions (hard rules)

- **The sidecar stays CommonJS.** NODE_PATH (the production module-resolution mechanism set by
  `NodeSidecarTransport`) is honored by `require()` but ignored by ESM `import` resolution.
  (`compiler-loader.js` may dynamic-`import()` the app's ESM @angular/compiler — that's the
  one sanctioned exception, resolved by explicit path.)
- **`analyzeProject`'s JSON shape is a frozen contract** (`schemaVersion: 1`, mirrored by
  `Models/AstProjectModels.cs`): additive changes only; bump the schema version for anything
  breaking and update `AstProjectAnalyzer`'s check.
- **Never `process.exit()` on stdin close** — handlers are async; the pending-counter pattern
  in `sidecar.js` lets the event loop drain so large responses flush.
- Adding an RPC method = dispatch entry in `sidecar.js` + a `lib/` module + a node:test suite
  + a Core-side caller mapping JSON defensively (see `AstProjectAnalyzer`).

### Configuration flow

`GeneratorOptions` (section `Generator`) is bound from `appsettings.json` and `POMGEN_`-prefixed
environment variables. Global CLI options `--header`, `--test-suffix`, `--debug`, `--engine`,
`--no-component-objects`, `--no-composition` are **pre-parsed in `Main` before the host is
built** and applied via `PostConfigure<GeneratorOptions>`, so they override file/env config.
`ToolVersion` is replaced at runtime with the Cli assembly's informational version (from
`<Version>` in the csproj) unless explicitly configured — no manual sync needed.

## CLI surface

Subcommands: `app`, `workspace`, `lib`, `component`, `bridge`, `artifacts`, `signalr-mock`,
`remote`. `component` emits root-`Locator`-scoped Component Objects; `app`/`workspace`/`remote`
also emit `components/` for embedded and non-routable components (opt out:
`--no-component-objects`); `bridge` emits window-exposed recording mocks of tokenized services
(requires Node); `artifacts` selectively emits subsets (`--fixtures`, `--page-objects`, etc.);
`app`/`workspace` accept `--dist` for build-output analysis. `--engine auto|ast|regex` selects
the analysis engine everywhere except `bridge` (always sidecar) and `signalr-mock` (no
analysis). See README.md for full per-command usage and generated-output examples.

## Conventions

- Nullable + ImplicitUsings enabled everywhere. Constructors guard args with
  `ArgumentNullException.ThrowIfNull` / `?? throw`.
- One interface, one implementation, constructor-injected — follow this when adding a service.
- Adding a command = new `Commands/Generate*Command.cs` (Command + Handler) + register the
  handler in `Program.ConfigureServices` + wire it in `Program.BuildRootCommand` + add the
  subcommand to the `RootCommand` list.
- Changing generated TypeScript = edit `TemplateEngine` (respect the partial-file split); new
  v2 emission must no-op when its model data is absent so the regex engine's output stays
  stable. Add/adjust a `TemplateEngine*Tests` or a Core integration test rather than running
  the emitted output. Pinned invariants: page objects root every locator on `page`; component
  objects root everything on `this.root`; `base.component.ts` must not contain `navigate`,
  `goto`, `import { Page`, or `: Page`.
- Bump `<Version>` in `PlaywrightPomGenerator.Cli.csproj` to publish — the
  `.github/workflows/publish-nuget.yml` pushes to NuGet on every push to `main` with
  `--skip-duplicate`, so an unchanged version is a no-op.
