# PowerShell extraction and browser workflows

[Back to the project overview](../README.MD)

## 🔧 PowerShell Cmdlets

### HTML/CSS/JavaScript Processing
- **Convert-HTMLToText** - Convert markup to plain text
- **ConvertFrom-HtmlTable** - Extract table elements into objects (supports rowspan/colspan). Nested tables keep their own rows and cells. Spans follow HTML limits, and `rowspan="0"` extends to the end of its row group. Parsing defaults to at most 10,000 columns per table, 100,000 source rows across tables, and 1,000,000 expanded result cells. Use `-MaximumColumns`, `-MaximumRows`, and `-MaximumExpandedCells` to set explicit limits for trusted larger tables; oversized results raise an error. .NET callers can pass `HtmlTableParseLimits` to the table parsing overloads.
- **ConvertFrom-HTMLAttributes** - Extract elements by tag, class, id or name
- **ConvertFrom-HTML** - Parse full documents or fragments
- **ConvertFrom-HtmlForm** - Extract form data and structure
- **ConvertFrom-HtmlList** - Parse list elements into structured data
- **ConvertFrom-HtmlMeta** - Extract name/content pairs from meta tags
- **ConvertFrom-HtmlMicrodata** - Extract structured data items (schema.org types)
- **ConvertFrom-HtmlOpenGraph** - Extract Open Graph metadata
- **ConvertFrom-HtmlJsonLd** - Extract JSON-LD structured data scripts
- **ConvertFrom-HtmlScriptData** - Extract generic JSON-bearing script data such as import maps and app settings
- **ConvertFrom-HtmlAppState** - Extract common framework state payloads such as `__NEXT_DATA__`
- **ConvertFrom-HtmlHeadLink** - Extract canonical, alternate, feed, icon, manifest, and preload head links
- **ConvertFrom-HtmlImageCandidate** - Extract image URLs and responsive `srcset` candidates
- **Select-HtmlNode** - Select HtmlAgilityPack nodes with XPath or common tag/attribute predicates
- **Select-HtmlAttributeValue** - Read HTML attribute values with fallback defaults
- **Select-HtmlInnerText** - Read node text with optional HTML entity decoding
- **Select-HtmlToken** - Find CSRF, XSRF, nonce, anti-forgery, and auth token values
- **Select-HtmlJavaScriptVariable** - Find JavaScript variable declarations and assignments inside HTML script tags
- **ConvertFrom-JavaScriptAst** - Parse JavaScript into an Acornima AST
- **Select-JavaScriptAstNode** - Traverse Acornima AST descendants by node type
- **Select-JavaScriptVariable** - Find JavaScript variable declarations and loose assignments by name or prefix
- **ConvertFrom-HtmlRscPayload** - Extract Next.js inline React Server Component / React Flight payload rows
- **ConvertFrom-JavaScriptEndpoint** - Discover likely endpoints from static JavaScript strings
- **ConvertFrom-HtmlLinkedJavaScriptEndpoint** - Download linked scripts and discover likely endpoints from JavaScript bundles
- **ConvertFrom-RobotsTxt** - Parse robots.txt groups, rules, crawl delays, and sitemap directives
- **ConvertFrom-WebManifest** - Parse web app manifest JSON
- **ConvertFrom-WellKnownText** - Parse `security.txt`, `humans.txt`, and `ads.txt`
- **Format-CSS** - Pretty-print style sheets
- **Format-HTML** - Tidy up HTML markup
- **Format-JavaScript** - Beautify JavaScript with customizable options
- **Optimize-CSS** - Minify style sheets
- **Optimize-Email** - Inline CSS for email bodies
- **Optimize-HTML** - Minify HTML
- **Optimize-JavaScript** - Minify JavaScript

