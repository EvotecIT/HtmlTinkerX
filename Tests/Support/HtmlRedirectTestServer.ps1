if (-not ('HtmlRedirectTestServer' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class HtmlRedirectTestServer : IDisposable {
    private readonly HttpListener listener;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

    public HtmlRedirectTestServer() {
        int port;
        using (var tcp = new TcpListener(IPAddress.Parse("127.0.0.1"), 0)) {
            tcp.Start();
            port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        }

        Url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/";
        listener = new HttpListener();
        listener.Prefixes.Add(Url);
        listener.Start();
        _ = Task.Run(RunAsync);
    }

    public string Url { get; }

    public void Dispose() {
        cancellation.Cancel();
        listener.Close();
        cancellation.Dispose();
    }

    private async Task RunAsync() {
        while (!cancellation.IsCancellationRequested && listener.IsListening) {
            HttpListenerContext context;
            try {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            } catch (HttpListenerException) {
                break;
            } catch (ObjectDisposedException) {
                break;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context) {
        string path = context.Request.Url.AbsolutePath;
        if (path == "/redirect-workbench") {
            Redirect(context.Response, "/final/workbench");
            return;
        }

        if (path == "/redirect-api") {
            Redirect(context.Response, "/final/api");
            return;
        }

        if (path == "/redirect-dataset") {
            Redirect(context.Response, "/final/dataset");
            return;
        }

        if (path == "/final/workbench") {
            await WriteAsync(context.Response, "<html><body><main><h1>Workbench</h1><a href=\"relative-link\">Relative</a><form method=\"post\" action=\"relative-api\"><input name=\"q\" /></form></main></body></html>").ConfigureAwait(false);
            return;
        }

        if (path == "/final/api") {
            await WriteAsync(context.Response, "<html><body><main><h1>API</h1><form method=\"get\" action=\"relative-api?token=abc123\"><input name=\"q\" /></form></main></body></html>").ConfigureAwait(false);
            return;
        }

        if (path == "/final/dataset") {
            await WriteAsync(context.Response, "<html><body><main><h1>Dataset</h1><p>Redirected dataset page.</p><script>fetch(\"relative-api\")</script></main></body></html>").ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = 404;
        context.Response.Close();
    }

    private static void Redirect(HttpListenerResponse response, string location) {
        response.StatusCode = 302;
        response.Headers["Location"] = location;
        response.Close();
    }

    private static async Task WriteAsync(HttpListenerResponse response, string content) {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        response.Close();
    }
}
'@
}
