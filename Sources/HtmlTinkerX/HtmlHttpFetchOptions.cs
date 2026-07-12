using System;

namespace HtmlTinkerX;

/// <summary>
/// Controls bounded HTTP response handling for URL-based parsing operations.
/// </summary>
public sealed class HtmlHttpFetchOptions {
    /// <summary>
    /// Default maximum response body size: 16 MiB.
    /// </summary>
    public const int DefaultMaximumResponseBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum response body size in bytes.
    /// </summary>
    /// <remarks>
    /// The limit is enforced both from Content-Length, when trustworthy metadata is present,
    /// and while streaming the body. Set a larger value explicitly for unusually large pages.
    /// </remarks>
    public int MaximumResponseBytes { get; set; } = DefaultMaximumResponseBytes;

    internal int GetValidatedMaximumResponseBytes() {
        if (MaximumResponseBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaximumResponseBytes), MaximumResponseBytes, "Maximum response bytes must be greater than zero.");
        }

        return MaximumResponseBytes;
    }
}
