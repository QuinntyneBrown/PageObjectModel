# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`PlaywrightPomGenerator` is a .NET 8 CLI tool (`ppg`, packaged as a global dotnet tool) that
statically analyzes Angular source and generates Playwright Page Object Model test
infrastructure: page objects, component objects, selector files, fixtures, configs, a service
bridge, and SignalR mocks. It does **not** run Angular or a browser — it parses `.ts`/`.html`
source with regex/string analysis (plus a Node TypeScript-compiler sidecar for the `bridge`
command only) and emits TypeScript files.

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
```

Tests use **xUnit + FluentAssertions + NSubstitute**. The file system is abstracted behind
`IFileSystem`; unit tests inject `TestUtilities/MockFileSystem` so generation can be verified
without touching disk.

### Smart App Control caveat (this Windows machine only)

Smart App Control is **On** here, so a freshly rebuilt `PlaywrightPomGenerator.Cli.dll` is
OS-blocked from loading (`0x800711C7`). This makes `dotnet test` on the *full* solution fail
every test that reflection-loads `Cli.dll` (`ProgramTests`, `*CommandHandlerTests`,
`CommandTests`) with an identical load error — these are **not** real defects, and `Unblock-File`
does not fix it. Verify changes with the Core suite instead, which never loads `Cli.dll` and
exercises the whole generation pipeline:

```powershell
dotnet test --filter "FullyQualifiedName~PlaywrightPomGenerator.Tests.Core"
```

For end-to-end coverage that still runs locally, prefer a Core integration test wiring real
`TemplateEngine` + `CodeGenerator` + `FileSystemService` against a temp dir (see
`ComponentObjectIntegrationTests`). The CLI handler tests still compile and run fine in normal CI.

## Architecture

Two projects, clean dependency direction `Cli -> Core` (Core has no dependency on Cli):

- **`src/PlaywrightPomGenerator.Core`** — all analysis and code generation. Everything is behind
  an interface in `Abstractions/` with a single implementation in `Services/`:
  - `IAngularAnalyzer` / `AngularAnalyzer` — discovers workspaces (`angular.json`), apps, and
    libraries; parses component `.ts` + template `.html` (and inline templates) into
    `AngularComponentInfo` + `ElementSelector` models. Pure regex/string parsing.
  - `ICodeGenerator` / `CodeGenerator` — orchestrates: takes analyzer output, creates the output
    directory tree, dedupes components, and calls the template engine per file.
  - `ITemplateEngine` / `TemplateEngine` — produces the actual TypeScript text (BasePage, page
    objects, component objects, selectors, fixtures, bridge files, etc.). This is where generated
    code shape lives — edit here to change emitted output.
  - `ITypeScriptAnalyzer` + `ISidecarTransport` / `NodeSidecarTransport` — the **only** part that
    shells out to Node. Used by the `bridge` command to resolve `InjectionToken` service
    interfaces accurately (including `extends` inheritance) via the TS compiler.
  - `IGitService` / `GitService` + `GitUrlParser` — clone-and-analyze for the `remote` command.
    Uses the `git` CLI directly; no third-party git library.
  - `IFileSystem` / `FileSystemService` — the disk seam that makes generation testable.

- **`src/PlaywrightPomGenerator.Cli`** — `Program.cs` (System.CommandLine + generic host DI) and
  one `Commands/Generate*Command.cs` file per subcommand. Each file holds both the `Command`
  (argument/option definitions) and its `*CommandHandler` (thin: validate path → analyzer →
  generator → print results, return exit code). `Program.BuildRootCommand` wires command parse
  results to handlers resolved from DI; `Program.ConfigureServices` registers everything as
  singletons (services) / transients (handlers).

- **`src/PlaywrightPomGenerator.Sidecar.Node`** — `sidecar.js`, a tiny newline-delimited JSON-RPC
  server over stdio that the bridge command spawns (`node sidecar.js`). It is **shipped inside the
  nupkg** under `tools/net8.0/any/sidecar/` (see the `<None Include>` items in the Cli csproj) and
  located at runtime by `SidecarLocator` (env `POMGEN_SIDECAR` → `sidecar/` next to the tool →
  walk up to the source copy for dev/test).

### Configuration flow

`GeneratorOptions` (section `Generator`) is bound from `appsettings.json` and `POMGEN_`-prefixed
environment variables. Global CLI options `--header`, `--test-suffix`, `--debug` are **pre-parsed
in `Main` before the host is built** and applied via `PostConfigure<GeneratorOptions>`, so they
override file/env config.

## CLI surface

Subcommands: `app`, `workspace`, `lib`, `component`, `bridge`, `artifacts`, `signalr-mock`,
`remote`. `component` emits root-`Locator`-scoped Component Objects (for non-routable components);
`bridge` emits window-exposed recording mocks of tokenized services (requires Node);
`artifacts` selectively emits subsets (`--fixtures`, `--page-objects`, etc.). See README.md for
full per-command usage and generated-output examples.

## Conventions

- Nullable + ImplicitUsings enabled everywhere. Constructors guard args with
  `ArgumentNullException.ThrowIfNull` / `?? throw`.
- One interface, one implementation, constructor-injected — follow this when adding a service.
- Adding a command = new `Commands/Generate*Command.cs` (Command + Handler) + register the
  handler in `Program.ConfigureServices` + wire it in `Program.BuildRootCommand` + add the
  subcommand to the `RootCommand` list.
- Changing generated TypeScript = edit `TemplateEngine`; add/adjust a `TemplateEngineTests` or a
  Core integration test rather than running the emitted output.
- Bump `<Version>` in `PlaywrightPomGenerator.Cli.csproj` to publish — the
  `.github/workflows/publish-nuget.yml` pushes to NuGet on every push to `main` with
  `--skip-duplicate`, so an unchanged version is a no-op.
