using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1591

namespace HtmlTinkerX;

public sealed class HtmlScriptDataItem {
    public int Index { get; set; }
    public int ScriptIndex { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public bool IsJson { get; set; }
    public string Selector { get; set; } = string.Empty;
}

public sealed class HtmlLinkedJavaScriptEndpoint {
    public int Index { get; set; }
    public int ScriptIndex { get; set; }
    public string Selector { get; set; } = string.Empty;
    public string ScriptUrl { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
    public bool IsDownloaded { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class HtmlImageCandidate {
    public int Index { get; set; }
    public string Element { get; set; } = string.Empty;
    public string SourceAttribute { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string WidthDescriptor { get; set; } = string.Empty;
    public string PixelDensityDescriptor { get; set; } = string.Empty;
    public string Sizes { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Media { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
}

public sealed class HtmlWebManifestDocument {
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StartUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string Orientation { get; set; } = string.Empty;
    public string ThemeColor { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string Dir { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public List<HtmlWebManifestImage> Icons { get; } = new();
    public List<HtmlWebManifestImage> Screenshots { get; } = new();
    public List<HtmlWebManifestRelatedApplication> RelatedApplications { get; } = new();
}

public sealed class HtmlWebManifestImage {
    public int Index { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Src { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sizes { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class HtmlWebManifestRelatedApplication {
    public int Index { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public sealed class HtmlWellKnownRecord {
    public int Index { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string PublisherId { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string CertificationAuthorityId { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

public static class HtmlScriptDataParser {
    public static IReadOnlyList<HtmlScriptDataItem> Parse(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<HtmlScriptDataItem> items = new();
        int scriptIndex = 0;
        foreach (IElement script in document.QuerySelectorAll("script")) {
            string type = (script.GetAttribute("type") ?? string.Empty).Trim();
            string content = (script.TextContent ?? string.Empty).Trim();
            if (content.Length == 0 || !IsDataScriptType(type)) {
                scriptIndex++;
                continue;
            }

            string id = script.GetAttribute("id") ?? string.Empty;
            items.Add(CreateItem(items.Count, scriptIndex, id, type, content));
            scriptIndex++;
        }

        return items;
    }

    /// <summary>Preserves the 2.0.x binary signature for URL script-data parsing.</summary>
    public static Task<IReadOnlyList<HtmlScriptDataItem>> ParseUrlAsync(string url, HttpClient? client) =>
        ParseUrlAsync(url, client, null, default);

    public static async Task<IReadOnlyList<HtmlScriptDataItem>> ParseUrlAsync(string url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        string html = await HtmlModernParserUtilities.GetUrlStringAsync(url, client, fetchOptions, cancellationToken).ConfigureAwait(false);
        return Parse(html);
    }

    private static HtmlScriptDataItem CreateItem(int index, int scriptIndex, string id, string type, string content) {
        string raw = content;
        bool isJson = false;
        try {
            using JsonDocument document = JsonDocument.Parse(content, HtmlModernParserUtilities.JsonOptions);
            raw = document.RootElement.GetRawText();
            isJson = true;
        } catch (JsonException) {
            // Keep original text for malformed script data so callers can inspect the source.
        }

        return new HtmlScriptDataItem {
            Index = index,
            ScriptIndex = scriptIndex,
            Id = id,
            Type = type,
            SourceKind = isJson ? "ScriptJson" : "InvalidJson",
            RawJson = raw,
            IsJson = isJson,
            Selector = string.IsNullOrEmpty(id) ? $"script:nth-of-type({scriptIndex + 1})" : $"script#{id}"
        };
    }

    private static bool IsDataScriptType(string type) {
        string mediaType = type.Split(';')[0].Trim();
        if (mediaType.Length == 0) {
            return false;
        }

        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("importmap", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("speculationrules", StringComparison.OrdinalIgnoreCase);
    }
}

public static class HtmlLinkedJavaScriptEndpointParser {
    /// <summary>Preserves the 2.0.x binary signature for linked JavaScript parsing.</summary>
    public static Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> ParseAsync(string html, Uri baseUri, bool includeExternal, HttpClient? client) =>
        ParseAsync(html, baseUri, includeExternal, client, null, null, default);

    public static async Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> ParseAsync(string html, Uri baseUri, bool includeExternal = false, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        return await ParseAsync(html, baseUri, includeExternal, client, null, fetchOptions, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> ParseAsync(string html, Uri baseUri, bool includeExternal, HttpClient? client, CancellationToken cancellationToken) {
        return await ParseAsync(html, baseUri, includeExternal, client, null, null, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> ParseAsync(string html, Uri pageBaseUri, bool includeExternal, HttpClient? client, Uri? effectiveBaseUriOverride, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        return await ParseAsync(html, pageBaseUri, includeExternal, client, client, effectiveBaseUriOverride, fetchOptions, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> ParseAsync(string html, Uri pageBaseUri, bool includeExternal, HttpClient? sameOriginClient, HttpClient? externalClient, Uri? effectiveBaseUriOverride, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        if (pageBaseUri == null) {
            throw new ArgumentNullException(nameof(pageBaseUri));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri effectiveBaseUri = effectiveBaseUriOverride ?? HtmlModernParserUtilities.GetEffectiveBaseUri(document, pageBaseUri) ?? pageBaseUri;
        List<HtmlLinkedJavaScriptEndpoint> endpoints = new();
        int scriptIndex = 0;
        foreach (IElement script in document.QuerySelectorAll("script")) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!script.HasAttribute("src")) {
                scriptIndex++;
                continue;
            }

            string type = script.GetAttribute("type") ?? string.Empty;
            if (!IsJavaScriptScriptType(type)) {
                scriptIndex++;
                continue;
            }

            string source = script.GetAttribute("src") ?? string.Empty;
            string selector = CreateScriptSelector(script, scriptIndex);
            string scriptUrl = HtmlModernParserUtilities.ResolveUrl(source, effectiveBaseUri);
            bool isExternal = HtmlModernParserUtilities.IsExternal(scriptUrl, pageBaseUri);
            if (isExternal && !includeExternal) {
                scriptIndex++;
                continue;
            }

            if (!IsHttpUrl(scriptUrl)) {
                endpoints.Add(new HtmlLinkedJavaScriptEndpoint {
                    Index = endpoints.Count,
                    ScriptIndex = scriptIndex,
                    Selector = selector,
                    ScriptUrl = scriptUrl,
                    IsExternal = isExternal,
                    IsDownloaded = false,
                    Error = "Only HTTP and HTTPS script URLs can be downloaded."
                });
                scriptIndex++;
                continue;
            }

            try {
                HttpClient http = (isExternal ? externalClient : sameOriginClient) ?? HtmlHttpClientFactory.Shared;
                string scriptContent = await HtmlUtilities.GetStringWithProperEncodingAsync(http, scriptUrl, fetchOptions, cancellationToken).ConfigureAwait(false);
                foreach (HtmlJavaScriptEndpoint endpoint in HtmlJavaScriptEndpointParser.ParseJavaScript(scriptContent)) {
                    endpoints.Add(new HtmlLinkedJavaScriptEndpoint {
                        Index = endpoints.Count,
                        ScriptIndex = scriptIndex,
                        Selector = selector,
                        ScriptUrl = scriptUrl,
                        IsExternal = isExternal,
                        IsDownloaded = true,
                        Url = endpoint.Url,
                        Method = endpoint.Method,
                        Client = endpoint.Client,
                        OperationName = endpoint.OperationName,
                        Source = endpoint.Source
                    });
                }
            } catch (Exception ex) when (!cancellationToken.IsCancellationRequested && (ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException || ex is InvalidDataException)) {
                endpoints.Add(new HtmlLinkedJavaScriptEndpoint {
                    Index = endpoints.Count,
                    ScriptIndex = scriptIndex,
                    Selector = selector,
                    ScriptUrl = scriptUrl,
                    IsExternal = isExternal,
                    IsDownloaded = false,
                    Error = ex.Message
                });
            }

            scriptIndex++;
        }

        return endpoints;
    }

    /// <summary>Preserves the 2.0.x binary signature for URL linked JavaScript parsing.</summary>
    public static Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> ParseUrlAsync(string url, bool includeExternal, HttpClient? client) =>
        ParseUrlAsync(url, includeExternal, client, null, default);

    public static async Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> ParseUrlAsync(string url, bool includeExternal = false, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        string html = await HtmlModernParserUtilities.GetUrlStringAsync(url, client, fetchOptions, cancellationToken).ConfigureAwait(false);
        return await ParseAsync(html, new Uri(url, UriKind.Absolute), includeExternal, client, fetchOptions, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsJavaScriptScriptType(string type) {
        string normalized = (type ?? string.Empty).Split(';')[0].Trim();
        return normalized.Length == 0
            || normalized.Equals("module", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("text/javascript", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("application/ecmascript", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("text/ecmascript", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateScriptSelector(IElement script, int scriptIndex) {
        string id = script.GetAttribute("id") ?? string.Empty;
        return string.IsNullOrEmpty(id) ? $"script:nth-of-type({scriptIndex + 1})" : $"script#{id}";
    }

    private static bool IsHttpUrl(string value) {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}

public static class HtmlImageCandidateParser {
    public static IReadOnlyList<HtmlImageCandidate> Parse(string html, Uri? baseUri = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri? effectiveBaseUri = HtmlModernParserUtilities.GetEffectiveBaseUri(document, baseUri);
        List<HtmlImageCandidate> candidates = new();
        foreach (IElement image in document.QuerySelectorAll("img")) {
            AddCandidate(candidates, image, "src", image.GetAttribute("src"), effectiveBaseUri, baseUri);
            AddSrcSetCandidates(candidates, image, "srcset", image.GetAttribute("srcset"), effectiveBaseUri, baseUri);
        }

        foreach (IElement source in document.QuerySelectorAll("source[srcset]")) {
            AddSrcSetCandidates(candidates, source, "srcset", source.GetAttribute("srcset"), effectiveBaseUri, baseUri);
        }

        foreach (IElement link in document.QuerySelectorAll("link[href], link[imagesrcset]")) {
            string rel = link.GetAttribute("rel") ?? string.Empty;
            string asValue = link.GetAttribute("as") ?? string.Empty;
            if (rel.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Any(token => token.Equals("preload", StringComparison.OrdinalIgnoreCase))
                && asValue.Equals("image", StringComparison.OrdinalIgnoreCase)) {
                AddCandidate(candidates, link, "href", link.GetAttribute("href"), effectiveBaseUri, baseUri);
                AddSrcSetCandidates(candidates, link, "imagesrcset", link.GetAttribute("imagesrcset"), effectiveBaseUri, baseUri);
            }
        }

        return candidates;
    }

    /// <summary>Preserves the 2.0.x binary signature for URL image-candidate parsing.</summary>
    public static Task<IReadOnlyList<HtmlImageCandidate>> ParseUrlAsync(string url, HttpClient? client) =>
        ParseUrlAsync(url, client, null, default);

    public static async Task<IReadOnlyList<HtmlImageCandidate>> ParseUrlAsync(string url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        string html = await HtmlModernParserUtilities.GetUrlStringAsync(url, client, fetchOptions, cancellationToken).ConfigureAwait(false);
        return Parse(html, new Uri(url, UriKind.Absolute));
    }

    private static void AddSrcSetCandidates(List<HtmlImageCandidate> candidates, IElement element, string attribute, string? srcset, Uri? resolveBaseUri, Uri? pageBaseUri) {
        if (string.IsNullOrWhiteSpace(srcset)) {
            return;
        }

        foreach (string part in SplitSrcSet(srcset!)) {
            string candidate = part.Trim();
            if (candidate.Length == 0) {
                continue;
            }

            string[] pieces = candidate.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 0) {
                continue;
            }

            HtmlImageCandidate item = CreateCandidate(candidates.Count, element, attribute, pieces[0], resolveBaseUri, pageBaseUri);
            foreach (string descriptor in pieces.Skip(1)) {
                if (descriptor.EndsWith("w", StringComparison.OrdinalIgnoreCase)) {
                    item.WidthDescriptor = descriptor;
                } else if (descriptor.EndsWith("x", StringComparison.OrdinalIgnoreCase)) {
                    item.PixelDensityDescriptor = descriptor;
                }
            }

            candidates.Add(item);
        }
    }

    private static IEnumerable<string> SplitSrcSet(string srcset) {
        int start = 0;
        for (int index = 0; index < srcset.Length; index++) {
            char current = srcset[index];
            if (current != ',') {
                continue;
            }

            string candidate = srcset.Substring(start, index - start).TrimStart();
            if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                && !candidate.Any(char.IsWhiteSpace)
                && !IsSeparatorAfterDataUrl(srcset, index)) {
                continue;
            }

            yield return srcset.Substring(start, index - start);
            start = index + 1;
        }

        yield return srcset.Substring(start);
    }

    internal static string GetBestSourceSetSource(string? srcset) {
        if (string.IsNullOrWhiteSpace(srcset)) return string.Empty;
        string bestSource = string.Empty;
        double bestScore = double.MinValue;
        foreach (string part in SplitSrcSet(srcset!)) {
            string[] pieces = part.Trim().Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 0) continue;

            double score = 0;
            foreach (string descriptor in pieces.Skip(1)) {
                string numeric = descriptor.TrimEnd('w', 'W', 'x', 'X');
                if (double.TryParse(
                    numeric,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double parsed)) {
                    score = Math.Max(score, descriptor.EndsWith("x", StringComparison.OrdinalIgnoreCase)
                        ? parsed * 10000d
                        : parsed);
                }
            }

            if (bestSource.Length == 0 || score >= bestScore) {
                bestSource = pieces[0];
                bestScore = score;
            }
        }

        return bestSource;
    }

    private static bool IsSeparatorAfterDataUrl(string srcset, int commaIndex) {
        return commaIndex + 1 >= srcset.Length || char.IsWhiteSpace(srcset[commaIndex + 1]);
    }

    private static void AddCandidate(List<HtmlImageCandidate> candidates, IElement element, string attribute, string? value, Uri? resolveBaseUri, Uri? pageBaseUri) {
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        candidates.Add(CreateCandidate(candidates.Count, element, attribute, value!, resolveBaseUri, pageBaseUri));
    }

    private static HtmlImageCandidate CreateCandidate(int index, IElement element, string attribute, string source, Uri? resolveBaseUri, Uri? pageBaseUri) {
        string url = HtmlModernParserUtilities.ResolveUrl(source, resolveBaseUri);
        return new HtmlImageCandidate {
            Index = index,
            Element = element.LocalName,
            SourceAttribute = attribute,
            Source = source,
            Url = url,
            Sizes = attribute.Equals("imagesrcset", StringComparison.OrdinalIgnoreCase)
                ? element.GetAttribute("imagesizes") ?? string.Empty
                : element.GetAttribute("sizes") ?? string.Empty,
            Type = element.GetAttribute("type") ?? string.Empty,
            Media = element.GetAttribute("media") ?? string.Empty,
            Alt = element.GetAttribute("alt") ?? string.Empty,
            IsExternal = HtmlModernParserUtilities.IsExternal(url, pageBaseUri)
        };
    }
}

public static class HtmlWebManifestParser {
    public static HtmlWebManifestDocument Parse(string json, Uri? baseUri = null) {
        if (json == null) {
            throw new ArgumentNullException(nameof(json));
        }

        using JsonDocument document = JsonDocument.Parse(json, HtmlModernParserUtilities.JsonOptions);
        JsonElement root = document.RootElement;
        HtmlWebManifestDocument manifest = new() {
            Name = GetString(root, "name"),
            ShortName = GetString(root, "short_name"),
            Description = GetString(root, "description"),
            StartUrl = Resolve(GetString(root, "start_url"), baseUri),
            Scope = Resolve(GetString(root, "scope"), baseUri),
            Display = GetString(root, "display"),
            Orientation = GetString(root, "orientation"),
            ThemeColor = GetString(root, "theme_color"),
            BackgroundColor = GetString(root, "background_color"),
            Lang = GetString(root, "lang"),
            Dir = GetString(root, "dir"),
            RawJson = root.GetRawText()
        };

        AddImages(root, "icons", "Icon", baseUri, manifest.Icons);
        AddImages(root, "screenshots", "Screenshot", baseUri, manifest.Screenshots);
        AddRelatedApplications(root, baseUri, manifest.RelatedApplications);
        return manifest;
    }

    /// <summary>Preserves the 2.0.x binary signature for URL web-manifest parsing.</summary>
    public static Task<HtmlWebManifestDocument> ParseUrlAsync(string url, HttpClient? client) =>
        ParseUrlAsync(url, client, null, default);

    public static async Task<HtmlWebManifestDocument> ParseUrlAsync(string url, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        string json = await HtmlModernParserUtilities.GetUrlStringAsync(url, client, fetchOptions, cancellationToken).ConfigureAwait(false);
        return Parse(json, new Uri(url, UriKind.Absolute));
    }

    private static void AddImages(JsonElement root, string propertyName, string kind, Uri? baseUri, List<HtmlWebManifestImage> images) {
        if (!root.TryGetProperty(propertyName, out JsonElement array) || array.ValueKind != JsonValueKind.Array) {
            return;
        }

        foreach (JsonElement item in array.EnumerateArray()) {
            string src = GetString(item, "src");
            images.Add(new HtmlWebManifestImage {
                Index = images.Count,
                Kind = kind,
                Src = src,
                Url = Resolve(src, baseUri),
                Sizes = GetString(item, "sizes"),
                Type = GetString(item, "type"),
                Purpose = GetString(item, "purpose"),
                Label = GetString(item, "label")
            });
        }
    }

    private static void AddRelatedApplications(JsonElement root, Uri? baseUri, List<HtmlWebManifestRelatedApplication> applications) {
        if (!root.TryGetProperty("related_applications", out JsonElement array) || array.ValueKind != JsonValueKind.Array) {
            return;
        }

        foreach (JsonElement item in array.EnumerateArray()) {
            applications.Add(new HtmlWebManifestRelatedApplication {
                Index = applications.Count,
                Platform = GetString(item, "platform"),
                Url = Resolve(GetString(item, "url"), baseUri),
                Id = GetString(item, "id")
            });
        }
    }

    private static string GetString(JsonElement element, string propertyName) {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string Resolve(string value, Uri? baseUri) {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : HtmlModernParserUtilities.ResolveUrl(value, baseUri);
    }
}

public static class HtmlWellKnownParser {
    public static IReadOnlyList<HtmlWellKnownRecord> Parse(string content, string kind, Uri? baseUri = null) {
        if (content == null) {
            throw new ArgumentNullException(nameof(content));
        }

        string normalizedKind = NormalizeKind(kind);
        return normalizedKind switch {
            "security.txt" => ParseSecurityTxt(content, baseUri),
            "humans.txt" => ParseHumansTxt(content),
            "ads.txt" => ParseAdsTxt(content),
            _ => throw new ArgumentException("Unsupported well-known file kind.", nameof(kind))
        };
    }

    /// <summary>Preserves the 2.0.x binary signature for well-known URL parsing.</summary>
    public static Task<IReadOnlyList<HtmlWellKnownRecord>> ParseUrlAsync(string url, string kind, HttpClient? client) =>
        ParseUrlAsync(url, kind, client, null, default);

    public static async Task<IReadOnlyList<HtmlWellKnownRecord>> ParseUrlAsync(string url, string kind, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        string content = await HtmlModernParserUtilities.GetUrlStringAsync(url, client, fetchOptions, cancellationToken).ConfigureAwait(false);
        return Parse(content, kind, new Uri(url, UriKind.Absolute));
    }

    private static IReadOnlyList<HtmlWellKnownRecord> ParseSecurityTxt(string content, Uri? baseUri) {
        List<HtmlWellKnownRecord> records = new();
        string[] lines = SplitLines(content);
        for (int index = 0; index < lines.Length; index++) {
            string line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) {
                continue;
            }

            int separator = line.IndexOf(':');
            if (separator <= 0) {
                continue;
            }

            string field = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            records.Add(new HtmlWellKnownRecord {
                Index = records.Count,
                Kind = "security.txt",
                Field = field,
                Value = value,
                Url = IsSecurityUrlField(field) ? HtmlModernParserUtilities.ResolveUrl(value, baseUri) : string.Empty,
                LineNumber = index + 1
            });
        }

        return records;
    }

    private static IReadOnlyList<HtmlWellKnownRecord> ParseHumansTxt(string content) {
        List<HtmlWellKnownRecord> records = new();
        string section = string.Empty;
        string[] lines = SplitLines(content);
        for (int index = 0; index < lines.Length; index++) {
            string line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) {
                continue;
            }

            if (line.StartsWith("/*", StringComparison.Ordinal) && line.EndsWith("*/", StringComparison.Ordinal)) {
                section = line.Trim('/', '*', ' ').Trim();
                continue;
            }

            int separator = line.IndexOf(':');
            string field = separator > 0 ? line.Substring(0, separator).Trim() : string.Empty;
            string value = separator > 0 ? line.Substring(separator + 1).Trim() : line;
            records.Add(new HtmlWellKnownRecord {
                Index = records.Count,
                Kind = "humans.txt",
                Section = section,
                Field = field,
                Value = value,
                LineNumber = index + 1
            });
        }

        return records;
    }

    private static IReadOnlyList<HtmlWellKnownRecord> ParseAdsTxt(string content) {
        List<HtmlWellKnownRecord> records = new();
        string[] lines = SplitLines(content);
        for (int index = 0; index < lines.Length; index++) {
            string line = StripComment(lines[index]).Trim();
            if (line.Length == 0) {
                continue;
            }

            if (line.Contains("=", StringComparison.Ordinal) && !line.Contains(",", StringComparison.Ordinal)) {
                int separator = line.IndexOf('=');
                records.Add(new HtmlWellKnownRecord {
                    Index = records.Count,
                    Kind = "ads.txt",
                    Field = line.Substring(0, separator).Trim(),
                    Value = line.Substring(separator + 1).Trim(),
                    LineNumber = index + 1
                });
                continue;
            }

            string[] parts = line.Split(',').Select(part => part.Trim()).ToArray();
            if (parts.Length >= 3) {
                records.Add(new HtmlWellKnownRecord {
                    Index = records.Count,
                    Kind = "ads.txt",
                    Domain = parts[0],
                    PublisherId = parts[1],
                    Relationship = parts[2],
                    CertificationAuthorityId = parts.Length > 3 ? parts[3] : string.Empty,
                    Value = line,
                    LineNumber = index + 1
                });
            }
        }

        return records;
    }

    private static string NormalizeKind(string kind) {
        string normalized = (kind ?? string.Empty).Trim();
        if (!normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) {
            normalized += ".txt";
        }

        return normalized.ToLowerInvariant();
    }

    private static string[] SplitLines(string content) {
        return content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static string StripComment(string line) {
        int index = line.IndexOf('#');
        return index >= 0 ? line.Substring(0, index) : line;
    }

    private static bool IsSecurityUrlField(string field) {
        return field.Equals("Contact", StringComparison.OrdinalIgnoreCase)
            || field.Equals("Encryption", StringComparison.OrdinalIgnoreCase)
            || field.Equals("Acknowledgments", StringComparison.OrdinalIgnoreCase)
            || field.Equals("Policy", StringComparison.OrdinalIgnoreCase)
            || field.Equals("Hiring", StringComparison.OrdinalIgnoreCase)
            || field.Equals("Canonical", StringComparison.OrdinalIgnoreCase);
    }
}
