using System;
using OfficeIMO.Markdown.Html;

namespace HtmlTinkerX;

internal static class HtmlMarkdownConverterAdapter {
    public static string ConvertToMarkdown(string html, string? pageUrl) {
        if (string.IsNullOrWhiteSpace(html)) {
            return string.Empty;
        }

        var options = HtmlToMarkdownOptions.CreatePortableProfile();
        if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri)) {
            options.BaseUri = baseUri;
        }

        return html.ToMarkdown(options);
    }
}
