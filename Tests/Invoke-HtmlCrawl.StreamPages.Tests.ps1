Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Invoke-HtmlCrawl page streaming' {
    BeforeAll {
        if (-not ('PesterPageStreamServer' -as [type])) {
            Add-Type -TypeDefinition @"
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
public sealed class PesterPageStreamServer : IDisposable {
    private readonly HttpListener _listener = new HttpListener();
    private readonly Task _server;
    public string Url { get; private set; }
    public PesterPageStreamServer() {
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
                    if (path == "/missing") context.Response.StatusCode = 404;
                    else {
                        string html = path == "/" ? "<main>Home<a href='/missing'>Missing</a></main>" : "<main>Second</main>";
                        byte[] bytes = Encoding.UTF8.GetBytes(html);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    }
                } finally { context.Response.Close(); }
            }
        } catch (HttpListenerException) { } catch (ObjectDisposedException) { }
    }
    public void Dispose() { _listener.Close(); _server.GetAwaiter().GetResult(); }
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
}
