using HtmlTinkerX;
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
        ICredentials? creds = credential?.GetNetworkCredential();
        return HtmlHttpClientFactory.Create(proxy, creds);
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
}
