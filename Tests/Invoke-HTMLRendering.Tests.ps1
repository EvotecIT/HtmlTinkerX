Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

function script:Get-AvailableTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Parse('127.0.0.1'), 0)
    $listener.Start()
    try {
        $endpoint = [Net.IPEndPoint]$listener.LocalEndpoint
        return $endpoint.Port
    } finally {
        $listener.Stop()
    }
}

function script:Start-SnapshotTestServer {
    param(
        [int] $Port,
        [int] $ExternalScriptPort = 0
    )

    if (-not ('SnapshotTestServer' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class SnapshotTestServer : IDisposable {
    private readonly TcpListener listener;
    private readonly int externalScriptPort;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private Task acceptLoop;
    private int assetRequestCount;

    public SnapshotTestServer(int port) {
        this.externalScriptPort = 0;
        listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
    }

    public SnapshotTestServer(int port, int externalScriptPort) {
        this.externalScriptPort = externalScriptPort;
        listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
    }

    public int AssetRequestCount {
        get {
            return Volatile.Read(ref assetRequestCount);
        }
    }

    public void Start() {
        listener.Start();
        acceptLoop = Task.Run(() => AcceptLoopAsync(cancellation.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            } catch (ObjectDisposedException) {
                break;
            } catch (SocketException) when (token.IsCancellationRequested) {
                break;
            }

            await HandleClientAsync(client).ConfigureAwait(false);
        }
    }

    private async Task HandleClientAsync(TcpClient client) {
        using (client) {
            NetworkStream stream = client.GetStream();
            using (StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true)) {
                string requestLine = await reader.ReadLineAsync().ConfigureAwait(false) ?? string.Empty;
                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false))) {
                    int separator = line.IndexOf(':');
                    if (separator > 0) {
                        headers[line.Substring(0, separator)] = line.Substring(separator + 1).Trim();
                    }
                }

                string[] parts = requestLine.Split(' ');
                string path = parts.Length > 1 ? parts[1] : "/";
                if (string.Equals(path, "/page", StringComparison.OrdinalIgnoreCase)) {
                    string scriptSrc = externalScriptPort > 0
                        ? "http://127.0.0.1:" + externalScriptPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/challenge.js"
                        : "/assets/app.js";
                    await WriteResponseAsync(
                        stream,
                        "text/html",
                        "<!doctype html><html><head><script src=\"" + scriptSrc + "\"></script></head><body><main>snapshot</main></body></html>",
                        "Set-Cookie: assetCookie=1; Path=/assets\r\n").ConfigureAwait(false);
                    return;
                }

                if (string.Equals(path, "/assets/app.js", StringComparison.OrdinalIgnoreCase)) {
                    Interlocked.Increment(ref assetRequestCount);
                    headers.TryGetValue("User-Agent", out string userAgent);
                    headers.TryGetValue("Cookie", out string cookie);
                    string body = string.Equals(userAgent, "SnapshotAgent", StringComparison.Ordinal)
                        && (cookie ?? string.Empty).IndexOf("assetCookie=1", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "fetch(\"/api/protected\", { method: \"POST\" });"
                        : "console.log(\"denied\");";
                    await WriteResponseAsync(stream, "application/javascript", body, null).ConfigureAwait(false);
                    return;
                }

                if (string.Equals(path, "/challenge.js", StringComparison.OrdinalIgnoreCase)) {
                    if (headers.ContainsKey("Authorization")) {
                        await WriteResponseAsync(stream, "application/javascript", "fetch(\"/api/leaked\", { method: \"POST\" });", null).ConfigureAwait(false);
                    } else {
                        await WriteResponseAsync(stream, "text/plain", "external credentials required", "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"external\"\r\n").ConfigureAwait(false);
                    }

                    return;
                }

                await WriteResponseAsync(stream, "text/plain", "not found", "HTTP/1.1 404 Not Found\r\n").ConfigureAwait(false);
            }
        }
    }

    private static async Task WriteResponseAsync(Stream stream, string contentType, string body, string extraHeaders) {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string status = extraHeaders != null && extraHeaders.StartsWith("HTTP/1.1", StringComparison.Ordinal)
            ? extraHeaders
            : "HTTP/1.1 200 OK\r\n" + (extraHeaders ?? string.Empty);
        string headers = status +
            "Content-Type: " + contentType + "\r\n" +
            "Content-Length: " + bodyBytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n" +
            "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
    }

    public void Dispose() {
        cancellation.Cancel();
        listener.Stop();
        try {
            if (acceptLoop != null) {
                acceptLoop.Wait(TimeSpan.FromSeconds(2));
            }
        } catch {
        }
        cancellation.Dispose();
    }
}
'@
    }

    $server = [SnapshotTestServer]::new($Port, $ExternalScriptPort)
    $server.Start()
    return $server
}


