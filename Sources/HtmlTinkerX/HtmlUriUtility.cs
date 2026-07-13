using System;

namespace HtmlTinkerX;

/// <summary>
/// Provides shared URI comparison rules for website discovery and audit workflows.
/// </summary>
internal static class HtmlUriUtility {
    /// <summary>
    /// Determines whether two absolute URIs have the same scheme, canonical host, and effective port.
    /// </summary>
    internal static bool HasSameOrigin(Uri left, Uri right) {
        if (left == null) {
            throw new ArgumentNullException(nameof(left));
        }

        if (right == null) {
            throw new ArgumentNullException(nameof(right));
        }

        return left.IsAbsoluteUri
            && right.IsAbsoluteUri
            && string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port;
    }
}
