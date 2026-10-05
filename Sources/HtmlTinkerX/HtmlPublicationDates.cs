using AngleSharp.Dom;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace HtmlTinkerX;

/// <summary>Publication and modification dates explicitly declared by an HTML document.</summary>
public sealed class HtmlPublicationDates {
    /// <summary>Original publication date, when declared and parseable.</summary>
    public DateTimeOffset? Published { get; internal set; }

    /// <summary>Modification date, kept separate from original publication.</summary>
    public DateTimeOffset? Modified { get; internal set; }

    /// <summary>Reads page-level meta tags and matching Article or WebPage JSON-LD. Never substitutes a modification date for publication.</summary>
    /// <param name="html">Complete HTML, before article selection removes document metadata.</param>
    /// <param name="pageUrl">Document URL used to exclude structured records for other pages.</param>
    public static HtmlPublicationDates Parse(string html, Uri? pageUrl = null) => ParseDocument(HtmlParser.ParseWithAngleSharp(html), pageUrl);

    internal static HtmlPublicationDates ParseDocument(IDocument document, Uri? pageUrl) {
        string? canonical = document.QuerySelectorAll("link[href]").FirstOrDefault(link =>
            (link.GetAttribute("rel") ?? "").Split(' ').Any(rel => rel.Equals("canonical",StringComparison.OrdinalIgnoreCase)))?.GetAttribute("href");
        if (!string.IsNullOrWhiteSpace(canonical) && Uri.TryCreate(pageUrl,canonical,out var canonicalUrl) && canonicalUrl.Scheme is "https" or "http") pageUrl = canonicalUrl;
        HtmlPublicationDates result = new();
        foreach (IElement meta in document.QuerySelectorAll("meta[content]")) {
            string name = (meta.GetAttribute("property") ?? meta.GetAttribute("name") ?? "").ToLowerInvariant();
            if (name is "article:published_time" or "published_time" or "pubdate") result.Published ??= ParseDate(meta.GetAttribute("content"));
            if (name is "article:modified_time" or "last-modified" or "modified_time") result.Modified ??= ParseDate(meta.GetAttribute("content"));
        }

        var candidates = HtmlJsonLdParser.ParseDocument(document)
            .Select(item => ReadCandidate(item, pageUrl)).Where(item => item is not null).ToArray();
        var matched = candidates.Where(item => item!.Value.MatchesPage).ToArray();
        // Several unbound articles commonly describe a listing; none is the page's publication date.
        var chosen = matched.Length > 0 ? matched : candidates.Length == 1 ? candidates : Array.Empty<(DateTimeOffset? Published, DateTimeOffset? Modified, bool MatchesPage)?>();
        var published = chosen.Select(item => item!.Value.Published).Where(date => date.HasValue).Distinct().ToArray();
        var modified = chosen.Select(item => item!.Value.Modified).Where(date => date.HasValue).Distinct().ToArray();
        if (published.Length == 1) result.Published ??= published[0];
        if (modified.Length == 1) result.Modified ??= modified[0];
        return result;
    }

    private static (DateTimeOffset? Published, DateTimeOffset? Modified, bool MatchesPage)? ReadCandidate(HtmlJsonLdItem item, Uri? pageUrl) {
        try {
            using JsonDocument json = JsonDocument.Parse(item.RawJson);
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("@type", out var type) || !IsPublication(type)) return null;
            string? url = String(root, "url");
            if (url is null && root.TryGetProperty("mainEntityOfPage",out var main))
                url = main.ValueKind == JsonValueKind.String ? main.GetString() : main.ValueKind == JsonValueKind.Object ? String(main,"@id") : null;
            url ??= String(root,"@id");
            bool matches = false;
            if (!string.IsNullOrWhiteSpace(url) && pageUrl is not null) {
                if (!Uri.TryCreate(pageUrl,url,out var declared) || Uri.Compare(declared,pageUrl,UriComponents.SchemeAndServer | UriComponents.PathAndQuery,UriFormat.SafeUnescaped,StringComparison.Ordinal) != 0) return null;
                matches = true;
            }
            return (ParseDate(String(root,"datePublished")),ParseDate(String(root,"dateModified")),matches);
        } catch (JsonException) { return null; }
    }

    private static bool IsPublication(JsonElement type) => type.ValueKind == JsonValueKind.Array
        ? type.EnumerateArray().Any(IsPublication)
        : type.ValueKind == JsonValueKind.String && (type.GetString() ?? "").Replace("https://schema.org/", "").Replace("http://schema.org/", "") is
            "Article" or "NewsArticle" or "BlogPosting" or "Report" or "WebPage";

    private static string? String(JsonElement item, string name) => item.TryGetProperty(name,out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var date) ? date : null;
}
