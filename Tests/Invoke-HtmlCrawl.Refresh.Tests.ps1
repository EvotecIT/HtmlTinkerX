Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Invoke-HtmlCrawl conditional refresh' {
    BeforeAll {
        if (-not ('PesterRefreshHttpServer' -as [type])) {
            Add-Type -TypeDefinition @"
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class PesterRefreshHttpServer : IDisposable {
    private readonly HttpListener _listener = new HttpListener();
    private readonly Task _server;
    private int _downloads;
    private int _validations;
    private int _requests;
    private readonly bool _transientOnce;
    public string Url { get; private set; }
    public int Downloads { get { return Volatile.Read(ref _downloads); } }
    public int Validations { get { return Volatile.Read(ref _validations); } }
    public int Requests { get { return Volatile.Read(ref _requests); } }

    public PesterRefreshHttpServer() : this(false) { }

    public PesterRefreshHttpServer(bool transientOnce) {
        _transientOnce = transientOnce;
        var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        int number = ((IPEndPoint)port.LocalEndpoint).Port;
        port.Stop();
        Url = "http://localhost:" + number + "/";
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _server = Task.Run(new Func<Task>(ListenAsync));
    }

    private async Task ListenAsync() {
        try {
            while (_listener.IsListening) {
                var context = await _listener.GetContextAsync();
                try {
                    if (Interlocked.Increment(ref _requests) == 1 && _transientOnce) {
                        context.Response.StatusCode = 503;
                        context.Response.Headers["Retry-After"] = "0";
                        continue;
                    }
                    context.Response.Headers["ETag"] = "W/\"original\"";
                    if (context.Request.Headers["If-None-Match"] == "W/\"original\"") {
                        Interlocked.Increment(ref _validations);
                        context.Response.StatusCode = 304;
                    } else {
                        Interlocked.Increment(ref _downloads);
                        byte[] bytes = Encoding.UTF8.GetBytes("<main>Main body</main><section id='alternate'>Alternate body</section>");
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    }
                } finally { context.Response.Close(); }
            }
        } catch (HttpListenerException) { } catch (ObjectDisposedException) { }
    }

    public void Dispose() {
        _listener.Close();
        _server.GetAwaiter().GetResult();
    }

}
"@
        }
    }

    It 'retries a transient response through the public command' {
        $server = [PesterRefreshHttpServer]::new($true)
        try {
            $result = Invoke-HtmlCrawl -Url $server.Url -MaxPages 1 -IgnoreRobotsTxt -NoSitemaps -HttpRetryCount 1
            $result.Pages.Count | Should -Be 1
            $result.Pages[0].Status.ToString() | Should -Be 'Success'
            $result.Pages[0].Text | Should -Match 'Main body'
            $server.Requests | Should -Be 2
        } finally {
            $server.Dispose()
        }
    }

    It 'revalidates through the public command and applies the current selector' {
        $server = [PesterRefreshHttpServer]::new()
        $source = Join-Path $TestDrive 'source'
        $destination = Join-Path $TestDrive 'refreshed'
        try {
            $original = Invoke-HtmlCrawl -Url $server.Url -MaxPages 1 -IgnoreRobotsTxt -NoSitemaps -Selector main -CacheResponses -OutPath $source
            $original.Pages[0].Text | Should -Match 'Main body'
            $manifest = Get-Content -LiteralPath (Join-Path $source 'crawl-result.json') -Raw

            $result = Invoke-HtmlCrawl -Url $server.Url -MaxPages 1 -IgnoreRobotsTxt -NoSitemaps -Selector '#alternate' -RefreshPath $source -OutPath $destination
            $result.Pages.Count | Should -Be 1
            $page = $result.Pages[0]
            $page.Status.ToString() | Should -Be 'Success'
            $page.StatusCode | Should -Be 304
            $page.ResponseRevalidated | Should -BeTrue
            $page.ResponseChanged | Should -BeFalse
            $page.ResponseContentHash | Should -Be $original.Pages[0].ResponseContentHash
            $page.Text | Should -Match 'Alternate body'
            $page.Text | Should -Not -Match 'Main body'
            $server.Downloads | Should -Be 1
            $server.Validations | Should -Be 1
            (Get-Content -LiteralPath (Join-Path $source 'crawl-result.json') -Raw) | Should -Be $manifest
            $export = Get-Content -LiteralPath $result.PagesJsonlPath -Raw | ConvertFrom-Json
            $export.ResponseRevalidated | Should -BeTrue
            $export.ResponseContentHash | Should -Be $page.ResponseContentHash
        } finally {
            $server.Dispose()
        }
    }
}
