# Evaluation: OpenAI Decisions API for PlaywrightPomGenerator

**Date:** 2026-10-07
**Scope:** Could `ppg` (v2.0.0) be improved by calling the OpenAI Decisions API, including
for troubleshooting and programmatic resolution of analysis/generation issues?
**Verdict:** **3 / 10** — do not integrate now. One narrow, opt-in use case is worth a
spike once the API reaches GA. Details and per-scenario scores below.

---

## 1. What the Decisions API actually is

Source: <https://developers.openai.com/api/docs/guides/decisions> (public beta, released
2026-10-06, powered by `gpt-6-luna`, the only supported model).

`POST /v1/decisions` takes a shared **input** (text, or user messages mixing text and inline
base64 images) plus an array of **questions**, and returns one *typed* answer per question.
It is a **classifier**, not a generator. It never returns free text.

| Question type | What it returns | Example from the docs |
|---|---|---|
| `predicate` | `probability` in [0, 1] that a condition holds | "Does the product have visible damage?" |
| `choice` | the selected `value` + a `probabilities` map over all options + `confidence` | Route a complaint to billing / technical / shipping / other |
| `score` | probability-weighted average over ordered levels (can land between levels) | Severity: Cosmetic → Workaround available → Fully blocked |

Other facts that matter for this evaluation:

- **~10x faster than the Responses API**; billed at **$0.10 per 1M input tokens, output is free**.
- **Dependent decisions require separate requests** (no chaining within one call).
- **No custom JSON output.** The docs explicitly say to use Structured Outputs on the
  Responses API when you need an object of your own shape, and function calling when you
  need tool arguments.
- The docs tell you to **calibrate thresholds against labelled production examples**.
- Public beta; one model; no published rate limits or latency SLO; ZDR and HIPAA available
  for eligible customers; US/EU data residency.

The design centre is: *"given some evidence, which bucket does it belong in, and how sure
are you?"* It is built for content moderation, ticket routing, quality gating and
agent-step gating.

---

## 2. What `ppg` actually decides today

Every decision in this tool is already made **deterministically from static facts** the
AST sidecar (or the regex engine) extracts. The relevant decision points:

| Decision | Where | How it is made today |
|---|---|---|
| Locator strategy per element | `SelectorNaming.ChooseStrategy` | Fixed priority: testid → label → role+name → placeholder → id → formControlName → text → css fallback |
| Property name per element | `SelectorNaming` (`HandlerBaseName`, `WidgetBaseName`, `DefaultTagName`) + numeric collision suffixes | Derived from testid / handler / widget / tag |
| Control type (click, fill, select, toggle, dialog trigger…) | `SelectorNaming.DeriveControlType` | Material widget table + tag + `type` attribute + dialog linkage |
| Ambiguous locators | `SelectorNaming.MarkAmbiguousLocators` | Exact (strategy, selector) duplicates get landmark scoping |
| Routable vs embedded component, host-page URL | route resolver + `CodeGenerator` `TemplateContext` | Route tree from `provideRouter` / `RouterModule` |
| Which engine ran, and why it fell back | `AngularAnalyzer.Ast.cs` → `AnalysisReport` | `SidecarUnavailableException.Reason` is already a closed enum: `SidecarMissing`, `NodeMissing`, `TypeScriptMissing`, `ProtocolError` |
| Per-component regex fallback | `AngularAnalyzer.Ast.cs` | Template parse failure → warning `"{Class}: template analyzed via regex fallback"` |

Two properties of this pipeline are load-bearing and would be threatened by a probabilistic
classifier in the loop:

1. **Reproducibility.** The same source must produce the same `e2e/` tree every run.
   `TemplateEngineDegradationTests` and the pinned invariants in `CLAUDE.md` rely on it,
   and users commit the output. A `probability: 0.62` that flips to `0.58` between runs
   would churn generated code.
2. **Offline, zero-credential operation.** Apart from `remote` cloning a git URL, the tool
   makes no network calls and sends no customer source anywhere. Enterprise users of a
   POM generator are analysing proprietary front-ends.

---

## 3. Scenario-by-scenario assessment

Scores are "usefulness of the Decisions API *specifically* for this scenario", 0–10.

### 3.1 Choosing the locator strategy — **1/10**

The priority list in `ChooseStrategy` encodes Playwright's own documented guidance
(user-facing locators first, CSS last). A `choice` question would at best agree with it,
and at worst introduce nondeterminism into the most visible part of the output. There is no
information the model has that the AST doesn't.

