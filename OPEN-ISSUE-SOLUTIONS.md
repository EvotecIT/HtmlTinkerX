# Open Issue Solution Plan

Generated: 2026-06-14

Scope: open GitHub issues returned by `gh issue list --repo EvotecIT/PSParseHTML --state open --limit 100` on 2026-06-14. GitHub currently redirects issue URLs to `EvotecIT/HtmlTinkerX`.

## Summary

| Issue | Title | Recommended state | Proposed owner layer |
| --- | --- | --- | --- |
| [#388](https://github.com/EvotecIT/HtmlTinkerX/issues/388) | `Select-JavaScript` Tooling | Addressed by PR [#389](https://github.com/EvotecIT/HtmlTinkerX/pull/389); close after confirming issue author agrees | PowerShell cmdlet surface over existing HtmlTinkerX AST utilities |
| [#390](https://github.com/EvotecIT/HtmlTinkerX/issues/390) | SSO Xml Auth | Implemented on `codex/open-issue-followups` as generic auto-submit form relay, not browser automation | HtmlTinkerX form/auth core plus thin cmdlet |
| [#391](https://github.com/EvotecIT/HtmlTinkerX/issues/391) | Shorter Interactive Aliases | Implemented on `codex/open-issue-followups`; ready for PR after final validation | PowerShell aliases and manifest exports |
| [#392](https://github.com/EvotecIT/HtmlTinkerX/issues/392) | `Html` Cmdlet Noun Prefix Casing | Implemented on `codex/open-issue-followups`; preferred display casing without duplicate uppercase aliases | PowerShell cmdlet attributes, aliases, manifest exports, focused command casing tests |

Suggested sequencing:

1. Close or comment on #388 as addressed by #389 after a quick maintainer check.
2. Do #391 first if we want the short interactive aliases.
3. #392 is now implemented as a display/export casing cleanup without duplicate uppercase aliases.
4. Review #390 as a behavior feature with auth/security implications; it is now implemented on this branch and deserves a focused PR.

Repository state note: PR [#389](https://github.com/EvotecIT/HtmlTinkerX/pull/389) is merged into `v2-speedygonzales` at `e0285dc` (`Improve rendered HTML extraction workflows (#389)`). The local checkout has been synced back to that merged remote state; the earlier local-only revert was backed up before cleanup.

## #388: `Select-JavaScript` Tooling

### Problem

The issue asks for a nicer way to reuse parsed AST nodes, inspect descendants, and optionally filter via a PowerShell scriptblock. The concrete user workflow is finding values such as `iv` and `key` inside a `ClassBody` without reparsing the same source repeatedly.

### Existing capability

`Select-JavaScriptAstNode` already has the right core shape: it can accept source or a piped Acornima `Node`, then uses `HtmlJavaScriptAstUtilities.DescendantNodes` / `DescendantNodesAndSelf`. `Select-JavaScriptVariable` also supports an AST-node parameter set. The missing pieces are mostly discoverability and PowerShell ergonomics.

Remote commit `e0285dc` already contains the small AST surface changes:

- aliases on `Select-JavaScriptAstNode`: `Select-JavaScriptDescendantNode`, `Select-JSAstNode`, `Select-JSDescendantNode`
- aliases on the AST parameter: `InputObject`, `Node`
- `-FilterScript` predicate support
- manifest alias exports and tests

### Proposed closure

Treat this as complete via PR #389:

- `Select-JavaScriptAstNode` now has descendant/discovery aliases.
- It accepts `InputObject` / `Node` aliases for piped AST-node workflows.
- It supports `-FilterScript`.
- Tests cover `ClassBody`, descendant alias traversal, and filter-script usage.

No new visitor framework is needed. A scriptblock predicate plus descendant traversal gives the requested workflow without exposing Acornima visitor internals as a second public model.

### Closure check

- `Invoke-Pester -Path .\Tests\ConvertFrom-JavaScriptAst.Tests.ps1 -Output Detailed`
- `dotnet test Sources\HtmlTinkerX.sln -c Debug --no-build`
- Module import smoke:
  - `Get-Command Select-JavaScriptDescendantNode`
  - `Get-Command Select-JSAstNode`
  - `Get-Command Select-JSDescendantNode`

If those are good on the merged branch, the practical next step is a short issue comment pointing to #389 and asking whether anything remains.

## #390: SSO Xml Auth

### Problem

The issue describes a browserless SSO relay pattern where a response contains HTML/XML-ish markup with a hidden form and JavaScript auto-submit, commonly seen in WS-Federation/SAML-style flows. The user currently parses the form and posts its hidden inputs manually with a web session.

This should not be modeled as "XML auth" specifically. The durable feature is hidden auto-submit form relay.

### Existing capability

The repo already has useful pieces:

- `ConvertFrom-HtmlForm` extracts form `Action`, `Method`, and fields.
- `HtmlFormSubmitter.SubmitAsync` can submit forms directly with `HttpClient`.
- Browser login/session helpers exist for Playwright-backed flows.

The gap is a non-browser relay loop that can detect an auto-submit form, resolve the action URL, preserve cookies, submit hidden fields, and stop safely.

### Implemented solution

Added a generic core feature in HtmlTinkerX:

- `HtmlFormRelayParser`
  - Parses one candidate relay form from content.
  - Returns action URI, method, fields, field names, protocol hint, and auto-submit marker status.
  - Detects common hints:
    - single form
    - all or mostly hidden inputs
    - `wa`, `wresult`, `wctx` for WS-Federation
    - `SAMLRequest`, `SAMLResponse`, `RelayState` for SAML
    - script/noscript auto-submit markers such as `document.forms[0].submit()`
- `HtmlFormRelayClient`
  - Follows one or more relay forms using `HttpClient`.
  - Resolves relative `action` values against the current response URI.
  - Preserves cookies via the caller-provided client; the cmdlet creates one cookie-enabled client and reuses it for the initial URL fetch and all relay submissions.
  - Enforces `MaxRelayCount`.
  - Refuses ambiguous pages by returning `NoRelayForm`.
  - Blocks cross-origin relay by default and uses an allow-list or explicit `AllowCrossHost` option because real SSO crosses hosts but silent cross-origin posting is sensitive.

Expose it through a thin cmdlet:

- Preferred name: `Invoke-HtmlFormRelay`
- Alias for issue vocabulary: `Invoke-HtmlAutoSubmitForm`
- Inputs:
  - `-Url`
  - `-Content` plus `-BaseUrl`
  - `-Path` plus `-BaseUrl`
  - `-Proxy`, `-ProxyCredential`, `-MaxRelayCount`, `-AllowCrossHost`, `-AllowedHost`
- Output:
  - `HtmlFormRelayResult` with final content, final URL, stop reason, whether a relay was submitted, and steps
  - each step reports method, action, field names, status code, response URL, protocol hint, and cross-host/cross-origin/blocking state without logging sensitive values

Avoid building a fake browser or evaluating arbitrary JavaScript. This feature should only support deterministic form relay where the form payload is already present in the markup.

### Validation

Implemented tests around real contracts:

- WS-Federation fixture with `wa`, `wresult`, `wctx` parses correctly.
- SAML fixture with `SAMLResponse` and `RelayState` posts correctly.
- Relative form action resolves against `BaseUrl`.
- Cookies survive across two relay hops.
- `-Url` mode keeps initial response cookies while following relay forms.
- Ambiguous/missing form stops as `NoRelayForm`.
- Cross-origin relay, including same-host different-port actions, is blocked unless explicitly allowed or allow-listed.
- `MaxRelayCount` prevents infinite loops.

Test placement:

- core parser/client tests in `Sources\HtmlTinkerX.Tests`
- thin cmdlet contract tests in `Tests\Invoke-HtmlFormRelay.Tests.ps1`

## #391: Shorter Interactive Aliases

### Problem

The user wants short aliases for interactive exploration:

- `cfhtml` for `ConvertFrom-HTML`
- `sjsn` for `Select-JavaScriptAstNode`
- `sjsv` for `Select-JavaScriptVariable`

They also mention staying aligned with approved PowerShell verbs. That applies to cmdlet names, not aliases, but the spirit is good: keep aliases sparse and predictable.

### Proposed solution

Add only a curated set of lower-case aliases:

| Alias | Target |
| --- | --- |
| `cfhtml` | `ConvertFrom-HTML` now, `ConvertFrom-Html` after #392 |
| `sjsn` | `Select-JavaScriptAstNode` |
| `sjsv` | `Select-JavaScriptVariable` |
| `sjsdn` | `Select-JavaScriptDescendantNode` |

Implementation notes:

- Add aliases on the cmdlet classes and export them in `PSParseHTML.psd1`.
- Keep long discovery aliases too.
- Do not add a large alias dictionary. Three or four shortcuts are enough for the workflow described.

### Validation

- Add/extend a command-export test:
  - `Get-Command cfhtml`
  - `Get-Command sjsn`
  - `Get-Command sjsv`
  - optionally `Get-Command sjsdn`
- Run the affected Pester tests:
  - `Invoke-Pester -Path .\Tests\PackagedSelectorCmdlets.Tests.ps1,.\Tests\ConvertFrom-JavaScriptAst.Tests.ps1 -Output Detailed`

## #392: `Html` Cmdlet Noun Prefix Casing

### Problem

The public command surface mixes `HTML`/`CSS` acronym-style nouns with newer `Html`/`Css` nouns. PowerShell guidance generally treats acronyms of three or more letters as PascalCase words, so `Html` is preferred over `HTML`. The issue specifically calls out aliases like `ConvertFrom-HTMLClass` becoming `ConvertFrom-HtmlClass`.

This is mostly a display/export casing cleanup, not a semantic compatibility change. PowerShell command discovery is case-insensitive, so `ConvertFrom-HTML` and `ConvertFrom-Html` resolve the same command name shape. We do not need duplicate uppercase aliases just to preserve case-only variants.

### Existing mixed surface

Examples of uppercase primary cmdlets today:

- `Compare-HTML`
- `Convert-HTMLToMarkdown`
- `Convert-HTMLToText`
- `ConvertFrom-HTML`
- `ConvertFrom-HTMLCookie`
- `Export-HTMLOutline`
- `Format-CSS`
- `Format-HTML`
- `Get-HTMLResource`
- `Invoke-HTMLCrawl`
- `Invoke-HTMLRendering`
- `Optimize-CSS`
- `Optimize-HTML`
- `Register-HTMLRoute`
- `Unregister-HTMLRoute`

Examples of uppercase compatibility aliases today:

- `ConvertFrom-HTMLClass`
- `ConvertFrom-HTMLTag`
- `Start-HTMLSession`
- `Get-HTMLCookie`
- `Save-HTMLScreenshot`
- `Submit-HTMLForm`

### Implemented solution

Staged this as a casing normalization:

1. Changed canonical `[Cmdlet]` nouns from `HTML` to `Html` and `CSS` to `Css`.
2. Updated alias declarations to the preferred display casing where the alias differs only by case:
   - `ConvertFrom-HtmlClass`
   - `ConvertFrom-HtmlTag`
   - `Start-HtmlSession`
   - `Open-HtmlSession`
   - `Close-HtmlSession`
   - `Stop-HtmlSession`
   - `Get-HtmlCookie`
   - `Save-HtmlScreenshot`
   - `Submit-HtmlForm`
   - and the rest of the existing `*-HTML*` browser aliases
3. Updated `PSParseHTML.psd1` exports so `Get-Command` displays the preferred casing.
4. Updated new planner/profile suggestions and focused tests to use preferred `Html`/`Css` names.
5. Kept existing uppercase spellings resolvable through PowerShell's case-insensitive command lookup.
6. Did not hand-edit generated command docs under `Docs\en-US`; regenerate them through the module docs/build pipeline if this PR later includes docs output.

For JavaScript, keep `JavaScript` in canonical command names. Short aliases can use `JS` because two-letter acronyms are acceptable and already familiar here.

### Validation

- Command export contract in `Tests\CommandCasing.Tests.ps1`:
  - canonical preferred names exist
  - old uppercase spellings still resolve because PowerShell lookup is case-insensitive
  - preferred alias display casing exists
  - old uppercase alias lookups still resolve to the preferred alias display name
- New planner/profile tests use preferred names, while broader historical tests can continue using uppercase spellings until docs/examples are refreshed.
- Import smoke on PowerShell 5.1 and PowerShell 7+ if available.
- Generated docs check only if docs are intentionally regenerated.

## Proposed PR Split

### PR 1: Close #388 and add interactive aliases

Issues: #388, #391

Scope:

- Confirm #388 is covered by #389 and comment/close accordingly.
- Add `cfhtml`, `sjsn`, `sjsv`, and `sjsdn`.
- Add `InputObject` / `Node` aliases to `Select-JavaScriptVariable` so reusable AST-node pipelines are symmetrical.
- Add focused Pester coverage.

Why first: small, low risk, immediately useful.

### PR 2: Cmdlet noun casing cleanup

Issue: #392

Scope:

- Rename canonical `HTML`/`CSS` command nouns to `Html`/`Css`.
- Normalize preferred-casing aliases for browser/session helpers.
- Update README/examples/tests and regenerate docs only if intended.

Why separate: public command surface migration deserves its own review.

### PR 3: Browserless auto-submit form relay

Issue: #390

Scope:

- Add `HtmlFormRelayParser` and `HtmlFormRelayClient` in HtmlTinkerX.
- Add thin `Invoke-HtmlFormRelay` cmdlet and `Invoke-HtmlAutoSubmitForm` alias.
- Add WS-Federation/SAML-style fixtures and safety tests.

Why separate: behavior feature with auth/security implications.

## Beyond The Open Issues

The open issues are useful, but they mostly polish the current surface. The larger opportunity is to make PSParseHTML feel like a web extraction workbench: one module that helps an operator decide whether a page needs static parsing, browser rendering, auth/session relay, crawling, network capture, or LLM-ready dataset output.

### Track A: Interactive Ergonomics

Goal: make quick shell exploration feel natural without growing a giant alias zoo.

Already started:

- `cfhtml` -> `ConvertFrom-HTML`
- `sjsn` -> `Select-JavaScriptAstNode`
- `sjsdn` -> `Select-JavaScriptDescendantNode`
- `sjsv` -> `Select-JavaScriptVariable`
- `Select-JavaScriptVariable -InputObject/-Node` for AST-node reuse

Next sensible additions:

- Add preferred `Html`/`Css` command casing from #392.
- Add one README section called "Interactive mode" that shows parse once, inspect links/forms/scripts, render only when needed, and crawl when a page family is worth persisting.
- Avoid dozens of aliases; short names should exist only for high-frequency exploratory commands.

### Track B: Browserless Auth Relay

Goal: make WS-Federation/SAML-style hidden-form relay usable without launching a browser when the flow is deterministic.

Implemented on this branch for #390, with the useful framing bigger than "XML auth":

- Parse hidden auto-submit forms.
- Detect WS-Federation/SAML markers.
- Submit one or more relay hops with cookies preserved.
- Report each hop and field name without dumping sensitive values.
- Refuse ambiguous or cross-origin relay unless the caller opts in.

This unlocks a practical middle ground between `Invoke-RestMethod` hand-rolling and full Playwright rendering.

### Track C: Page Extraction Plan

Goal: add a diagnostic command that answers "what should I do with this page?" before the user guesses between static parsing, rendering, snapshots, and crawling.

Candidate command:

- `Test-HtmlExtractionPlan`

Inputs:

- `-Url`, `-Content`, `-Path`
- optional `-Render` for a static-vs-rendered comparison
- optional `-IncludeNetworkHints`

Output should be a small object with:

- `RecommendedMode`: Static, RenderedSnapshot, Crawl, AuthRequired, BrowserlessRelayCandidate
- `Reasons`: thin JS shell, forms detected, app state detected, static/rendered delta, login markers, anti-bot/noisy resources, low readable text, sitemap/docs markers
- `SuggestedCommand`: a ready-to-run PSParseHTML command using existing cmdlets
- `Confidence`: low/medium/high

This is high leverage because it turns the module into a guide, not just a toolbox.

### Track D: One-Object Page Workbench

Goal: expose one stable "page intelligence" object that combines the best existing pieces without making users chain ten cmdlets.

Started command:

- `Invoke-HtmlPageWorkbench`

The first version wraps existing reusable core features, not duplicate them:

- raw/static HTML
- readable text
- Markdown
- structured data
- app state
- JavaScript config
- forms and hidden fields
- interaction surface
- endpoints
- warnings about sensitive fields, auth, cross-origin scripts, and missing readiness signals

Implemented first slice:

- `HtmlPageWorkbench.AnalyzeAsync` in HtmlTinkerX.
- `HtmlPageWorkbenchResult` with grouped `Forms`, `HiddenFields`, `Links`, `Assets`, `JsonLd`, `OpenGraph`, `AppState`, `JavaScriptConfig`, `InteractionSurface`, `Endpoints`, `Warnings`, and `SuggestedNextCommand`.
- Rendered snapshot integration through `HtmlPageWorkbenchOptions.RenderedSnapshot` and `Invoke-HtmlPageWorkbench -RenderedSnapshot`.
- Static-vs-rendered comparison when a rendered snapshot is supplied.
- Primary workbench extraction switches to rendered content while keeping `StaticData`, `RenderedData`, `StaticInteractionSurface`, and `RenderedInteractionSurface`.
- Classified API/form endpoint inventory through `ApiEndpoints` and `ApiEndpointCount`.
- `Invoke-HtmlPageWorkbench` with `-Url`, `-Content`, `-Path`, `-BaseUrl`, `-RenderedSnapshot`, `-NoStaticRenderedComparison`, `-NoHtml`, `-IncludeLinkedScripts`, and `-IncludeExternalLinkedScripts`.
- Sensitive-surface warnings when hidden fields or token-like surfaces are present.

Next version:

- Add optional response-body excerpts and compact dataset chunks.
- Connect profile guidance so workbench, planner, and crawl profiles speak the same language.

### Track E: Network And API Intelligence

Goal: make endpoint discovery safer and more useful than "grep for fetch."

Implemented first slice:

- `HtmlApiEndpointInventory` builds deduplicated records from the workbench interaction surface.
- `HtmlApiEndpointRecord` resolves relative URLs, keeps original URL shape, records origin, method, source, selector, and reason codes.
- Risk classification identifies same-origin reads as `Low`, external/auth-hinted endpoints as `Medium`, and state-changing or sensitive-query endpoints as `High`.
- Sensitive query detection records parameter-name risk without copying query values into endpoint metadata.
- `Find-HtmlApiEndpoint` exposes the inventory from a workbench object, raw content, URL, or path.
- `-ExcludeForms` and `-ExcludeScriptEndpoints` let users focus the inventory without reparsing the page themselves.

Next version:

- Add optional network-log fusion from rendered sessions so observed API calls and static JavaScript endpoints appear in one inventory.
- Add request/response excerpt support only behind explicit opt-in and redaction.
- Add profile hints for API docs, app shells, auth pages, and crawl candidates.

### Track F: Dataset And LLM Readiness

Goal: make PSParseHTML excellent at producing trustworthy page/document datasets for downstream automation.

Implemented first single-page upgrade:

- `HtmlPageDatasetBuilder` builds dataset chunks from `HtmlPageWorkbenchResult`.
- `HtmlPageDatasetChunk` carries text, Markdown, source/final URL, title, analysis mode, headings, data kinds, form/endpoint counts, provenance, and redaction hints.
- `HtmlPageDatasetProvenanceEntry` records contributing page surfaces such as readable text, structured data, forms, tokens, and endpoints.
- `ConvertTo-HtmlDatasetJsonL` converts a workbench result, URL, content, or path into compact JSONL.
- `ConvertTo-HtmlDatasetJsonL -AsObject` emits typed chunk objects for further PowerShell filtering.
- Redaction hints identify hidden form fields, token surfaces, login forms, and browserless auth relay candidates.

Still useful upgrades:

- richer table extraction beside the text chunks
- content-mode comparison baked into summaries
- reason codes explaining why content was selected
- response-body excerpts when explicitly requested and redacted
- tighter alignment between single-page chunks and crawler chunk records

The crawler already has much of this; the first single-page path is now equally easy, and the next work is to align the two chunk contracts more closely.

### Track G: Profiles As Product

Goal: promote profiles from internal crawler tuning to a visible workflow concept.

Implemented first product-level profile slice:

- `HtmlExtractionProfile` models reusable workflow profiles across static parsing, rendering, crawling, auth relay, and dataset output.
- `HtmlExtractionProfiles` exposes built-in profiles:
  - `static-page`
  - `docs-content`
  - `api-docs-content`
  - `app-shell`
  - `auth-relay-page`
  - `login-protected-page`
  - `dataset-page`
- Existing crawler profiles are reused through `CrawlProfileName` for `docs-content` and `api-docs-content` instead of duplicating crawler tuning.
- Render-heavy profiles expose `RenderProfile = HeavyDynamicPage`.
- `Test-HtmlExtractionPlan` now adds `SuggestedProfileName`, `SuggestedProfileCommand`, and `SuggestedProfileReason`.
- `Get-HtmlExtractionProfile` lists profiles, filters by name or recommended mode, and accepts a plan from the pipeline to return the suggested profile.

Profile coverage now includes:

- Docs/API docs
- App shells/SPAs
- Login/auth relay pages
- Single-page dataset output

Still useful later:

- News/article pages
- E-commerce/product pages
- Event/ticket pages
- More profile-specific extraction knobs as real issues appear

Each profile should map to render settings, cleanup rules, structured presets, and extraction guidance. That gives users a practical vocabulary:

```powershell
Invoke-HTMLRendering -Url $url -Snapshot -RenderProfile HeavyDynamicPage
Invoke-HTMLCrawl -Url $url -Scenario Dataset -Profile api-docs-content
Test-HtmlExtractionPlan -Url $url
Test-HtmlExtractionPlan -Url $url | Get-HtmlExtractionProfile
```

### Started Big Bet

This branch starts **Track C: `Test-HtmlExtractionPlan`**, **Track D: `Invoke-HtmlPageWorkbench`**, **Track E: `Find-HtmlApiEndpoint`**, **Track F: `ConvertTo-HtmlDatasetJsonL`**, and **Track G: `Get-HtmlExtractionProfile`**.

Why:

- It reuses current static/rendered/snapshot/crawl capabilities.
- It reduces confusion for every future issue.
- It creates a place for smart guidance without bloating every individual cmdlet.
- It can start small and become smarter over time.

Implemented first version:

- Static-only plan from `-Content` / `-Url`.
- Detect forms, hidden auto-submit forms, app state, script-heavy shells, JSON-LD/OpenGraph, readable text, links, and assets.
- Return `RecommendedMode`, `Reasons`, `SuggestedCommand`, and `Confidence`.

Next version:

- Add optional `-Render` to compare static vs rendered content.
- Add a compact `-AsCommand` output mode that returns only the suggested command.
- Feed the planner into crawl profiles so `Invoke-HTMLCrawl -AutoProfile` and single-page planning speak the same language.
