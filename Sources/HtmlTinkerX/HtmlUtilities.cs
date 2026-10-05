using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for working with file paths.
/// </summary>
public static class HtmlUtilities {
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TagWhitespaceRegex = new(@">\s+<", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MetaCharsetRegex = new(
        @"<meta[^>]+charset\s*=\s*[""']?(?<charset>[^""'>\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
#if !NETFRAMEWORK
    private static readonly Lazy<bool> CodePagesEncodingProviderRegistration = new(() => {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return true;
    });
#endif

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
    /// Downloads content using the default 16 MiB response limit and HTML encoding detection.
    /// </summary>
    /// <param name="client">HttpClient to use for the request.</param>
    /// <param name="url">URL to download from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content as a string with proper encoding.</returns>
    public static Task<string> GetStringWithProperEncodingAsync(HttpClient client, string url, CancellationToken cancellationToken = default) =>
        GetStringWithProperEncodingAsync(client, url, fetchOptions: null, cancellationToken);

    /// <summary>
    /// Downloads bounded content from a URL with proper encoding detection.
    /// </summary>
    /// <param name="client">HTTP client to use for the request.</param>
    /// <param name="url">URL to download from.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content as a string with proper encoding.</returns>
    public static async Task<string> GetStringWithProperEncodingAsync(
        HttpClient client,
        string url,
        HtmlHttpFetchOptions? fetchOptions,
        CancellationToken cancellationToken = default) {
        HtmlHttpTextResult result = await GetTextWithProperEncodingAsync(
            client,
            url,
            fetchOptions,
            cancellationToken).ConfigureAwait(false);
        return result.Content;
    }

    /// <summary>
    /// Downloads bounded content and returns both decoded text and the final URL after redirects.
    /// </summary>
    /// <param name="client">HTTP client to use for the request.</param>
    /// <param name="url">URL to download from.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded content and final response URL.</returns>
    public static async Task<HtmlHttpTextResult> GetTextWithProperEncodingAsync(
        HttpClient client,
        string url,
        HtmlHttpFetchOptions? fetchOptions,
        CancellationToken cancellationToken = default) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        using CancellationTokenSource requestTimeout = CreateRequestTimeoutTokenSource(client, cancellationToken);
        CancellationToken requestToken = requestTimeout.Token;
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        using HttpResponseMessage response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string content = await ReadResponseContentWithProperEncodingAsync(response, fetchOptions, requestToken).ConfigureAwait(false);
        return new HtmlHttpTextResult {
            Content = content,
            FinalUri = response.RequestMessage?.RequestUri
        };
    }

    /// <summary>
    /// Reads an HTTP response using the default 16 MiB limit and BOM, header, and HTML meta charset detection.
    /// </summary>
    /// <param name="response">HTTP response to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content as a string with proper encoding.</returns>
    public static Task<string> ReadResponseContentWithProperEncodingAsync(HttpResponseMessage response, CancellationToken cancellationToken = default) =>
        ReadResponseContentWithProperEncodingAsync(response, fetchOptions: null, cancellationToken);

    /// <summary>
    /// Reads a bounded HTTP response body with header, BOM, and HTML meta charset detection.
    /// </summary>
    /// <param name="response">HTTP response to read.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content as a string with proper encoding.</returns>
    public static async Task<string> ReadResponseContentWithProperEncodingAsync(
        HttpResponseMessage response,
        HtmlHttpFetchOptions? fetchOptions,
        CancellationToken cancellationToken = default) {
        if (response == null) {
            throw new ArgumentNullException(nameof(response));
        }

        int maximumBytes = fetchOptions?.GetValidatedMaximumResponseBytes() ?? HtmlHttpFetchOptions.DefaultMaximumResponseBytes;
        long? declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength > maximumBytes) {
            throw CreateResponseTooLargeException(maximumBytes, declaredLength);
        }

        byte[] bytes = await ReadBoundedContentAsync(response.Content, maximumBytes, cancellationToken, fetchOptions?.ResponseBudget).ConfigureAwait(false);
        return DecodeHtmlResponse(bytes, response.Content.Headers.ContentType?.CharSet);
    }

    internal static string DecodeHtmlResponse(byte[] bytes, string? charset) {
        // HTML's BOM sniffing precedes transport and meta charset declarations.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
            return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
            return System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (!string.IsNullOrWhiteSpace(charset)) {
            try {
                var encoding = GetEncodingWithCodePagesFallback(charset!.Trim().Trim('"').Trim('\''));
                return encoding.GetString(bytes);
            } catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException) {
                // An unsupported transport label falls through to HTML detection.
            }
        }

        // Try to detect encoding from HTML meta tag
        var asciiContent = System.Text.Encoding.ASCII.GetString(bytes);
        Match metaMatch = MetaCharsetRegex.Match(asciiContent);

        if (metaMatch.Success) {
            try {
                var encoding = GetEncodingWithCodePagesFallback(metaMatch.Groups["charset"].Value);
                return encoding.GetString(bytes);
            } catch {
                // If the detected encoding is not supported, fall through to UTF-8
            }
        }

