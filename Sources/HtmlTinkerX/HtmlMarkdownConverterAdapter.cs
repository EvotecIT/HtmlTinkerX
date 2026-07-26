using System;
using OfficeIMO.Html;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;

namespace HtmlTinkerX;

internal static class HtmlMarkdownConverterAdapter {
    public static MarkdownDoc ConvertToMarkdownDocument(
        string html,
        string? pageUrl,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        HtmlListingCardMetadataMode listingCardMetadataMode = HtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        if (string.IsNullOrWhiteSpace(html)) {
            return MarkdownDoc.Create();
        }

        var options = CreateOptions(pageUrl, imageMode, listingCardMetadataMode, markdownProfile);
        return HtmlConversionDocument.Parse(html).ToMarkdownDocument(options);
    }

    public static string ConvertToMarkdown(
        string html,
        string? pageUrl,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        HtmlListingCardMetadataMode listingCardMetadataMode = HtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        if (string.IsNullOrWhiteSpace(html)) {
            return string.Empty;
        }

        var options = CreateOptions(pageUrl, imageMode, listingCardMetadataMode, markdownProfile);
        return HtmlConversionDocument.Parse(html).ToMarkdown(options);
    }

    internal static HtmlToMarkdownOptions CreateOptions(
        string? pageUrl,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        HtmlListingCardMetadataMode listingCardMetadataMode = HtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        var options = markdownProfile == HtmlMarkdownProfile.OfficeIMO
            ? HtmlToMarkdownOptions.CreateOfficeIMOProfile()
            : HtmlToMarkdownOptions.CreatePortableProfile();
        options.MarkdownWriteOptions ??= markdownProfile == HtmlMarkdownProfile.OfficeIMO
            ? MarkdownWriteOptions.CreateOfficeIMOProfile()
            : MarkdownWriteOptions.CreatePortableProfile();
        options.MarkdownWriteOptions.ImageRenderingMode = imageMode;
        options.ListingCardMetadataMode = listingCardMetadataMode;
        if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri)) {
            options.BaseUri = baseUri;
        }

        return options;
    }
}
