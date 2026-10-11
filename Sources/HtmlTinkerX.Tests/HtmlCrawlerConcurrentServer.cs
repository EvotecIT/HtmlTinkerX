using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    private sealed class ConcurrentCrawlServer : IAsyncDisposable {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly List<Task> _responses = new();
        private readonly Task _accepting;
        private readonly Func<HttpListenerContext, CancellationToken, Task> _respond;
        internal string Url { get; }

        internal ConcurrentCrawlServer(Func<HttpListenerContext, CancellationToken, Task> respond) {
            _respond = respond;
            StartListenerWithFreePort(_listener, out string root);
            Url = root;
            _accepting = AcceptAsync();
        }

        private async Task AcceptAsync() {
            try {
                while (!_stop.IsCancellationRequested) {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    _responses.Add(RespondAsync(context));
                }
            } catch (Exception error) when (_stop.IsCancellationRequested &&
                (error is HttpListenerException || error is ObjectDisposedException)) { }
        }

        private async Task RespondAsync(HttpListenerContext context) {
            bool complete = false;
            try { await _respond(context, _stop.Token); complete = true; }
            catch (IOException) { /* The crawler can close a rejected or canceled response. */ }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            finally {
                try {
                    if (complete) context.Response.Close();
                    else context.Response.Abort();
                } catch (ObjectDisposedException) when (_stop.IsCancellationRequested) {
                    // Closing the listener also closes responses that are still unwinding.
                }
            }
        }

        public async ValueTask DisposeAsync() {
            _stop.Cancel();
            _listener.Close();
            try {
                await _accepting;
                await Task.WhenAll(_responses);
            } finally { _stop.Dispose(); }
        }
    }

    private static TaskCompletionSource<bool> CrawlSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task AwaitCrawlSignalAsync(Task signal, CancellationToken token = default) {
        Task completed = await Task.WhenAny(signal, Task.Delay(5000, token));
        token.ThrowIfCancellationRequested();
        Assert.Same(signal, completed);
        await signal;
    }
    private static async Task SendHeldCrawlBodyAsync(HttpListenerContext context, string text, Task release, CancellationToken token) {
        byte[] body = System.Text.Encoding.UTF8.GetBytes(text);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body, 0, 1, token);
        await context.Response.OutputStream.FlushAsync(token);
        await AwaitCrawlSignalAsync(release, token);
        await context.Response.OutputStream.WriteAsync(body, 1, body.Length - 1, token);
    }
}
