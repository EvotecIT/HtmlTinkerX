[CmdletBinding()]
param(
    [string] $StatePath = (Join-Path $PSScriptRoot 'Output\browser-extraction-state.json')
)

Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$storyUrl = 'https://psparsehtml.local/local-extraction.html'
$staticHtml = @'
<!doctype html>
<html>
<head>
<title>Browser extraction local story</title>
<script>
window.__APP_CONFIG__ = { apiBase: "/api", feature: "browser-extraction" };
document.addEventListener("DOMContentLoaded", () => {
  const root = document.getElementById("root");
  root.innerHTML = `
<button id="cookieBanner" onclick="this.remove()">Accept</button>
<main>
<h1>Search demo</h1>
<form id="searchForm">
<input id="search" name="q" type="search" autocomplete="off">
<button type="submit">Search</button>
</form>
<button id="loadMore" type="button">Load more</button>
<section id="results">No results yet.</section>
</main>`;
  localStorage.setItem("storyLocal", "1");
  sessionStorage.setItem("storySession", "1");
  document.getElementById("searchForm").addEventListener("submit", async (event) => {
    event.preventDefault();
    const query = document.getElementById("search").value;
    const response = await fetch("/api/products?q=" + encodeURIComponent(query));
    const data = await response.json();
    document.getElementById("results").innerHTML = data.items
      .map(item => `<article class="product"><h2>${item.name}</h2><p>${item.description}</p></article>`)
      .join("");
  });
  document.getElementById("loadMore").addEventListener("click", () => {
    document.getElementById("results").insertAdjacentHTML(
      "beforeend",
      '<article class="product"><h2>Workbench profile sample</h2><p>Added after a click.</p></article>');
  });
});
</script>
</head>
<body>
<div id="root">Loading...</div>
</body>
</html>
'@

$session = Start-HtmlBrowserSession -Url 'about:blank'
try {
    Register-HtmlRoute -Session $session -Pattern '**/local-extraction.html' -ScriptBlock {
        param($route)
        $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
            Status      = 200
            ContentType = 'text/html'
            Body        = $staticHtml
        })
    } | Out-Null

    Register-HtmlRoute -Session $session -Pattern '**/api/products**' -ScriptBlock {
        param($route)
        $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
            Status      = 200
            ContentType = 'application/json'
            Body        = '{"items":[{"name":"Found HtmlTinkerX guide","description":"Rendered from a local API call."}],"token":"local-secret-value"}'
        })
    } | Out-Null

    $extractionPlan = Test-HtmlExtractionPlan -Content $staticHtml
    $extractionProfile = $extractionPlan | Get-HtmlExtractionProfile

    Invoke-HtmlBrowserNavigation -Session $session -Url $storyUrl -LoadState DomContentLoaded
    Close-HtmlBrowserOverlay -Session $session | Out-Null
    Set-HtmlBrowserInput -Session $session -Selector '#search' -Value 'HtmlTinkerX' -Type -DelayMs 0
    Invoke-HtmlBrowserKey -Session $session -Selector '#search' -Key 'Enter'
    Wait-HtmlBrowserReady -Session $session -Selector '#results' -Stable -StableMilliseconds 100 -PollMilliseconds 25
    Wait-HtmlBrowserContent -Session $session -Text 'Found HtmlTinkerX guide' -Selector '#results' -Exact
    Invoke-HtmlBrowserClick -Session $session -Text 'Load more' -Exact
    Wait-HtmlBrowserContent -Session $session -Text 'Workbench profile sample' -Selector '#results' -Exact
    Wait-HtmlBrowserContent -Session $session -Element -Selector '#results' -Visible -InViewport

    $resultElements = Get-HtmlBrowserElement -Session $session -Selector '.product' -VisibleOnly -IncludeAttributes
    $isResultsVisible = Test-HtmlBrowserElement -Session $session -Selector '#results' -Visible -InViewport
    Invoke-HtmlBrowserClick -Session $session -Selector '#search'
    $activeElement = Get-HtmlBrowserActiveElement -Session $session -IncludeAttributes
    Set-HtmlBrowserStorage -Session $session -Scope Local -Key storyMode -Value browser-extraction
    $storage = Get-HtmlBrowserStorage -Session $session -Scope All
    $diagnostics = Get-HtmlBrowserDiagnostics -Session $session
    $renderedHtml = Get-HtmlBrowserContent -Session $session
    $renderedText = Get-HtmlBrowserContent -Session $session -AsText
    $comparison = Compare-HtmlStaticRendered -StaticContent $staticHtml -RenderedContent $renderedHtml -BaseUrl $storyUrl
    $savedContentPath = Join-Path (Split-Path -Parent $StatePath) 'browser-extraction-results.html'
    Save-HtmlBrowserContent -Session $session -Selector '#results' -OutFile $savedContentPath -PassThru | Out-Null
    $evidencePath = Join-Path (Split-Path -Parent $StatePath) 'browser-extraction-evidence'
    $evidence = Export-HtmlBrowserEvidence -Session $session -OutFolder $evidencePath -BaseFileName results -NetworkSummary

    $snapshot = [HtmlTinkerX.HtmlRenderedPageSnapshot]::new()
    $snapshot.Url = $storyUrl
    $snapshot.FinalUrl = $diagnostics.Url
    $snapshot.Title = $diagnostics.Title
    $snapshot.Html = $renderedHtml
    $snapshot.Text = $renderedText
    $snapshot.Content = $renderedHtml
    $snapshot.ContentKind = 'DocumentHtml'
    $snapshot.StaticRenderedComparison = $comparison
    $snapshot.NetworkLog = [HtmlTinkerX.HtmlBrowser]::GetNetworkLog($session)

    $workbench = Invoke-HtmlPageWorkbench -Content $staticHtml -BaseUrl $storyUrl -RenderedSnapshot $snapshot

    $stateDirectory = Split-Path -Parent $StatePath
    if ($stateDirectory -and -not (Test-Path -LiteralPath $stateDirectory)) {
        New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    }
    Export-HtmlBrowserState -Session $session -Path $StatePath

    [pscustomobject] @{
        PlannerMode              = $extractionPlan.RecommendedMode.ToString()
        ProfileName              = $extractionProfile.Name
        RenderProfile            = $extractionProfile.RenderProfile.ToString()
        ResultText               = $renderedText
        ProductElementCount      = @($resultElements).Count
        ResultsVisible           = $isResultsVisible
        ActiveElementId          = $activeElement.Id
        StorageKeys              = @($storage).Key
        WorkbenchMode            = $workbench.AnalysisMode
        WorkbenchTitle           = $workbench.Title
        ObservedApiCallCount     = @($diagnostics.ObservedApiCalls).Count
        LocalStorageKeys         = $diagnostics.LocalStorageKeys
        StaticRenderedDeltaCount = @($comparison.Deltas).Count
        SavedContentPath         = $savedContentPath
        EvidencePath             = $evidence.OutFolder
        EvidenceArtifactCount    = @($evidence.Artifacts).Count
        StatePath                = $StatePath
    }
} finally {
    Close-HtmlBrowserSession -Session $session
}
