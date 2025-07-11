using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Type of HTML resource that can be extracted.
/// </summary>
public enum HtmlResourceType
{
    Script,
    InlineScript,
    Css,
    InlineCss
}

/// <summary>
/// Represents a script or stylesheet resource found in HTML.
/// </summary>
public sealed class HtmlResourceLink
{
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
    public async Task<string> SaveAsync(string path, Uri? baseUri = null, HttpClient? client = null)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        string resolved = HtmlUtilities.ResolvePath(path);
        if (Directory.Exists(resolved) || !Path.HasExtension(resolved))
        {
            Directory.CreateDirectory(resolved);
            string fileName = string.IsNullOrEmpty(Name)
                ? (!string.IsNullOrEmpty(Source) ? Path.GetFileName(Source) : $"resource_{Index}")
                : Name;
            resolved = Path.Combine(resolved, fileName);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
        }

        if (!string.IsNullOrEmpty(Content))
        {
#if NETSTANDARD2_0 || NETFRAMEWORK
            await Task.Run(() => File.WriteAllText(resolved, Content)).ConfigureAwait(false);
#else
            await File.WriteAllTextAsync(resolved, Content).ConfigureAwait(false);
#endif
            return resolved;
        }

        if (string.IsNullOrEmpty(Source))
        {
            throw new InvalidOperationException("No Source or Content available to save.");
        }

        Uri srcUri = Uri.TryCreate(Source, UriKind.Absolute, out var abs)
            ? abs
            : baseUri != null
                ? new Uri(baseUri, Source)
                : new Uri(Source, UriKind.RelativeOrAbsolute);

        if (srcUri.IsFile)
        {
#if NETSTANDARD2_0 || NETFRAMEWORK
            await Task.Run(() => File.Copy(srcUri.LocalPath, resolved, overwrite: true)).ConfigureAwait(false);
#else
            await Task.Run(() => File.Copy(srcUri.LocalPath, resolved, true)).ConfigureAwait(false);
#endif
            return resolved;
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
#if NETSTANDARD2_0 || NETFRAMEWORK
        using (HttpResponseMessage response = await http.GetAsync(srcUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using FileStream fileStream = new(resolved, FileMode.Create, FileAccess.Write, FileShare.None);
            await contentStream.CopyToAsync(fileStream).ConfigureAwait(false);
        }
#else
        using HttpResponseMessage response = await http.GetAsync(srcUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using FileStream fileStream = new(resolved, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream).ConfigureAwait(false);
#endif
        return resolved;
    }
}