### Browser Automation & Interaction
- **Start-HtmlBrowserSession** / **Invoke-HtmlRendering** - Create browser sessions with authentication support
- **Close-HtmlBrowserSession** - Dispose browser sessions
- **Invoke-HtmlBrowserNavigation** - Navigate to different URLs
- **Invoke-HtmlBrowserScript** - Execute JavaScript in browser context
- **Invoke-HtmlBrowserDomScript** - Run JavaScript with AngleSharp (no browser required)
- **Invoke-HtmlBrowserClick** - Click elements in the browser
- **Get-HtmlBrowserInteractable** - List clickable elements
- **Set-HtmlBrowserInput** - Set input field values
- **Invoke-HtmlBrowserKey** - Send keyboard input such as `Enter`, `Control+A`, or `ArrowDown`
- **Invoke-HtmlBrowserHover** - Hover over an element
- **Invoke-HtmlBrowserScroll** - Scroll an element into view
- **Wait-HtmlBrowserReady** - Wait for load state, selector, JavaScript readiness, and/or DOM stability
- **Find-HtmlBrowserLocator** - Rank resilient selector and Playwright-style locator candidates for visible controls
- **Wait-HtmlBrowserContent** - Wait for text or a target element state
- **Close-HtmlBrowserOverlay** - Try common cookie/modal dismissals
- **Get-HtmlBrowserDiagnostics** - Inspect navigator, viewport, storage, console errors, observed API calls, and WebSockets
- **Get-HtmlBrowserElement** / **Test-HtmlBrowserElement** - Inspect selector matches, geometry, attributes, and state
- **Get-HtmlBrowserActiveElement** - Inspect the focused element after clicks or keyboard navigation
- **Get-HtmlBrowserStorage** / **Set-HtmlBrowserStorage** - Read or update local/session storage entries
- **Save-HtmlBrowserContent** - Save rendered session or one-shot URL/file HTML or text to disk
- **Export-HtmlBrowserEvidence** - Save screenshots, PDFs, rendered content, text/Markdown, optional network summary, redacted SSO handoff summary, and manifest hashes; text artifacts and manifest URLs redact common secrets by default, and visual artifacts mask common sensitive fields by default; use `-Artifact` for minimal packs, `-NoRedaction` only when exact raw proof is required, and `-NoVisualMask` only when image/PDF masking is not appropriate
- **New-HtmlBrowserProfile -Scenario** / **Start-HtmlBrowserSession -Scenario** - Apply intent-focused browser defaults for audit proof, mailbox proof, login-protected pages, SPAs, low bandwidth, network capture, and download evidence
- **Start-HtmlBrowserSession -ManualLogin** - Open a visible persistent session for enterprise SSO/MFA and optionally wait for a post-login selector before returning
- **Start-HtmlBrowserSession -PreventSsoAutoSubmit** / **Get-HtmlBrowserSsoHandoff -Wait** - Pause and inspect SAML, WS-Federation, OAuth, or OpenID Connect handoff forms after user-attended login; assertion and token fields are redacted by default and require `-IncludeSensitiveValues` to reveal
- **Find-HtmlBrowserDataSource** - Convert observed browser XHR/fetch traffic into browserless extraction sources and recipes
- **Find-HtmlDataSource** - Discover static, app-state, JSON, and endpoint sources before opening a browser
- **Invoke-HtmlDataExtraction** - Extract a discovered browserless source
- **Export-HtmlExtractionRecipe** / **Import-HtmlExtractionRecipe** / **Invoke-HtmlExtractionRecipe** - Save and rerun browserless extraction recipes
- **Start-HtmlBrowserRecipeRecording** / **Stop-HtmlBrowserRecipeRecording** / **Export-HtmlBrowserRecipe** / **Optimize-HtmlBrowserRecipe** / **Invoke-HtmlBrowserRecipe** - Record successful session actions, harden selector alternates from the live page, save JSON browser recipes, and replay them with step results, locator output, evidence packs, optional failure evidence, and recorder-side redaction for sensitive input fields
- **Set-HtmlBrowserSelectOption** - Select dropdown options
- **Set-HtmlBrowserChecked** - Check/uncheck checkboxes and radio buttons
- **Submit-HtmlBrowserForm** - Submit forms

#### Browser Extraction Mode

Browser extraction mode is the rendering-backed path for pages where static HTML is not enough. The intended workflow is: plan the page, choose the lightest render profile that matches the problem, interact only as needed, snapshot the rendered state, inspect diagnostics, and feed the same rendered snapshot into the workbench.

Browser scenarios are a higher-level starting point for admin automation. They set practical defaults first, then profile JSON and explicit parameters can refine them. `AuditProof`, `MailboxProof`, and `DownloadEvidence` use a stable 1366x900 evidence viewport and DOM-ready navigation. `LoginProtected` extends timeouts for SSO/MFA work. `SinglePageApp` avoids waiting forever on busy app traffic. `LowBandwidth` skips heavy images, media, and fonts. `NetworkCapture` keeps rendered output and observed traffic together for diagnostics.

For Azure AD, Okta, ADFS, and other enterprise SSO pages, prefer a real browser session over rebuilding the login flow with `Invoke-WebRequest`. SAML and WS-Federation often use a hidden auto-submitting form that appears only after JavaScript, redirects, MFA, and policy checks finish, so `-PreventSsoAutoSubmit` can hold recognized handoff forms while `Get-HtmlBrowserSsoHandoff -Wait -Timeout 60000` reports protocol, action URL, field names, original value lengths, and redacted values. OAuth and OpenID Connect redirects can also put `code`, `state`, `id_token`, `access_token`, or `error` fields in the current URL query or fragment; those are returned as a `location` handoff with the page URL and field values redacted by default. Use `-IncludeSensitiveValues` only for your own authorized workflow when the raw assertion or token is genuinely required.

When the site only works from a seeded real browser profile, attach to Chrome or Edge over the Chrome DevTools Protocol instead of launching a fresh Playwright browser. Start the browser yourself with a remote debugging port and the profile/history/session state you want to reuse, then pass `-CdpEndpointUrl` directly or save it in a profile JSON. HtmlTinkerX creates and owns only the automation page; closing the PowerShell session does not close the attached browser or profile.