### 3.2 Better property names for CSS-fallback elements — **2/10**

Naming is the generator's weakest output when an element has no testid, label, handler or
text (you get `div1`, `button2`). This is a *generation* problem. The Decisions API cannot
emit a name; it can only pick among candidates you already produced. Producing good
candidates is the hard part, so the API adds nothing. If AI naming is ever wanted, it needs a
generative endpoint (Responses API with Structured Outputs, or an Anthropic model), not this.

### 3.3 Classifying custom / third-party controls — **5/10** (the one real gap)

`DeriveControlType` understands native HTML and Angular Material. For an in-house design
system (`<ds-dropdown>`, `<acme-toggle>`, `<app-date-picker>`) the generator emits a generic
`Locator` with no typed interaction, because nothing in the AST says what the thing *is*.

A `choice` question is a natural fit here:

```json
{
  "model": "gpt-6-luna",
  "input": "<ds-dropdown [options]=\"countries\" (selectionChange)=\"onCountry($event)\" placeholder=\"Country\"></ds-dropdown>\n--- component source of DsDropdownComponent ---\n...",
  "questions": [
    { "type": "choice", "name": "controlType",
      "instructions": "Which Playwright interaction best fits this custom element?",
      "choices": ["click", "fill", "select", "check", "dialogTrigger", "none"] },
    { "type": "predicate", "name": "isRepeatedList",
      "instructions": "Does this element render a repeated list of items?" }
  ]
}
```

Why it still only scores 5:

- The same facts are usually recoverable statically: the custom component's *own* template
  is in the workspace, and the sidecar already walks it. A deterministic "look through the
  custom element to its root native control" pass would solve most cases with no network.
- Results must be cached (e.g. a `ppg.lock.json` keyed by component source hash) to keep
  output stable, and must be threshold-calibrated per the docs. That is real engineering
  for a modest gain.
- It must be opt-in (`--enrich openai`) behind an `IControlClassifier` seam with a
  no-op default, so the existing degradation tests keep passing and the regex engine stays
  pure .NET.

### 3.4 Flagging low-quality or ambiguous locators — **3/10**

A `score` question ("how brittle is this selector?") could rank
`div.container` below `getByRole('button', { name: 'Save' })`. But brittleness is a function
of the strategy, which `ppg` already knows, so a static scoring table gives the same
warning for free. The API would only earn its place for *semantic* judgements such as "this
accessible name is not meaningful to a user" ("btn-1", "Click here"), which is a nice
lint, not a generator feature.

### 3.5 Troubleshooting analysis failures — **2/10**

The question asked was whether the API could help *troubleshoot and programmatically
resolve* issues. For the failures `ppg` itself raises, the answer is no:

- Sidecar failures already arrive as a **closed enum** with a human-readable
  `FallbackReason`. There is nothing to classify; `NodeMissing` means install Node,
  `TypeScriptMissing` means `npm install` in the analysed app. The right improvement is
  a deterministic "suggested fix" line in `ResultPrinter`, not an LLM call.
- Per-component template parse failures already fall back to regex and warn. A `score`
  question could rank which warnings matter most, but the warning list is short and the
  severity is knowable from the element count difference between engines.
- **The API cannot resolve anything.** It has no output other than a probability or a
  pick. Any "programmatic resolution" (rewriting a template, patching a selector, editing
  `tsconfig` paths) requires a generative endpoint. The Decisions API could at most be the
  *router* in front of a fix step, and the routing here is already trivial.

### 3.6 Triage of failing generated Playwright tests — **5/10, but out of scope**

This is the most "Decisions-shaped" idea, and it is a *new product*, not an improvement to
the generator. A hypothetical `ppg triage` command would read Playwright's JSON reporter
output plus the page object and ask:

```json
{ "type": "choice", "name": "cause",
  "instructions": "Classify the root cause of this failing Playwright test.",
  "choices": ["selectorDrift", "timing", "appRegression", "testLogic", "environment"] }
```

…then route deterministically: `selectorDrift` → re-run `ppg component` for that
component and diff; `timing` → suggest `expect(...).toBeVisible()` before interaction;
`environment` → print the baseURL/dist checks. That is a credible use of the API's strengths
(fast, cheap, typed, probability-weighted). It would also need image input support for
failure screenshots, which the API has (inline base64 only). But `ppg` does not run tests,
has no runtime data today, and this would be a separate command with its own
configuration, secrets handling and tests.

### 3.7 Anything image-based — **0/10 today**

