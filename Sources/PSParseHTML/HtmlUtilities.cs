using System;
using System.IO;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Helper methods for working with file paths.
/// </summary>
public static class HtmlUtilities {
    /// <summary>
    /// Resolves the provided path to an absolute file system path.
    /// Environment variables are expanded and relative paths are
    /// converted to full paths.
    /// </summary>
    /// <param name="path">File system path to resolve.</param>
    /// <returns>Absolute file path.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public static string ResolvePath(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }
        string expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(expanded);
    }

    /// <summary>
    /// Reads the contents of a file after verifying that it exists.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <returns>File contents.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string ReadFileChecked(string path) {
        string fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) {
            throw new FileNotFoundException($"File not found: {path}", fullPath);
        }
        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Asynchronously reads the contents of a file after verifying that it exists.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <returns>File contents.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> ReadFileCheckedAsync(string path) {
        string fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) {
            throw new FileNotFoundException($"File not found: {path}", fullPath);
        }
#if NETSTANDARD2_0 || NETFRAMEWORK
        return await Task.Run(() => File.ReadAllText(fullPath)).ConfigureAwait(false);
#else
        return await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);
#endif
    }

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