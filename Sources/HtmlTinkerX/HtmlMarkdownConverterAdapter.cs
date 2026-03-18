using System;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;

namespace HtmlTinkerX;

internal static class HtmlMarkdownConverterAdapter {
    public static MarkdownDoc ConvertToMarkdownDocument(string html, string? pageUrl) {
        if (string.IsNullOrWhiteSpace(html)) {
            return MarkdownDoc.Create();
        }

        var options = CreateOptions(pageUrl);
        return html.LoadFromHtml(options);
    }

    public static string ConvertToMarkdown(string html, string? pageUrl) {
        if (string.IsNullOrWhiteSpace(html)) {
            return string.Empty;
        }

        var options = CreateOptions(pageUrl);
        return html.LoadFromHtml(options).ToMarkdown(options.MarkdownWriteOptions);
    }

    internal static HtmlToMarkdownOptions CreateOptions(string? pageUrl) {
        var options = HtmlToMarkdownOptions.CreatePortableProfile();
        if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri)) {
            options.BaseUri = baseUri;
        }

        return options;
    }
}
