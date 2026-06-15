Describe 'Save-HTMLAttachment' {
    BeforeAll {
        if (-not ('DownloadAttachmentTestHttpServer' -as [type])) {
            Add-Type -TypeDefinition @"
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class DownloadAttachmentTestHttpServer : IDisposable {
    private sealed class ServerResponse {
        public string Body { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/html; charset=utf-8";
    }

    private readonly HttpListener _listener = new HttpListener();
    private readonly ConcurrentDictionary<string, ServerResponse> _responses = new ConcurrentDictionary<string, ServerResponse>(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    private readonly Task _serverTask;

    public DownloadAttachmentTestHttpServer(string prefix) {
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

        function New-DownloadAttachmentServerPrefix {
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            try {
                $port = ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port
                "http://127.0.0.1:$port/"
            } finally {
                $listener.Stop()
            }
        }

        function Start-DownloadAttachmentServer {
            $documents = Join-Path $PSScriptRoot 'Documents'

            for ($attempt = 1; $attempt -le 10; $attempt++) {
                try {
                    $server = [DownloadAttachmentTestHttpServer]::new((New-DownloadAttachmentServerPrefix))
                    $server.AddResponse('/multi_manual_download.html', [System.IO.File]::ReadAllText((Join-Path $documents 'multi_manual_download.html')), 'text/html; charset=utf-8')
                    $server.AddResponse('/download1.txt', [System.IO.File]::ReadAllText((Join-Path $documents 'download1.txt')), 'text/plain; charset=utf-8')
                    $server.AddResponse('/download2.txt', [System.IO.File]::ReadAllText((Join-Path $documents 'download2.txt')), 'text/plain; charset=utf-8')
                    return $server
                } catch [System.Net.HttpListenerException] {
                    if ($server) {
                        $server.Dispose()
                    }

                    if ($attempt -eq 10) {
                        throw
                    }

                    Start-Sleep -Milliseconds 100
                }
            }
        }
    }

    It 'Saves downloads on the page by filter' {
        $server = Start-DownloadAttachmentServer
        try {
            $dest = Join-Path $TestDrive 'dl'
            [array] $files = Save-HTMLAttachment -Url ($server.Prefix + 'multi_manual_download.html') -Path $dest -Filter 'download'
            $files.Count | Should -Be 2
            (Get-Item -Path $files[0]).Name | Should -BeIn @('download1.txt', 'download2.txt')
            (Get-Item -Path $files[1]).Name | Should -BeIn @('download1.txt', 'download2.txt')
        } finally {
            if ($server) {
                $server.Dispose()
            }
        }
    }

    It 'Downloads are fully written to disk' {
        $server = Start-DownloadAttachmentServer
        try {
            $dest = Join-Path $TestDrive 'dl-full'
            [array] $files = Save-HTMLAttachment -Url ($server.Prefix + 'multi_manual_download.html') -Path $dest -Filter 'download'
            foreach ($path in $files) {
                (Get-Item $path).Length | Should -BeGreaterThan 0
            }
        } finally {
            if ($server) {
                $server.Dispose()
            }
        }
    }
}
