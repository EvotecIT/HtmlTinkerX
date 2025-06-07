using System.Net;
using System.Net.Http;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

internal static class HttpClientHelper {
    internal static HttpClient Create(string? proxy, PSCredential? credential) {
        if (string.IsNullOrEmpty(proxy)) {
            return new HttpClient();
        }

        HttpClientHandler handler = new HttpClientHandler {
            Proxy = new WebProxy(proxy),
            UseProxy = true
        };

        if (credential != null) {
            handler.Proxy!.Credentials = credential.GetNetworkCredential();
        }

        return new HttpClient(handler, disposeHandler: true);
    }
}