```powershell
$userDataPath = Join-Path $PWD 'real-chrome-user-data'
chrome.exe --remote-debugging-port=9222 --user-data-dir="$userDataPath"

New-HtmlBrowserProfile `
    -Name 'RealChromeCdp' `
    -Path '.\real-chrome-cdp.profile.json' `
    -CdpEndpointUrl 'http://127.0.0.1:9222' `
    -PreventSsoAutoSubmit | Out-Null

$session = Start-HtmlBrowserSession `
    -Url 'https://example.com/protected' `
    -ProfilePath '.\real-chrome-cdp.profile.json' `
    -NoDefault
```

When a captured handoff needs inspection rather than replay, start with `Get-HtmlBrowserSsoHandoff -Analyze`. It auto-detects `SAMLResponse`, `id_token`, `access_token`, authorization code, `state`, and `RelayState` fields and routes known artifacts through safe protocol summaries without returning raw assertion or token values. If you already have a saved/deserialized handoff object, pipe it to `ConvertFrom-HtmlSsoHandoff` for the same analysis. For deeper protocol-specific inspection, use `ConvertFrom-HtmlSamlResponse` for SAML assertions or `ConvertFrom-HtmlJsonWebToken` for OpenID Connect/OAuth tokens. These report issuer, destination or audience, validity window, status or scopes, key id, and claim or attribute names while redacting subject, assertion, and user-identifying values by default. Add `-IncludeSensitiveValues` only for authorized troubleshooting, and `-IncludeXml` or `-IncludeJson` only when you need decoded payloads.

Evidence screenshots and PDFs mask common sensitive fields by default, including password, token, SAML, MFA, OTP, PIN, credential, and secret inputs. Add `-VisualMaskSelector` for app-specific fields, change the mask color with `-VisualMaskColor`, or use `-NoVisualMask` only when the visual proof must intentionally show those fields. The older `-ScreenshotMaskSelector`, `-ScreenshotMaskColor`, and `-NoScreenshotMask` names remain available. Direct `Save-HtmlBrowserScreenshot`, `Save-HtmlBrowserPdf`, `Save-HtmlBrowserContent`, and `Export-HtmlBrowserEvidence` calls can reuse one-shot launch defaults such as `-Scenario`, `-ProfilePath`, `-StatePath`, `-UserDataDirectory`, `-LoadState`, `-Proxy`, and resource blocking. Screenshot and PDF calls also expose opt-in masking through `-MaskSensitiveElement`, `-MaskSelector`, and `-MaskColor`. `BlockResourceType Document` is intentionally rejected because it would abort the page itself rather than only blocking subresources.

| Profile | Use when | PowerShell story | C# story |
| --- | --- | --- | --- |
| `FastStaticFallback` | static extraction is likely enough but a cheap browser fallback is useful | `Invoke-HtmlRendering -RenderProfile FastStaticFallback` | `HtmlBrowser.GetPageContentAsync(...)` with blocked heavy resources |
| `InteractivePage` | forms, buttons, search boxes, or reactive inputs reveal content | `Start-HtmlBrowserSession`, `Set-HtmlBrowserInput`, `Invoke-HtmlBrowserKey`, `Wait-HtmlBrowserReady`, `Wait-HtmlBrowserContent` | `HtmlBrowser.TypeInputAsync`, `PressKeysAsync`, `WaitUntilReadyAsync`, `WaitForTextAsync` |
| `LazyLoadedContent` | content appears after scrolling or delayed viewport work | `Invoke-HtmlRendering -RenderProfile LazyLoadedContent -AutoScroll` | `HtmlBrowser.CreateSnapshotAsync(...)` after scroll/wait helpers |
| `AppShell` | the original HTML is a thin JavaScript shell | `Invoke-HtmlRendering -RenderProfile AppShell -Snapshot` | planner recommends `HtmlRenderProfile.AppShell` before workbench analysis |
| `LoginProtected` | storage state or a visible login session is needed | `Start-HtmlBrowserSession`, `Export-HtmlBrowserState`, `-StatePath` / `-UserDataDirectory` | `HtmlBrowser.ExportBrowserStateAsync`, `ImportBrowserStateAsync`, and persistent launch options |
| `NetworkCapture` | API calls, response bodies, console errors, or WebSockets explain the page | `-IncludeNetworkLog -IncludeResponseBody -RedactResponseBody` | `HtmlBrowser.GetNetworkLog`, `GetDiagnosticsAsync` |
| `LowBandwidth` | bandwidth is constrained or media/fonts/styles are not needed | `Invoke-HtmlRendering -RenderProfile LowBandwidth` | browser content with aggressive resource blocking |
| `HeavyDynamicPage` | legacy broad dynamic-page profile is still needed | `Invoke-HtmlRendering -RenderProfile HeavyDynamicPage` | existing high-wait dynamic rendering behavior |

```powershell
# Hydrated app page with visible-only interactions before extraction
$snapshot = Invoke-HtmlRendering -Url 'https://example.com/app' `
    -RenderProfile InteractivePage `
    -DismissText 'Accept' `
    -ClickText 'Load more' `
    -WaitForSelector 'main' `
    -Selector 'main' `
    -Snapshot