`ppg` never launches a browser or reads build screenshots. No input exists to send.

---

## 4. Cross-cutting costs of integrating

| Concern | Impact |
|---|---|
| **Determinism** | Must add a result cache / lock file and a fixed decision threshold, otherwise generated output churns between runs and CI diffs become noise. |
| **Privacy / data egress** | Sends customers' Angular templates and component source to a third party. Needs explicit opt-in, documented ZDR posture, and probably an enterprise-controlled off switch. |
| **New dependency class** | Core currently has three Microsoft.Extensions packages and no `HttpClient`. An `IDecisionClient` seam + NSubstitute fakes are straightforward, but API-key plumbing (`POMGEN_OPENAI_API_KEY`), retries and timeouts are new surface area for a 2.0.0 CLI. |
| **Beta risk** | One model, released yesterday, no rate-limit docs, GA "expected within weeks". Model swaps can shift calibrated thresholds. |
| **Cost** | Negligible. A 200-component app at ~2k tokens/template with 3 questions each is ~0.4M tokens ≈ $0.04 per run. Cost is not the blocker. |
| **Latency** | Thousands of elements → either thousands of calls or large batched inputs; dependent questions need separate round-trips. Acceptable with batching, but turns a sub-second local step into a network-bound one. |
| **Testing story** | Fine: the repo's one-interface-one-implementation rule and `MockFileSystem` pattern extend naturally to a fake classifier. Every v2 emission already no-ops when enrichment data is absent, so an "AI hint" could ride that same gate. |

---

## 5. Overall score and recommendation

### Score: **3 / 10**

| Criterion | Weight | Score | Notes |
|---|---|---|---|
| Fit between API capability (classify) and project needs (generate code deterministically) | high | 2 | The tool's hard problems are generation and naming, which this API cannot do |
| Troubleshooting value | medium | 2 | Failure causes are already a typed enum |
| Programmatic resolution value | medium | 1 | API has no generative output; cannot fix anything |
| Genuine gap it could fill (custom control classification) | medium | 5 | Real but small; largely solvable statically |
| Operational cost (determinism, privacy, beta status) | high | 3 | Significant for a committed-output code generator |
| Future option value (test-failure triage command) | low | 5 | Good idea, different product |

**Would it be a great improvement?** No. The Decisions API is an excellent fit for routing,
moderation and gating workloads. `ppg` is a deterministic static-analysis code generator
whose remaining weaknesses are *generative* (naming, custom-control semantics, fix
synthesis). Where `ppg` does make categorical decisions, it already has complete
information and makes them for free, offline and reproducibly.

**Recommendation:**

1. **Do not integrate now.** Spend the effort on deterministic improvements the evaluation
   surfaced instead:
   - a "look-through" pass that resolves a custom element's control type from its own
     template (closes most of §3.3 without any API);
   - a suggested-fix line per `SidecarUnavailableReason` in `ResultPrinter` (closes §3.5);
   - a static locator-quality warning tier based on `SelectorStrategy` (closes §3.4).
2. **Re-evaluate at GA** with a time-boxed spike on §3.3 only: `IControlClassifier` seam,
   no-op default, opt-in flag, hash-keyed result cache, threshold calibrated on the test
   fixtures. Ship only if it measurably increases the share of typed interactions on a
   real design-system app.
3. **If AI assistance is wanted sooner,** the right tool for naming and fix synthesis is a
   generative API with structured output, not Decisions. Decisions could later sit in front
   of it as the cheap router (§3.6) once `ppg` has runtime signal to route on.

---

## Sources

- OpenAI, *Decisions* guide: <https://developers.openai.com/api/docs/guides/decisions>
- Unite.AI, *OpenAI Releases Decisions API in Public Beta, Powered by GPT-6 Luna* (2026-10-06):
  <https://www.unite.ai/openai-releases-decisions-api-in-public-beta-powered-by-gpt-6-luna/>
- Vercel, *What is OpenAI's Decisions API?*: <https://vercel.com/i/what-is-openai-decisions-api>
- Hugging Face blog, *What Is OpenAI Decisions API? A Practical Guide*:
  <https://huggingface.co/blog/sora-2/what-is-openai-decisions-api-a-practical-guide>
- This repository: `src/PlaywrightPomGenerator.Core/Services/SelectorNaming.cs`,
  `AngularAnalyzer.Ast.cs`, `SidecarUnavailableException.cs`, `Models/AnalysisReport.cs`,
  `src/PlaywrightPomGenerator.Cli/Commands/ResultPrinter.cs`
