Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Invoke-HtmlCrawl page streaming' {
    BeforeAll {
        if (-not ('PesterPageStreamServer' -as [type])) {
            Add-Type -TypeDefinition @"
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
public sealed class PesterPageStreamServer : IDisposable {
    private readonly HttpListener _listener = new HttpListener();
    private readonly Task _server;
    private readonly bool _holdSecond;
    private readonly TaskCompletionSource<bool> _release = new TaskCompletionSource<bool>();
    private int _secondCompleted;
    public bool SecondCompleted { get { return Volatile.Read(ref _secondCompleted) != 0; } }
    public bool GateExpired { get; private set; }
    public string Url { get; private set; }
    public PesterPageStreamServer(bool holdSecond = false) {
        _holdSecond = holdSecond;
        var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start(); int number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
        Url = "http://localhost:" + number + "/";
        _listener.Prefixes.Add(Url); _listener.Start();
        _server = Task.Run(new Func<Task>(ListenAsync));
    }
    private async Task ListenAsync() {
        try {
            while (_listener.IsListening) {
                var context = await _listener.GetContextAsync();
                try {
                    string path = context.Request.Url.AbsolutePath;
                    if (path == "/second" && _holdSecond) {
                        if (await Task.WhenAny(_release.Task, Task.Delay(5000)) != _release.Task) GateExpired = true;
                    }
                    if (path == "/missing") context.Response.StatusCode = 404;
                    else {
                        string html = path == "/" ? "<main>Home<a href='" + (_holdSecond ? "/second" : "/missing") + "'>Next</a></main>" : "<main>Second</main>";
                        byte[] bytes = Encoding.UTF8.GetBytes(html);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        if (path == "/second") Interlocked.Exchange(ref _secondCompleted, 1);
                    }
                } finally { context.Response.Close(); }
            }
        } catch (HttpListenerException) { } catch (ObjectDisposedException) { }
    }
    public void ReleaseSecond() { _release.TrySetResult(true); }
    public void Dispose() { ReleaseSecond(); _listener.Close(); _server.GetAwaiter().GetResult(); }
}
"@
        }
    }

    It 'returns completed pages through the public pipeline and retains normal result output by default' {
        $server = [PesterPageStreamServer]::new()
        try {
            $pages = @(Invoke-HtmlCrawl -Url $server.Url -MaxPages 2 -IgnoreRobotsTxt -NoSitemaps -StreamPages)
            $pages.Count | Should -Be 2
            $pages[0].GetType().FullName | Should -Be 'HtmlTinkerX.HtmlCrawlPage'
            $pages[0].Text | Should -Match 'Home'
            $pages[1].Status.ToString() | Should -Be 'Failed'
            $pages[1].StatusCode | Should -Be 404
            $result = Invoke-HtmlCrawl -Url $server.Url -MaxPages 1 -IgnoreRobotsTxt -NoSitemaps
            $result.GetType().FullName | Should -Be 'HtmlTinkerX.HtmlCrawlResult'
            $result.Pages.Count | Should -Be 1
        } finally { $server.Dispose() }
    }

    It 'keeps streamed content when the persisted crawl releases page bodies' {
        $server = [PesterPageStreamServer]::new()
        $outputPath = Join-Path $TestDrive 'released-stream'
        try {
            $pages = @(Invoke-HtmlCrawl -Url $server.Url -MaxPages 2 -IgnoreRobotsTxt -NoSitemaps -OutPath $outputPath -IncludeHtml -IncludeMarkdown -StreamPages -ReleasePageContent)
            $pages.Count | Should -Be 2
            $pages[0].Html | Should -Match 'Home'
            $pages[0].Text | Should -Match 'Home'
            $pages[0].Markdown | Should -Match 'Home'
            $pages[1].Status.ToString() | Should -Be 'Failed'
            $saved = [HtmlTinkerX.HtmlCrawler]::LoadResultAsync($outputPath).GetAwaiter().GetResult()
            $saved.Pages[0].Text | Should -Match 'Home'
        } finally { $server.Dispose() }
    }

    It 'emits the first page while the second response is still pending' {
        $server = [PesterPageStreamServer]::new($true)
        $firstWasIncremental = $false
        try {
            $pages = @(Invoke-HtmlCrawl -Url $server.Url -MaxPages 2 -IgnoreRobotsTxt -NoSitemaps -StreamPages | ForEach-Object {
                if ($_.Url -eq $server.Url) {
                    $firstWasIncremental = -not $server.SecondCompleted
                    $server.ReleaseSecond()
                }
                $_
            })
            $pages.Count | Should -Be 2
            $firstWasIncremental | Should -BeTrue
            $server.GateExpired | Should -BeFalse
            $server.SecondCompleted | Should -BeTrue
        } finally { $server.Dispose() }
    }
}
