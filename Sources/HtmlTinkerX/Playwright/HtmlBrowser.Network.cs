using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Returns network log captured in the specified session.
    /// </summary>
    /// <param name="session">Browser session containing network data.</param>
    public static IEnumerable<HtmlNetworkEntry> GetNetworkLog(HtmlBrowserSession session)
        => session.NetworkLog;

    /// <summary>
    /// Captures response bodies for selected network resource types after the page has finished issuing requests.
    /// </summary>
    /// <param name="session">Browser session containing network responses.</param>
    /// <param name="maxBytes">Maximum UTF-8 bytes stored per response body.</param>
    /// <param name="resourceTypes">Resource types to capture. When omitted, XHR and Fetch responses are captured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task CaptureResponseBodiesAsync(
        HtmlBrowserSession session,
        int maxBytes = 65536,
        IEnumerable<HtmlNetworkResourceType>? resourceTypes = null,
        CancellationToken cancellationToken = default) {
        HtmlNetworkResourceType[] requestedTypes = resourceTypes?.ToArray() ?? System.Array.Empty<HtmlNetworkResourceType>();
        HashSet<HtmlNetworkResourceType> effectiveTypes = requestedTypes.Length == 0
            ? new HashSet<HtmlNetworkResourceType> { HtmlNetworkResourceType.XHR, HtmlNetworkResourceType.Fetch }
            : new HashSet<HtmlNetworkResourceType>(requestedTypes);

        return session.CaptureResponseBodiesAsync(maxBytes, effectiveTypes, cancellationToken);
    }
}