Describe 'Invoke-HTMLRendering' {
    It 'Loads dynamic content from a local file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri
        $html | Should -Match 'Dynamic Content'
    }

    It 'Can wait for and return rendered text from a focused selector' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -WaitForSelector '#loaded' -Selector '#loaded' -AsText
        $text | Should -Be 'Dynamic Content'
    }

    It 'Can use a non-network-idle load state with an explicit wait function' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -LoadState Commit -WaitForFunction '() => window.renderReady === true' -Selector '#loaded' -AsText
        $text | Should -Be 'Dynamic Content'
    }

    It 'Can open a browser session with commit load state' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session -LoadState Commit

        try {
            $session.GetType().Name | Should -Be 'HtmlBrowserSession'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'Can use DOMContentLoaded navigation with selector readiness' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -LoadState DomContentLoaded -WaitForSelector '#loaded' -Selector '#loaded' -AsText
        $text | Should -Be 'Dynamic Content'
    }

    It 'Can use a heavy dynamic render profile' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -RenderProfile HeavyDynamicPage -Selector '#loaded' -AsText
        $text | Should -Be 'Dynamic Content'
    }

    It 'Applies load delay before auto-scrolling lazy pages' {
        $htmlPath = Join-Path $TestDrive 'delayed-scroll.html'
        @'
<!doctype html>
<html>
<body style="min-height:4000px">
<main>loading</main>
<script>
setTimeout(() => {
  window.addEventListener('scroll', () => {
    if (!document.getElementById('lazy')) {
      const item = document.createElement('div');
      item.id = 'lazy';
      item.textContent = 'Lazy after hydration';
      document.querySelector('main').appendChild(item);
    }
  });
}, 75);
</script>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8
        $uri = [System.Uri]::new($htmlPath).AbsoluteUri

        $text = Invoke-HTMLRendering -Url $uri -LoadState Commit -WaitAfterLoadMs 150 -AutoScroll -AutoScrollSteps 1 -AutoScrollDelayMs 20 -WaitForSelector '#lazy' -Selector '#lazy' -AsText

        $text | Should -Be 'Lazy after hydration'
    }

    It 'Can apply rendered click interactions before extraction' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -LoadState DomContentLoaded -WaitForSelector '#show-details' -ClickSelector '#show-details' -Selector '#details' -AsText
        $text | Should -Be 'Clicked Details'
    }

    It 'Can wait for content revealed by rendered click interactions' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -LoadState DomContentLoaded -ClickSelector '#show-details' -WaitForSelector '#details' -Selector '#details' -AsText
        $text | Should -Be 'Clicked Details'
    }

    It 'Can click targets that appear after initial load' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -LoadState DomContentLoaded -ClickSelector '#delayed-click' -WaitForFunction '() => document.getElementById("details").textContent === "Delayed Click"' -Selector '#details' -AsText
        $text | Should -Be 'Delayed Click'
    }

    It 'Can return inner HTML from a focused rendered selector' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri -WaitForSelector '#loaded' -Selector '#content' -InnerHtml
        $html | Should -Match '<p id="loaded">Dynamic Content</p>'
        $html | Should -Not -Match '<body>'
    }

    It 'Can return a rendered page snapshot with parsed app data' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $snapshot = Invoke-HTMLRendering -Url $uri -WaitForSelector '#loaded' -Selector '#loaded' -AsText -Snapshot -IncludeStaticRenderedComparison

        $snapshot.GetType().Name | Should -Be 'HtmlRenderedPageSnapshot'
        $snapshot.ContentKind | Should -Be 'ElementText'
        $snapshot.Content | Should -Be 'Dynamic Content'
        $snapshot.Html | Should -Match 'Dynamic Content'
        $snapshot.Text | Should -Match 'Dynamic Content'
        $snapshot.ReadableText.Text | Should -Match 'Dynamic Content'
        $snapshot.Markdown | Should -Match 'Dynamic Content'
        $snapshot.AppState.Name | Should -Contain '__NEXT_DATA__'
        $snapshot.ScriptData.Id | Should -Contain '__NEXT_DATA__'
        $snapshot.Scripts.Count | Should -BeGreaterThan 0
        $snapshot.Data.Kind | Should -Contain 'AppState'
        $snapshot.Data.Kind | Should -Contain 'Form'
        ($snapshot.JavaScriptConfig | Where-Object Path -EQ 'window.__CONFIG__').Value.api.baseUrl | Should -Be '/api'
        ($snapshot.InteractionSurface | Where-Object { $_.Kind -eq 'Form' -and $_.Name -eq 'checkout' }).Method | Should -Be 'POST'
        ($snapshot.InteractionSurface | Where-Object { $_.Kind -eq 'Endpoint' -and $_.Url -eq '/api/details' }).Method | Should -Be 'POST'
        $snapshot.StaticRenderedComparison.RenderedHtmlLength | Should -BeGreaterThan $snapshot.StaticRenderedComparison.StaticHtmlLength
        $snapshot.NetworkLog.Count | Should -Be 0
    }

    It 'Can capture bounded fetch response bodies in snapshots' {
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        try {
            Register-HTMLRoute -Session $session -Pattern '**/response-body.html' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                    Status = 200
                    ContentType = 'text/html'
                    Body = @'
<!doctype html>
<html>
<head>
<script>
window.renderReady = false;
document.addEventListener('DOMContentLoaded', async () => {
  const response = await fetch('/api/data');
  const data = await response.json();
  document.querySelector('main').textContent = data.message;
  window.renderReady = true;
});
</script>
</head>
<body><main>loading</main></body>
</html>
'@
                }) | Out-Null
            }
            Register-HTMLRoute -Session $session -Pattern '**/api/data' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                    Status = 200
                    ContentType = 'application/json'
                    Body = '{"message":"ticket-data-response-body-that-is-long-enough-to-truncate"}'
                }) | Out-Null
            }

            Invoke-HTMLNavigation -Session $session -Url 'https://example.com/response-body.html'
            $session.Page.WaitForFunctionAsync('() => window.renderReady === true', $null).GetAwaiter().GetResult()
            [HtmlTinkerX.HtmlBrowser]::CaptureResponseBodiesAsync(
                $session,
                20,
                [HtmlTinkerX.HtmlNetworkResourceType[]]@(),
                [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()

            $snapshot = [HtmlTinkerX.HtmlBrowser]::CreateSnapshotAsync(
                $session,
                'https://example.com/response-body.html',
                'main',
                $false,
                $true,
                [string[]]@(),
                $null,
                $false,
                $false,
                $false,
                $false,
                [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()

            $snapshot.Content | Should -Match 'ticket-data'
            $dataRequest = $snapshot.NetworkLog | Where-Object { $_.Url -like '*/api/data' } | Select-Object -First 1
            $dataRequest.ResponseBody | Should -Be '{"message":"ticket-d'
            $dataRequest.ResponseBodyTruncated | Should -BeTrue
            $documentRequest = $snapshot.NetworkLog | Where-Object { $_.Url -like '*/response-body.html' } | Select-Object -First 1
            $documentRequest | Should -BeNullOrEmpty
            $snapshot.NetworkLog.Count | Should -Be 1
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'Can capture document response bodies when explicitly requested' {
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        try {
            Register-HTMLRoute -Session $session -Pattern '**/document-body.html' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                    Status = 200
                    ContentType = 'text/html'
                    Body = '<!doctype html><html><body><main>document-body</main></body></html>'
                }) | Out-Null
            }

            Invoke-HTMLNavigation -Session $session -Url 'https://example.com/document-body.html'
            [HtmlTinkerX.HtmlBrowser]::CaptureResponseBodiesAsync(
                $session,
                200,
                [HtmlTinkerX.HtmlNetworkResourceType[]]@([HtmlTinkerX.HtmlNetworkResourceType]::Document),
                [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()

            $entry = $session.NetworkLog | Where-Object { $_.Url -like '*/document-body.html' } | Select-Object -First 1
            $entry.ResponseBody | Should -Match 'document-body'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'Truncates captured UTF-8 response bodies on character boundaries' {
        $session = Invoke-HTMLRendering -Url 'about:blank' -Session
        try {
            Register-HTMLRoute -Session $session -Pattern '**/utf8-body.html' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                    Status = 200
                    ContentType = 'text/html'
                    Body = @'
<!doctype html>
<html>
<head>
<script>
window.renderReady = false;
document.addEventListener('DOMContentLoaded', async () => {
  const response = await fetch('/api/utf8');
  document.querySelector('main').textContent = await response.text();
  window.renderReady = true;
});
</script>
</head>
<body><main>loading</main></body>
</html>
'@
                }) | Out-Null
            }
            Register-HTMLRoute -Session $session -Pattern '**/api/utf8' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
                    Status = 200
                    ContentType = 'text/plain; charset=utf-8'
                    Body = 'éx'
                }) | Out-Null
            }

            Invoke-HTMLNavigation -Session $session -Url 'https://example.com/utf8-body.html'
            $session.Page.WaitForFunctionAsync('() => window.renderReady === true', $null).GetAwaiter().GetResult()
            [HtmlTinkerX.HtmlBrowser]::CaptureResponseBodiesAsync(
                $session,
                2,
                [HtmlTinkerX.HtmlNetworkResourceType[]]@([HtmlTinkerX.HtmlNetworkResourceType]::Fetch),
                [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()

            $entry = $session.NetworkLog | Where-Object { $_.Url -like '*/api/utf8' } | Select-Object -First 1
            $entry.ResponseBody | Should -Be 'é'
            $entry.ResponseBodyTruncated | Should -BeTrue
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'Can report linked script discovery in snapshots' {
        $scriptPath = Join-Path $TestDrive 'linked-app.js'
        $htmlPath = Join-Path $TestDrive 'linked-script.html'
        'fetch("/api/linked", { method: "POST" });' | Set-Content -LiteralPath $scriptPath -Encoding UTF8
        @'
<!doctype html>
<html>
<head><script src="linked-app.js"></script></head>
<body><main>linked</main></body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8

        $snapshot = Invoke-HTMLRendering -Path $htmlPath -LoadState Commit -WaitAfterLoadMs 100 -Selector 'main' -AsText -Snapshot -IncludeLinkedScripts

        $snapshot.Content | Should -Be 'linked'
        $snapshot.LinkedJavaScriptEndpoints.ScriptUrl | Should -Match 'linked-app.js'
        $snapshot.LinkedJavaScriptEndpoints.Error | Should -Contain 'Only HTTP and HTTPS script URLs can be downloaded.'
    }

    It 'Reuses browser cookies and User-Agent for snapshot linked-script fetches' {
        $port = Get-AvailableTcpPort
        $server = Start-SnapshotTestServer -Port $port

        try {
            $snapshot = Invoke-HTMLRendering -Url "http://127.0.0.1:$port/page" -LoadState DomContentLoaded -WaitForSelector 'main' -Selector 'main' -AsText -Snapshot -IncludeLinkedScripts -UserAgent 'SnapshotAgent'

            $snapshot.Content | Should -Be 'snapshot'
            $snapshot.LinkedJavaScriptEndpoints.Url | Should -Contain '/api/protected'
            $server.AssetRequestCount | Should -Be 2
        } finally {
            $server.Dispose()
        }
    }

    It 'Does not send page credentials to external linked-script fetches' {
        $externalPort = Get-AvailableTcpPort
        $pagePort = Get-AvailableTcpPort
        $externalServer = Start-SnapshotTestServer -Port $externalPort
        $pageServer = Start-SnapshotTestServer -Port $pagePort -ExternalScriptPort $externalPort
        $securePassword = ConvertTo-SecureString 'page-pass' -AsPlainText -Force
        $credential = [pscredential]::new('page-user', $securePassword)

        try {
            $snapshot = Invoke-HTMLRendering -Url "http://127.0.0.1:$pagePort/page" -LoadState DomContentLoaded -WaitForSelector 'main' -Selector 'main' -AsText -Snapshot -IncludeLinkedScripts -IncludeExternalLinkedScripts -Credential $credential -UserAgent 'SnapshotAgent'

            $snapshot.Content | Should -Be 'snapshot'
            $snapshot.LinkedJavaScriptEndpoints.Url | Should -Not -Contain '/api/leaked'
            ($snapshot.LinkedJavaScriptEndpoints | Where-Object ScriptUrl -Like "http://127.0.0.1:$externalPort/challenge.js").Error | Should -Not -BeNullOrEmpty
        } finally {
            $pageServer.Dispose()
            $externalServer.Dispose()
        }
    }

    It 'Can return applied interactions in a rendered page snapshot' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $snapshot = Invoke-HTMLRendering -Url $uri -LoadState DomContentLoaded -WaitForSelector '#show-details' -ClickSelector '#show-details' -Selector '#details' -AsText -Snapshot

        $snapshot.ContentKind | Should -Be 'ElementText'
        $snapshot.Content | Should -Be 'Clicked Details'
        $snapshot.AppliedInteractions | Should -Contain 'Clicked: #show-details'
    }

    It 'Can block matching requests before first navigation' {
        $path = Join-Path $PSScriptRoot 'Documents/route_page.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $text = Invoke-HTMLRendering -Url $uri -LoadState Commit -WaitForFunction '() => document.getElementById("result").textContent !== "loading"' -Selector '#result' -AsText -BlockResourcePattern '**/data.json'

        $text | Should -Be 'error'
    }

    It 'Rejects ambiguous content extraction parameter combinations' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri

        { Invoke-HTMLRendering -Url $uri -InnerHtml } | Should -Throw '*InnerHtml requires Selector*'
        { Invoke-HTMLRendering -Url $uri -Session -Selector '#loaded' } | Should -Throw '*cannot be used with -Session*'
        { Invoke-HTMLRendering -Url $uri -Snapshot -OutFile (Join-Path $TestDrive 'snapshot.html') } | Should -Throw '*Snapshot output is an object*'
        { Invoke-HTMLRendering -Url $uri -IncludeNetworkLog } | Should -Throw '*only valid with -Snapshot*'
        { Invoke-HTMLRendering -Url $uri -IncludeStaticRenderedComparison } | Should -Throw '*only valid with -Snapshot*'
        { Invoke-HTMLRendering -Url $uri -IncludeLinkedScripts } | Should -Throw '*only valid with -Snapshot*'
        { Invoke-HTMLRendering -Url $uri -IncludeExternalLinkedScripts } | Should -Throw '*requires -IncludeLinkedScripts*'
        { Invoke-HTMLRendering -Url $uri -IncludeResponseBody } | Should -Throw '*only valid with -Snapshot*'
        { Invoke-HTMLRendering -Url $uri -LoadState Commit } | Should -Throw '*requires WaitForSelector, WaitForFunction, or WaitAfterLoadMs*'
        { Invoke-HTMLRendering -Url $uri -BlockResourceType Document } | Should -Throw '*would abort page navigation*'
    }

    It 'Honors timeout while waiting for extraction selectors' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri

        $elapsed = Measure-Command {
            { Invoke-HTMLRendering -Url $uri -LoadState Commit -WaitAfterLoadMs 50 -Selector '#missing' -AsText -Timeout 500 } | Should -Throw
        }

        $elapsed.TotalSeconds | Should -BeLessThan 10
    }

    It 'Loads content using Firefox engine' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri -Browser Firefox -Timeout 30000
        $html | Should -Match 'Dynamic Content'
    }

    It 'Applies custom browser context options' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session -UserAgent 'MyAgent' -ViewportWidth 123 -ViewportHeight 77 -DeviceScaleFactor 2
        $ua = $session.Page.EvaluateAsync('navigator.userAgent',$null).GetAwaiter().GetResult()
        $w = [int]($session.Page.EvaluateAsync('window.innerWidth',$null).GetAwaiter().GetResult().ToString())
        $d = [double]($session.Page.EvaluateAsync('window.devicePixelRatio',$null).GetAwaiter().GetResult().ToString())
        Close-HtmlBrowserSession -Session $session
        $ua | Should -Be 'MyAgent'
        $w | Should -Be 123
        [double]$d | Should -Be 2
    }

    It 'Applies geolocation and timezone settings' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session -GeoLatitude 50 -GeoLongitude 20 -Timezone 'Europe/Warsaw'
        $lat = [double]($session.Page.EvaluateAsync('new Promise(r=>navigator.geolocation.getCurrentPosition(p=>r(p.coords.latitude)))',$null).GetAwaiter().GetResult().ToString())
        $tz = $session.Page.EvaluateAsync('Intl.DateTimeFormat().resolvedOptions().timeZone',$null).GetAwaiter().GetResult()
        Close-HtmlBrowserSession -Session $session
        [math]::Round($lat,0) | Should -Be 50
        $tz | Should -Be 'Europe/Warsaw'
    }

    It 'Supports proxy parameters' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $html = Invoke-HTMLRendering -Url $uri -Proxy 'http://localhost:8080'
        $html | Should -Match 'Dynamic Content'
    }
}