# Lazy-loaded content that appears while scrolling
$content = Invoke-HtmlRendering -Url 'https://example.com/catalog' `
    -RenderProfile LazyLoadedContent `
    -WaitForSelector '.product-card' `
    -Selector '.product-grid'

# JavaScript app shell with parsed snapshot data
$app = Invoke-HtmlRendering -Url 'https://example.com/dashboard' `
    -RenderProfile AppShell `
    -WaitForSelector 'main' `
    -Snapshot `
    -IncludeLinkedScripts `
    -IncludeStaticRenderedComparison

# Network-focused pass for observed API calls and console errors
$network = Invoke-HtmlRendering -Url 'https://example.com/search' `
    -RenderProfile NetworkCapture `
    -WaitForSelector '#results' `
    -Snapshot `
    -IncludeNetworkLog `
    -IncludeResponseBody `
    -RedactResponseBody

# Session-style interaction with readiness waits and paced typing
$session = Start-HtmlBrowserSession -Url 'https://example.com/search' -Scenario SinglePageApp
Close-HtmlBrowserOverlay -Session $session
Set-HtmlBrowserInput -Session $session -Selector 'input[type=search]' -Value 'HtmlTinkerX' -Type -DelayMs 25
Invoke-HtmlBrowserKey -Session $session -Selector 'input[type=search]' -Key 'Enter'
Wait-HtmlBrowserReady -Session $session -Selector 'main' -Stable
Wait-HtmlBrowserContent -Session $session -Text 'Results' -Selector 'main'
Wait-HtmlBrowserContent -Session $session -Element -Selector 'main' -Visible -InViewport
$items = Get-HtmlBrowserElement -Session $session -Selector '.result' -VisibleOnly -IncludeAttributes
$items | Select-Object Text, Selector, Visible, InViewport, Width, Height
Get-HtmlBrowserContent -Session $session -Selector 'main' -AsText
$allTitles = Get-HtmlBrowserContent -Session $session -Selector '.result-title' -All -AsText
$active = Get-HtmlBrowserActiveElement -Session $session -IncludeAttributes
Set-HtmlBrowserStorage -Session $session -Scope Local -Key extractionMode -Value browser
$storage = Get-HtmlBrowserStorage -Session $session -Scope All
Save-HtmlBrowserContent -Session $session -Selector 'main' -OutFile .\rendered-main.html
$evidence = Export-HtmlBrowserEvidence -Session $session -OutFolder .\evidence\search -NetworkSummary -SsoHandoffSummary -VisualMaskSelector '.account-secret'
$diagnostics = Get-HtmlBrowserDiagnostics -Session $session
$diagnostics.ConsistencyWarnings
$diagnostics.ObservedApiCalls
$apiSource = Find-HtmlBrowserDataSource -Session $session -IncludeResponseBody | Select-Object -First 1
$apiSource | Export-HtmlExtractionRecipe -Path .\observed-api.recipe.json -IncludeRawContent
$apiResult = $apiSource | Invoke-HtmlDataExtraction
Close-HtmlBrowserSession -Session $session

# Record a manual session into a replayable browser recipe
$recorded = Start-HtmlBrowserSession -Url 'https://example.com/search' -Scenario SinglePageApp
Start-HtmlBrowserRecipeRecording -Session $recorded -Name 'SearchProof' -IncludeCurrentUrl
Set-HtmlBrowserInput -Session $recorded -Selector 'input[type=search]' -Value 'HtmlTinkerX'
Invoke-HtmlBrowserClick -Session $recorded -Text 'Search' -Exact
Wait-HtmlBrowserContent -Session $recorded -Text 'Results' -Selector 'main'
Export-HtmlBrowserEvidence -Session $recorded -OutFolder .\evidence\recorded-search -BaseFileName search-proof -Artifact Html,Text -NoManifest
Stop-HtmlBrowserRecipeRecording -Session $recorded -Path .\search-proof.browser.recipe.json -VariableTemplatePath .\search-proof.browser.variables.json -HardenSelectors -HardeningReportPath .\search-proof.browser.hardening.json
Close-HtmlBrowserSession -Session $recorded
Invoke-HtmlBrowserRecipe -Path .\search-proof.browser.recipe.json

Browser recipe recording redacts values entered into selectors that look sensitive, such as password, token, SAML, MFA, OTP, or secret fields. The recipe step keeps `ValueRedacted`, `ValueRedactionReason`, and `ValueVariable` so you can see that a value was intentionally omitted and provide it at replay time. Keep real credentials in a vault or provide them at runtime instead of committing them into recipe JSON.

