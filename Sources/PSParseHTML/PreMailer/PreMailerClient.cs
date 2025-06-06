using System;
using System.IO;
using PreMailer.Net;

namespace PSParseHTML;

/// <summary>
/// Helper class wrapping PreMailer.Net functionality with object-oriented configuration.
/// </summary>
public class PreMailerClient {
    private readonly string _html;
    public PreMailerOptions Options { get; }

    private PreMailerClient(string html, PreMailerOptions? options) {
        _html = html;
        Options = options ?? new PreMailerOptions();
    }

    /// <summary>
    /// Creates a client from a HTML file path.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static PreMailerClient FromFile(string htmlFilePath, PreMailerOptions? options = null) {
        if (!File.Exists(htmlFilePath)) {
            throw new FileNotFoundException($"HTML file not found: {htmlFilePath}", htmlFilePath);
        }
        string html = File.ReadAllText(htmlFilePath);
        return new PreMailerClient(html, options);
    }

    /// <summary>
    /// Creates a client from a HTML string.
    /// </summary>
    public static PreMailerClient FromHtml(string html, PreMailerOptions? options = null) {
        return new PreMailerClient(html, options);
    }

    /// <summary>
    /// Processes the HTML and returns the result.
    /// </summary>
    public PreMailerResult MoveCssInline() {
        try {
            string cssContent = Options.Css ?? string.Empty;
            if (!string.IsNullOrEmpty(Options.CssFilePath)) {
                if (!File.Exists(Options.CssFilePath)) {
                    throw new FileNotFoundException($"CSS file not found: {Options.CssFilePath}", Options.CssFilePath);
                }
                cssContent += File.ReadAllText(Options.CssFilePath);
            }

            PreMailer.Net.PreMailer preMailer = Options.BaseUri != null
                ? new PreMailer.Net.PreMailer(_html, Options.BaseUri)
                : new PreMailer.Net.PreMailer(_html);

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

            if (result.Warnings != null) {
                foreach (string warning in result.Warnings) {
                    LoggingMessages.Logger.WriteWarning(warning);
                }
            }

            return new PreMailerResult(result.Html, result.Warnings);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("MoveCssInline failed with error: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Convenience method for processing a HTML string directly using options.
    /// </summary>
    public static PreMailerResult MoveCssInline(string html, PreMailerOptions? options = null) {
        return FromHtml(html, options).MoveCssInline();
    }

    /// <summary>
    /// Convenience method for processing a HTML file directly using options.
    /// </summary>
    public static PreMailerResult MoveCssInlineFromFile(string htmlFilePath, PreMailerOptions? options = null) {
        return FromFile(htmlFilePath, options).MoveCssInline();
    }

    /// <summary>
    /// Parameter-based helper constructing an options object internally.
    /// </summary>
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
            AddAnalyticsTags = addAnalyticsTags,
            AnalyticsSource = analyticsSource,
            AnalyticsMedium = analyticsMedium,
            AnalyticsCampaign = analyticsCampaign,
            AnalyticsContent = analyticsContent,
            AnalyticsDomain = analyticsDomain
        };

        return MoveCssInlineFromFile(htmlFilePath, opts);
    }
}
