using System.Net;
using System.Net.Http;
using System.Management.Automation;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

internal static class HttpClientHelper {
    internal static HttpClient Create(string? proxy, PSCredential? credential) {
        ICredentials? creds = credential?.GetNetworkCredential();
        return HtmlHttpClientFactory.Create(proxy, creds);
    }
}