```powershell
$validation = Test-HtmlBrowserRecipe -Path .\search-proof.browser.recipe.json
$validation.RequiredVariables
$validation.VariableTemplate
$validation.BlockingIssues | Format-Table Severity, StepIndex, Action, Property, Message, SuggestedFix, SuggestedCommand -AutoSize

Test-HtmlBrowserRecipe -Path .\search-proof.browser.recipe.json -StrictPreflight -ThrowOnFailure

$reviewSession = Start-HtmlBrowserSession -Url 'https://example.com/search' -Scenario SinglePageApp
$review = Optimize-HtmlBrowserRecipe -Session $reviewSession -Path .\search-proof.browser.recipe.json -OutPath .\search-proof.browser.hardened.recipe.json -ReportPath .\search-proof.browser.hardening.json
$review.Steps | Where-Object Changed | Select-Object StepIndex, Action, AddedAlternates, Reason
Close-HtmlBrowserSession -Session $reviewSession

$secret = Read-Host -AsSecureString -Prompt 'Portal password'
Invoke-HtmlBrowserRecipe -Path .\search-proof.browser.recipe.json -VariablePath .\search-proof.browser.variables.json -Variable @{ password = $secret }
```

`Test-HtmlBrowserRecipe` returns CI-friendly fields such as `Passed`, `BlockingIssues`, `BlockingIssueCount`, `RecommendedExitCode`, and `Summary`. Each issue includes `SuggestedFix`, `SuggestedCommand`, and `DocumentationHint`, so the next repair step is visible without opening DevTools or guessing which browser cmdlet applies. Use `-StrictPreflight` when CI or a scheduled run should also block on warnings such as `ContinueOnError`, long fixed waits, or sensitive selectors, and add `-ThrowOnFailure` when the validation command itself should fail the job. `Invoke-HtmlBrowserRecipe` also preflights before opening a browser. Missing runtime variables, empty steps, invalid timeouts, and missing selectors return a failed recipe run result with `SkippedBeforeExecution`, `Validation`, `FailureSummary`, and `SuggestedCommand` populated, so scheduled jobs can fail fast without launching Chromium. Use `-SkipPreflight` only when you intentionally want to reproduce the old replay-time failure path.

# Visible enterprise login with persistent browser profile and evidence output
$browserProfilePath = Join-Path $PWD 'browser-profile.json'
$userDataPath = Join-Path $PWD 'browser-user-data'
New-HtmlBrowserProfile -Name 'EnterpriseLogin' -Scenario LoginProtected -Path $browserProfilePath -UserDataDirectory $userDataPath
$loginSession = Start-HtmlBrowserSession -Url 'https://example.com/protected' -ProfilePath $browserProfilePath -ManualLogin -LoginSuccessSelector 'main'
Export-HtmlBrowserState -Session $loginSession -Path (Join-Path $PWD 'browser-state.json')
Export-HtmlBrowserEvidence -Session $loginSession -OutFolder (Join-Path $PWD 'evidence\login') -NetworkSummary -SsoHandoffSummary
Close-HtmlBrowserSession -Session $loginSession

# Pause an enterprise SSO handoff form long enough to inspect it safely
$ssoSession = Start-HtmlBrowserSession -Url 'https://example.com/protected' -Scenario LoginProtected -Visible -ManualLogin -PreventSsoAutoSubmit
try {
    Get-HtmlBrowserSsoHandoff -Session $ssoSession -Wait -Timeout 60000
    Get-HtmlBrowserSsoHandoff -Session $ssoSession -Analyze
    Export-HtmlBrowserEvidence -Session $ssoSession -OutFolder (Join-Path $PWD 'evidence\sso') -SsoHandoffSummary
    Get-HtmlBrowserSsoHandoff -Session $ssoSession -IncludeSensitiveValues
} finally {
    Close-HtmlBrowserSession -Session $ssoSession
}

