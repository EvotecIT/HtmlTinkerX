Describe 'Save-HTMLAttachment streaming' {
    BeforeAll {
        if (-not ('StreamingAttachmentTestHttpServer' -as [type])) {
            Add-Type -TypeDefinition @"
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class StreamingAttachmentTestHttpServer : IDisposable {
    private sealed class ServerResponse {
        public string Body { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/html; charset=utf-8";
    }

    private readonly HttpListener _listener = new HttpListener();
    private readonly ConcurrentDictionary<string, ServerResponse> _responses = new ConcurrentDictionary<string, ServerResponse>(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    private readonly Task _serverTask;

    public StreamingAttachmentTestHttpServer(string prefix) {
        Prefix = prefix;
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _serverTask = Task.Run(ListenAsync);
    }

    public string Prefix { get; }

    public void AddResponse(string path, string body, string contentType) {
        _responses[path] = new ServerResponse {
            Body = body ?? string.Empty,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "text/html; charset=utf-8" : contentType
        };
    }

    private async Task ListenAsync() {
        while (!_cancellation.IsCancellationRequested) {
            HttpListenerContext context;

            try {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            } catch (HttpListenerException) when (_cancellation.IsCancellationRequested || !_listener.IsListening) {
                break;
            } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
                break;
            }

            string rawUrl = string.IsNullOrWhiteSpace(context.Request.RawUrl) ? "/" : context.Request.RawUrl;
            if (!_responses.TryGetValue(rawUrl, out ServerResponse response)) {
                context.Response.StatusCode = 404;
                context.Response.Close();
                continue;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(response.Body);
            context.Response.ContentType = response.ContentType;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }
    }

    public void Dispose() {
        _cancellation.Cancel();
        try {
            if (_listener.IsListening) {
                _listener.Stop();
            }
        } catch {
        }

        _listener.Close();

        try {
            _serverTask.Wait(TimeSpan.FromSeconds(5));
        } catch {
        }

        _cancellation.Dispose();
    }
}
"@
        }

        function New-StreamingAttachmentServerPrefix {
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            $port = ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port
            $listener.Stop()

            "http://127.0.0.1:$port/"
        }

        function Start-StreamingAttachmentServer {
            $documents = Join-Path $PSScriptRoot 'Documents'
            $server = [StreamingAttachmentTestHttpServer]::new((New-StreamingAttachmentServerPrefix))
            $server.AddResponse('/multi_download.html', [System.IO.File]::ReadAllText((Join-Path $documents 'multi_download.html')), 'text/html; charset=utf-8')
            $server.AddResponse('/download1.txt', [System.IO.File]::ReadAllText((Join-Path $documents 'download1.txt')), 'text/plain; charset=utf-8')
            $server.AddResponse('/download2.txt', [System.IO.File]::ReadAllText((Join-Path $documents 'download2.txt')), 'text/plain; charset=utf-8')
            $server
        }
    }

    It 'Outputs file paths as downloads complete' {
        $server = Start-StreamingAttachmentServer
        try {
            $uri = $server.Prefix + 'multi_download.html'
            $dest = Join-Path $TestDrive 'stream'
            $results = @()
            foreach ($file in Save-HTMLAttachment -Url $uri -Path $dest) {
                $results += $file
            }
            $results.Count | Should -Be 2
            Test-Path (Join-Path $dest 'download1.txt') | Should -BeTrue
            Test-Path (Join-Path $dest 'download2.txt') | Should -BeTrue
        }
        finally {
            if ($server) {
                $server.Dispose()
            }
        }
    }
}
