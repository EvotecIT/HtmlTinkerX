using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

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
    /// Converts a path to an absolute file system path.
    /// </summary>
    /// <param name="path">File system path to resolve.</param>
    /// <returns>Absolute file path.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    public static string ToFullPath(this string path) => ResolvePath(path);

    /// <summary>
    /// Ensures the directory for the specified path exists and returns the
    /// resolved absolute path.
    /// </summary>
    /// <param name="path">Directory or file path.</param>
    /// <returns>The resolved absolute path.</returns>
    public static string EnsureDirectoryExists(string path) {
        string fullPath = ResolvePath(path);
        string? dir = Directory.Exists(fullPath) || !Path.HasExtension(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }
        return fullPath;
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File contents.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> ReadFileCheckedAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) {
            throw new FileNotFoundException($"File not found: {path}", fullPath);
        }
#if NETSTANDARD2_0 || NETFRAMEWORK
        return await Task.Run(() => File.ReadAllText(fullPath), cancellationToken).ConfigureAwait(false);
#else
        return await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
#endif
    }

    /// <summary>
    /// Downloads content from a URL with proper encoding detection.
    /// </summary>
    /// <param name="client">HttpClient to use for the request.</param>
    /// <param name="url">URL to download from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content as a string with proper encoding.</returns>
    public static async Task<string> GetStringWithProperEncodingAsync(HttpClient client, string url, CancellationToken cancellationToken = default) {
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // Try to get encoding from Content-Type header
        var contentType = response.Content.Headers.ContentType;
        if (contentType?.CharSet != null) {
            try {
                string charset = contentType.CharSet.Trim().Trim('"').Trim('\'');
                var encoding = System.Text.Encoding.GetEncoding(charset);
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
            @"<meta[^>]+charset\s*=\s*[""']?(?<charset>[^""'>\s]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (metaMatch.Success) {
            try {
                var encoding = System.Text.Encoding.GetEncoding(metaMatch.Groups["charset"].Value);
                return encoding.GetString(bytes);
            } catch {
                // If the detected encoding is not supported, fall through to UTF-8
            }
        }

        // Default to UTF-8 if no encoding could be determined
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Removes redundant whitespace from the provided HTML string.
    /// </summary>
    /// <param name="html">HTML markup to normalize.</param>
    /// <returns>Markup with collapsed whitespace.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is null.</exception>
    public static string RemoveRedundantWhitespace(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        string collapsed = System.Text.RegularExpressions.Regex.Replace(html, "\\s+", " ");
        collapsed = System.Text.RegularExpressions.Regex.Replace(collapsed, @">\s+<", "><");
        return collapsed.Trim();
    }
}