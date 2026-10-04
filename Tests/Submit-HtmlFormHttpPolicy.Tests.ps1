Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

BeforeAll {
    if (-not ('HtmlResponsePolicyTestServer' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class HtmlResponsePolicyTestServer : IDisposable {
    private readonly HttpListener listener = new HttpListener();
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly Task loop;
    public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>();
    public readonly TaskCompletionSource<bool> Disconnected = new TaskCompletionSource<bool>();
    public string Url { get; private set; }

    public HtmlResponsePolicyTestServer() {
        TcpListener portFinder = new TcpListener(IPAddress.Loopback, 0);
        portFinder.Start();
        int port = ((IPEndPoint)portFinder.LocalEndpoint).Port;
        portFinder.Stop();
        Url = "http://127.0.0.1:" + port + "/";
        listener.Prefixes.Add(Url);
        listener.Start();
        loop = Task.Run(() => RunAsync());
    }

    private async Task RunAsync() {
        try {
            while (!cancellation.IsCancellationRequested) {
                HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                await HandleAsync(context).ConfigureAwait(false);
            }
        } catch (HttpListenerException) { } catch (ObjectDisposedException) { }
    }

    private async Task HandleAsync(HttpListenerContext context) {
        try {
            context.Response.ContentType = "text/html; charset=utf-8";
            if (context.Request.Url.AbsolutePath == "/stream") {
                context.Response.SendChunked = true;
                byte[] chunk = Encoding.UTF8.GetBytes("streaming");
                for (int index = 0; index < 500; index++) {
                    await context.Response.OutputStream.WriteAsync(chunk, 0, chunk.Length).ConfigureAwait(false);
                    await context.Response.OutputStream.FlushAsync().ConfigureAwait(false);
                    Started.TrySetResult(true);
                    await Task.Delay(20, cancellation.Token).ConfigureAwait(false);
                }
            } else {
                byte[] bytes = Encoding.UTF8.GetPreamble();
                byte[] text = Encoding.UTF8.GetBytes("<main>Za\u017c\u00f3\u0142\u0107</main>");
                context.Response.ContentType = "text/html; charset=windows-1252";
                context.Response.ContentLength64 = bytes.Length + text.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await context.Response.OutputStream.WriteAsync(text, 0, text.Length).ConfigureAwait(false);
            }
        } catch (IOException) { if (!cancellation.IsCancellationRequested) Disconnected.TrySetResult(true); }
          catch (HttpListenerException) { if (!cancellation.IsCancellationRequested) Disconnected.TrySetResult(true); }
          catch (OperationCanceledException) { }
          catch (ObjectDisposedException) { }
        finally { context.Response.Close(); }
    }

    public void Dispose() {
        cancellation.Cancel();
        listener.Close();
        try { loop.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        cancellation.Dispose();
    }
}
'@
    }
}

Describe 'Submit-HtmlForm HTTP response policy' {
    It 'decodes a BOM before a conflicting header and enforces its byte limit' {
        $server = [HtmlResponsePolicyTestServer]::new()
        try {
            $form = [pscustomobject]@{ Action = $server.Url + 'normal'; Method = 'POST' }
            $expected = '<main>Za' + [char]0x017c + [char]0x00f3 + [char]0x0142 + [char]0x0107 + '</main>'
            $form | Submit-HtmlForm -FieldValue @{ x = '1' } | Should -BeExactly $expected
            { $form | Submit-HtmlForm -FieldValue @{ x = '1' } -MaximumResponseBytes 5 } | Should -Throw '*5-byte limit*'
        } finally { $server.Dispose() }
    }

    It 'applies its HTTP timeout while streaming the response body' {
        $server = [HtmlResponsePolicyTestServer]::new()
        try {
            $form = [pscustomobject]@{ Action = $server.Url + 'stream'; Method = 'POST' }
            { $form | Submit-HtmlForm -FieldValue @{ x = '1' } -Timeout 250 } | Should -Throw
            $server.Started.Task.IsCompleted | Should -BeTrue
            $server.Disconnected.Task.Wait(3000) | Should -BeTrue
        } finally { $server.Dispose() }
    }

    It 'cancels the underlying request when its PowerShell pipeline is stopped' {
        $server = [HtmlResponsePolicyTestServer]::new()
        $pipeline = [powershell]::Create()
        try {
            $modulePath = Join-Path $PSScriptRoot '../PSParseHTML.psd1'
            $null = $pipeline.AddCommand('Import-Module').AddParameter('Name', $modulePath).AddParameter('Force').Invoke()
            $pipeline.Commands.Clear()
            $form = [pscustomobject]@{ Action = $server.Url + 'stream'; Method = 'POST' }
            $null = $pipeline.AddCommand('Submit-HtmlBrowserForm').AddParameter('Form', $form).AddParameter('FieldValue', @{ x = '1' }).AddParameter('Timeout', 0)
            $running = $pipeline.BeginInvoke()
            $server.Started.Task.Wait(5000) | Should -BeTrue
            $pipeline.Stop()
            $running.AsyncWaitHandle.WaitOne(3000) | Should -BeTrue
            $server.Disconnected.Task.Wait(3000) | Should -BeTrue
            $pipeline.InvocationStateInfo.State | Should -Be ([System.Management.Automation.PSInvocationState]::Stopped)
        } finally { $pipeline.Dispose(); $server.Dispose() }
    }
}
