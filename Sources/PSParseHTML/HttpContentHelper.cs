using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Helper class for downloading HTTP content with proper encoding detection.
/// </summary>
public static class HttpContentHelper {
    /// <summary>
    /// Downloads content from a URL with proper encoding detection.
    /// </summary>
    /// <param name="client">HttpClient to use for the request.</param>
    /// <param name="url">URL to download from.</param>
    /// <returns>Content as a string with proper encoding.</returns>
    public static async Task<string> GetStringWithProperEncodingAsync(HttpClient client, string url) {
        using var response = await client.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        // Try to get encoding from Content-Type header
        var contentType = response.Content.Headers.ContentType;
        if (contentType?.CharSet != null) {
            try {
                var encoding = System.Text.Encoding.GetEncoding(contentType.CharSet);
                return encoding.GetString(bytes);
            } catch {
                // If the specified encoding is not supported, fall through to detection
            }
        }

        // Try to detect encoding from byte order mark (BOM)
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
            return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
            return System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // Try to detect encoding from HTML meta tag
        var asciiContent = System.Text.Encoding.ASCII.GetString(bytes);
        var metaMatch = System.Text.RegularExpressions.Regex.Match(
            asciiContent,
            @"<meta[^>]+charset\s*=\s*[""']?([^""'>\s]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (metaMatch.Success) {
            try {
                var encoding = System.Text.Encoding.GetEncoding(metaMatch.Groups[1].Value);
                return encoding.GetString(bytes);
            } catch {
                // If the detected encoding is not supported, fall through to UTF-8
            }
        }

        // Default to UTF-8 if no encoding could be determined
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}