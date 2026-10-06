using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    private sealed class LoopbackPopupServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly System.Collections.Concurrent.ConcurrentBag<Task> _clients = new();
        private readonly Task _serverTask;
        private readonly string _popupRedirectTarget;
        private string? _lastRedirectToken;
        private int _redirectRequestCount;
        private string? _lastPopupToken;
        private string? _lastProtectedToken;
        private string? _lastPopupReferer;
        private string? _lastSelfReferer;
        private string? _lastExistingContextToken;
        private string? _lastSubmitAction;
        private string? _lastImageSubmitCoordinates;
        private string? _lastPopupFetchToken;
        private string? _lastPopupCssToken;
        private string? _lastPopupScriptToken;
        private string? _lastPopupWorkerToken;
        private string? _lastPopupEventToken;
        private int _blankPopupResourceRequests;
        private readonly TaskCompletionSource<bool> _blankPopupResourceReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _unauthorizedBlankPopupResourceRequests;
        private int _styleTextResourceRequests;
        private int _removedNamespacedResourceRequests;
        private int _popupRequestCount;
        private readonly ConcurrentDictionary<string, (int Count, string Cookie)> _blankPopupSources = new(StringComparer.Ordinal);
        private readonly string _namedContextInitialUrl;
        internal LoopbackPopupServer(string? namedContextInitialUrl = null, string? popupRedirectTarget = null) {
            _namedContextInitialUrl = namedContextInitialUrl ?? "/existing-context-initial";
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            HeaderUrl = $"http://127.0.0.1:{port}/header-main";
            CrossOriginRedirectUrl = $"http://127.0.0.1:{port}/redirect-to-header-popup";
            _popupRedirectTarget = popupRedirectTarget ?? HeaderUrl.Replace("/header-main", "/header-popup");
            BlankPopupResourceUrl = $"http://127.0.0.1:{port}/blank-popup-resource";
            NestedPopupUrl = $"http://127.0.0.1:{port}/nested-main";
            NoOpenerHeaderUrl = $"http://127.0.0.1:{port}/header-noopener-main";
            StorageUrl = $"http://127.0.0.1:{port}/storage-main";
            StorageForgeryUrl = $"http://127.0.0.1:{port}/storage-forgery-main";
            ExistingContextUrl = $"http://127.0.0.1:{port}/existing-context-main";
            NamedContextUrl = $"http://127.0.0.1:{port}/named-context-main";
            DeclarativeAnchorUrl = $"http://127.0.0.1:{port}/declarative-anchor-main";
            DeclarativeFormUrl = $"http://127.0.0.1:{port}/declarative-form-main";
            DeclarativeFormOpenerUrl = $"http://127.0.0.1:{port}/declarative-form-opener-main";
            DeclarativeNamedUrl = $"http://127.0.0.1:{port}/declarative-named-main";
            DeclarativeSingleSubmitUrl = $"http://127.0.0.1:{port}/declarative-single-submit-main";
            DeclarativeImageSubmitUrl = $"http://127.0.0.1:{port}/declarative-image-submit-main";
            DeclarativeSelfAnchorUrl = $"http://127.0.0.1:{port}/declarative-self-anchor-main";
            DeclarativeSelfFormUrl = $"http://127.0.0.1:{port}/declarative-self-form-main";
            DeclarativeExplicitSelfAnchorUrl = $"http://127.0.0.1:{port}/declarative-explicit-self-anchor-main";
            DeclarativeExplicitSelfFormUrl = $"http://127.0.0.1:{port}/declarative-explicit-self-form-main";
            DeclarativeExplicitSelfNativeFormUrl = $"http://127.0.0.1:{port}/declarative-explicit-self-native-form-main";
            DeclarativeCancelledAnchorUrl = $"http://127.0.0.1:{port}/declarative-cancelled-anchor-main";
            DeclarativeWindowCancelledAnchorUrl = $"http://127.0.0.1:{port}/declarative-window-cancelled-anchor-main";
            DeclarativeReferrerPolicyUrl = $"http://127.0.0.1:{port}/declarative-referrer-policy-main";
            DeclarativeExplicitSelfReferrerPolicyUrl = $"http://127.0.0.1:{port}/declarative-explicit-self-referrer-policy-main";
            SiblingNamedContextUrl = $"http://127.0.0.1:{port}/sibling-named-context-main";
            _serverTask = ServeAsync();
        }
        internal string HeaderUrl { get; }
        internal string CrossOriginRedirectUrl { get; }
        internal string? LastRedirectToken => Volatile.Read(ref _lastRedirectToken);
        internal int RedirectRequestCount => Volatile.Read(ref _redirectRequestCount);
        internal string BlankPopupResourceUrl { get; }
        internal string NestedPopupUrl { get; }
        internal string NoOpenerHeaderUrl { get; }
        internal string StorageUrl { get; }
        internal string StorageForgeryUrl { get; }
        internal string ExistingContextUrl { get; }
        internal string NamedContextUrl { get; }
        internal string DeclarativeAnchorUrl { get; }
        internal string DeclarativeFormUrl { get; }
        internal string DeclarativeFormOpenerUrl { get; }
        internal string DeclarativeNamedUrl { get; }
        internal string DeclarativeSingleSubmitUrl { get; }
        internal string DeclarativeImageSubmitUrl { get; }
        internal string DeclarativeSelfAnchorUrl { get; }
        internal string DeclarativeSelfFormUrl { get; }
        internal string DeclarativeExplicitSelfAnchorUrl { get; }
        internal string DeclarativeExplicitSelfFormUrl { get; }
        internal string DeclarativeExplicitSelfNativeFormUrl { get; }
        internal string DeclarativeCancelledAnchorUrl { get; }
        internal string DeclarativeWindowCancelledAnchorUrl { get; }
        internal string DeclarativeReferrerPolicyUrl { get; }
        internal string DeclarativeExplicitSelfReferrerPolicyUrl { get; }
        internal string SiblingNamedContextUrl { get; }
        internal string? LastPopupToken => Volatile.Read(ref _lastPopupToken);
        internal string? LastProtectedToken => Volatile.Read(ref _lastProtectedToken);
        internal string? LastPopupReferer => Volatile.Read(ref _lastPopupReferer);
        internal string? LastSelfReferer => Volatile.Read(ref _lastSelfReferer);
        internal string? LastExistingContextToken => Volatile.Read(ref _lastExistingContextToken);
        internal int BlankPopupResourceRequests => Volatile.Read(ref _blankPopupResourceRequests);
        internal async Task<bool> WaitForBlankPopupResourceAsync() =>
            await Task.WhenAny(_blankPopupResourceReceived.Task, Task.Delay(5000)) == _blankPopupResourceReceived.Task;
        internal int UnauthorizedBlankPopupResourceRequests => Volatile.Read(ref _unauthorizedBlankPopupResourceRequests);
        internal int StyleTextResourceRequests => Volatile.Read(ref _styleTextResourceRequests);
        internal int BlankPopupSourceRequests(string source) => _blankPopupSources.TryGetValue(source, out var request) ? request.Count : 0;
        internal string BlankPopupSourceCookie(string source) => _blankPopupSources.TryGetValue(source, out var request) ? request.Cookie : string.Empty;
        internal int RemovedNamespacedResourceRequests => Volatile.Read(ref _removedNamespacedResourceRequests);
        internal int PopupRequestCount => Volatile.Read(ref _popupRequestCount);
        internal string? LastPopupFetchToken => Volatile.Read(ref _lastPopupFetchToken);
        internal string? LastPopupCssToken => Volatile.Read(ref _lastPopupCssToken);
        internal string? LastPopupScriptToken => Volatile.Read(ref _lastPopupScriptToken);
        internal string? LastPopupWorkerToken => Volatile.Read(ref _lastPopupWorkerToken);
        internal string? LastPopupEventToken => Volatile.Read(ref _lastPopupEventToken);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _clients.Add(HandleClientAsync(client));
                } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
                    return;
                } catch (SocketException) when (_cancellation.IsCancellationRequested) {
                    return;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client) {
            try {
                using (client) {
                    using NetworkStream stream = client.GetStream();
                    using CancellationTokenRegistration registration = _cancellation.Token.Register(client.Dispose);
                    string request = await LoopbackHttpRequestReader.ReadAsync(stream, _cancellation.Token);
                    string requestTarget = request.Split(' ')[1];
                    string contentType = "text/html; charset=utf-8";
                    string body;
                    string status = "200 OK";
                    string locationHeader = string.Empty;
                    if (requestTarget.StartsWith("/redirect-to-header-popup", StringComparison.Ordinal)) {
                        Interlocked.Increment(ref _redirectRequestCount);
                        Volatile.Write(ref _lastRedirectToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        status = "302 Found";
                        locationHeader = $"Location: {_popupRedirectTarget}\r\n";
                        body = string.Empty;
                    } else if (requestTarget.StartsWith("/nested-parent", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = "<script>window.open('/nested-child', '_blank');</script>";
                    } else if (requestTarget.StartsWith("/nested-child", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = "<p>nested child</p>";
                    } else if (requestTarget.StartsWith("/nested-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token"
                            ? "nested popup authorized"
                            : "pending";
                    } else if (requestTarget.StartsWith("/nested-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => fetch('/nested-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/header-popup-noopener", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        Volatile.Write(ref _lastPopupReferer, LoopbackHtmlServer.ReadHeader(request, "Referer"));
                        body = "<script>fetch('/protected').then(response => response.text()).then(text => localStorage.setItem('popup-result', text));</script>";
                    } else if (requestTarget.StartsWith("/header-popup", StringComparison.Ordinal)) {
                        Interlocked.Increment(ref _popupRequestCount);
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        Volatile.Write(ref _lastPopupReferer, LoopbackHtmlServer.ReadHeader(request, "Referer"));
                        if (requestTarget.Contains("action=approve", StringComparison.Ordinal)) {
                            Volatile.Write(ref _lastSubmitAction, "approve");
                        }
                        string? imageX = ReadQueryValue(requestTarget, "approval.x");
                        string? imageY = ReadQueryValue(requestTarget, "approval.y");
                        if (imageX != null && imageY != null) {
                            Volatile.Write(ref _lastImageSubmitCoordinates, imageX + "," + imageY);
                        }
                        body = "<script>fetch('/protected').then(response => response.text()).then(text => opener.postMessage(text, '*'));</script>";
                    } else if (requestTarget.StartsWith("/blank-popup-fetch", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" ? "popup authorized" : "popup denied";
                    } else if (requestTarget.StartsWith("/popup/fetch-result", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupFetchToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = "popup fetch completed";
                    } else if (requestTarget.StartsWith("/popup/protected.css", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupCssToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/css; charset=utf-8";
                        body = "body { color: black; }";
                    } else if (requestTarget.StartsWith("/popup/script-result", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupScriptToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = "popup script completed";
                    } else if (requestTarget.StartsWith("/popup/worker.js", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupWorkerToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "application/javascript; charset=utf-8";
                        body = LastPopupWorkerToken == "popup-token"
                            ? "onmessage = () => postMessage('popup worker authorized');"
                            : "onmessage = () => postMessage('popup worker denied');";
                    } else if (requestTarget.StartsWith("/popup/events", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupEventToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/event-stream; charset=utf-8";
                        body = LastPopupEventToken == "popup-token"
                            ? "data: popup event authorized\n\n"
                            : "data: popup event denied\n\n";
                    } else if (requestTarget.StartsWith("/popup/blocking.js", StringComparison.Ordinal)) {
                        contentType = "application/javascript; charset=utf-8";
                        body = "globalThis.externalReady = true;";
                    } else if (requestTarget.StartsWith("/popup-fetch-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = Volatile.Read(ref _lastPopupFetchToken) == "popup-token"
                            ? "popup fetch authorized"
                            : "pending";
                    } else if (requestTarget.StartsWith("/popup-resource-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = Volatile.Read(ref _lastPopupCssToken) == "popup-token"
                            && Volatile.Read(ref _lastPopupScriptToken) == "popup-token"
                                ? "popup resources authorized"
                                : "pending";
                    } else if (requestTarget.StartsWith("/split-style-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = BlankPopupSourceRequests("split-write-style").ToString(System.Globalization.CultureInfo.InvariantCulture);
                    } else if (requestTarget.StartsWith("/blank-popup-location", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        string result = LastPopupToken == "popup-token" ? "popup authorized" : "popup denied";
                        body = $"<script>opener.postMessage('{result}', '*');</script>";
                    } else if (requestTarget.StartsWith("/blank-popup-resource", StringComparison.Ordinal)) {
                        Interlocked.Increment(ref _blankPopupResourceRequests);
                        int sourceStart = requestTarget.IndexOf("source=", StringComparison.Ordinal);
                        if (sourceStart >= 0) {
                            string source = requestTarget.Substring(sourceStart + 7).Split('&')[0];
                            string cookie = LoopbackHtmlServer.ReadHeader(request, "Cookie") ?? string.Empty;
                            _blankPopupSources.AddOrUpdate(source, (1, cookie), (_, previous) => (previous.Count + 1, cookie));
                        }
                        if (requestTarget.Contains("source=style-text", StringComparison.Ordinal)) Interlocked.Increment(ref _styleTextResourceRequests);
                        if (requestTarget.Contains("source=removed-namespace", StringComparison.Ordinal)) Interlocked.Increment(ref _removedNamespacedResourceRequests);
                        string? token = LoopbackHtmlServer.ReadHeader(request, "X-Render-Token");
                        if (token != "popup-token") Interlocked.Increment(ref _unauthorizedBlankPopupResourceRequests);
                        Volatile.Write(ref _lastPopupToken, token);
                        _blankPopupResourceReceived.TrySetResult(true);
                        int bodyOffset = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        bool echoBody = requestTarget.Contains("echo-body", StringComparison.Ordinal); bool imageBody = requestTarget.Contains("source=image-decode", StringComparison.Ordinal);
                        contentType = echoBody ? "text/plain; charset=utf-8" : imageBody ? "image/svg+xml" : "application/javascript; charset=utf-8";
                        body = echoBody && bodyOffset >= 0 ? request.Substring(bodyOffset + 4) : imageBody ? "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'/>" : "void 0;";
                    } else if (requestTarget.StartsWith("/protected", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token" ? "popup authorized" : "popup denied";
                    } else if (requestTarget.StartsWith("/popup-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token" ? "popup authorized" : "pending";
                    } else if (requestTarget.StartsWith("/popup-count-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = PopupRequestCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    } else if (requestTarget.StartsWith("/submitter-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token"
                            ? "popup authorized|" + (Volatile.Read(ref _lastSubmitAction) ?? "missing")
                            : "pending";
                    } else if (requestTarget.StartsWith("/image-submit-status", StringComparison.Ordinal)) {
                        contentType = "text/plain; charset=utf-8";
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token"
                            ? "popup authorized|" + (Volatile.Read(ref _lastImageSubmitCoordinates) ?? "missing")
                            : "pending";
                    } else if (requestTarget.StartsWith("/storage-popup", StringComparison.Ordinal)) {
                        body = "<script>localStorage.setItem('observed', localStorage.getItem('token') || 'missing'); close();</script>";
                    } else if (requestTarget.StartsWith("/storage-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => document.querySelector('#result').textContent = localStorage.getItem('observed') || 'pending', 20);</script>";
                    } else if (requestTarget.StartsWith("/storage-forgery-main", StringComparison.Ordinal)) {
                        body = "<p>storage forgery probe</p><script>for (const key of Object.getOwnPropertyNames(globalThis)) { if (key.startsWith('__htmltinkerx_storage_') || key.startsWith('htmltinkerxStorage')) globalThis[key] = JSON.stringify({ completed: true, errors: [] }); }</script>";
                    } else if (requestTarget.StartsWith("/existing-context-destination", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastExistingContextToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = LastExistingContextToken == "popup-token"
                            ? "<p id='result'>existing context authorized</p>"
                            : "<p id='result'>existing context denied</p>";
                    } else if (requestTarget.StartsWith("/named-context-main", StringComparison.Ordinal)) {
                        body = $"<p id='result'>pending</p><iframe name='reportFrame' src='{System.Net.WebUtility.HtmlEncode(_namedContextInitialUrl)}'></iframe><script>setInterval(() => {{ try {{ document.querySelector('#result').textContent = frames.reportFrame.document.querySelector('#result').textContent; }} catch {{ }} }}, 20);</script>";
                    } else if (requestTarget.StartsWith("/sibling-named-context-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><iframe name='sourceFrame' srcdoc=\"<a href='/existing-context-destination' target='reportFrame'>open</a>\"></iframe><iframe name='reportFrame' src='about:blank'></iframe><script>setInterval(() => { try { const text = frames.reportFrame.document.querySelector('#result')?.textContent; if (text) document.querySelector('#result').textContent = text; } catch { } }, 20);</script>";
                    } else if (requestTarget.StartsWith("/existing-context-initial", StringComparison.Ordinal)
                        || requestTarget.StartsWith("/existing-context-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p>";
                    } else if (requestTarget.StartsWith("/header-noopener-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><script>setInterval(() => document.querySelector('#result').textContent = localStorage.getItem('popup-result') || 'pending', 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank'>open</a><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-form-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/header-popup' method='post' target='_blank'><button type='submit'>open</button></form><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-form-opener-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/header-popup' method='post' target='_blank' rel='opener'><button type='submit'>open</button></form><script>addEventListener('message', event => document.querySelector('#result').textContent = event.data);</script>";
                    } else if (requestTarget.StartsWith("/declarative-named-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='reportWindow'>open</a><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-single-submit-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/wrong-popup' method='post' target='_blank'><button type='submit' name='action' value='approve' formaction='/header-popup' formmethod='get'>open</button></form><script>let submitCount = 0; document.addEventListener('submit', () => submitCount++); setInterval(() => fetch('/submitter-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text + '|' + submitCount), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-image-submit-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/header-popup' method='get' target='_blank'><input name='approval' type='image' alt='approve' style='width:40px;height:30px' src='data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2240%22 height=%2230%22/%3E'></form><script>setInterval(() => fetch('/image-submit-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-self-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/self-destination'>open</a>";
                    } else if (requestTarget.StartsWith("/declarative-self-form-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><form action='/self-destination' method='post'><button type='submit'>open</button></form>";
                    } else if (requestTarget.StartsWith("/declarative-explicit-self-anchor-main", StringComparison.Ordinal)) {
                        body = "<base target='_blank'><p id='result'>pending</p><a href='/self-destination' target=''>open</a>";
                    } else if (requestTarget.StartsWith("/declarative-explicit-self-form-main", StringComparison.Ordinal)) {
                        body = "<base target='_blank'><p id='result'>pending</p><form action='/self-destination' method='post' target='_blank'><button type='submit' formtarget=''>open</button></form>";
                    } else if (requestTarget.StartsWith("/declarative-explicit-self-native-form-main", StringComparison.Ordinal)) {
                        body = "<base target='_blank'><p id='result'>pending</p><form action='/self-destination' method='post' target=''><button type='submit'>open</button></form>";
                    } else if (requestTarget.StartsWith("/declarative-cancelled-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank'>open</a><script>document.addEventListener('click', event => { event.preventDefault(); document.querySelector('#result').textContent = 'navigation cancelled'; });</script>";
                    } else if (requestTarget.StartsWith("/declarative-window-cancelled-anchor-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank'>open</a><script>window.addEventListener('click', event => { event.preventDefault(); document.querySelector('#result').textContent = 'window navigation cancelled'; });</script>";
                    } else if (requestTarget.StartsWith("/declarative-referrer-policy-main", StringComparison.Ordinal)) {
                        body = "<p id='result'>pending</p><a href='/header-popup' target='_blank' rel='opener' referrerpolicy='no-referrer'>open</a><script>setInterval(() => fetch('/popup-status').then(response => response.text()).then(text => document.querySelector('#result').textContent = text), 20);</script>";
                    } else if (requestTarget.StartsWith("/declarative-explicit-self-referrer-policy-main", StringComparison.Ordinal)) {
                        body = "<base target='_blank'><p id='result'>pending</p><a href='/self-destination' target='' referrerpolicy='no-referrer'>open</a>";
                    } else if (requestTarget.StartsWith("/self-destination", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastSelfReferer, LoopbackHtmlServer.ReadHeader(request, "Referer"));
                        body = "<p id='self-result'>self navigated</p>";
                    } else {
                        body = "<p id='result'>pending</p><script>addEventListener('message', event => document.querySelector('#result').textContent = event.data);</script>";
                    }
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n{locationHeader}Content-Type: {contentType}\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, 0, response.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                }
            } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
            } catch (SocketException) when (_cancellation.IsCancellationRequested) {
            } catch (IOException) when (_cancellation.IsCancellationRequested) {
            }
        }

        private static string? ReadQueryValue(string requestTarget, string name) {
            int queryIndex = requestTarget.IndexOf('?');
            if (queryIndex < 0) return null;
            foreach (string part in requestTarget.Substring(queryIndex + 1).Split('&')) {
                int equals = part.IndexOf('=');
                string key = equals < 0 ? part : part.Substring(0, equals);
                if (string.Equals(Uri.UnescapeDataString(key), name, StringComparison.Ordinal)) {
                    return equals < 0 ? string.Empty : Uri.UnescapeDataString(part.Substring(equals + 1));
                }
            }
            return null;
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            await Task.WhenAll(_clients.ToArray());
            _cancellation.Dispose();
        }
    }

    private sealed class LoopbackStreamingPopupServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Task> _clients = new();
        private readonly TaskCompletionSource<bool> _protectedObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _serverTask;
        private string? _lastPopupToken;
        private string? _lastProtectedToken;
        private long _nextClient;

        internal LoopbackStreamingPopupServer() {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/streaming-main";
            _serverTask = ServeAsync();
        }

        internal string Url { get; }
        internal string? LastPopupToken => Volatile.Read(ref _lastPopupToken);
        internal string? LastProtectedToken => Volatile.Read(ref _lastProtectedToken);

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    long id = Interlocked.Increment(ref _nextClient);
                    Task handling = HandleClientAsync(client);
                    _clients[id] = handling;
                    _ = handling.ContinueWith(
                        completed => {
                            _clients.TryRemove(id, out _);
                            _ = completed.Exception;
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                } catch (Exception ex) when (_cancellation.IsCancellationRequested
                    && (ex is ObjectDisposedException || ex is SocketException)) {
                    return;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client) {
            using (client)
            using (NetworkStream stream = client.GetStream()) {
                try {
                    byte[] buffer = new byte[8192];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellation.Token);
                    string request = Encoding.ASCII.GetString(buffer, 0, read);
                    string requestTarget = request.Split(' ')[1];
                    if (requestTarget.StartsWith("/streaming-popup", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastPopupToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        const string streamingBody = "<script>fetch('/streaming-protected').then(response => response.text()).then(text => opener.postMessage(text, '*'));</script>";
                        byte[] bodyBytes = Encoding.UTF8.GetBytes(streamingBody);
                        byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n");
                        byte[] chunkHeader = Encoding.ASCII.GetBytes(bodyBytes.Length.ToString("X") + "\r\n");
                        await stream.WriteAsync(headers, 0, headers.Length, _cancellation.Token);
                        await stream.WriteAsync(chunkHeader, 0, chunkHeader.Length, _cancellation.Token);
                        await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, _cancellation.Token);
                        await stream.WriteAsync(new byte[] { 13, 10 }, 0, 2, _cancellation.Token);
                        await stream.FlushAsync(_cancellation.Token);
                        await _protectedObserved.Task;
                        byte[] completed = Encoding.ASCII.GetBytes("0\r\n\r\n");
                        await stream.WriteAsync(completed, 0, completed.Length, _cancellation.Token);
                        return;
                    }

                    string body;
                    string contentType = "text/html; charset=utf-8";
                    if (requestTarget.StartsWith("/streaming-protected", StringComparison.Ordinal)) {
                        Volatile.Write(ref _lastProtectedToken, LoopbackHtmlServer.ReadHeader(request, "X-Render-Token"));
                        body = LastPopupToken == "popup-token" && LastProtectedToken == "popup-token"
                            ? "streaming popup authorized"
                            : "streaming popup denied";
                        contentType = "text/plain; charset=utf-8";
                        _protectedObserved.TrySetResult(true);
                    } else {
                        body = "<p id='result'>pending</p><script>addEventListener('message', event => document.querySelector('#result').textContent = event.data);</script>";
                    }
                    byte[] bodyResponse = Encoding.UTF8.GetBytes(body);
                    byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bodyResponse.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, 0, response.Length, _cancellation.Token);
                    await stream.WriteAsync(bodyResponse, 0, bodyResponse.Length, _cancellation.Token);
                } catch (Exception ex) when (_cancellation.IsCancellationRequested
                    && (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException || ex is IOException)) {
                    return;
                }
            }
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _protectedObserved.TrySetCanceled();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            Task[] clients = _clients.Values.ToArray();
            if (clients.Length > 0) {
                try { await Task.WhenAll(clients); } catch (OperationCanceledException) { }
            }
            _cancellation.Dispose();
        }
    }
}
