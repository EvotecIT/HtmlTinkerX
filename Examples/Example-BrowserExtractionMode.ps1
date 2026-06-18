Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

# Browser extraction mode groups the browser-backed commands into repeatable workflows.
# Replace example URLs and selectors with the target site's real controls.

# Product/search workflow: dismiss overlays, type like a user, submit, then extract rendered text.
$session = Start-HtmlBrowserSession -Url 'https://example.com/search'
try {
    Close-HtmlBrowserOverlay -Session $session | Out-Null
    Set-HtmlBrowserInput -Session $session -Selector 'input[type=search]' -Value 'HtmlTinkerX' -Type -DelayMs 25
    Invoke-HtmlBrowserKey -Session $session -Selector 'input[type=search]' -Key 'Enter'
    Wait-HtmlBrowserReady -Session $session -Selector 'main' -Stable
    Wait-HtmlBrowserContent -Session $session -Text 'Results' -Selector 'main'
    Get-HtmlBrowserContent -Session $session -Selector 'main' -AsText
} finally {
    Close-HtmlBrowserSession -Session $session
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
$login = Start-HtmlBrowserSession -Url 'https://example.com/login' -Visible
try {
    Set-HtmlBrowserInput -Session $login -Selector '#username' -Value 'user@example.com' -Type -DelayMs 20
    Set-HtmlBrowserInput -Session $login -Selector '#password' -Value 'REPLACE_WITH_SECRET' -Type -DelayMs 20
    Invoke-HtmlBrowserClick -Session $login -Selector 'button[type=submit]'
    Wait-HtmlBrowserContent -Session $login -Text 'Dashboard' -Selector 'body'
    Export-HtmlBrowserState -Session $login -Path "$PSScriptRoot\Output\browser-state.json"
} finally {
    Close-HtmlBrowserSession -Session $login
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
$diagSession = Start-HtmlBrowserSession -Url 'https://example.com/app'
try {
    Wait-HtmlBrowserReady -Session $diagSession -Stable -StableMilliseconds 500
    $diagnostics = Get-HtmlBrowserDiagnostics -Session $diagSession
    $diagnostics.ConsistencyWarnings
    $diagnostics.ObservedApiCalls
    $diagnostics.FailedRequests
} finally {
    Close-HtmlBrowserSession -Session $diagSession
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
