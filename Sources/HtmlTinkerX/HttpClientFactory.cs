using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace HtmlTinkerX;

/// <summary>
/// Factory for creating configured <see cref="HttpClient"/> instances.
/// Default timeout, headers and proxy settings can be configured globally.
/// </summary>
public static class HtmlHttpClientFactory {
    private static readonly object _sharedClientLock = new();
    private static volatile HttpClient? _sharedClient;

    /// <summary>Default timeout used for new clients.</summary>
    public static TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Default headers applied to created clients.</summary>
    /// <remarks>
    /// A descriptive user agent is included because a number of otherwise public sites reject
    /// requests that omit the header entirely. Consumers can replace or remove it through this
    /// dictionary before creating a client.
    /// </remarks>
    public static IDictionary<string, string> DefaultHeaders { get; } = CreateDefaultHeaders();

    /// <summary>Default proxy address.</summary>
    public static string? DefaultProxy { get; set; }

    /// <summary>Default proxy credentials.</summary>
    public static ICredentials? DefaultProxyCredential { get; set; }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> using defaults or the provided overrides.
    /// </summary>
    /// <param name="proxy">Optional proxy address. When omitted, <see cref="DefaultProxy"/> is used.</param>
    /// <param name="credential">Optional proxy credentials. When omitted, <see cref="DefaultProxyCredential"/> is used.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    public static HttpClient Create(string? proxy = null, ICredentials? credential = null, bool allowAutoRedirect = true) {
        HttpClientHandler handler = new() {
            AllowAutoRedirect = allowAutoRedirect
        };
        ConfigureProxy(handler, proxy, credential);
        HttpClient client = new HttpClient(handler, disposeHandler: true);
        ApplyDefaults(client);
        return client;
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> using configured defaults plus optional target-page credentials.
    /// </summary>
    /// <param name="proxy">Optional proxy address. When omitted, <see cref="DefaultProxy"/> is used.</param>
    /// <param name="proxyCredential">Optional proxy credentials. When omitted, <see cref="DefaultProxyCredential"/> is used.</param>
    /// <param name="credentials">Optional credentials for the target page.</param>
    /// <param name="preAuthenticate">Enables HTTP pre-authentication when page credentials are provided.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    public static HttpClient Create(string? proxy, ICredentials? proxyCredential, ICredentials? credentials, bool preAuthenticate = true, bool allowAutoRedirect = true) {
        HttpClientHandler handler = new() {
            AllowAutoRedirect = allowAutoRedirect,
            Credentials = credentials,
            PreAuthenticate = credentials != null && preAuthenticate
        };

