using Microsoft.Playwright;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>Reads the original bytes of one selected response and rejects bodies above the acceptance limit.</summary>
    /// <remarks>Playwright buffers response bodies. This limit rejects oversized results but is not a streaming network or browser-memory quota. Apply process/resource budgets separately for untrusted sites. The bytes are neither truncated nor redacted.</remarks>
    /// <param name="response">The exact response selected from the owned session.</param>
    /// <param name="maximumBytes">Maximum accepted body size in bytes.</param>
    /// <param name="cancellationToken">Cancels waiting for Playwright; the caller retains session ownership.</param>
    public static async Task<byte[]> ReadResponseBodyAsync(IResponse response, int maximumBytes, CancellationToken cancellationToken = default) {
        if (response == null) throw new ArgumentNullException(nameof(response));
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var header in response.Headers) {
            if (string.Equals(header.Key, "content-length", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(header.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long declared)
                && declared > maximumBytes) {
                throw new InvalidDataException($"Response body exceeds the {maximumBytes}-byte acceptance limit.");
            }
        }
        byte[] body = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
            response.BodyAsync, static () => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (body.Length > maximumBytes) throw new InvalidDataException($"Response body exceeds the {maximumBytes}-byte acceptance limit.");
        return body;
    }
}
