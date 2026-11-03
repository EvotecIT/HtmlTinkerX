using System;
using OfficeIMO.Html;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;

namespace HtmlTinkerX;

internal static class HtmlMarkdownConverterAdapter {
    public static MarkdownDocument ConvertToMarkdownDocument(
        string html,
        string? pageUrl,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        MarkdownHtmlListingCardMetadataMode listingCardMetadataMode = MarkdownHtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        if (string.IsNullOrWhiteSpace(html)) {
            return MarkdownDocument.Create();
        }

        var options = CreateOptions(pageUrl, imageMode, listingCardMetadataMode, markdownProfile);
        return CreateDocument(html, options.BaseUri).ToMarkdown(options);
    }

    public static string ConvertToMarkdown(
        string html,
        string? pageUrl,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        MarkdownHtmlListingCardMetadataMode listingCardMetadataMode = MarkdownHtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        if (string.IsNullOrWhiteSpace(html)) {
            return string.Empty;
        }

        return ConvertToMarkdown(
            CreateDocument(html, ParseBaseUri(pageUrl)),
            pageUrl,
            imageMode,
            listingCardMetadataMode,
            markdownProfile);
    }

    internal static string ConvertToMarkdown(
        HtmlConversionDocument document,
        string? pageUrl,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        MarkdownHtmlListingCardMetadataMode listingCardMetadataMode = MarkdownHtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var options = CreateOptions(pageUrl, imageMode, listingCardMetadataMode, markdownProfile);
        using MarkdownDocument markdown = document.ToMarkdown(options);
        return markdown.ToMarkdown(options.MarkdownWriteOptions);
    }

    internal static HtmlMarkdownExportOptions CreateOptions(
        string? pageUrl,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        MarkdownHtmlListingCardMetadataMode listingCardMetadataMode = MarkdownHtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        var options = markdownProfile == HtmlMarkdownProfile.OfficeIMO
            ? HtmlMarkdownExportOptions.CreateOfficeIMOProfile()
            : HtmlMarkdownExportOptions.CreatePortableProfile();
        options.MarkdownWriteOptions ??= markdownProfile == HtmlMarkdownProfile.OfficeIMO
            ? MarkdownSaveOptions.CreateOfficeIMOProfile()
            : MarkdownSaveOptions.CreatePortableProfile();
        options.MarkdownWriteOptions.ImageRenderingMode = imageMode;
        options.ListingCardMetadataMode = listingCardMetadataMode;
        if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri)) {
            options.BaseUri = baseUri;
        }

        return options;
    }

    private static HtmlConversionDocument CreateDocument(string html, Uri? baseUri) {
        HtmlConversionDocumentOptions options = HtmlConversionDocumentOptions.UntrustedDefaults;
        options.BaseUri = baseUri;
        options.IncludeNormalizedHtml = false;
        return HtmlConversionDocument.Parse(html, options);
    }

    private static Uri? ParseBaseUri(string? pageUrl) =>
        !string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? uri)
            ? uri
            : null;
}
