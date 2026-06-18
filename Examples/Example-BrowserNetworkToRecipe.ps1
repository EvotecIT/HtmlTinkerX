[CmdletBinding()]
param(
    [string] $RecipePath = (Join-Path $PSScriptRoot 'Output\observed-api.recipe.json')
)

Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$recipeDirectory = Split-Path -Parent $RecipePath
if ($recipeDirectory -and -not (Test-Path -LiteralPath $recipeDirectory)) {
    New-Item -ItemType Directory -Path $recipeDirectory -Force | Out-Null
}

$session = Start-HtmlBrowserSession -Url 'about:blank' -Scenario NetworkCapture
try {
    Register-HtmlRoute -Session $session -Pattern '**/orders.html' -ScriptBlock {
        param($route)
        $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
            Status      = 200
            ContentType = 'text/html'
            Body        = @'
<!doctype html>
<html>
<body>
<main id="status">loading</main>
<script>
fetch('/api/orders', {
  headers: {
    'Accept': 'application/json',
    'X-Requested-With': 'XMLHttpRequest',
    'X-CSRF-Token': 'example-secret-token'
  }
})
  .then(response => response.json())
  .then(data => {
    document.getElementById('status').textContent = data[0].name;
  });
</script>
</body>
</html>
'@
        }) | Out-Null
    } | Out-Null

    Register-HtmlRoute -Session $session -Pattern '**/api/orders' -ScriptBlock {
        param($route)
        $request = $route.Request
        if (-not $request.Headers.ContainsKey('x-csrf-token')) {
            $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
                Status      = 403
                ContentType = 'application/json'
                Body        = '{"error":"missing csrf"}'
            }) | Out-Null
            return
        }

        $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
            Status      = 200
            ContentType = 'application/json'
            Body        = '[{"name":"Quarterly export confirmation","id":42},{"name":"Audit evidence","id":43}]'
        }) | Out-Null
    } | Out-Null

    Invoke-HtmlBrowserNavigation -Session $session -Url 'https://proof.local/orders.html' -LoadState DomContentLoaded
    Wait-HtmlBrowserContent -Session $session -Text 'Quarterly export confirmation' -Selector '#status' -Exact

    $null = [HtmlTinkerX.HtmlBrowser]::CaptureResponseBodiesAsync(
        $session,
        65536,
        [HtmlTinkerX.HtmlNetworkResourceType[]] @([HtmlTinkerX.HtmlNetworkResourceType]::Fetch, [HtmlTinkerX.HtmlNetworkResourceType]::XHR),
        [Threading.CancellationToken]::None,
        $true).GetAwaiter().GetResult()

    $source = Find-HtmlBrowserDataSource -Session $session -IncludeResponseBody |
        Where-Object Url -like '*/api/orders' |
        Select-Object -First 1

    $source | Export-HtmlExtractionRecipe -Path $RecipePath -IncludeRawContent
    $result = Import-HtmlExtractionRecipe -Path $RecipePath | Invoke-HtmlExtractionRecipe

    [pscustomobject] @{
        RecipePath = $RecipePath
        SourceKind = $source.Kind
        SourceUrl  = $source.Url
        ReplaySafeHeaders = $source.ReplayRequestHeaders
        SensitiveHeaders = $source.SensitiveRequestHeaderNames
        SuggestedReplayCommand = $source.SuggestedReplayCommand
        ItemCount  = @($result.Items).Count
        FirstItem  = $result.Items[0].Name
    }
} finally {
    Close-HtmlBrowserSession -Session $session
}
