using AngleSharp.Dom;
using Microsoft.Playwright;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static HtmlCrawlStructuredApiEndpoint GetOrCreateStructuredApiEndpoint(
        IDictionary<string, HtmlCrawlStructuredApiEndpoint> endpoints,
        string method,
        string path) {
        string key = method.ToUpperInvariant() + " " + path;
        if (endpoints.TryGetValue(key, out HtmlCrawlStructuredApiEndpoint? existing)) {
            return existing;
        }

        HtmlCrawlStructuredApiEndpoint created = new() {
            Method = method.ToUpperInvariant(),
            Path = path
        };
        endpoints[key] = created;
        return created;
    }

    private static string DetectStructuredCodeSampleKind(string code, string? language) {
        if (TryParseApiMethodAndPath(code, out _, out _)) {
            return "http";
        }
        if (string.Equals(language, "http", StringComparison.OrdinalIgnoreCase)) {
            return "http";
        }
        if (Regex.IsMatch(code, @"(?im)^\s*curl\b")) {
            return "curl";
        }
        if (string.Equals(language, "powershell", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "ps1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "bash", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "sh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "shell", StringComparison.OrdinalIgnoreCase)) {
            return "command";
        }
        if (LooksLikeJson(code)) {
            return "json";
        }

        return string.IsNullOrWhiteSpace(language) ? "text" : "code";
    }

    private static string? BuildStructuredCodeSampleTitle(string? heading, string kind, string? method, string? path, string? language) {
        if (!string.IsNullOrWhiteSpace(heading)) {
            return heading;
        }
        if (!string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(path)) {
            return method + " " + path;
        }
        if (!string.IsNullOrWhiteSpace(language)) {
            return CultureInfoInvariantTitle(language!) + " sample";
        }

        return kind switch {
            "curl" => "cURL example",
            "http" => "HTTP example",
            "json" => "JSON example",
            "command" => "Command example",
            _ => "Code sample"
        };
    }

    private static bool LooksLikeRequestPayloadHeading(string? heading) {
        if (string.IsNullOrWhiteSpace(heading)) {
            return false;
        }

        return ContainsAnyToken(heading!,
            "request body",
            "request payload",
            "payload",
            "example request",
            "request example");
    }

    private static string? FindNearbyHeadingText(IElement element) {
        IElement? sibling = element.PreviousElementSibling;
        while (sibling != null) {
            if (Regex.IsMatch(sibling.LocalName, "^h[1-6]$", RegexOptions.IgnoreCase)) {
                return NormalizeWhitespace(sibling.TextContent);
            }

            sibling = sibling.PreviousElementSibling;
        }

        return null;
    }

    private static string? FindNearbyApiHeadingText(IElement element) {
        IElement? sibling = element.PreviousElementSibling;
        while (sibling != null) {
            if (Regex.IsMatch(sibling.LocalName, "^h[1-6]$", RegexOptions.IgnoreCase)) {
                string heading = NormalizeWhitespace(sibling.TextContent);
                if (TryParseApiMethodAndPath(heading, out _, out _)) {
                    return heading;
                }
            }

            sibling = sibling.PreviousElementSibling;
        }

        return null;
    }

    private static string? FindFollowingParagraphText(IElement element) {
        IElement? sibling = element.NextElementSibling;
        while (sibling != null) {
            if (string.Equals(sibling.LocalName, "p", StringComparison.OrdinalIgnoreCase)) {
                return NormalizeWhitespace(sibling.TextContent);
            }
            if (Regex.IsMatch(sibling.LocalName, "^h[1-6]$", RegexOptions.IgnoreCase)) {
                break;
            }

            sibling = sibling.NextElementSibling;
        }

        return null;
    }

    private static string? BuildStructuredApiPrimaryResource(string? path) {
        return GetStructuredApiLiteralPathSegments(path).FirstOrDefault();
    }

    private static IList<string> BuildStructuredApiTags(string? path, string? title, string? description) {
        List<string> tags = new();
        foreach (string segment in GetStructuredApiLiteralPathSegments(path)) {
            AppendDistinct(tags, segment);
        }

        if (tags.Count == 0) {
            foreach (string token in ExtractStructuredTitleTokens(title)) {
                AppendDistinct(tags, token);
            }
        }

        if (tags.Count == 0 && !string.IsNullOrWhiteSpace(description)) {
            foreach (string token in ExtractStructuredTitleTokens(description).Take(2)) {
                AppendDistinct(tags, token);
            }
        }

        return tags;
    }

    private static string BuildStructuredApiOperationId(string method, string path, string? title) {
        List<string> tokens = new();
        tokens.Add(method.ToLowerInvariant());

        List<string> segments = GetStructuredApiPathSegments(path);
        foreach (string segment in segments) {
            if (segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal)) {
                string parameterName = segment.Substring(1, segment.Length - 2);
                tokens.Add("by");
                tokens.Add(parameterName);
                continue;
            }

            if (!IsStructuredApiVersionSegment(segment)) {
                tokens.Add(segment);
            }
        }

        if (tokens.Count <= 1) {
            foreach (string token in ExtractStructuredTitleTokens(title)) {
                tokens.Add(token);
            }
        }

        if (tokens.Count <= 1) {
            tokens.Add("operation");
        }

        return BuildStructuredCamelIdentifier(tokens);
    }

    private static List<string> GetStructuredApiLiteralPathSegments(string? path) {
        return GetStructuredApiPathSegments(path)
            .Where(segment => !segment.StartsWith("{", StringComparison.Ordinal) || !segment.EndsWith("}", StringComparison.Ordinal))
            .Where(segment => !IsStructuredApiVersionSegment(segment))
            .ToList();
    }

    private static List<string> GetStructuredApiPathSegments(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return new List<string>();
        }

        string normalized = path!;
        int queryIndex = normalized.IndexOf('?');
        if (queryIndex >= 0) {
            normalized = normalized.Substring(0, queryIndex);
        }

        return normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => NormalizeWhitespace(segment))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();
    }

    private static bool IsStructuredApiVersionSegment(string segment) =>
        Regex.IsMatch(segment, @"^v\d+(?:\.\d+)?$", RegexOptions.IgnoreCase);

    private static IEnumerable<string> ExtractStructuredTitleTokens(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return Array.Empty<string>();
        }

        return Regex.Matches(value, @"[A-Za-z][A-Za-z0-9]*")
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(token => !IsStructuredStopWord(token))
            .Select(token => token.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsStructuredStopWord(string token) =>
        token.Equals("a", StringComparison.OrdinalIgnoreCase)
        || token.Equals("an", StringComparison.OrdinalIgnoreCase)
        || token.Equals("the", StringComparison.OrdinalIgnoreCase)
        || token.Equals("and", StringComparison.OrdinalIgnoreCase)
        || token.Equals("or", StringComparison.OrdinalIgnoreCase)
        || token.Equals("for", StringComparison.OrdinalIgnoreCase)
        || token.Equals("from", StringComparison.OrdinalIgnoreCase)
        || token.Equals("with", StringComparison.OrdinalIgnoreCase)
        || token.Equals("your", StringComparison.OrdinalIgnoreCase)
        || token.Equals("endpoint", StringComparison.OrdinalIgnoreCase)
        || token.Equals("api", StringComparison.OrdinalIgnoreCase)
        || token.Equals("request", StringComparison.OrdinalIgnoreCase)
        || token.Equals("response", StringComparison.OrdinalIgnoreCase);

    private static string BuildStructuredCamelIdentifier(IEnumerable<string> tokens) {
        List<string> parts = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .SelectMany(token => Regex.Matches(token, @"[A-Za-z0-9]+").Cast<Match>().Select(match => match.Value))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
        if (parts.Count == 0) {
            return "operation";
        }

        StringBuilder builder = new(parts[0].ToLowerInvariant());
        for (int index = 1; index < parts.Count; index++) {
            string part = parts[index].ToLowerInvariant();
            builder.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) {
                builder.Append(part.Substring(1));
            }
        }

        return builder.ToString();
    }

    private static bool ContainsAnyToken(string text, params string[] tokens) {
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        return tokens.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void AppendDistinct(IList<string> values, string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase)) {
            values.Add(value);
        }
    }

    private static string? DetectCodeBlockLanguage(IElement element) {
        foreach (IElement candidate in new[] { element, element.ParentElement }.Where(item => item != null)!) {
            string? attributeLanguage = candidate.GetAttribute("data-language")
                ?? candidate.GetAttribute("data-lang")
                ?? candidate.GetAttribute("language")
                ?? candidate.GetAttribute("lang");
            if (!string.IsNullOrWhiteSpace(attributeLanguage)) {
                return NormalizeStructuredLanguage(attributeLanguage!);
            }

            foreach (string className in candidate.ClassList) {
                if (className.StartsWith("language-", StringComparison.OrdinalIgnoreCase)) {
                    return NormalizeStructuredLanguage(className.Substring("language-".Length));
                }
                if (className.StartsWith("lang-", StringComparison.OrdinalIgnoreCase)) {
                    return NormalizeStructuredLanguage(className.Substring("lang-".Length));
                }
            }
        }

        return null;
    }

    private static string NormalizeCodeBlockText(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string normalized = value!.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
        int start = 0;
        int end = lines.Length - 1;
        while (start <= end && string.IsNullOrWhiteSpace(lines[start])) {
            start++;
        }
        while (end >= start && string.IsNullOrWhiteSpace(lines[end])) {
            end--;
        }

        if (start > end) {
            return string.Empty;
        }

        return string.Join("\n", lines.Skip(start).Take(end - start + 1));
    }

    private static string NormalizeStructuredLanguage(string language) {
        return language.Trim().Trim('.', ':').ToLowerInvariant();
    }

    private static string CultureInfoInvariantTitle(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
    }

    private static bool TryParseApiMethodAndPath(string input, out string? method, out string? path) {
        method = null;
        path = null;
        if (string.IsNullOrWhiteSpace(input)) {
            return false;
        }

        Match directMatch = Regex.Match(input, @"(?im)\b(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\s+((?:https?://[^\s'""]+)?/(?:[^\s'""]*)?)");
        if (!directMatch.Success) {
            return TryParseCurlMethodAndPath(input, out method, out path);
        }

        method = directMatch.Groups[1].Value.ToUpperInvariant();
        path = NormalizeStructuredApiPath(directMatch.Groups[2].Value);
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryParseCurlMethodAndPath(string input, out string? method, out string? path) {
        method = null;
        path = null;
        if (string.IsNullOrWhiteSpace(input) || !Regex.IsMatch(input, @"(?im)^\s*curl\b")) {
            return false;
        }

        if (TryExtractCurlMethod(input, out string? parsedMethod)) {
            method = parsedMethod;
        }

        if (TryExtractCurlTarget(input, out string? target)) {
            path = NormalizeStructuredApiPath(target!);
        }

        if (string.IsNullOrWhiteSpace(method)) {
            method = Regex.IsMatch(input, @"(?is)(?<!\S)(?:--data-raw|--data-binary|--data|--data-urlencode|-d)(?:\s|$)")
                ? "POST"
                : "GET";
        }

        return !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryExtractCurlMethod(string code, out string? method) {
        method = null;
        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        Match methodMatch = Regex.Match(code, @"(?is)(?<!\S)(?:-X|--request)(?:\s+|=)(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\b");
        if (!methodMatch.Success) {
            return false;
        }

        method = methodMatch.Groups[1].Value.ToUpperInvariant();
        return true;
    }

    private static bool TryExtractCurlTarget(string code, out string? target) {
        target = null;
        if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code, @"(?im)^\s*curl\b")) {
            return false;
        }

        Match urlOptionMatch = Regex.Match(code, @"(?is)(?<!\S)--url(?:\s+|=)(?:""([^""]+)""|'([^']+)'|([^\s]+))");
        if (urlOptionMatch.Success) {
            target = FirstNonEmptyValue(
                urlOptionMatch.Groups[1].Value,
                urlOptionMatch.Groups[2].Value,
                urlOptionMatch.Groups[3].Value);
            return !string.IsNullOrWhiteSpace(target);
        }

        List<string> tokens = TokenizeShellLikeArguments(code);
        if (tokens.Count == 0 || !string.Equals(tokens[0], "curl", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        HashSet<string> optionsWithSeparateValue = new(StringComparer.OrdinalIgnoreCase) {
            "-X",
            "--request",
            "-H",
            "--header",
            "-d",
            "--data",
            "--data-raw",
            "--data-binary",
            "--data-urlencode",
            "-e",
            "--referer",
            "-A",
            "--user-agent",
            "-u",
            "--user",
            "-F",
            "--form",
            "-o",
            "--output",
            "--url",
            "--cookie",
            "-b",
            "--proxy",
            "-x",
            "--cacert",
            "--cert",
            "--key"
        };

        for (int index = 1; index < tokens.Count; index++) {
            string token = tokens[index];
            if (string.IsNullOrWhiteSpace(token)) {
                continue;
            }

            if (token == "--") {
                continue;
            }

            if (optionsWithSeparateValue.Contains(token)) {
                index++;
                continue;
            }

            if (LooksLikeCurlOptionWithInlineValue(token)) {
                continue;
            }

            if (LooksLikeCurlTargetToken(token)) {
                target = token;
            }
        }

        return !string.IsNullOrWhiteSpace(target);
    }

    private static bool LooksLikeCurlOptionWithInlineValue(string token) {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("-", StringComparison.Ordinal)) {
            return false;
        }

        return token.StartsWith("--request=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--header=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data-raw=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data-binary=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data-urlencode=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--url=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--referer=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-X", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-H", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-d", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-e", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-A", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-u", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-F", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-o", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCurlTargetToken(string token) =>
        !string.IsNullOrWhiteSpace(token)
        && (Regex.IsMatch(token, @"^https?://", RegexOptions.IgnoreCase)
            || token.StartsWith("/", StringComparison.Ordinal));

    private static List<string> TokenizeShellLikeArguments(string command) {
        List<string> tokens = new();
        foreach (Match match in Regex.Matches(command, @"(?:""((?:\\""|[^""])*)""|'((?:\\'|[^'])*)'|(\S+))")) {
            string? value = FirstNonEmptyValue(
                match.Groups[1].Value.Replace("\\\"", "\""),
                match.Groups[2].Value.Replace("\\'", "'"),
                match.Groups[3].Value);
            if (!string.IsNullOrWhiteSpace(value)) {
                tokens.Add(value!);
            }
        }

        return tokens;
    }

    private static string? FirstNonEmptyValue(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string NormalizeStructuredApiPath(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))) {
            return string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
        }

        int queryIndex = trimmed.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0) {
            trimmed = trimmed.Substring(0, queryIndex);
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "/" : trimmed;
    }

    private static bool LooksLikeJson(string code) {
        string trimmed = code.Trim();
        return (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            || (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal));
    }

    private static string? TryResolveStructuredHref(Uri? baseUri, string? href) {
        if (string.IsNullOrWhiteSpace(href)) {
            return null;
        }

        if (baseUri == null) {
            return href;
        }

        return TryResolveAbsoluteUri(baseUri, href!, out Uri? resolved) && resolved != null ? resolved.AbsoluteUri : href;
    }

    private static string? FindMetaContent(IEnumerable<HtmlMetaTag> metaTags, params string[] names) {
        foreach (string name in names) {
            HtmlMetaTag? match = metaTags.FirstOrDefault(tag =>
                string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(tag.Content));
            if (match != null) {
                return match.Content;
            }
        }

        return null;
    }

    private static string? FindOpenGraphValue(HtmlOpenGraph openGraph, string propertyName) {
        OpenGraphProperty? match = openGraph.Properties.FirstOrDefault(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return match?.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static IList<string> SplitMetadataKeywords(string? keywords) {
        if (string.IsNullOrWhiteSpace(keywords)) {
            return new List<string>();
        }

        return keywords!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IDictionary<string, object?> BuildStructuredSchemaExtraction(
        HtmlCrawlStructuredJson structuredJson,
        IDocument document,
        IDocument selectedDocument,
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema) {
        Dictionary<string, object?> extracted = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlJsonSchemaField> field in structuredSchema) {
            extracted[field.Key] = ExtractStructuredSchemaFieldValue(structuredJson, document, selectedDocument, field.Value);
        }

        return extracted;
    }

    private static object? ExtractStructuredSchemaFieldValue(
        HtmlCrawlStructuredJson structuredJson,
        IDocument document,
        IDocument selectedDocument,
        HtmlCrawlJsonSchemaField field) {
        if (!string.IsNullOrWhiteSpace(field.Path)) {
            return ResolveStructuredPath(structuredJson, field.Path!);
        }

        if (string.IsNullOrWhiteSpace(field.Selector)) {
            return null;
        }

        IDocument sourceDocument = ResolveStructuredSchemaSourceDocument(document, selectedDocument, field.Source);
        IHtmlCollection<IElement> elements = sourceDocument.QuerySelectorAll(field.Selector!);
        string mode = string.IsNullOrWhiteSpace(field.Mode) ? "Text" : field.Mode!.Trim();
        if (string.Equals(mode, "Exists", StringComparison.OrdinalIgnoreCase)) {
            return elements.Length > 0;
        }

        if (string.Equals(mode, "Count", StringComparison.OrdinalIgnoreCase)) {
            return elements.Length;
        }

        if (field.All) {
            return elements
                .Select(element => ExtractStructuredSchemaElementValue(element, mode, field.Attribute))
                .Where(value => value != null)
                .ToList();
        }

        IElement? first = elements.FirstOrDefault();
        return first == null ? null : ExtractStructuredSchemaElementValue(first, mode, field.Attribute);
    }

    private static IDocument ResolveStructuredSchemaSourceDocument(IDocument document, IDocument selectedDocument, string? source) {
        if (string.IsNullOrWhiteSpace(source)) {
            return selectedDocument;
        }

        return source!.Trim().ToLowerInvariant() switch {
            "page" or "document" or "full" => document,
            _ => selectedDocument
        };
    }

    private static object? ExtractStructuredSchemaElementValue(IElement element, string mode, string? attribute) {
        if (string.Equals(mode, "Html", StringComparison.OrdinalIgnoreCase)) {
            return element.OuterHtml;
        }

        if (string.Equals(mode, "Markdown", StringComparison.OrdinalIgnoreCase)) {
            return ConvertSelectedHtmlToMarkdown(element.OuterHtml, null);
        }

        if (string.Equals(mode, "Attribute", StringComparison.OrdinalIgnoreCase)) {
            return string.IsNullOrWhiteSpace(attribute) ? null : element.GetAttribute(attribute!);
        }

        return NormalizeWhitespace(element.TextContent);
    }

    private static object? ResolveStructuredPath(object? current, string path) {
        if (current == null || string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        object? value = current;
        foreach (string segment in path.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
            if (value == null) {
                return null;
            }

            if (value is IDictionary<string, object?> dictionary) {
                if (!TryGetDictionaryValue(dictionary, segment, out value)) {
                    return null;
                }
                continue;
            }

            if (value is IDictionary nonGenericDictionary) {
                if (!TryGetDictionaryValue(nonGenericDictionary, segment, out value)) {
                    return null;
                }
                continue;
            }

            if (value is IList list) {
                if (string.Equals(segment, "Count", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment, "Length", StringComparison.OrdinalIgnoreCase)) {
                    value = list.Count;
                    continue;
                }

                if (!int.TryParse(segment, out int index) || index < 0 || index >= list.Count) {
                    return null;
                }

                value = list[index];
                continue;
            }

            PropertyInfo? property = value.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(item => string.Equals(item.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (property == null) {
                return null;
            }

            value = property.GetValue(value);
        }

        return value;
    }

    private static bool TryGetDictionaryValue(IDictionary<string, object?> dictionary, string key, out object? value) {
        foreach (KeyValuePair<string, object?> item in dictionary) {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetDictionaryValue(IDictionary dictionary, string key, out object? value) {
        foreach (DictionaryEntry item in dictionary) {
            string? itemKey = Convert.ToString(item.Key, System.Globalization.CultureInfo.InvariantCulture);
            if (string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase)) {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string[] ExtractHeadings(string? html) {
        if (string.IsNullOrWhiteSpace(html)) {
            return Array.Empty<string>();
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html!);
        return document.QuerySelectorAll("h1, h2, h3, h4, h5, h6")
            .Select(element => NormalizeWhitespace(element.TextContent))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .ToArray();
    }

    private static string[] ExtractKeywords(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return Array.Empty<string>();
        }

        Dictionary<string, int> frequencies = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text, @"\b[\p{L}\p{N}][\p{L}\p{N}'_-]*\b")) {
            string token = NormalizeKeyword(match.Value);
            if (token.Length < 3 || SearchStopWords.Contains(token)) {
                continue;
            }

            frequencies[token] = frequencies.TryGetValue(token, out int count) ? count + 1 : 1;
        }

        return frequencies
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(pair => pair.Key)
            .ToArray();
    }

    private static string NormalizeKeyword(string value) {
        string normalized = value.Trim().Trim('\'', '"', '-', '_').ToLowerInvariant();
        if (normalized.EndsWith("'s", StringComparison.Ordinal)) {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }

        return normalized;
    }

    internal static int CountWords(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return 0;
        }

        return Regex.Matches(text, @"\b[\p{L}\p{N}][\p{L}\p{N}'_-]*\b").Count;
    }

    private static string BuildSummary(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }

        const int maxLength = 180;
        string normalized = NormalizeWhitespace(text);
        if (normalized.Length <= maxLength) {
            return normalized;
        }

        int cut = normalized.LastIndexOf(' ', maxLength);
        if (cut < maxLength / 2) {
            cut = maxLength;
        }

        return normalized.Substring(0, cut).TrimEnd() + "...";
    }

    private static string NormalizeWhitespace(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        return Regex.Replace(value!, @"\s+", " ").Trim();
    }

    private static string? BuildRelativeOptionalPath(string? fromFilePath, string? toFilePath) {
        if (string.IsNullOrWhiteSpace(fromFilePath) || string.IsNullOrWhiteSpace(toFilePath)) {
            return null;
        }

        return BuildRelativePath(fromFilePath!, toFilePath!);
    }

}
