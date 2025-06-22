using System.Net;
using System.Net.Http;
using System.Management.Automation;
using PSParseHTML;

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
}