# Ask the planner which browser/content profile to use
$plan = Test-HtmlExtractionPlan -Url 'https://example.com/app'
$plan.SuggestedProfileCommand
$plan | Get-HtmlExtractionProfile
```

Run [Examples/Example-BrowserExtractionModeLocal.ps1](../Examples/Example-BrowserExtractionModeLocal.ps1) for an offline, self-contained version of the story. It stubs a local page and API with `Register-HtmlRoute`, then runs:

```powershell
Start-HtmlBrowserSession
Invoke-HtmlNavigation
Close-HtmlBrowserOverlay
Set-HtmlBrowserInput -Type
Invoke-HtmlBrowserKey
Invoke-HtmlBrowserClick -Text
Wait-HtmlBrowserReady
Wait-HtmlBrowserContent
Get-HtmlBrowserElement
Test-HtmlBrowserElement
Get-HtmlBrowserActiveElement
Get-HtmlBrowserStorage
Set-HtmlBrowserStorage
Save-HtmlBrowserContent
Export-HtmlBrowserEvidence
Get-HtmlBrowserDiagnostics
Compare-HtmlStaticRendered
Invoke-HtmlPageWorkbench -RenderedSnapshot
Export-HtmlBrowserState
```

Run [Examples/Example-BrowserEvidenceProofPack.ps1](../Examples/Example-BrowserEvidenceProofPack.ps1) for a self-contained proof-pack workflow with a reusable browser profile, persistent user-data directory, readiness wait, masked screenshot evidence, and evidence manifest.

Run [Examples/Example-BrowserManualLogin.ps1](../Examples/Example-BrowserManualLogin.ps1) for a visible enterprise login workflow that reuses a persistent profile, waits for a post-login selector, exports browser state, and writes an evidence pack.

Run [Examples/Example-BrowserSsoHandoff.ps1](../Examples/Example-BrowserSsoHandoff.ps1) for an offline SAML handoff workflow. It waits for a delayed auto-submitting handoff form, reports redacted field values by default, and demonstrates the explicit reveal switch without using a real tenant.

Run [Examples/Example-BrowserNetworkToRecipe.ps1](../Examples/Example-BrowserNetworkToRecipe.ps1) for an offline network-to-recipe workflow. It observes a browser fetch, captures the JSON body, turns it into a browserless data source, exports a recipe, and replays the recipe without navigating the page again.

Run [Examples/Example-BrowserRecipeRecording.ps1](../Examples/Example-BrowserRecipeRecording.ps1) for an offline recorder workflow. It performs normal HtmlTinkerX session actions, redacts a password-like recorded input, records a minimal HTML/text evidence pack, saves the successful steps as a browser recipe, then replays that recipe in a fresh browser session with the same evidence artifact choices.

Run [Examples/Example-BrowserFailureEvidenceAndLocators.ps1](../Examples/Example-BrowserFailureEvidenceAndLocators.ps1) for an offline locator-discovery and failure-evidence workflow. It ranks locator candidates, uses the best candidate for a click, then intentionally fails a wait with `-OnFailureEvidence` so the screenshot, HTML, text, network summary, failure context, and manifest can be inspected.

Run [Examples/Example-BrowserRecipe.ps1](../Examples/Example-BrowserRecipe.ps1) for an offline browser recipe workflow. It writes a JSON recipe, replays it with `Invoke-HtmlBrowserRecipe`, records locator candidates in the step results, and exports an evidence manifest.

The matching C# example is [Sources/HtmlTinkerX.Examples/BrowserExtractionModeExample.cs](../Sources/HtmlTinkerX.Examples/BrowserExtractionModeExample.cs). It follows the same sequence with `HtmlBrowser.OpenSessionAsync`, `RegisterRouteAsync`, `TypeInputAsync`, `PressKeysAsync`, `WaitForTextAsync`, `WaitForElementStateAsync`, `GetElementsAsync`, `TestElementAsync`, `GetActiveElementAsync`, `GetStorageAsync`, `SetStorageAsync`, `SaveContentAsync`, `GetDiagnosticsAsync`, `CreateSnapshotAsync`, `HtmlPageWorkbench.AnalyzeAsync`, and `ExportBrowserStateAsync`.

Diagnostics are intentionally diagnostics, not stealth. `Get-HtmlBrowserDiagnostics` reports browser/runtime consistency, storage keys, failed or blocked requests, observed API calls, WebSockets, and console errors so you can understand why extraction did or did not work. It does not try to hide automation, defeat access controls, or bypass site protections.

Browser extraction should stay browser-last where possible. Prefer `Test-HtmlExtractionPlan`, static parsers, `Find-HtmlDataSource`, `Invoke-HtmlDataExtraction`, endpoint discovery, linked JavaScript inspection, crawl profiles, and static-vs-rendered comparison before adding click-through automation.

See [Examples/Example-BrowserExtractionMode.ps1](../Examples/Example-BrowserExtractionMode.ps1) for longer workflow samples covering product search, lazy-loaded pages, app-shell snapshots, login state reuse, network capture, diagnostics, and docs crawling.

#### Browserless Extraction Mode

Browserless extraction mode is the Playwright-free path for pages where useful data already exists in static HTML, embedded framework state, JSON-LD, script data, or low-risk API endpoints. It does not imitate a browser; it inspects legitimate page artifacts and only performs direct HTTP reads when the source is a low-risk GET candidate or the caller explicitly opts in.

```powershell
$sources = Find-HtmlDataSource -Url 'https://example.com/products' -IncludeLinkedScripts -DirectOnly

$result = $sources |
    Where-Object Kind -In 'AppState', 'JsonLd', 'ScriptData' |
    Select-Object -First 1 |
    Invoke-HtmlDataExtraction

$result.Mode
$result.Source.Kind
$result.Items | Select-Object Name, Type, Path, Value

$source = $sources | Select-Object -First 1
$source | Export-HtmlExtractionRecipe -Path .\product.recipe.json -IncludeRawContent
Import-HtmlExtractionRecipe -Path .\product.recipe.json | Invoke-HtmlExtractionRecipe
```

Saved recipes can compare fresh output with a structure you have inspected and accepted. For DOM recipes,
pass `-BaselineContent $acceptedHtml` to `Export-HtmlExtractionRecipe`. For a structured-data source, pass
`-AcceptedResult $result` after its extraction succeeds. Baselines contain field names, value kinds and
source paths; they do not add extracted values or raw responses to the recipe.

`Invoke-HtmlExtractionRecipe` returns `DriftReport` when comparison runs. Changed item or JSON response structure, or lower
DOM collection confidence makes `Success` false while preserving the extracted records for inspection.
Value changes and record order do not cause drift. Count changes are reported; the declared minimum and
maximum counts decide whether they are allowed. A later run never silently replaces the accepted baseline.
If current data exceeds the comparison limits (256 distinct item shapes, 4096 shape nodes or depth 32),
the result preserves its records and reports `DriftReport.ComparisonError` with `Success` false.

Endpoint sources are guarded by default:

```powershell
$api = Find-HtmlDataSource -Url 'https://example.com/products' -IncludeLinkedScripts |
    Where-Object { $_.Kind -eq 'ApiEndpoint' -and $_.CanExtractDirectly } |
    Select-Object -First 1

