using AngleSharp;
using AngleSharp.Dom;
using NUglify;
using NUglify.Css;
using NUglify.Html;
using NUglify.JavaScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for optimizing markup resources.
/// </summary>
public static class HtmlOptimizer {
    /// <summary>
    /// Creates a new <see cref="HtmlSettings"/> instance that reflects HtmlTinkerX's safe defaults.
    /// </summary>
    /// <returns>A settings object that callers can further customize.</returns>
    public static HtmlSettings CreateDefaultHtmlSettings() {
        HtmlSettings settings = new();
        settings.RemoveComments = false;
        settings.RemoveOptionalTags = false;
        settings.ShortBooleanAttribute = false;
        settings.IsFragmentOnly = true;
        settings.CssSettings.DecodeEscapes = false;
        return settings;
    }

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
    /// <param name="treatAsDocument">When set, NUglify treats the input as a full HTML document.</param>
    /// <param name="removeComments">Remove HTML comments while minifying.</param>
    /// <param name="removeOptionalTags">Remove optional HTML tags such as closing <c>&lt;/p&gt;</c>.</param>
    /// <param name="shortBooleanAttributes">Shorten boolean attributes (for example, turn <c>disabled="disabled"</c> into <c>disabled</c>).</param>
    /// <returns>Optimized HTML output.</returns>
    public static string OptimizeHtml(
        string html,
        bool cssDecodeEscapes,
        bool treatAsDocument = false,
        bool removeComments = false,
        bool removeOptionalTags = false,
        bool shortBooleanAttributes = false) {
        HtmlSettings settings = CreateDefaultHtmlSettings();
        settings.IsFragmentOnly = !treatAsDocument;
        settings.RemoveComments = removeComments;
        settings.RemoveOptionalTags = removeOptionalTags;
        settings.ShortBooleanAttribute = shortBooleanAttributes;
        settings.CssSettings.DecodeEscapes = cssDecodeEscapes;

        return OptimizeHtml(html, settings);
    }

    /// <summary>
    /// Minifies the provided HTML content using the supplied <see cref="HtmlSettings"/> instance.
    /// </summary>
    /// <param name="html">HTML string to optimize.</param>
    /// <param name="settings">NUglify settings that control how the HTML is minified.</param>
    /// <returns>Optimized HTML output.</returns>
    public static string OptimizeHtml(string html, HtmlSettings settings) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        HtmlSettings effectiveSettings = CloneHtmlSettings(settings);

        bool hasMotw =
            html.IndexOf("<!-- saved from url=(0014)about:internet -->", StringComparison.OrdinalIgnoreCase) >= 0;

