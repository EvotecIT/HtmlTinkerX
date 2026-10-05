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
                byte[] bytes = Encoding.UTF8.GetPreamble();
                byte[] text = Encoding.UTF8.GetBytes("<main>Za\u017c\u00f3\u0142\u0107</main>");
                context.Response.ContentType = "text/html; charset=windows-1252";
                context.Response.ContentLength64 = bytes.Length + text.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await context.Response.OutputStream.WriteAsync(text, 0, text.Length).ConfigureAwait(false);
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

public sealed class HtmlStalledResponseTestServer : IDisposable {
    private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly Task loop;
    private TcpClient client;
    public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>();
    public readonly TaskCompletionSource<bool> Disconnected = new TaskCompletionSource<bool>();
    public bool StallFinished { get; private set; }
    public string Url { get; private set; }

    public HtmlStalledResponseTestServer() {
        listener.Start();
        Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
        loop = Task.Run(() => RunAsync());
    }

    private async Task RunAsync() {
        try {
            client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            MemoryStream headers = new MemoryStream();
            byte[] one = new byte[1];
            int matched = 0;
            byte[] terminator = { 13, 10, 13, 10 };
            while (matched < 4 && headers.Length < 65536) {
                if (await stream.ReadAsync(one, 0, 1).ConfigureAwait(false) == 0) return;
                headers.WriteByte(one[0]);
                matched = one[0] == terminator[matched] ? matched + 1 : (one[0] == 13 ? 1 : 0);
            }
            int remaining = 0;
            string headerText = Encoding.ASCII.GetString(headers.ToArray());
            foreach (string line in headerText.Split(new[] { "\r\n" }, StringSplitOptions.None)) {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    remaining = Int32.Parse(line.Substring("Content-Length:".Length).Trim());
            }
            if (headerText.IndexOf("Expect: 100-continue", StringComparison.OrdinalIgnoreCase) >= 0) {
                byte[] interim = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                await stream.WriteAsync(interim, 0, interim.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            byte[] buffer = new byte[4096];
            while (remaining > 0) {
                int read = await stream.ReadAsync(buffer, 0, Math.Min(remaining, buffer.Length)).ConfigureAwait(false);
                if (read == 0) return;
                remaining -= read;
            }
            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n1\r\nx\r\n");
            await stream.WriteAsync(response, 0, response.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            Started.TrySetResult(true);
            DateTime until = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < until) {
                if (client.Client.Poll(1000, SelectMode.SelectRead) && client.Client.Available == 0) {
                    Disconnected.TrySetResult(true);
                    return;
                }
                await Task.Delay(10, cancellation.Token).ConfigureAwait(false);
            }
            StallFinished = true;
            byte[] final = Encoding.ASCII.GetBytes("1\r\ny\r\n0\r\n\r\n");
            await stream.WriteAsync(final, 0, final.Length).ConfigureAwait(false);
        } catch (OperationCanceledException) { }
          catch (IOException) { if (!cancellation.IsCancellationRequested) Disconnected.TrySetResult(true); }
          catch (SocketException) { if (!cancellation.IsCancellationRequested) Disconnected.TrySetResult(true); }
          catch (ObjectDisposedException) { }
        finally { if (client != null) client.Close(); }
    }

    public void Dispose() {
        cancellation.Cancel();
        listener.Stop();
        if (client != null) client.Close();
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

    It 'applies its HTTP timeout while the response body is stalled' {
        $server = [HtmlStalledResponseTestServer]::new()
        try {
            $form = [pscustomobject]@{ Action = $server.Url + 'stall'; Method = 'POST' }
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            { $form | Submit-HtmlForm -FieldValue @{ x = '1' } -Timeout 250 } | Should -Throw
            $watch.Stop()
            $server.Started.Task.IsCompleted | Should -BeTrue
            $watch.ElapsedMilliseconds | Should -BeLessThan 2500
            $server.StallFinished | Should -BeFalse
            $server.Disconnected.Task.Wait(2000) | Should -BeTrue
        } finally { $server.Dispose() }
    }

    It 'cancels the underlying request when its PowerShell pipeline is stopped' {
        $server = [HtmlStalledResponseTestServer]::new()
        $pipeline = [powershell]::Create()
        try {
            $modulePath = Join-Path $PSScriptRoot '../PSParseHTML.psd1'
            $null = $pipeline.AddCommand('Import-Module').AddParameter('Name', $modulePath).AddParameter('Force').Invoke()
            $pipeline.Commands.Clear()
            $form = [pscustomobject]@{ Action = $server.Url + 'stall'; Method = 'POST' }
            $null = $pipeline.AddCommand('Submit-HtmlBrowserForm').AddParameter('Form', $form).AddParameter('FieldValue', @{ x = '1' }).AddParameter('Timeout', 0)
            $running = $pipeline.BeginInvoke()
            $server.Started.Task.Wait(5000) | Should -BeTrue
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $pipeline.Stop()
            $running.AsyncWaitHandle.WaitOne(3000) | Should -BeTrue
            $watch.Stop()
            $watch.ElapsedMilliseconds | Should -BeLessThan 2500
            $server.StallFinished | Should -BeFalse
            $server.Disconnected.Task.Wait(2000) | Should -BeTrue
            $pipeline.InvocationStateInfo.State | Should -Be ([System.Management.Automation.PSInvocationState]::Stopped)
        } finally { $pipeline.Dispose(); $server.Dispose() }
    }
}
