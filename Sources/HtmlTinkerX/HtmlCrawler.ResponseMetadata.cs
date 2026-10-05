using Microsoft.Playwright;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static void SetRenderedResponseMetadata(HtmlCrawlPage page, IResponse? response) {
        if (response != null) {
            response.Headers.TryGetValue("etag", out string? tag);
            response.Headers.TryGetValue("last-modified", out string? date);
            SetResponseMetadata(page, response.Url, tag, date);
        }
    }

    private static void SetResponseMetadata(HtmlCrawlPage page, string responseUrl, string? entityTag, string? lastModified) {
        page.ResponseUrl = responseUrl;
        page.EntityTag = EntityTagHeaderValue.TryParse(entityTag, out EntityTagHeaderValue? tag) && tag.Tag != "*"
            ? tag.ToString() : null;
        page.LastModified = ParseHttpDate(lastModified);
    }

    private static DateTimeOffset? ParseHttpDate(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Use the platform's HTTP-date parser for both static and browser response headers.
        using HttpResponseMessage headers = new();
        headers.Headers.TryAddWithoutValidation("Date", value);
        return headers.Headers.Date;
    }
}