        UglifyResult result = Uglify.Html(html, effectiveSettings);
        string output = result.Code ?? string.Empty;
        output = ShortenBooleanAttributesIfRequested(output, effectiveSettings);

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
    /// <param name="treatAsDocument">When set, NUglify treats the input as a full HTML document.</param>
    /// <param name="removeComments">Remove HTML comments while minifying.</param>
    /// <param name="removeOptionalTags">Remove optional HTML tags such as closing <c>&lt;/p&gt;</c>.</param>
    /// <param name="shortBooleanAttributes">Shorten boolean attributes (for example, turn <c>disabled="disabled"</c> into <c>disabled</c>).</param>
    /// <param name="embedImages">When set, downloads external images and embeds them using data URIs.</param>
    /// <param name="baseUrl">Optional base URL used to resolve relative image paths.</param>
    /// <param name="client">Optional <see cref="HttpClient"/> used for downloads.</param>
    /// <param name="cancellationToken">Token used to cancel download operations.</param>
    /// <returns>Optimized HTML output.</returns>
    public static async Task<string> OptimizeHtmlAsync(
        string html,
        bool cssDecodeEscapes,
        bool treatAsDocument = false,
        bool removeComments = false,
        bool removeOptionalTags = false,
        bool shortBooleanAttributes = false,
        bool embedImages = false,
        string? baseUrl = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        HtmlSettings settings = CreateDefaultHtmlSettings();
        settings.IsFragmentOnly = !treatAsDocument;
        settings.RemoveComments = removeComments;
        settings.RemoveOptionalTags = removeOptionalTags;
        settings.ShortBooleanAttribute = shortBooleanAttributes;
        settings.CssSettings.DecodeEscapes = cssDecodeEscapes;

        return await OptimizeHtmlAsync(html, settings, embedImages, baseUrl, client, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously minifies the provided HTML content using the supplied <see cref="HtmlSettings"/> instance
    /// and optionally embeds referenced images as data URIs.
    /// </summary>
    /// <param name="html">HTML string to optimize.</param>
    /// <param name="settings">NUglify settings that control how the HTML is minified.</param>
    /// <param name="embedImages">When set, downloads external images and embeds them using data URIs.</param>
    /// <param name="baseUrl">Optional base URL used to resolve relative image paths.</param>
    /// <param name="client">Optional <see cref="HttpClient"/> used for downloads.</param>
    /// <param name="cancellationToken">Token used to cancel download operations.</param>
    /// <returns>Optimized HTML output.</returns>
    public static async Task<string> OptimizeHtmlAsync(
        string html,
        HtmlSettings settings,
        bool embedImages = false,
        string? baseUrl = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        string optimized = await Task.Run(() => OptimizeHtml(html, settings), cancellationToken).ConfigureAwait(false);
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
    /// <param name="treatAsDocument">When set, NUglify treats the input as a full HTML document.</param>
    /// <param name="removeComments">Remove HTML comments while minifying.</param>
    /// <param name="removeOptionalTags">Remove optional HTML tags such as closing <c>&lt;/p&gt;</c>.</param>
    /// <param name="shortBooleanAttributes">Shorten boolean attributes (for example, turn <c>disabled="disabled"</c> into <c>disabled</c>).</param>
    /// <returns>Optimized HTML output.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeHtmlFile(string filePath, bool cssDecodeEscapes, bool treatAsDocument = false, bool removeComments = false, bool removeOptionalTags = false, bool shortBooleanAttributes = false) {
        string html = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeHtml(html, cssDecodeEscapes, treatAsDocument, removeComments, removeOptionalTags, shortBooleanAttributes);
    }

    /// <summary>
    /// Minifies HTML content from a file using the supplied <see cref="HtmlSettings"/> instance.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <param name="settings">NUglify settings that control how the HTML is minified.</param>
    /// <returns>Optimized HTML output.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string OptimizeHtmlFile(string filePath, HtmlSettings settings) {
        string html = HtmlUtilities.ReadFileChecked(filePath);
        return OptimizeHtml(html, settings);
    }

    /// <summary>
    /// Asynchronously minifies HTML content from a file.
    /// </summary>
    /// <inheritdoc cref="OptimizeHtmlFile(string,bool,bool,bool,bool,bool)"/>
    public static async Task<string> OptimizeHtmlFileAsync(
        string filePath,
        bool cssDecodeEscapes,
        bool treatAsDocument = false,
        bool removeComments = false,
        bool removeOptionalTags = false,
        bool shortBooleanAttributes = false,
        bool embedImages = false,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        string html = await HtmlUtilities.ReadFileCheckedAsync(filePath, cancellationToken).ConfigureAwait(false);
        string? baseUrl = null;
        try {
            baseUrl = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
        } catch {
        }
        return await OptimizeHtmlAsync(html, cssDecodeEscapes, treatAsDocument, removeComments, removeOptionalTags, shortBooleanAttributes, embedImages, baseUrl, client, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously minifies HTML content from a file using the supplied <see cref="HtmlSettings"/> instance.
    /// </summary>
    /// <inheritdoc cref="OptimizeHtmlFileAsync(string,bool,bool,bool,bool,bool,bool,HttpClient?,CancellationToken)"/>
    public static async Task<string> OptimizeHtmlFileAsync(
        string filePath,
        HtmlSettings settings,
        bool embedImages = false,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        string html = await HtmlUtilities.ReadFileCheckedAsync(filePath, cancellationToken).ConfigureAwait(false);
        string? baseUrl = null;
        try {
            baseUrl = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
        } catch {
        }
        return await OptimizeHtmlAsync(html, settings, embedImages, baseUrl, client, cancellationToken).ConfigureAwait(false);
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
            if (src == null || src.Length == 0 || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
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

        return document.DocumentElement!.OuterHtml;
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

    private static readonly Regex BooleanAttributeValuePattern = new(
        "(?<=\\s)(?<name>[A-Za-z_:][\\w:.-]*?)=(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s>/]+))(?=[\\s>/])",
#if NETSTANDARD2_0 || NET472
        RegexOptions.IgnoreCase
#else
        RegexOptions.IgnoreCase | RegexOptions.Compiled
#endif
    );

    private static readonly HashSet<string> BooleanAttributes = new(
        new[] {
            "allowfullscreen",
            "async",
            "autofocus",
            "autoplay",
            "checked",
            "compact",
            "controls",
            "declare",
            "default",
            "defer",
            "disabled",
            "formnovalidate",
            "hidden",
            "inert",
            "ismap",
            "itemscope",
            "loop",
            "multiple",
            "muted",
            "nomodule",
            "novalidate",
            "open",
            "playsinline",
            "readonly",
            "required",
            "reversed",
            "scoped",
            "selected",
            "typemustmatch"
        },
        StringComparer.OrdinalIgnoreCase);

    private static string ShortenBooleanAttributesIfRequested(string html, HtmlSettings settings) {
        if (string.IsNullOrEmpty(html) || !settings.ShortBooleanAttribute) {
            return html;
        }

        return BooleanAttributeValuePattern.Replace(html, static match => {
            string name = match.Groups["name"].Value;
            if (!BooleanAttributes.Contains(name)) {
                return match.Value;
            }

            string value = match.Groups["value"].Value;
            if (!value.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                return match.Value;
            }

            return name;
        });
    }

    private static HtmlSettings CloneHtmlSettings(HtmlSettings settings) {
        HtmlSettings clone = new() {
            AttributesCaseSensitive = settings.AttributesCaseSensitive,
            TagsCaseSensitive = settings.TagsCaseSensitive,
            CollapseWhitespaces = settings.CollapseWhitespaces,
            RemoveComments = settings.RemoveComments,
            RemoveOptionalTags = settings.RemoveOptionalTags,
            RemoveInvalidClosingTags = settings.RemoveInvalidClosingTags,
            RemoveEmptyAttributes = settings.RemoveEmptyAttributes,
            RemoveAttributeQuotes = settings.RemoveAttributeQuotes,
#pragma warning disable CS0618 // RemoveQuotedAttributes is obsolete in favor of RemoveAttributeQuotes but still exposed for older targets
            RemoveQuotedAttributes = settings.RemoveQuotedAttributes,
#pragma warning restore CS0618
            DecodeEntityCharacters = settings.DecodeEntityCharacters,
            AttributeQuoteChar = settings.AttributeQuoteChar,
            RemoveScriptStyleTypeAttribute = settings.RemoveScriptStyleTypeAttribute,
            ShortBooleanAttribute = settings.ShortBooleanAttribute,
            IsFragmentOnly = settings.IsFragmentOnly,
            MinifyJs = settings.MinifyJs,
            MinifyJsAttributes = settings.MinifyJsAttributes,
            JsSettings = settings.JsSettings?.Clone() ?? new CodeSettings(),
            MinifyCss = settings.MinifyCss,
            MinifyCssAttributes = settings.MinifyCssAttributes,
            CssSettings = settings.CssSettings?.Clone() ?? new CssSettings(),
            PrettyPrint = settings.PrettyPrint,
            OutputTextNodesOnNewLine = settings.OutputTextNodesOnNewLine,
            Indent = settings.Indent,
            RemoveJavaScript = settings.RemoveJavaScript,
            KeepOneSpaceWhenCollapsing = settings.KeepOneSpaceWhenCollapsing,
            AlphabeticallyOrderAttributes = settings.AlphabeticallyOrderAttributes
        };

        if (settings.InlineTagsPreservingSpacesAround != null) {
            clone.InlineTagsPreservingSpacesAround.Clear();
            foreach (var kvp in settings.InlineTagsPreservingSpacesAround) {
                clone.InlineTagsPreservingSpacesAround[kvp.Key] = kvp.Value;
            }
        }

        if (settings.TagsWithNonCollapsibleWhitespaces != null) {
            clone.TagsWithNonCollapsibleWhitespaces.Clear();
            foreach (var kvp in settings.TagsWithNonCollapsibleWhitespaces) {
                clone.TagsWithNonCollapsibleWhitespaces[kvp.Key] = kvp.Value;
            }
        }

#pragma warning disable CS0618 // TagsWithNonCollapsableWhitespaces is retained for backwards compatibility
        if (settings.TagsWithNonCollapsableWhitespaces != null) {
            clone.TagsWithNonCollapsableWhitespaces.Clear();
            foreach (var kvp in settings.TagsWithNonCollapsableWhitespaces) {
                clone.TagsWithNonCollapsableWhitespaces[kvp.Key] = kvp.Value;
            }
        }
#pragma warning restore CS0618

        if (settings.KeepCommentsRegex != null) {
            clone.KeepCommentsRegex.Clear();
            foreach (var regex in settings.KeepCommentsRegex) {
                clone.KeepCommentsRegex.Add(regex);
            }
        }

        if (settings.KeepTags != null) {
            clone.KeepTags.Clear();
            clone.KeepTags.UnionWith(settings.KeepTags);
        }

        if (settings.RemoveAttributes != null) {
            clone.RemoveAttributes.Clear();
            clone.RemoveAttributes.UnionWith(settings.RemoveAttributes);
        }

        return clone;
    }
}
