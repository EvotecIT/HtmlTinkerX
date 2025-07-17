using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using PreMailer.Net;

namespace PSParseHTML;

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
        string path = HtmlUtilities.ResolvePath(htmlFilePath);
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
            string cssContent = Options.Css ?? string.Empty;
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

                if (Options.DownloadRemoteCss && href != null) {
                    try {
                        Uri uri = new Uri(href, UriKind.RelativeOrAbsolute);
                        if (!uri.IsAbsoluteUri && Options.BaseUri != null) {
                            uri = new Uri(Options.BaseUri, uri);
                        }

                        if (uri.IsFile) {
                            string localPath = NormalizeFileUriPath(uri);
                            cssContent += HtmlUtilities.ReadFileChecked(localPath);
                        } else if (uri.IsAbsoluteUri) {
                            using HttpClient client = new();
                            cssContent += HtmlUtilities
                                .GetStringWithProperEncodingAsync(client, uri.ToString())
                                .GetAwaiter().GetResult();
                        }
                    } catch (Exception ex) {
                        LoggingMessages.Logger.WriteError("Failed to download CSS from {0}: {1}", href, ex.Message);
                    }
                }

                    link.Remove();
                }

            htmlToProcess = document.DocumentElement.OuterHtml;
            if (!string.IsNullOrEmpty(Options.CssFilePath)) {
                cssContent += HtmlUtilities.ReadFileChecked(Options.CssFilePath!);
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
                cssContent,
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

            return new PreMailerResult(result.Html, warnings);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("MoveCssInline failed with error: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Asynchronously processes the HTML and returns the result.
    /// </summary>
    public async Task<PreMailerResult> MoveCssInlineAsync() {
        try {
            string cssContent = Options.Css ?? string.Empty;
            string htmlToProcess = _html;

            var document = HtmlParser.ParseWithAngleSharp(_html);
            foreach (var link in document.QuerySelectorAll("link")) {
                string? href = link.GetAttribute("href");
                string? rel = link.GetAttribute("rel");

                bool isCss = false;
                if (!string.IsNullOrEmpty(href)) {
                    isCss = href.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        (rel != null && rel.Equals("stylesheet", StringComparison.OrdinalIgnoreCase));
                }
                if (!isCss) {
                    continue;
                }

                if (Options.DownloadRemoteCss && href != null) {
                    try {
                        Uri uri = new Uri(href, UriKind.RelativeOrAbsolute);
                        if (!uri.IsAbsoluteUri && Options.BaseUri != null) {
                            uri = new Uri(Options.BaseUri, uri);
                        }

                        if (uri.IsFile) {
                            string localPath = NormalizeFileUriPath(uri);
#if NETSTANDARD2_0 || NETFRAMEWORK
                            cssContent += await Task.Run(() => File.ReadAllText(localPath)).ConfigureAwait(false);
#else
                            cssContent += await File.ReadAllTextAsync(localPath).ConfigureAwait(false);
#endif
                        } else if (uri.IsAbsoluteUri) {
                            using HttpClient client = new();
                            cssContent += await HtmlUtilities
                                .GetStringWithProperEncodingAsync(client, uri.ToString())
                                .ConfigureAwait(false);
                        }
                    } catch (Exception ex) {
                        LoggingMessages.Logger.WriteError("Failed to download CSS from {0}: {1}", href, ex.Message);
                    }
                }

                link.Remove();
            }

            htmlToProcess = document.DocumentElement.OuterHtml;
            if (!string.IsNullOrEmpty(Options.CssFilePath)) {
                cssContent += await HtmlUtilities.ReadFileCheckedAsync(Options.CssFilePath!).ConfigureAwait(false);
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
                cssContent,
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

            return new PreMailerResult(result.Html, warnings);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("MoveCssInlineAsync failed with error: {0}", ex.Message);
            throw;
        }
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
    public static Task<PreMailerResult> MoveCssInlineAsync(string html, PreMailerOptions? options = null)
        => FromHtml(html, options).MoveCssInlineAsync();

    /// <summary>
    /// Asynchronously processes a HTML file using the provided options.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="options">Optional processing options.</param>
    public static async Task<PreMailerResult> MoveCssInlineFromFileAsync(string htmlFilePath, PreMailerOptions? options = null) {
        string html = await HtmlUtilities.ReadFileCheckedAsync(htmlFilePath).ConfigureAwait(false);
        return await MoveCssInlineAsync(html, options).ConfigureAwait(false);
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

    private static string NormalizeFileUriPath(Uri uri)
    {
        if (!uri.IsFile)
        {
            throw new ArgumentException("URI must be a file path", nameof(uri));
        }

        string path = uri.LocalPath;

        // Normalize directory separators
        char desired = Path.DirectorySeparatorChar;
        char alt = desired == '/' ? '\\' : '/';
        path = path.Replace(alt, desired);

        // UNC paths need special handling on Unix where LocalPath starts with
        // double separators
        if (uri.IsUnc && desired == '/')
        {
            path = "/" + path.TrimStart(desired);
        }

        // Preserve trailing slash if present in the original URI
        bool endsWithSlash = uri.AbsolutePath.EndsWith("/", StringComparison.Ordinal);
        if (endsWithSlash && !path.EndsWith(desired.ToString()))
        {
            path += desired;
        }

        return path;
    }
}
