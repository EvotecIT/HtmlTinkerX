using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Sends each request through one handler of a pool, normally one per proxy. Requests take the handlers in turn
/// unless a selector chooses; retries by <see cref="HtmlFetchHandler"/> therefore leave through the next proxy.
/// Owns and disposes the handlers.
/// </summary>
public sealed class HtmlProxyRotator : HttpMessageHandler {
    private readonly HttpMessageInvoker[] _handlers;
    private readonly HttpMessageHandler[] _inner;
    private readonly Func<HttpRequestMessage, int, int>? _selector;
    private int _next = -1;

    /// <summary>Creates a rotator over existing handlers.</summary>
    /// <param name="handlers">Handlers to send through; each must not follow redirects automatically.</param>
    /// <param name="selector">Optional choice of handler index from the request and the pool size; cyclic when null.</param>
    public HtmlProxyRotator(IEnumerable<HttpMessageHandler> handlers, Func<HttpRequestMessage, int, int>? selector = null) {
        if (handlers == null) throw new ArgumentNullException(nameof(handlers));
        _inner = handlers.ToArray();
        if (_inner.Length == 0 || _inner.Any(static handler => handler == null)) throw new ArgumentException("At least one handler is required.", nameof(handlers));
        foreach (HttpMessageHandler handler in _inner) HtmlFetchHandler.RequireManualRedirects(handler);
        _handlers = _inner.Select(static handler => new HttpMessageInvoker(handler, disposeHandler: true)).ToArray();
        _selector = selector;
    }

    /// <summary>
    /// Creates a rotator with one handler per proxy address, using the factory's proxy configuration. The handlers
    /// share one cookie container, so a session cookie set through one proxy is sent through the next.
    /// </summary>
    /// <param name="proxies">Proxy addresses, for example <c>http://proxy1:8080</c>.</param>
    /// <param name="credential">Optional credentials for every proxy.</param>
    /// <param name="selector">Optional choice of proxy index from the request and the pool size; cyclic when null.</param>
    /// <param name="cookies">The shared cookie container, for example one that browser cookies are handed into; a new one when null.</param>
    public static HtmlProxyRotator Create(IEnumerable<string> proxies, ICredentials? credential = null, Func<HttpRequestMessage, int, int>? selector = null, CookieContainer? cookies = null) {
        if (proxies == null) throw new ArgumentNullException(nameof(proxies));
        string[] addresses = proxies.Where(static proxy => !string.IsNullOrWhiteSpace(proxy)).ToArray();
        if (addresses.Length == 0) throw new ArgumentException("At least one proxy is required.", nameof(proxies));
        cookies ??= new CookieContainer();
        return new HtmlProxyRotator(addresses.Select(proxy => {
            HttpClientHandler handler = HtmlHttpClientFactory.CreateHandler(proxy, credential, allowAutoRedirect: false);
            handler.CookieContainer = cookies;
            return handler;
        }), selector);
    }

    /// <summary>Number of handlers in the pool.</summary>
    public int Count => _handlers.Length;

    internal IEnumerable<HttpMessageHandler> Handlers => _inner;

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        int index = _selector != null
            ? _selector(request, _handlers.Length)
            : (int)((uint)Interlocked.Increment(ref _next) % (uint)_handlers.Length);
        if (index < 0 || index >= _handlers.Length) throw new InvalidOperationException($"The proxy selector returned {index}; expected 0 to {_handlers.Length - 1}.");
        return _handlers[index].SendAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        if (disposing) {
            foreach (HttpMessageInvoker handler in _handlers) handler.Dispose();
        }
        base.Dispose(disposing);
    }
}
