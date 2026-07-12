using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Type of HTML resource that can be extracted.
/// </summary>
public enum HtmlResourceType {
    /// <summary>
    /// JavaScript code referenced via a <c>&lt;script&gt;</c> element.
    /// </summary>
    Script,

    /// <summary>
    /// JavaScript code defined directly within the HTML document.
    /// </summary>
    InlineScript,

    /// <summary>
    /// Cascading Style Sheet referenced via a <c>&lt;link&gt;</c> element.
    /// </summary>
    Css,

    /// <summary>
    /// Cascading Style Sheet defined directly within the HTML document.
    /// </summary>
    InlineCss
}

/// <summary>
/// Represents a script or stylesheet resource found in HTML.
/// </summary>
public sealed partial class HtmlResourceLink {
    /// <summary>Index of the element within the document.</summary>
    public int Index { get; set; }

    /// <summary>Type of resource.</summary>
    public HtmlResourceType Type { get; set; }

    /// <summary>Source URL of the resource, if external.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Inline content of the resource.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Description extracted from a preceding HTML comment.</summary>
    public string? Comment { get; set; }

    /// <summary>Suggested file name for saving the resource.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Saves the resource to the specified path and returns the created path.</summary>
    /// <param name="path">Destination file or directory.</param>
    /// <param name="baseUri">Optional base URI used to resolve a relative source.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> SaveAsync(
        string path,
        Uri? baseUri = null,
        HttpClient? client = null,
        HtmlHttpFetchOptions? fetchOptions = null,
        CancellationToken cancellationToken = default) {
        if (path == null) {
            throw new ArgumentNullException(nameof(path));
        }

        string resolved = HtmlUtilities.EnsureDirectoryExists(path);
        if (Directory.Exists(resolved) || !Path.HasExtension(resolved)) {
            string fileName = string.IsNullOrEmpty(Name)
                ? (!string.IsNullOrEmpty(Source) ? Path.GetFileName(Source) : $"resource_{Index}")
                : Name;
            resolved = Path.Combine(resolved, fileName);
        }

        if (!string.IsNullOrEmpty(Content)) {
#if NETSTANDARD2_0 || NETFRAMEWORK
            await Task.Run(() => File.WriteAllText(resolved, Content), cancellationToken).ConfigureAwait(false);
#else
            await File.WriteAllTextAsync(resolved, Content, cancellationToken).ConfigureAwait(false);
#endif
            return resolved;
        }

        if (string.IsNullOrEmpty(Source)) {
            throw new InvalidOperationException("No Source or Content available to save.");
        }

        Uri srcUri = Uri.TryCreate(Source, UriKind.Absolute, out var abs)
            ? abs
            : baseUri != null
                ? new Uri(baseUri, Source)
                : new Uri(Source, UriKind.RelativeOrAbsolute);

        if (srcUri.IsFile) {
#if NETSTANDARD2_0 || NETFRAMEWORK
            await Task.Run(() => File.Copy(srcUri.LocalPath, resolved, overwrite: true), cancellationToken).ConfigureAwait(false);
#else
            await Task.Run(() => File.Copy(srcUri.LocalPath, resolved, true), cancellationToken).ConfigureAwait(false);
#endif
            return resolved;
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        await HtmlUtilities.DownloadToFileAsync(http, srcUri, resolved, fetchOptions, cancellationToken).ConfigureAwait(false);
        return resolved;
    }
}
