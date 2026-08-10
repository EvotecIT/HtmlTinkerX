using HtmlTinkerX;
using System.Collections;
using System.Management.Automation;
using System.Net;
using System.Net.Http;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Helper methods for creating <see cref="HttpClient"/> instances.
/// </summary>
internal static class HttpClientHelper {
    /// <summary>
    /// Creates a new <see cref="HttpClient"/> using optional proxy settings.
    /// </summary>
    /// <param name="proxy">Proxy server address.</param>
    /// <param name="credential">Credentials for the proxy.</param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    internal static HttpClient Create(string? proxy, PSCredential? credential) {
        System.Net.ICredentials? creds = credential?.GetNetworkCredential();
        return HtmlHttpClientFactory.Create(proxy, creds);
    }

    /// <summary>
    /// Creates a new client and applies request-specific headers over the shared defaults.
    /// </summary>
    /// <param name="proxy">Proxy server address.</param>
    /// <param name="credential">Credentials for the proxy.</param>
    /// <param name="userAgent">Optional user agent override.</param>
    /// <param name="headers">Optional request header overrides.</param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    internal static HttpClient Create(string? proxy, PSCredential? credential, string? userAgent, IDictionary? headers) {
        HttpClient client = Create(proxy, credential);
        if (headers != null) {
            foreach (DictionaryEntry entry in headers) {
                if (entry.Key == null || entry.Value == null) {
                    continue;
                }

                string name = entry.Key.ToString()!;
                client.DefaultRequestHeaders.Remove(name);
                client.DefaultRequestHeaders.TryAddWithoutValidation(name, entry.Value.ToString());
            }
        }

        if (!string.IsNullOrWhiteSpace(userAgent)) {
            client.DefaultRequestHeaders.Remove("User-Agent");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        }

        return client;
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> using optional proxy settings and page credentials.
    /// </summary>
    /// <param name="proxy">Proxy server address.</param>
    /// <param name="proxyCredential">Credentials for the proxy.</param>
    /// <param name="credential">Credentials for the target page.</param>
    /// <param name="username">Username for the target page when <paramref name="credential"/> is not provided.</param>
    /// <param name="password">Password for the target page when <paramref name="credential"/> is not provided.</param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    internal static HttpClient Create(string? proxy, PSCredential? proxyCredential, PSCredential? credential, string? username, string? password) {
        NetworkCredential? pageCredential = credential?.GetNetworkCredential();
        if (pageCredential == null && !string.IsNullOrEmpty(username)) {
            pageCredential = new NetworkCredential(username, password ?? string.Empty);
        }

        if (pageCredential == null) {
            return Create(proxy, proxyCredential);
        }

        return HtmlHttpClientFactory.Create(proxy, proxyCredential?.GetNetworkCredential(), pageCredential);
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> using optional proxy settings, page credentials, and a returned cookie container.
    /// </summary>
    /// <param name="proxy">Proxy server address.</param>
    /// <param name="proxyCredential">Credentials for the proxy.</param>
    /// <param name="credential">Credentials for the target page.</param>
    /// <param name="username">Username for the target page when <paramref name="credential"/> is not provided.</param>
    /// <param name="password">Password for the target page when <paramref name="credential"/> is not provided.</param>
    /// <param name="cookieContainer">Container used by the returned client.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    internal static HttpClient CreateWithCookies(string? proxy, PSCredential? proxyCredential, PSCredential? credential, string? username, string? password, out CookieContainer cookieContainer, bool allowAutoRedirect = true) {
        NetworkCredential? pageCredential = credential?.GetNetworkCredential();
        if (pageCredential == null && !string.IsNullOrEmpty(username)) {
            pageCredential = new NetworkCredential(username, password ?? string.Empty);
        }

        return HtmlHttpClientFactory.Create(out cookieContainer, proxy, proxyCredential?.GetNetworkCredential(), pageCredential, allowAutoRedirect: allowAutoRedirect);
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> over an existing cookie container.
    /// </summary>
    /// <param name="cookieContainer">Container used by the returned client.</param>
    /// <param name="proxy">Proxy server address.</param>
    /// <param name="proxyCredential">Credentials for the proxy.</param>
    /// <param name="allowAutoRedirect">Whether the handler should automatically follow redirects.</param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    internal static HttpClient CreateWithCookies(CookieContainer cookieContainer, string? proxy, PSCredential? proxyCredential, bool allowAutoRedirect = true) {
        return HtmlHttpClientFactory.Create(cookieContainer, proxy, proxyCredential?.GetNetworkCredential(), allowAutoRedirect);
    }
}
