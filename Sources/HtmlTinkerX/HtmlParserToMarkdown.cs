using System;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;

namespace HtmlTinkerX;

/// <summary>
/// Utility helpers for converting HTML content to Markdown.
/// </summary>
public static class HtmlParserToMarkdown {
    /// <summary>
    /// Converts HTML markup to Markdown using the configured output profile.
    /// </summary>
    /// <param name="html">HTML string to convert.</param>
    /// <param name="pageUrl">Optional absolute page URL used to resolve relative links and images.</param>
    /// <param name="imageMode">Controls how images are emitted in Markdown.</param>
    /// <param name="listingCardMetadataMode">Controls whether repeated listing-card metadata is kept.</param>
    /// <param name="markdownProfile">Controls the Markdown dialect profile.</param>
    /// <returns>Markdown converted from the provided HTML.</returns>
    public static string ConvertToMarkdown(
        string html,
        string? pageUrl = null,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        HtmlListingCardMetadataMode listingCardMetadataMode = HtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        return HtmlMarkdownConverterAdapter.ConvertToMarkdown(
            html,
            pageUrl,
            imageMode,
            listingCardMetadataMode,
            markdownProfile);
    }

    /// <summary>
    /// Converts HTML markup to a Markdown document model using the configured output profile.
    /// </summary>
    /// <param name="html">HTML string to convert.</param>
    /// <param name="pageUrl">Optional absolute page URL used to resolve relative links and images.</param>
    /// <param name="imageMode">Controls how images are emitted in Markdown.</param>
    /// <param name="listingCardMetadataMode">Controls whether repeated listing-card metadata is kept.</param>
    /// <param name="markdownProfile">Controls the Markdown dialect profile.</param>
    /// <returns>A Markdown document model converted from the provided HTML.</returns>
    public static MarkdownDoc ConvertToMarkdownDocument(
        string html,
        string? pageUrl = null,
        MarkdownImageRenderingMode imageMode = MarkdownImageRenderingMode.PortableMarkdown,
        HtmlListingCardMetadataMode listingCardMetadataMode = HtmlListingCardMetadataMode.SuppressInRepeatedCards,
        HtmlMarkdownProfile markdownProfile = HtmlMarkdownProfile.Portable) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        return HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(
            html,
            pageUrl,
            imageMode,
            listingCardMetadataMode,
            markdownProfile);
    }
}