$api | Invoke-HtmlDataExtraction -AllowHttpFetch
```

The matching C# story uses the same core:

```csharp
var sources = await HtmlBrowserlessExtraction.DiscoverAsync(
    html,
    new HtmlBrowserlessDiscoveryOptions {
        BaseUri = new Uri("https://example.com/products"),
        IncludeLinkedScripts = true,
        DirectOnly = true
    });

var source = sources.First();
var result = await HtmlBrowserlessExtraction.ExtractAsync(source);
var recipe = HtmlBrowserlessExtraction.CreateRecipe(source, includeRawContent: true);
var replayed = await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe);
```

Run [Examples/Example-BrowserlessExtraction.ps1](../Examples/Example-BrowserlessExtraction.ps1) or the C# [BrowserlessExtractionExample](../Sources/HtmlTinkerX.Examples/BrowserlessExtractionExample.cs) for a self-contained app-state extraction and recipe round trip.

### Screenshots & Media
- **Save-HtmlBrowserScreenshot** - Capture page screenshots with advanced options, including explicit sensitive-field masking
- **Save-HtmlBrowserPdf** - Generate PDFs from rendered pages
- **Start-HtmlBrowserVideoCapture** / **Stop-HtmlBrowserVideoCapture** - Record browser sessions

### Network & Debugging
- **Get-HtmlBrowserNetworkLog** - View captured network requests and responses
- **Get-HtmlBrowserConsoleLog** - Retrieve browser console messages
- **Export-HtmlBrowserHar** - Export network traffic to HAR files
- **Start-HtmlBrowserTracing** / **Stop-HtmlBrowserTracing** - Record Playwright traces
- **Register-HtmlRoute** / **Unregister-HtmlRoute** - Intercept and mock requests
- **Test-HtmlBrowser** - Comprehensive browser testing for errors, performance, and resources
- **Clear-HtmlBrowserCache** - Clean Playwright browser downloads

### Cookies & State Management
- **Get-HtmlBrowserCookie** - Retrieve cookies from sessions
- **Set-HtmlBrowserCookie** - Add cookies to sessions
- **New-HtmlBrowserCookie** - Create cookie objects
- **Export-HtmlBrowserState** / **Import-HtmlBrowserState** - Save/restore browser state
- **Export-HtmlBrowserSession** / **Import-HtmlBrowserSession** - Session state management

### Content & Resources
- **Get-HTMLResource** - Extract script and CSS resources
- **Invoke-HTMLCrawl** - Crawl sites offline with optional browser rendering
: supports sitemap discovery, `robots.txt`, filtering, auth, and selector-based extraction
- **Save-HtmlBrowserAttachment** - Download files from pages
- **Get-HtmlBrowserContent** - Retrieve page content
- **Get-HtmlBrowserFormField** - Extract form field information
- **Get-HtmlBrowserLoginForm** - Detect login forms
- **Export-HTMLOutline** - Generate document outlines
- **Show-HtmlBrowserHar** - Visualize HAR files
- **Compare-HTML** - Compare HTML documents
- **Measure-HTMLDocument** - Analyze document metrics

### JavaScript AST parsing

HtmlTinkerX currently uses Jint 4.x, which uses Acornima for JavaScript parsing. Older Jint 3.x builds used Esprima, so older examples that referenced `[Esprima.JavaScriptParser]` should be translated to the Acornima surface:

```powershell
$ast = ConvertFrom-JavaScriptAst -Content 'const settings = { apiKey: "abc" };'
$ast | Select-JavaScriptAstNode -Type ObjectExpression
$ast | Select-JavaScriptVariable -Name api -Contains

$config = Select-JavaScriptVariable -Source '$Config = { sCtx: "abc", enabled: !0 }' -Name '$Config'
$config.Value['sCtx']

