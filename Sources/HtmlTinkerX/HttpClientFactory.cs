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
    private static HttpClient? _sharedClient;

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
        string? proxyToUse = proxy ?? DefaultProxy;
        if (!string.IsNullOrEmpty(proxyToUse)) {
            handler.Proxy = new WebProxy(proxyToUse);
            handler.UseProxy = true;
            ICredentials? credToUse = credential ?? DefaultProxyCredential;
            if (credToUse != null) {
                handler.Proxy!.Credentials = credToUse;
            }
        }
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
                _sharedClient = Create();
            }
            return _sharedClient;
        }
    }

    /// <summary>Recreates the shared client with current defaults.</summary>
    public static void ResetShared() {
        _sharedClient?.Dispose();
        _sharedClient = null;
    }

    private static void ApplyDefaults(HttpClient client) {
        client.Timeout = DefaultTimeout;
        client.DefaultRequestHeaders.Clear();
        foreach (var header in DefaultHeaders) {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}