        ConfigureProxy(handler, proxy, proxyCredential);
        HttpClient client = new HttpClient(handler, disposeHandler: true);
        ApplyDefaults(client);
        return client;
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> with cookie support and returns the used container.
    /// </summary>
    /// <param name="cookieContainer">Container that will store cookies for the created handler.</param>
    /// <param name="proxy">Optional proxy address.</param>
    /// <param name="credential">Optional proxy credentials.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    public static HttpClient Create(out CookieContainer cookieContainer, string? proxy = null, ICredentials? credential = null, bool allowAutoRedirect = true) {
        cookieContainer = new CookieContainer();
        return Create(cookieContainer, proxy, credential, allowAutoRedirect);
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> with an existing cookie container.
    /// </summary>
    /// <param name="cookieContainer">Container used by the returned handler.</param>
    /// <param name="proxy">Optional proxy address.</param>
    /// <param name="credential">Optional proxy credentials.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    public static HttpClient Create(CookieContainer cookieContainer, string? proxy = null, ICredentials? credential = null, bool allowAutoRedirect = true) {
        HttpClientHandler handler = new() {
            AllowAutoRedirect = allowAutoRedirect,
            CookieContainer = cookieContainer,
            UseCookies = true
        };
        ConfigureProxy(handler, proxy, credential);
        HttpClient client = new HttpClient(handler, disposeHandler: true);
        ApplyDefaults(client);
        return client;
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> with cookie support plus optional proxy and target-page credentials.
    /// </summary>
    /// <param name="cookieContainer">Container that will store cookies for the created handler.</param>
    /// <param name="proxy">Optional proxy address.</param>
    /// <param name="proxyCredential">Optional proxy credentials.</param>
    /// <param name="credentials">Optional credentials for the target page.</param>
    /// <param name="preAuthenticate">Enables HTTP pre-authentication when page credentials are provided.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    public static HttpClient Create(out CookieContainer cookieContainer, string? proxy, ICredentials? proxyCredential, ICredentials? credentials, bool preAuthenticate = true, bool allowAutoRedirect = true) {
        cookieContainer = new CookieContainer();
        return Create(cookieContainer, proxy, proxyCredential, credentials, preAuthenticate, allowAutoRedirect);
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> with an existing cookie container plus optional proxy and target-page credentials.
    /// </summary>
    /// <param name="cookieContainer">Container used by the returned handler.</param>
    /// <param name="proxy">Optional proxy address.</param>
    /// <param name="proxyCredential">Optional proxy credentials.</param>
    /// <param name="credentials">Optional credentials for the target page.</param>
    /// <param name="preAuthenticate">Enables HTTP pre-authentication when page credentials are provided.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    public static HttpClient Create(CookieContainer cookieContainer, string? proxy, ICredentials? proxyCredential, ICredentials? credentials, bool preAuthenticate = true, bool allowAutoRedirect = true) {
        HttpClientHandler handler = new() {
            AllowAutoRedirect = allowAutoRedirect,
            CookieContainer = cookieContainer,
            Credentials = credentials,
            PreAuthenticate = credentials != null && preAuthenticate,
            UseCookies = true
        };

        ConfigureProxy(handler, proxy, proxyCredential);
        HttpClient client = new HttpClient(handler, disposeHandler: true);
        ApplyDefaults(client);
        return client;
    }

    /// <summary>
    /// Gets a shared instance using the configured defaults.
    /// </summary>
    public static HttpClient Shared {
        get {
            if (_sharedClient == null) {
                lock (_sharedClientLock) {
                    if (_sharedClient == null) {
                        _sharedClient = Create();
                    }
                }
            }
            return _sharedClient!;
        }
    }

    /// <summary>Recreates the shared client with current defaults.</summary>
    public static void ResetShared() {
        lock (_sharedClientLock) {
            _sharedClient?.Dispose();
            _sharedClient = null;
        }
    }

    /// <summary>Clears custom headers and restores the default product user agent.</summary>
    public static void ResetDefaultHeaders() {
        DefaultHeaders.Clear();
        foreach (KeyValuePair<string, string> header in CreateDefaultHeaders()) {
            DefaultHeaders[header.Key] = header.Value;
        }
        ResetShared();
    }

    private static void ApplyDefaults(HttpClient client) {
        client.Timeout = DefaultTimeout;
        client.DefaultRequestHeaders.Clear();
        foreach (var header in DefaultHeaders) {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static IDictionary<string, string> CreateDefaultHeaders() {
        Version? version = typeof(HtmlHttpClientFactory).Assembly.GetName().Version;
        string productVersion = version == null
            ? "unknown"
            : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["User-Agent"] = $"HtmlTinkerX/{productVersion}"
        };
    }

    private static void ConfigureProxy(HttpClientHandler handler, string? proxy, ICredentials? credential) {
        string? proxyToUse = proxy ?? DefaultProxy;
        if (!string.IsNullOrEmpty(proxyToUse)) {
            handler.Proxy = new WebProxy(proxyToUse);
            handler.UseProxy = true;
            ICredentials? credToUse = credential ?? DefaultProxyCredential;
            if (credToUse != null) {
                handler.Proxy!.Credentials = credToUse;
            }
        }
    }
}
