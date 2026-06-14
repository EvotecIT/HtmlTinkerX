Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$htmlPath = Join-Path $env:TEMP 'psparsehtml-rendered-snapshot.html'
@'
<!doctype html>
<html>
<head>
    <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"event":"demo"}}}</script>
    <script>
        window.__CONFIG__ = { api: { baseUrl: "/api" } };
        window.renderReady = false;
        document.addEventListener('DOMContentLoaded', () => {
            setTimeout(() => {
                document.querySelector('main').textContent = 'Loaded from client-side JavaScript';
                window.renderReady = true;
            }, 100);
            fetch("/api/events", { method: "POST" });
        });
    </script>
</head>
<body>
    <main>Loading...</main>
    <form id="checkout" method="post" action="/checkout">
        <input type="hidden" name="csrf" value="demo-token" />
    </form>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8

# Use DOMContentLoaded or Commit for pages that keep background requests open.
# Then wait for a page-specific selector or JavaScript predicate before extracting.
$snapshot = Invoke-HTMLRendering `
    -Path $htmlPath `
    -RenderProfile HeavyDynamicPage `
    -WaitForFunction '() => window.renderReady === true' `
    -BlockResourcePattern '**/analytics/**' `
    -Selector 'main' `
    -AsText `
    -Snapshot `
    -IncludeStaticRenderedComparison `
    -IncludeLinkedScripts `
    -IncludeResponseBody `
    -ResponseBodyMaxBytes 32768

Write-Output "Rendered content:"
Write-Output $snapshot.Content

Write-Output "`nReadable text:"
Write-Output $snapshot.ReadableText.Text

Write-Output "`nMarkdown:"
Write-Output $snapshot.Markdown

Write-Output "`nApp state:"
$snapshot.AppState |
    Select-Object Name, Framework, SourceKind |
    Format-Table -AutoSize |
    Out-String

Write-Output "Script data:"
$snapshot.ScriptData |
    Select-Object Id, Type, IsJson |
    Format-Table -AutoSize |
    Out-String

Write-Output "Inline JavaScript endpoints:"
$snapshot.JavaScriptEndpoints |
    Select-Object Method, Url, Client, OperationName |
    Format-Table -AutoSize |
    Out-String

Write-Output "Linked JavaScript endpoints:"
$snapshot.LinkedJavaScriptEndpoints |
    Select-Object Method, Url, ScriptUrl, IsExternal, IsDownloaded |
    Format-Table -AutoSize |
    Out-String

Write-Output "Static vs rendered:"
$snapshot.StaticRenderedComparison |
    Select-Object StaticHtmlLength, RenderedHtmlLength, StaticLinkCount, RenderedLinkCount, StaticFormCount, RenderedFormCount |
    Format-List |
    Out-String

Write-Output "Normalized data:"
$snapshot.Data |
    Select-Object Kind, Name, Type, Source -First 8 |
    Format-Table -AutoSize |
    Out-String

Write-Output "JavaScript config:"
$snapshot.JavaScriptConfig |
    Select-Object Name, Path, PropertyPath, Value |
    Format-Table -AutoSize |
    Out-String

Write-Output "Interaction surface:"
$snapshot.InteractionSurface |
    Select-Object Kind, Name, Method, Url, Source |
    Format-Table -AutoSize |
    Out-String

# Network capture is opt-in because request and response headers can contain sensitive values.
$networkSnapshot = Invoke-HTMLRendering `
    -Path $htmlPath `
    -LoadState Commit `
    -WaitForFunction '() => window.renderReady === true' `
    -Snapshot `
    -IncludeNetworkLog

$networkSnapshot.NetworkLog |
    Where-Object ResourceType -in 'Fetch', 'XHR' |
    Select-Object Method, Status, ResourceType, Url
