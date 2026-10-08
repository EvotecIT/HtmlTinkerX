Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Parsed HTTP form submission' {
    BeforeAll {
        if (-not ('HtmlFormHttpTestServer' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class HtmlFormHttpTestServer : IDisposable {
    private readonly HttpListener listener = new HttpListener();
    private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
    private readonly Task loop;
    public readonly string Root;
    public int Requests;
    public HtmlFormHttpTestServer() {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        Root = "http://localhost:" + port + "/";
        listener.Prefixes.Add(Root);
        listener.Start();
        loop = Task.Run(new Func<Task>(ListenAsync));
    }
    private async Task ListenAsync() {
        try {
            while (listener.IsListening) {
                var context = await listener.GetContextAsync();
                Interlocked.Increment(ref Requests);
                try {
                    string path = context.Request.Url.AbsolutePath;
                    if (path == "/forms/start") {
                        context.Response.StatusCode = 302;
                        context.Response.RedirectLocation = "/forms/login";
                        continue;
                    }
                    if (path == "/slow") {
                        await Task.Delay(10000, shutdown.Token);
                    }
                    string text;
                    if (path == "/forms/login") {
                        context.Response.Headers["Set-Cookie"] = "form-session=known; Path=/; HttpOnly";
                        text = "<base href='/submit/'><form action='save?mode=edit' method='post'>"
                            + "<input name='csrf' value='nonce-token'><input name='tag' value='one'>"
                            + "<input name='q' value='original'><input name='tag' value='two'>"
                            + "<input name='disabled' value='omit' disabled><input type='checkbox' name='unchecked'></form>";
                    } else {
                        using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) {
                            text = await reader.ReadToEndAsync();
                        }
                        text += "|cookie=" + context.Request.Headers["Cookie"] + "|uri=" + context.Request.Url.PathAndQuery;
                    }
                    byte[] bytes = Encoding.UTF8.GetBytes(text);
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                } catch (IOException) { }
                finally { context.Response.Close(); }
            }
        } catch (HttpListenerException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
    }
    public void Dispose() {
        shutdown.Cancel();
        listener.Close();
        loop.GetAwaiter().GetResult();
        shutdown.Dispose();
    }
}
'@
        }
    }

    BeforeEach { $server = [HtmlFormHttpTestServer]::new() }
    AfterEach { $server.Dispose() }

    It 'preserves parsed defaults and repeated overrides' {
        $form = ConvertFrom-HtmlForm -Content "<form action='$($server.Root)receive' method='post'><input name='csrf' value='nonce'><input name='tag' value='one'><input name='q' value='old'><input name='tag' value='two'></form>" -BaseUri $server.Root
        $response = Submit-HtmlBrowserForm -Form $form -FieldValue @{ tag = @('new one', '+two'); q = 'updated' }
        $response | Should -Be 'csrf=nonce&tag=new+one&tag=%2Btwo&q=updated|cookie=|uri=/receive'
        $form.SuccessfulFields.Count | Should -Be 4
        $form.SuccessfulFields[1].Value | Should -Be 'one'
    }

    It 'uses a relative action resolved against the HTML base' {
        $form = ConvertFrom-HtmlForm -Content "<base href='/nested/'><form action='receive'><input name='q' value='original'></form>" -BaseUri $server.Root
        Submit-HtmlBrowserForm -Form $form -FieldValue @{ q = 'updated' } | Should -Be '|cookie=|uri=/nested/receive?q=updated'
    }

    It 'submits successful defaults when overrides are omitted' {
        $form = ConvertFrom-HtmlForm -Content "<form action='/receive' method='post'><input name='hidden' type='hidden' value='nonce'><input name='tag' value='one'><input name='tag' value='two'><input name='omit' disabled value='disabled'></form>" -BaseUri $server.Root
        Submit-HtmlBrowserForm -Form $form | Should -Be 'hidden=nonce&tag=one&tag=two|cookie=|uri=/receive'
    }

    It 'replaces the GET action query and retains repeated parsed values' {
        $form = ConvertFrom-HtmlForm -Content "<form action='/query?discard=old'><input name='tag' value='one'><input name='tag' value='two'><input name='q' value='original'></form>" -BaseUri $server.Root
        Submit-HtmlBrowserForm -Form $form -FieldValue @{ q = 'new + value' } | Should -Be '|cookie=|uri=/query?tag=one&tag=two&q=new+%2B+value'
    }

    It 'reuses the downloading client across redirects, HTML bases, and cookie-dependent submission' {
        $client = [HtmlTinkerX.HtmlHttpClientFactory]::Create()
        $client.Timeout = [TimeSpan]::FromSeconds(23)
        try {
            $form = ConvertFrom-HtmlForm -Url ($server.Root + 'forms/start') -HttpClient $client -IncludeMetadata
            $form.FinalUrl | Should -Be ($server.Root + 'forms/login')
            $form.BaseUrl | Should -Be ($server.Root + 'submit/')
            Submit-HtmlBrowserForm -Form $form -HttpClient $client -FieldValue @{ q = 'updated' } | Should -Be 'csrf=nonce-token&tag=one&q=updated&tag=two|cookie=form-session=known|uri=/submit/save?mode=edit'
            $client.Timeout.TotalSeconds | Should -Be 23
            $response = $client.GetAsync($server.Root + 'forms/login').GetAwaiter().GetResult()
            try { $response.IsSuccessStatusCode | Should -BeTrue } finally { $response.Dispose() }
        } finally { $client.Dispose() }
    }

    It 'rejects an unresolved action and an empty override array before sending' {
        $form = ConvertFrom-HtmlForm -Content "<form action='relative'><input name='q' value='original'></form>"
        { Submit-HtmlBrowserForm -Form $form } | Should -Throw '*absolute HTTP action*'
        $form = ConvertFrom-HtmlForm -Content "<form action='/receive'><input name='q' value='original'></form>" -BaseUri $server.Root
        { Submit-HtmlBrowserForm -Form $form -FieldValue @{ q = @() } } | Should -Throw '*at least one value*'
        $server.Requests | Should -Be 0
    }

    It 'applies a submission timeout without changing or disposing a supplied client' {
        $client = [HtmlTinkerX.HtmlHttpClientFactory]::Create()
        $client.Timeout = [TimeSpan]::FromSeconds(30)
        try {
            $form = ConvertFrom-HtmlForm -Content "<form action='/slow' method='post'></form>" -BaseUri $server.Root
            { Submit-HtmlBrowserForm -Form $form -HttpClient $client -Timeout 200 } | Should -Throw
            $client.Timeout.TotalSeconds | Should -Be 30
        } finally { $client.Dispose() }
    }
}
