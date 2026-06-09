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
    public static IDictionary<string, string> DefaultHeaders { get; } = new Dictionary<string, string>();

    /// <summary>Default proxy address.</summary>
    public static string? DefaultProxy { get; set; }

    /// <summary>Default proxy credentials.</summary>
    public static ICredentials? DefaultProxyCredential { get; set; }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> using defaults or the provided overrides.
    /// </summary>
    public static HttpClient Create(string? proxy = null, ICredentials? credential = null) {
        HttpClientHandler handler = new();
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
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    public static HttpClient Create(string? proxy, ICredentials? proxyCredential, ICredentials? credentials, bool preAuthenticate = true) {
        HttpClientHandler handler = new() {
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
    public static HttpClient Create(out CookieContainer cookieContainer, string? proxy = null, ICredentials? credential = null) {
        cookieContainer = new CookieContainer();
        HttpClientHandler handler = new() {
            CookieContainer = cookieContainer,
            UseCookies = true
        };
        ConfigureProxy(handler, proxy, credential);
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

    private static void ApplyDefaults(HttpClient client) {
        client.Timeout = DefaultTimeout;
        client.DefaultRequestHeaders.Clear();
        foreach (var header in DefaultHeaders) {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
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
