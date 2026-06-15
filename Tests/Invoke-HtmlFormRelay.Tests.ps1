Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

BeforeAll {
    if (-not ('HtmlRelayTestServer' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class HtmlRelayTestServer : IDisposable {
    private readonly HttpListener listener;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly Task loop;

    public HtmlRelayTestServer() {
        int port = GetFreePort();
        Url = "http://127.0.0.1:" + port + "/";
        listener = new HttpListener();
        listener.Prefixes.Add(Url);
        listener.Start();
        loop = Task.Run(() => RunAsync());
    }

    public string Url { get; private set; }

    public void Dispose() {
        cancellation.Cancel();
        listener.Close();
        try {
            loop.Wait(TimeSpan.FromSeconds(2));
        } catch {
        }
        cancellation.Dispose();
    }

    private async Task RunAsync() {
        while (!cancellation.IsCancellationRequested) {
            try {
                HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(context));
            } catch (HttpListenerException) {
                return;
            } catch (ObjectDisposedException) {
                return;
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context) {
        if (context.Request.Url.AbsolutePath == "/start") {
            context.Response.Headers.Add("Set-Cookie", "relay=ok; Path=/");
            await WriteAsync(context.Response, "<form method=\"POST\" name=\"hiddenform\" action=\"" + Url + "complete\"><input type=\"hidden\" name=\"SAMLResponse\" value=\"redacted\" /><input type=\"hidden\" name=\"RelayState\" value=\"state\" /></form><script>document.forms[0].submit()</script>").ConfigureAwait(false);
            return;
        }

        if (context.Request.Url.AbsolutePath == "/redirect-start") {
            context.Response.StatusCode = 302;
            context.Response.RedirectLocation = Url + "idp/page";
            context.Response.Close();
            return;
        }

        if (context.Request.Url.AbsolutePath == "/idp/page") {
            context.Response.Headers.Add("Set-Cookie", "relay=ok; Path=/");
            await WriteAsync(context.Response, "<form method=\"POST\" name=\"hiddenform\" action=\"complete\"><input type=\"hidden\" name=\"SAMLResponse\" value=\"redacted\" /><input type=\"hidden\" name=\"RelayState\" value=\"state\" /></form><script>document.forms[0].submit()</script>").ConfigureAwait(false);
            return;
        }

        if (context.Request.Url.AbsolutePath == "/complete") {
            Cookie cookie = context.Request.Cookies["relay"];
            if (cookie == null || cookie.Value != "ok") {
                context.Response.StatusCode = 401;
                await WriteAsync(context.Response, "<main>missing cookie</main>").ConfigureAwait(false);
                return;
            }

            await WriteAsync(context.Response, "<main>done</main>").ConfigureAwait(false);
            return;
        }

        if (context.Request.Url.AbsolutePath == "/idp/complete") {
            Cookie cookie = context.Request.Cookies["relay"];
            if (cookie == null || cookie.Value != "ok") {
                context.Response.StatusCode = 401;
                await WriteAsync(context.Response, "<main>missing cookie</main>").ConfigureAwait(false);
                return;
            }

            await WriteAsync(context.Response, "<main>redirect done</main>").ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = 404;
        await WriteAsync(context.Response, "<main>not found</main>").ConfigureAwait(false);
    }

    private static async Task WriteAsync(HttpListenerResponse response, string content) {
        byte[] buffer = Encoding.UTF8.GetBytes(content);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        response.Close();
    }

    private static int GetFreePort() {
        TcpListener tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }
}
'@
    }
}

Describe 'Invoke-HtmlFormRelay' {
    It 'exports the browserless relay cmdlet and issue vocabulary alias' {
        Get-Command Invoke-HtmlFormRelay | Should -Not -BeNullOrEmpty
        (Get-Command Invoke-HtmlAutoSubmitForm).ResolvedCommandName | Should -Be 'Invoke-HtmlFormRelay'
    }

    It 'returns a blocked cross-host relay result by default' {
        $html = @'
<form method="POST" name="hiddenform" action="https://idp.example.net/signin">
<input type="hidden" name="wa" value="signin1.0" />
<input type="hidden" name="wresult" value="redacted" />
<input type="hidden" name="wctx" value="state" />
</form>
<script>document.forms[0].submit()</script>
'@

        $result = Invoke-HtmlFormRelay -Content $html -BaseUrl 'https://rp.example.org/start'

        $result.StopReason | Should -Be 'CrossHostBlocked'
        $result.SubmittedRelay | Should -BeFalse
        $result.Steps[0].FieldNames | Should -Contain 'wresult'
    }

    It 'keeps initial Url cookies while following hidden relay forms' {
        $server = [HtmlRelayTestServer]::new()
        try {
            $result = Invoke-HtmlFormRelay -Url ($server.Url + 'start')

            $result.StopReason | Should -Be 'NoRelayForm'
            $result.SubmittedRelay | Should -BeTrue
            $result.FinalContent | Should -Match 'done'
            $result.Steps[0].ProtocolHint | Should -Be 'Saml'
            ($result.Steps[0].FieldNames -join ',') | Should -Not -Match 'redacted'
        } finally {
            $server.Dispose()
        }
    }

    It 'uses the post-redirect Url as the base for relative relay actions' {
        $server = [HtmlRelayTestServer]::new()
        try {
            $result = Invoke-HtmlFormRelay -Url ($server.Url + 'redirect-start')

            $result.StopReason | Should -Be 'NoRelayForm'
            $result.SubmittedRelay | Should -BeTrue
            $result.FinalUrl | Should -Be ($server.Url + 'idp/complete')
            $result.FinalContent | Should -Match 'redirect done'
            $result.Steps[0].ActionUrl | Should -Be ($server.Url + 'idp/complete')
        } finally {
            $server.Dispose()
        }
    }
}