        // Default to UTF-8 if no encoding could be determined
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    internal static async Task<byte[]> ReadResponseBytesAsync(
        HttpResponseMessage response,
        int maximumBytes,
        CancellationToken cancellationToken,
        HtmlCrawlResponseBudget? responseBudget = null) {
        if (maximumBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), maximumBytes, "Maximum response bytes must be greater than zero.");
        }

        long? declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength > maximumBytes) {
            throw CreateResponseTooLargeException(maximumBytes, declaredLength);
        }

        return await ReadBoundedContentAsync(response.Content, maximumBytes, cancellationToken, responseBudget).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBoundedContentAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken, HtmlCrawlResponseBudget? responseBudget = null) {
        using Stream stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
        using MemoryStream buffer = new(Math.Min(maximumBytes, 81920));
        await CopyBoundedStreamAsync(stream, buffer, maximumBytes, cancellationToken, responseBudget).ConfigureAwait(false);
        return buffer.ToArray();
    }

    internal static async Task DownloadToFileAsync(
        HttpClient client,
        Uri uri,
        string filePath,
        HtmlHttpFetchOptions? fetchOptions,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        int maximumBytes = fetchOptions?.GetValidatedMaximumResponseBytes() ?? HtmlHttpFetchOptions.DefaultMaximumResponseBytes;
        using CancellationTokenSource requestTimeout = CreateRequestTimeoutTokenSource(client, cancellationToken);
        CancellationToken requestToken = requestTimeout.Token;
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        using HttpResponseMessage response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength > maximumBytes) {
            throw CreateResponseTooLargeException(maximumBytes, declaredLength);
        }

        string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using FileStream fileStream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await CopyBoundedStreamAsync(contentStream, fileStream, maximumBytes, requestToken).ConfigureAwait(false);

            fileStream.Close();
            requestToken.ThrowIfCancellationRequested();
            CommitTemporaryFile(temporaryPath, filePath);
        } catch {
            if (File.Exists(temporaryPath)) {
                File.Delete(temporaryPath);
            }
            throw;
        }
    }

    internal static Task WriteBytesAtomicallyAsync(string path, byte[] bytes, CancellationToken cancellationToken) =>
        WriteAtomicallyAsync(path, (stream, token) => stream.WriteAsync(bytes, 0, bytes.Length, token), cancellationToken);

    internal static async Task WriteAtomicallyAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = EnsureDirectoryExists(path);
        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true)) {
                await write(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            CommitTemporaryFile(temporaryPath, fullPath);
        } finally {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void CommitTemporaryFile(string temporaryPath, string filePath) {
        if (File.Exists(filePath)) File.Replace(temporaryPath, filePath, null);
        else File.Move(temporaryPath, filePath);
    }

    internal static CancellationTokenSource CreateRequestTimeoutTokenSource(HttpClient client, CancellationToken cancellationToken) {
        if (client == null) {
            throw new ArgumentNullException(nameof(client));
        }

        CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (client.Timeout != Timeout.InfiniteTimeSpan) {
            requestTimeout.CancelAfter(client.Timeout);
        }
        return requestTimeout;
    }

    private static async Task CopyBoundedStreamAsync(Stream source, Stream destination, int maximumBytes, CancellationToken cancellationToken, HtmlCrawlResponseBudget? responseBudget = null) {
        using CancellationTokenRegistration cancellationRegistration = RegisterResponseStreamCancellation(source, cancellationToken);
        byte[] chunk = new byte[81920];
        int totalBytes = 0;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            int readSize = responseBudget?.GetReadSize(chunk.Length) ?? chunk.Length;
            int bytesRead = await ReadResponseStreamAsync(source, chunk, readSize, cancellationToken, responseBudget).ConfigureAwait(false);
            if (bytesRead == 0) {
                break;
            }

            totalBytes = checked(totalBytes + bytesRead);
            if (totalBytes > maximumBytes) {
                throw CreateResponseTooLargeException(maximumBytes, totalBytes);
            }

            await destination.WriteAsync(chunk, 0, bytesRead, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static CancellationTokenRegistration RegisterResponseStreamCancellation(Stream stream, CancellationToken cancellationToken) =>
        cancellationToken.Register(state => {
            // Framework HTTP streams do not abort an in-flight BeginRead when its token is canceled.
            try {
                ((Stream)state!).Dispose();
            } catch (IOException) {
                // Disposal may race the transport closing the response.
            } catch (ObjectDisposedException) {
                // The completed response can already have closed its stream.
            }
        }, stream);

    internal static async Task<int> ReadResponseStreamAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken, HtmlCrawlResponseBudget? responseBudget = null) {
        cancellationToken.ThrowIfCancellationRequested();
        try {
            int read = await stream.ReadAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
            responseBudget?.RecordBytes(read);
            cancellationToken.ThrowIfCancellationRequested();
            responseBudget?.ThrowIfExceeded();
            return read;
        } catch (Exception exception) when (cancellationToken.IsCancellationRequested &&
            (exception is IOException || exception is ObjectDisposedException || exception is HttpRequestException)) {
            throw new OperationCanceledException("The HTTP response read was canceled.", exception, cancellationToken);
        }
    }

    private static InvalidDataException CreateResponseTooLargeException(int maximumBytes, long? actualBytes) {
        string actual = actualBytes.HasValue ? $" The response reported or supplied {actualBytes.Value} bytes." : string.Empty;
        return new InvalidDataException($"The HTTP response exceeded the configured {maximumBytes}-byte limit.{actual}");
    }

    internal static System.Text.Encoding GetEncodingWithCodePagesFallback(string charset) {
        try {
            return System.Text.Encoding.GetEncoding(charset);
        } catch (ArgumentException) {
            EnsureCodePagesEncodingProvider();
            return System.Text.Encoding.GetEncoding(charset);
        }
    }

    private static void EnsureCodePagesEncodingProvider() {
#if !NETFRAMEWORK
        _ = CodePagesEncodingProviderRegistration.Value;
#endif
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

        string collapsed = WhitespaceRegex.Replace(html, " ");
        collapsed = TagWhitespaceRegex.Replace(collapsed, "><");
        return collapsed.Trim();
    }
}