Select-JavaScriptVariable -Source 'window.$Config = { auth: { sCtx: "abc" } }' -Name '$Config' -PropertyPath auth.sCtx

Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath auth.sCtx
```

### CSS and HTML workflow audits

Use the CSS query cmdlets to inspect generated stylesheets for theme tokens, declarations, asset URLs, media-query overrides, and selector specificity:

```powershell
$css = @'
:root { --brand-color: #0369a1; }
.btn { color: var(--brand-color); background-image: url("/img/button.png"); }
@media (min-width: 40rem) {
    .btn.primary { color: white !important; }
}
'@

Select-CssRule -Content $css -Selector '.btn'
Select-CssDeclaration -Content $css -Property color
Get-CssVariable -Content $css -Name '--brand-color'
ConvertFrom-CssUrl -Content $css -BaseUrl 'https://example.org/app/'
Measure-CssSpecificity -Selector '#app .btn:hover'
```

Use the HTML workflow bridges to inspect scripts, page assets, and common compatibility issues without launching a browser:

```powershell
$html = @'
<link rel="stylesheet" href="/css/site.css">
<link rel="manifest" href="/site.webmanifest">
<script type="application/ld+json">{"name":"schema"}</script>
<script type="module" src="/app/module.js"></script>
<img id="logo" src="/img/logo.png">
<label for="missing"></label>
<input id="email" name="email">
'@

Select-HtmlScript -Content $html -BaseUrl 'https://example.org/' -JavaScript
Select-HtmlAsset -Content $html -BaseUrl 'https://example.org/'
Measure-HtmlCompatibility -Content $html -BaseUrl 'https://example.org/'
```

### React Server Component payload extraction

Next.js can inline React Flight payload instructions in scripts that push data into `self.__next_f`. Use `ConvertFrom-HtmlRscPayload` to inspect that server-rendered app state through stable HtmlTinkerX objects without exposing framework dependency types:

```powershell
$rows = ConvertFrom-HtmlRscPayload -Content $html
$rows | Where-Object IsJson | Select-Object Id, Tag, Kind, Data

$payloads = ConvertFrom-HtmlRscPayload -Content $html -RawPayload
$document = ConvertFrom-HtmlRscPayload -Content $html -AsDocument
```

This is a static extractor. It does not hydrate React, execute application JavaScript, or resolve client module references.

### Modern page parsing helpers

Several cmdlets expose stable HtmlTinkerX models for common data embedded in modern pages:

```powershell
$jsonLd = ConvertFrom-HtmlJsonLd -Content $html
$scriptData = ConvertFrom-HtmlScriptData -Content $html
$appState = ConvertFrom-HtmlAppState -Content $html
$headLinks = ConvertFrom-HtmlHeadLink -Content $html -BaseUrl https://example.org/
$images = ConvertFrom-HtmlImageCandidate -Content $html -BaseUrl https://example.org/
$tokens = Select-HtmlToken -Content $html
$rows = ConvertFrom-HtmlRscPayload -Content $html
$endpoints = ConvertFrom-JavaScriptEndpoint -Content $html -Html
$linkedEndpoints = ConvertFrom-HtmlLinkedJavaScriptEndpoint -Url https://example.org/
$robotsRules = ConvertFrom-RobotsTxt -Content $robots -BaseUrl https://example.org/robots.txt
$manifest = ConvertFrom-WebManifest -Content $manifestJson -BaseUrl https://example.org/manifest.webmanifest
$security = ConvertFrom-WellKnownText -Content $securityTxt -Kind SecurityTxt -BaseUrl https://example.org/.well-known/security.txt

$tokens | Where-Object Source -in 'Input', 'Meta'
$endpoints | Where-Object Method -eq POST
```

The same parsing layer is available from C# through HtmlTinkerX:

```csharp
var jsonLd = HtmlJsonLdParser.Parse(html);
var scriptData = HtmlScriptDataParser.Parse(html);
var appState = HtmlAppStateParser.Parse(html);
var headLinks = HtmlHeadLinkParser.Parse(html, new Uri("https://example.org/"));
var images = HtmlImageCandidateParser.Parse(html, new Uri("https://example.org/"));
var tokens = HtmlTokenParser.Parse(html);
var reactFlight = HtmlReactFlightParser.Parse(html);
var endpoints = HtmlJavaScriptEndpointParser.ParseHtml(html);
var linkedEndpoints = await HtmlLinkedJavaScriptEndpointParser.ParseUrlAsync("https://example.org/");
var robotsRules = HtmlRobotsParser.Parse(robots, new Uri("https://example.org/robots.txt"));
var manifest = HtmlWebManifestParser.Parse(manifestJson, new Uri("https://example.org/manifest.webmanifest"));
var security = HtmlWellKnownParser.Parse(securityTxt, "security.txt", new Uri("https://example.org/.well-known/security.txt"));
```

Runnable examples are available in `Examples\Example-ModernParsing.ps1` and `Sources\HtmlTinkerX.Examples\ModernParsingExample.cs`.

These helpers are static parsers. They do not execute application JavaScript and they return PSParseHTML/HtmlTinkerX objects rather than exposed dependency implementation types.

For lower-level exploration, the packaged PowerShell module exposes a small dependency type accelerator surface through its AssemblyLoadContext boundary: common HtmlAgilityPack document/node types, core Acornima AST document types, and public dependency enums.

```powershell
$script = ConvertFrom-JavaScriptAst -Content 'const answer = 42;'
$script | Select-JavaScriptAstNode -Type VariableDeclaration
$script | Select-JavaScriptAstNode -Type Script -IncludeRoot
[HtmlAgilityPack.HtmlNodeType]::Element
```
