using AngleSharp;
using AngleSharp.Dom;
using NUglify;
using NUglify.Html;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for optimizing markup resources.
/// </summary>
public static class HtmlOptimizer {
    /// <summary>
    /// Minifies the provided CSS using NUglify.
    /// </summary>
    /// <param name="css">CSS code to optimize.</param>
    /// <returns>Minified CSS string.</returns>
    public static string OptimizeCss(string css) {
        if (css == null) {
            throw new ArgumentNullException(nameof(css));
        }

        var settings = new NUglify.Css.CssSettings { DecodeEscapes = false };
        var result = NUglify.Uglify.Css(css, settings);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"OptimizeCss -Errors: {errors}");
        }
        return result.Code ?? string.Empty;
    }

    /// <summary>
    /// Asynchronously minifies the provided CSS using NUglify.
    /// </summary>
    /// <param name="css">CSS code to optimize.</param>
    /// <returns>Minified CSS string.</returns>
    public static Task<string> OptimizeCssAsync(string css)
        => Task.Run(() => OptimizeCss(css));

    /// <summary>
    /// Minifies CSS from a file.
    /// </summary>
    /// <param name="filePath">Path to the CSS file.</param>
    /// <returns>Minified CSS string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeCssFile(string filePath) {
        string css = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeCss(css);
    }

    /// <summary>
    /// Asynchronously minifies CSS from a file.
    /// </summary>
    /// <param name="filePath">Path to the CSS file.</param>
    /// <returns>Minified CSS string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> OptimizeCssFileAsync(string filePath) {
        string css = await HtmlUtilities.ReadFileCheckedAsync(filePath).ConfigureAwait(false);
        return await OptimizeCssAsync(css).ConfigureAwait(false);
    }

    /// <summary>
    /// Minifies the provided HTML content.
    /// </summary>
    /// <param name="html">HTML string to optimize.</param>
    /// <param name="cssDecodeEscapes">Whether to decode CSS escape sequences.</param>
    /// <returns>Optimized HTML output.</returns>
    public static string OptimizeHtml(string html, bool cssDecodeEscapes) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlSettings settings = new() { RemoveOptionalTags = false, RemoveComments = true };
        settings.CssSettings.DecodeEscapes = cssDecodeEscapes;

        bool hasMotw =
            html.IndexOf("<!-- saved from url=(0014)about:internet -->", StringComparison.OrdinalIgnoreCase) >= 0;

        UglifyResult result = Uglify.Html(html, settings);
        string output = result.Code ?? string.Empty;

        if (hasMotw) {
            return "<!-- saved from url=(0014)about:internet -->" + Environment.NewLine + output;
        }

        return output;
    }

    /// <summary>
    /// Asynchronously minifies the provided HTML content and optionally embeds referenced images as data URIs.
    /// </summary>
    /// <param name="html">HTML string to optimize.</param>
    /// <param name="cssDecodeEscapes">Whether to decode CSS escape sequences.</param>
    /// <param name="embedImages">When set, downloads external images and embeds them using data URIs.</param>
    /// <param name="baseUrl">Optional base URL used to resolve relative image paths.</param>
    /// <param name="client">Optional <see cref="HttpClient"/> used for downloads.</param>
    /// <param name="cancellationToken">Token used to cancel download operations.</param>
    /// <returns>Optimized HTML output.</returns>
    public static async Task<string> OptimizeHtmlAsync(string html, bool cssDecodeEscapes, bool embedImages = false, string? baseUrl = null, HttpClient? client = null, CancellationToken cancellationToken = default) {
        string optimized = await Task.Run(() => OptimizeHtml(html, cssDecodeEscapes), cancellationToken).ConfigureAwait(false);
        if (!embedImages) {
            return optimized;
        }

        return await EmbedImagesAsDataUriAsync(optimized, baseUrl, client, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Minifies HTML content from a file.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <param name="cssDecodeEscapes">Whether to decode CSS escape sequences.</param>
    /// <returns>Optimized HTML output.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeHtmlFile(string filePath, bool cssDecodeEscapes) {
        string html = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeHtml(html, cssDecodeEscapes);
    }

    /// <summary>
    /// Asynchronously minifies HTML content from a file.
    /// </summary>
    /// <inheritdoc cref="OptimizeHtmlFile(string,bool)"/>
    public static async Task<string> OptimizeHtmlFileAsync(string filePath, bool cssDecodeEscapes, bool embedImages = false, HttpClient? client = null, CancellationToken cancellationToken = default) {
        string html = await HtmlUtilities.ReadFileCheckedAsync(filePath, cancellationToken).ConfigureAwait(false);
        string? baseUrl = null;
        try {
            baseUrl = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
        } catch {
        }
        return await OptimizeHtmlAsync(html, cssDecodeEscapes, embedImages, baseUrl, client, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads image resources referenced in the provided HTML and embeds them using data URIs.
    /// </summary>
    /// <param name="html">HTML content that may reference external images.</param>
    /// <param name="baseUrl">Optional base URL used to resolve relative image sources.</param>
    /// <param name="client">Optional <see cref="HttpClient"/> to perform downloads.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>HTML with image <c>src</c> attributes replaced by data URIs.</returns>
    public static async Task<string> EmbedImagesAsDataUriAsync(string html, string? baseUrl = null, HttpClient? client = null, CancellationToken cancellationToken = default) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        IConfiguration config = Configuration.Default;
        IBrowsingContext context = BrowsingContext.New(config);
        IDocument document = await context.OpenAsync(req => req.Content(html).Address(baseUrl ?? "about:blank"), cancellationToken).ConfigureAwait(false);

        foreach (var element in document.Images) {
            string? src = element.GetAttribute("src");
            if (string.IsNullOrEmpty(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            Uri? uri = null;
            if (Uri.TryCreate(src, UriKind.Absolute, out Uri? absolute)) {
                uri = absolute;
            } else if (!string.IsNullOrEmpty(baseUrl) && Uri.TryCreate(new Uri(baseUrl), src, out Uri? relative)) {
                uri = relative;
            }

            if (uri == null) {
                continue;
            }

            using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
#if NETSTANDARD2_0 || NET472
            byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#else
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#endif
            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(mediaType)) {
                string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
                mediaType = ext switch {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".svg" => "image/svg+xml",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };
            }
            string base64 = Convert.ToBase64String(bytes);
            element.SetAttribute("src", $"data:{mediaType};base64,{base64}");
        }

        return document.DocumentElement?.OuterHtml ?? string.Empty;
    }

    /// <summary>
    /// Minifies JavaScript code using NUglify.
    /// </summary>
    /// <param name="js">JavaScript code to optimize.</param>
    /// <returns>Minified JavaScript.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="js"/> is null.</exception>
    public static string OptimizeJavaScript(string js) {
        if (js == null) {
            throw new ArgumentNullException(nameof(js));
        }

        var result = NUglify.Uglify.Js(js);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"Optimize-JavaScript -Errors: {errors}");
        }

        return result.Code ?? string.Empty;
    }

    /// <summary>
    /// Asynchronously minifies JavaScript code using NUglify.
    /// </summary>
    /// <param name="js">JavaScript code to optimize.</param>
    /// <returns>Minified JavaScript.</returns>
    public static Task<string> OptimizeJavaScriptAsync(string js)
        => Task.Run(() => OptimizeJavaScript(js));

    /// <summary>
    /// Minifies JavaScript code from a file using NUglify.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <returns>Minified JavaScript.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeJavaScriptFile(string filePath) {
        string js = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeJavaScript(js);
    }

    /// <summary>
    /// Asynchronously minifies JavaScript code from a file using NUglify.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <returns>Minified JavaScript.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> OptimizeJavaScriptFileAsync(string filePath) {
        string js = await HtmlUtilities.ReadFileCheckedAsync(filePath).ConfigureAwait(false);
        return await OptimizeJavaScriptAsync(js).ConfigureAwait(false);
    }
}