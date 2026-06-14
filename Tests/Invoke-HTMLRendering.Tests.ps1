Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

$script:Python3Available = $false
$pythonCommand = Get-Command python3 -ErrorAction SilentlyContinue
if ($pythonCommand) {
    try {
        & python3 --version *> $null
        if ($LASTEXITCODE -eq 0) {
            $script:Python3Available = $true
        }
    } catch {
        $script:Python3Available = $false
    }
}

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

function script:Wait-TestTcpPort {
    param(
        [int] $Port
    )

    $timeout = [System.Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        try {
            $socket = [Net.Sockets.TcpClient]::new()
            $socket.Connect('127.0.0.1', $Port)
            $socket.Dispose()
            break
        } catch {
            if ($timeout.Elapsed -gt [TimeSpan]::FromSeconds(10)) {
                throw 'HTTP server failed to start.'
            }
            Start-Sleep -Milliseconds 500
        }
    }
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

    It 'Reuses browser cookies and User-Agent for snapshot linked-script fetches' -Skip:(-not $script:Python3Available) {
        $port = Get-AvailableTcpPort
        $serverPath = Join-Path $TestDrive 'snapshot_server.py'
        @'
from http.server import BaseHTTPRequestHandler, HTTPServer
import sys

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        ua = self.headers.get("User-Agent", "")
        cookie = self.headers.get("Cookie", "")
        if self.path == "/page":
            body = b'<!doctype html><html><head><script src="/assets/app.js"></script></head><body><main>snapshot</main></body></html>'
            self.send_response(200)
            self.send_header("Content-Type", "text/html")
            self.send_header("Set-Cookie", "assetCookie=1; Path=/assets")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path == "/assets/app.js":
            if "assetCookie=1" in cookie and ua == "SnapshotAgent":
                body = b'fetch("/api/protected", { method: "POST" });'
            else:
                body = b'console.log("denied");'
            self.send_response(200)
            self.send_header("Content-Type", "application/javascript")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        self.send_response(404)
        self.end_headers()

    def log_message(self, format, *args):
        return

HTTPServer(("127.0.0.1", int(sys.argv[1])), Handler).serve_forever()
'@ | Set-Content -LiteralPath $serverPath -Encoding UTF8
        $server = Start-Process -FilePath 'python3' -ArgumentList '-u', $serverPath, $port -PassThru
        Wait-TestTcpPort -Port $port

        try {
            $snapshot = Invoke-HTMLRendering -Url "http://127.0.0.1:$port/page" -LoadState DomContentLoaded -WaitForSelector 'main' -Selector 'main' -AsText -Snapshot -IncludeLinkedScripts -UserAgent 'SnapshotAgent'

            $snapshot.Content | Should -Be 'snapshot'
            $snapshot.LinkedJavaScriptEndpoints.Url | Should -Contain '/api/protected'
        } finally {
            $server | Stop-Process
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
