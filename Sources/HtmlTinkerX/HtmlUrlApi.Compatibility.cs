using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlFormFieldExtractor {
    /// <summary>Preserves the 2.0.x binary signature for URL field extraction.</summary>
    public static Task<List<HtmlFormField>> ExtractUrlFieldsAsync(string? url, HttpClient? client) =>
        ExtractUrlFieldsAsync(url, client, null, default);
}

public static partial class HtmlOutlineBuilder {
    /// <summary>Preserves the 2.0.x binary signature for URL outline extraction.</summary>
    public static Task<List<HtmlOutlineItem>> BuildFromUrlAsync(string url, HtmlParserEngine engine, HttpClient? client) =>
        BuildFromUrlAsync(url, engine, client, null, default);
}

public static partial class HtmlParserFromForm {
    /// <summary>Preserves the 2.0.x binary signature for URL form parsing.</summary>
    public static Task<List<HtmlFormResult>> ParseUrlFormsWithAngleSharpAsync(string? url, HttpClient? client) =>
        ParseUrlFormsWithAngleSharpAsync(url, client, null, default);
}

public static partial class HtmlParserFromMeta {
    /// <summary>Preserves the 2.0.x binary signature for URL metadata parsing.</summary>
    public static Task<List<HtmlMetaTag>> ParseUrlMetaTagsAsync(string url, HttpClient? client) =>
        ParseUrlMetaTagsAsync(url, client, null, default);
}

public static partial class HtmlParserFromMicrodata {
    /// <summary>Preserves the 2.0.x binary signature for URL microdata parsing.</summary>
    public static Task<List<HtmlMicrodataItem>> ParseUrlMicrodataItemsAsync(string url, HttpClient? client) =>
        ParseUrlMicrodataItemsAsync(url, client, null, default);
}

public static partial class HtmlParserFromOpenGraph {
    /// <summary>Preserves the 2.0.x binary signature for URL Open Graph parsing.</summary>
    public static Task<HtmlOpenGraph> ParseUrlOpenGraphAsync(string? url, HttpClient? client) =>
        ParseUrlOpenGraphAsync(url, client, null, default);
}

public static partial class HtmlReactFlightParser {
    /// <summary>Preserves the 2.0.x binary signature for URL React Flight parsing.</summary>
    public static Task<HtmlReactFlightDocument> ParseUrlAsync(string url, HttpClient? client) =>
        ParseUrlAsync(url, client, null, default);
}

public sealed partial class HtmlResourceLink {
    /// <summary>Preserves the 2.0.x binary signature for saving resources.</summary>
    public Task<string> SaveAsync(string path, Uri? baseUri, HttpClient? client) =>
        SaveAsync(path, baseUri, client, null, default);
}
