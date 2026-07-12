using AngleSharp.Dom;
using PreMailer.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper class wrapping PreMailer.Net functionality with object-oriented configuration.
/// </summary>
public class PreMailerClient {
    private readonly string _html;
    /// <summary>
    /// Options controlling how CSS is inlined.
    /// </summary>
    public PreMailerOptions Options { get; }

    private PreMailerClient(string html, PreMailerOptions? options) {
        _html = html;
        Options = options ?? new PreMailerOptions();
    }

    /// <summary>
    /// Creates a client from a HTML file path.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="options">Optional processing options.</param>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static PreMailerClient FromFile(string htmlFilePath, PreMailerOptions? options = null) {
        string path = htmlFilePath.ToFullPath();
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"HTML file not found: {path}", path);
        }
        string html = File.ReadAllText(path);
        return new PreMailerClient(html, options);
    }

    /// <summary>
    /// Creates a client from a HTML string.
    /// </summary>
    /// <param name="html">HTML markup to process.</param>
    /// <param name="options">Optional processing options.</param>
    public static PreMailerClient FromHtml(string html, PreMailerOptions? options = null) {
        return new PreMailerClient(html, options);
    }

    /// <summary>
    /// Processes the HTML synchronously and returns the result.
    /// </summary>
    public PreMailerResult MoveCssInline() {
        try {
            StringBuilder cssContent = new();
            if (!string.IsNullOrEmpty(Options.Css)) {
                cssContent.AppendLine(Options.Css);
            }
            List<IReadOnlyList<KeyValuePair<string, string>>> preservedStylesheetLinks = new();
            string htmlToProcess = _html;

            var document = HtmlParser.ParseWithAngleSharp(_html);
            foreach (var link in document.QuerySelectorAll("link")) {
                string? href = link.GetAttribute("href");
                string? rel = link.GetAttribute("rel");

                bool isCss = false;
                if (!string.IsNullOrEmpty(href)) {
                    isCss = href!.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        (rel != null && rel.Equals("stylesheet", StringComparison.OrdinalIgnoreCase));
                }
                if (!isCss) {
                    continue;
                }

                bool inlined = Options.DownloadRemoteCss && href != null && TryAppendLinkedStylesheet(cssContent, href);
                if (!inlined) {
                    preservedStylesheetLinks.Add(CaptureAttributes(link));
                }
                link.Remove();
            }

            htmlToProcess = document.DocumentElement.OuterHtml;
            if (!string.IsNullOrEmpty(Options.CssFilePath)) {
                cssContent.AppendLine(HtmlUtilities.ReadFileChecked(Options.CssFilePath!));
            }

            PreMailer.Net.PreMailer preMailer = Options.BaseUri != null
                ? new PreMailer.Net.PreMailer(htmlToProcess, Options.BaseUri)
                : new PreMailer.Net.PreMailer(htmlToProcess);

            if (Options.AddAnalyticsTags && !string.IsNullOrEmpty(Options.AnalyticsSource)) {
                preMailer.AddAnalyticsTags(
                    Options.AnalyticsSource,
                    Options.AnalyticsMedium,
                    Options.AnalyticsCampaign,
                    Options.AnalyticsContent,
                    Options.AnalyticsDomain);
            }

            InlineResult result = preMailer.MoveCssInline(
                Options.RemoveStyleElements,
                Options.IgnoreElements,
                cssContent.ToString(),
                Options.StripIdAndClassAttributes,
                Options.RemoveComments,
                Options.CustomFormatter,
                Options.PreserveMediaQueries);

            List<PreMailerWarning> warnings = new();
            if (result.Warnings != null) {
                foreach (string warning in result.Warnings) {
                    var w = new PreMailerWarning(warning);
                    warnings.Add(w);
                    LoggingMessages.Logger.WriteWarning(w.Message);
                }
            }

            return new PreMailerResult(RestoreStylesheetLinks(result.Html, preservedStylesheetLinks), warnings);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("MoveCssInline failed with error: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously processes the HTML and returns the result.
    /// </summary>
    public async Task<PreMailerResult> MoveCssInlineAsync(CancellationToken cancellationToken = default) {
        if (cancellationToken.IsCancellationRequested) {
            return await Task.FromCanceled<PreMailerResult>(cancellationToken).ConfigureAwait(false);
        }
        try {
            StringBuilder cssContent = new();
            if (!string.IsNullOrEmpty(Options.Css)) {
                cssContent.AppendLine(Options.Css);
            }
            List<IReadOnlyList<KeyValuePair<string, string>>> preservedStylesheetLinks = new();
            string htmlToProcess = _html;

            var document = HtmlParser.ParseWithAngleSharp(_html);
            foreach (var link in document.QuerySelectorAll("link")) {
                string? href = link.GetAttribute("href");
                string? rel = link.GetAttribute("rel");

                bool isCss = false;
                if (!string.IsNullOrEmpty(href)) {
                    isCss = href!.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        (rel != null && rel.Equals("stylesheet", StringComparison.OrdinalIgnoreCase));
                }
                if (!isCss) {
                    continue;
                }

                bool inlined = Options.DownloadRemoteCss
                    && href != null
                    && await TryAppendLinkedStylesheetAsync(cssContent, href, cancellationToken).ConfigureAwait(false);
                if (!inlined) {
                    preservedStylesheetLinks.Add(CaptureAttributes(link));
                }
                link.Remove();
            }

            htmlToProcess = document.DocumentElement.OuterHtml;
            if (!string.IsNullOrEmpty(Options.CssFilePath)) {
                cssContent.AppendLine(await HtmlUtilities.ReadFileCheckedAsync(Options.CssFilePath!, cancellationToken).ConfigureAwait(false));
            }

            PreMailer.Net.PreMailer preMailer = Options.BaseUri != null
                ? new PreMailer.Net.PreMailer(htmlToProcess, Options.BaseUri)
                : new PreMailer.Net.PreMailer(htmlToProcess);

            if (Options.AddAnalyticsTags && !string.IsNullOrEmpty(Options.AnalyticsSource)) {
                preMailer.AddAnalyticsTags(
                    Options.AnalyticsSource,
                    Options.AnalyticsMedium,
                    Options.AnalyticsCampaign,
                    Options.AnalyticsContent,
                    Options.AnalyticsDomain);
            }

            InlineResult result = preMailer.MoveCssInline(
                Options.RemoveStyleElements,
                Options.IgnoreElements,
                cssContent.ToString(),
                Options.StripIdAndClassAttributes,
                Options.RemoveComments,
                Options.CustomFormatter,
                Options.PreserveMediaQueries);

            List<PreMailerWarning> warnings = new();
            if (result.Warnings != null) {
                foreach (string warning in result.Warnings) {
                    var w = new PreMailerWarning(warning);
                    warnings.Add(w);
                    LoggingMessages.Logger.WriteWarning(w.Message);
                }
            }

            return new PreMailerResult(RestoreStylesheetLinks(result.Html, preservedStylesheetLinks), warnings);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("MoveCssInlineAsync failed with error: {0}", ex.Message);
            throw;
        }
    }

    private bool TryAppendLinkedStylesheet(StringBuilder cssContent, string href) {
        try {
            Uri uri = ResolveStylesheetUri(href);
            string downloadedCss;
            if (uri.IsFile) {
                downloadedCss = HtmlUtilities.ReadFileChecked(NormalizeFileUriPath(uri));
            } else {
                downloadedCss = HtmlUtilities
                    .GetStringWithProperEncodingAsync(Options.HttpClient ?? HtmlHttpClientFactory.Shared, uri.AbsoluteUri)
                    .GetAwaiter().GetResult();
            }

            cssContent.AppendLine(downloadedCss);
            return true;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("Failed to download CSS from {0}: {1}", href, ex.Message);
            return false;
        }
    }

    private async Task<bool> TryAppendLinkedStylesheetAsync(StringBuilder cssContent, string href, CancellationToken cancellationToken) {
        try {
            Uri uri = ResolveStylesheetUri(href);
            string downloadedCss;
            if (uri.IsFile) {
                downloadedCss = await HtmlUtilities.ReadFileCheckedAsync(NormalizeFileUriPath(uri), cancellationToken).ConfigureAwait(false);
            } else {
                downloadedCss = await HtmlUtilities
                    .GetStringWithProperEncodingAsync(Options.HttpClient ?? HtmlHttpClientFactory.Shared, uri.AbsoluteUri, cancellationToken)
                    .ConfigureAwait(false);
            }

            cssContent.AppendLine(downloadedCss);
            return true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("Failed to download CSS from {0}: {1}", href, ex.Message);
            return false;
        }
    }

    private Uri ResolveStylesheetUri(string href) {
        Uri uri = new(href, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri) {
            return uri;
        }

        if (Options.BaseUri == null) {
            throw new InvalidOperationException("A base URI is required to resolve a relative stylesheet URL.");
        }

        return new Uri(Options.BaseUri, uri);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> CaptureAttributes(IElement element) {
        List<KeyValuePair<string, string>> attributes = new(element.Attributes.Length);
        foreach (IAttr attribute in element.Attributes) {
            attributes.Add(new KeyValuePair<string, string>(attribute.Name, attribute.Value));
        }
        return attributes;
    }

    private static string RestoreStylesheetLinks(string html, IReadOnlyList<IReadOnlyList<KeyValuePair<string, string>>> links) {
        if (links.Count == 0) {
            return html;
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        IElement? head = document.QuerySelector("head");
        if (head == null) {
            return html;
        }

        foreach (IReadOnlyList<KeyValuePair<string, string>> attributes in links) {
            IElement link = document.CreateElement("link");
            foreach (KeyValuePair<string, string> attribute in attributes) {
                link.SetAttribute(attribute.Key, attribute.Value);
            }
            head.AppendChild(link);
        }

        return document.DocumentElement.OuterHtml;
    }

    /// <summary>
    /// Convenience method for processing a HTML string directly using options.
    /// </summary>
    /// <param name="html">HTML markup to process.</param>
    /// <param name="options">Optional processing options.</param>
    public static PreMailerResult MoveCssInline(string html, PreMailerOptions? options = null) {
        return FromHtml(html, options).MoveCssInline();
    }

    /// <summary>
    /// Convenience method for processing a HTML file directly using options.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="options">Optional processing options.</param>
    public static PreMailerResult MoveCssInlineFromFile(string htmlFilePath, PreMailerOptions? options = null) {
        return FromFile(htmlFilePath, options).MoveCssInline();
    }

    /// <summary>
    /// Asynchronously processes a HTML string using the provided options.
    /// </summary>
    /// <param name="html">HTML markup to process.</param>
    /// <param name="options">Optional processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<PreMailerResult> MoveCssInlineAsync(string html, PreMailerOptions? options = null, CancellationToken cancellationToken = default)
        => FromHtml(html, options).MoveCssInlineAsync(cancellationToken);

    /// <summary>
    /// Asynchronously processes a HTML file using the provided options.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="options">Optional processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<PreMailerResult> MoveCssInlineFromFileAsync(string htmlFilePath, PreMailerOptions? options = null, CancellationToken cancellationToken = default) {
        string html = await HtmlUtilities.ReadFileCheckedAsync(htmlFilePath, cancellationToken).ConfigureAwait(false);
        return await MoveCssInlineAsync(html, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parameter-based helper constructing an options object internally.
    /// </summary>
    /// <param name="html">HTML markup to process.</param>
    /// <param name="baseUri">Base URI used for resolving relative links.</param>
    /// <param name="removeStyleElements">Remove &lt;style&gt; elements after inlining.</param>
    /// <param name="ignoreElements">CSS selector of elements to ignore.</param>
    /// <param name="css">CSS content to inline.</param>
    /// <param name="cssFilePath">Path to a CSS file to inline.</param>
    /// <param name="stripIdAndClassAttributes">Strip id and class attributes from the output.</param>
    /// <param name="removeComments">Remove HTML and CSS comments.</param>
    /// <param name="customFormatter">Custom formatter used when generating HTML.</param>
    /// <param name="preserveMediaQueries">Preserve media queries from style nodes.</param>
    /// <param name="useEmailFormatter">Use the built in email formatter.</param>
    /// <param name="downloadRemoteCss">Download remote CSS referenced by link tags.</param>
    /// <param name="addAnalyticsTags">Add Google Analytics tags.</param>
    /// <param name="analyticsSource">UTM source parameter.</param>
    /// <param name="analyticsMedium">UTM medium parameter.</param>
    /// <param name="analyticsCampaign">UTM campaign parameter.</param>
    /// <param name="analyticsContent">UTM content parameter.</param>
    /// <param name="analyticsDomain">Domain used when constructing analytics links.</param>
    public static PreMailerResult MoveCssInline(
        string html,
        Uri? baseUri = null,
        bool removeStyleElements = false,
        string? ignoreElements = null,
        string? css = null,
        string? cssFilePath = null,
        bool stripIdAndClassAttributes = false,
        bool removeComments = false,
        global::AngleSharp.IMarkupFormatter? customFormatter = null,
        bool preserveMediaQueries = false,
        bool useEmailFormatter = false,
        bool downloadRemoteCss = false,
        bool addAnalyticsTags = false,
        string? analyticsSource = null,
        string? analyticsMedium = null,
        string? analyticsCampaign = null,
        string? analyticsContent = null,
        string? analyticsDomain = null) {

        var opts = new PreMailerOptions {
            BaseUri = baseUri,
            RemoveStyleElements = removeStyleElements,
            IgnoreElements = ignoreElements,
            Css = css,
            CssFilePath = cssFilePath,
            StripIdAndClassAttributes = stripIdAndClassAttributes,
            RemoveComments = removeComments,
            CustomFormatter = customFormatter,
            PreserveMediaQueries = preserveMediaQueries,
            UseEmailFormatter = useEmailFormatter,
            DownloadRemoteCss = downloadRemoteCss,
            AddAnalyticsTags = addAnalyticsTags,
            AnalyticsSource = analyticsSource,
            AnalyticsMedium = analyticsMedium,
            AnalyticsCampaign = analyticsCampaign,
            AnalyticsContent = analyticsContent,
            AnalyticsDomain = analyticsDomain
        };

        return MoveCssInline(html, opts);
    }

    /// <summary>
    /// Parameter-based helper for processing a HTML file directly.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="baseUri">Base URI used for resolving relative links.</param>
    /// <param name="removeStyleElements">Remove &lt;style&gt; elements after inlining.</param>
    /// <param name="ignoreElements">CSS selector of elements to ignore.</param>
    /// <param name="css">CSS content to inline.</param>
    /// <param name="cssFilePath">Path to a CSS file to inline.</param>
    /// <param name="stripIdAndClassAttributes">Strip id and class attributes from the output.</param>
    /// <param name="removeComments">Remove HTML and CSS comments.</param>
    /// <param name="customFormatter">Custom formatter used when generating HTML.</param>
    /// <param name="preserveMediaQueries">Preserve media queries from style nodes.</param>
    /// <param name="useEmailFormatter">Use the built in email formatter.</param>
    /// <param name="downloadRemoteCss">Download remote CSS referenced by link tags.</param>
    /// <param name="addAnalyticsTags">Add Google Analytics tags.</param>
    /// <param name="analyticsSource">UTM source parameter.</param>
    /// <param name="analyticsMedium">UTM medium parameter.</param>
    /// <param name="analyticsCampaign">UTM campaign parameter.</param>
    /// <param name="analyticsContent">UTM content parameter.</param>
    /// <param name="analyticsDomain">Domain used when constructing analytics links.</param>
    public static PreMailerResult MoveCssInlineFromFile(
        string htmlFilePath,
        Uri? baseUri = null,
        bool removeStyleElements = false,
        string? ignoreElements = null,
        string? css = null,
        string? cssFilePath = null,
        bool stripIdAndClassAttributes = false,
        bool removeComments = false,
        global::AngleSharp.IMarkupFormatter? customFormatter = null,
        bool preserveMediaQueries = false,
        bool useEmailFormatter = false,
        bool downloadRemoteCss = false,
        bool addAnalyticsTags = false,
        string? analyticsSource = null,
        string? analyticsMedium = null,
        string? analyticsCampaign = null,
        string? analyticsContent = null,
        string? analyticsDomain = null) {

        var opts = new PreMailerOptions {
            BaseUri = baseUri,
            RemoveStyleElements = removeStyleElements,
            IgnoreElements = ignoreElements,
            Css = css,
            CssFilePath = cssFilePath,
            StripIdAndClassAttributes = stripIdAndClassAttributes,
            RemoveComments = removeComments,
            CustomFormatter = customFormatter,
            PreserveMediaQueries = preserveMediaQueries,
            UseEmailFormatter = useEmailFormatter,
            DownloadRemoteCss = downloadRemoteCss,
            AddAnalyticsTags = addAnalyticsTags,
            AnalyticsSource = analyticsSource,
            AnalyticsMedium = analyticsMedium,
            AnalyticsCampaign = analyticsCampaign,
            AnalyticsContent = analyticsContent,
            AnalyticsDomain = analyticsDomain
        };

        return MoveCssInlineFromFile(htmlFilePath, opts);
    }

    private static string NormalizeFileUriPath(Uri uri) {
        if (!uri.IsFile) {
            throw new ArgumentException("URI must be a file path", nameof(uri));
        }

        string path = uri.LocalPath;

        // Normalize directory separators
        char desired = Path.DirectorySeparatorChar;
        char alt = desired == '/' ? '\\' : '/';
        path = path.Replace(alt, desired);

        // UNC paths need special handling on Unix where LocalPath starts with
        // double separators - we want to end up with a single leading slash
        if (uri.IsUnc && desired == '/' && path.StartsWith("//")) {
            path = path.Substring(1);
        }

        // Preserve trailing slash if present in the original URI
        bool endsWithSlash = uri.AbsolutePath.EndsWith("/", StringComparison.Ordinal);
        if (endsWithSlash && !path.EndsWith(desired.ToString())) {
            path += desired;
        }

        return path;
    }
}
