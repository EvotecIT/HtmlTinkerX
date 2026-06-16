Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

# Browser extraction mode groups the browser-backed commands into repeatable workflows.
# Replace example URLs and selectors with the target site's real controls.

# Product/search workflow: dismiss overlays, type like a user, submit, then extract rendered text.
$session = Start-HtmlSession -Url 'https://example.com/search' -Session
try {
    Invoke-HtmlOverlayDismissal -Session $session | Out-Null
    Set-HtmlInput -Session $session -Selector 'input[type=search]' -Value 'HtmlTinkerX' -Type -DelayMs 25
    Invoke-HtmlKey -Session $session -Selector 'input[type=search]' -Key 'Enter'
    Wait-HtmlContent -Session $session -Text 'Results' -Selector 'main'
    Get-HtmlContent -Session $session -Selector 'main' -AsText
} finally {
    Close-HtmlSession -Session $session
}

# Lazy-loaded listing workflow.
$catalog = Invoke-HtmlRendering -Url 'https://example.com/catalog' `
    -RenderProfile LazyLoadedContent `
    -WaitForSelector '.product-card' `
    -Selector '.product-grid' `
    -Snapshot

$catalog.InteractionSurface

# App-shell snapshot with static-vs-rendered comparison and linked-script endpoint hints.
$app = Invoke-HtmlRendering -Url 'https://example.com/app' `
    -RenderProfile AppShell `
    -WaitForSelector 'main' `
    -Selector 'main' `
    -Snapshot `
    -IncludeLinkedScripts `
    -IncludeStaticRenderedComparison

$app.StaticRenderedComparison
$app.LinkedJavaScriptEndpoints

# Login reuse workflow: export storage state once, then use it in later sessions.
$login = Start-HtmlSession -Url 'https://example.com/login' -Session -Visible
try {
    Set-HtmlInput -Session $login -Selector '#username' -Value 'user@example.com' -Type -DelayMs 20
    Set-HtmlInput -Session $login -Selector '#password' -Value 'REPLACE_WITH_SECRET' -Type -DelayMs 20
    Invoke-HtmlClick -Session $login -Selector 'button[type=submit]'
    Wait-HtmlContent -Session $login -Text 'Dashboard' -Selector 'body'
    Export-BrowserState -Session $login -Path "$PSScriptRoot\Output\browser-state.json"
} finally {
    Close-HtmlSession -Session $login
}

$dashboard = Invoke-HtmlRendering -Url 'https://example.com/dashboard' `
    -StorageStatePath "$PSScriptRoot\Output\browser-state.json" `
    -RenderProfile LoginProtected `
    -WaitForSelector 'main' `
    -Selector 'main' `
    -Snapshot

$dashboard.Title

# Network diagnostics workflow.
$network = Invoke-HtmlRendering -Url 'https://example.com/search' `
    -RenderProfile NetworkCapture `
    -WaitForSelector '#results' `
    -Snapshot `
    -IncludeNetworkLog `
    -IncludeResponseBody `
    -RedactResponseBody `
    -ResponseBodyResourceType XHR, Fetch `
    -ResponseBodyMaxBytes 32768

$network.NetworkLog | Where-Object ResourceType -In 'XHR', 'Fetch'

# One-page diagnostics from an active session.
$diagSession = Start-HtmlSession -Url 'https://example.com/app' -Session
try {
    Wait-HtmlContent -Session $diagSession -Stable -StableMilliseconds 500
    $diagnostics = Get-HtmlDiagnostics -Session $diagSession
    $diagnostics.ConsistencyWarnings
    $diagnostics.ObservedApiCalls
    $diagnostics.FailedRequests
} finally {
    Close-HtmlSession -Session $diagSession
}

# Planner-to-profile workflow.
$plan = Test-HtmlExtractionPlan -Url 'https://example.com/app'
$plan.SuggestedProfileCommand
$plan | Get-HtmlExtractionProfile

# Docs crawl workflow for offline/LLM-ready output.
Invoke-HtmlCrawl -Url 'https://example.com/docs' `
    -Scenario Dataset `
    -Profile docs-content `
    -IncludeMarkdown `
    -IncludeStructuredJson `
    -OutPath "$PSScriptRoot\Output\docs-crawl"
