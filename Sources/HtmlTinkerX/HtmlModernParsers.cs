using Acornima;
using Acornima.Ast;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcornimaNode = Acornima.Ast.Node;

#pragma warning disable CS1591

namespace HtmlTinkerX;

/// <summary>JSON-LD item extracted from an HTML document.</summary>
public sealed class HtmlJsonLdItem {
    public int ScriptIndex { get; set; }
    public int Index { get; set; }
    public int? GraphIndex { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Id { get; set; }
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>Embedded application state object extracted from an HTML document.</summary>
public sealed class HtmlAppStateEntry {
    public int ScriptIndex { get; set; }
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>Structured link or metadata relation discovered in the document head.</summary>
public sealed class HtmlHeadLink {
    public int Index { get; set; }
    public string Element { get; set; } = string.Empty;
    public string Rel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Media { get; set; } = string.Empty;
    public string HrefLang { get; set; } = string.Empty;
    public string Sizes { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
}

/// <summary>Potential token discovered in forms, meta tags, attributes, or inline script values.</summary>
public sealed class HtmlToken {
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
}

/// <summary>Potential endpoint discovered in static JavaScript.</summary>
public sealed class HtmlJavaScriptEndpoint {
    public int Index { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

/// <summary>Rule or directive extracted from a robots.txt file.</summary>
public sealed class HtmlRobotsRule {
    public int Index { get; set; }
    public int GroupIndex { get; set; }
    public string Directive { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? CrawlDelay { get; set; }
    public int LineNumber { get; set; }
}

/// <summary>Extracts JSON-LD structured data from HTML.</summary>
public static class HtmlJsonLdParser {
    public static IReadOnlyList<HtmlJsonLdItem> Parse(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<HtmlJsonLdItem> items = new();
        int scriptIndex = 0;
        foreach (IElement script in document.QuerySelectorAll("script")) {
            string type = script.GetAttribute("type") ?? string.Empty;
            if (!type.Contains("ld+json", StringComparison.OrdinalIgnoreCase)) {
                scriptIndex++;
                continue;
            }

            string json = (script.TextContent ?? string.Empty).Trim();
            if (json.Length == 0) {
                scriptIndex++;
                continue;
            }

            AddJsonLdItems(json, scriptIndex, items);
            scriptIndex++;
        }

        return items;
    }

    public static async Task<IReadOnlyList<HtmlJsonLdItem>> ParseUrlAsync(string url, HttpClient? client = null) {
        string html = await HtmlModernParserUtilities.GetUrlStringAsync(url, client).ConfigureAwait(false);
        return Parse(html);
    }

    private static void AddJsonLdItems(string json, int scriptIndex, List<HtmlJsonLdItem> items) {
        try {
            using JsonDocument document = JsonDocument.Parse(json, HtmlModernParserUtilities.JsonOptions);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array) {
                int arrayIndex = 0;
                foreach (JsonElement element in root.EnumerateArray()) {
                    AddJsonLdElement(element, scriptIndex, items, "ArrayItem", arrayIndex++);
                }
            } else {
                AddJsonLdElement(root, scriptIndex, items, "Script", null);
            }
        } catch (JsonException) {
            items.Add(new HtmlJsonLdItem {
                ScriptIndex = scriptIndex,
                Index = items.Count,
                SourceKind = "InvalidJson",
                RawJson = json
            });
        }
    }

    private static void AddJsonLdElement(JsonElement element, int scriptIndex, List<HtmlJsonLdItem> items, string sourceKind, int? graphIndex) {
        if (element.ValueKind != JsonValueKind.Object) {
            items.Add(CreateItem(element, scriptIndex, items.Count, sourceKind, graphIndex));
            return;
        }

        if (element.TryGetProperty("@graph", out JsonElement graph) && graph.ValueKind == JsonValueKind.Array) {
            int index = 0;
            foreach (JsonElement graphNode in graph.EnumerateArray()) {
                items.Add(CreateItem(graphNode, scriptIndex, items.Count, "GraphNode", index++));
            }
            return;
        }

        items.Add(CreateItem(element, scriptIndex, items.Count, sourceKind, graphIndex));
    }

    private static HtmlJsonLdItem CreateItem(JsonElement element, int scriptIndex, int index, string sourceKind, int? graphIndex) {
        return new HtmlJsonLdItem {
            ScriptIndex = scriptIndex,
            Index = index,
            GraphIndex = graphIndex,
            SourceKind = sourceKind,
            Type = HtmlModernParserUtilities.GetJsonLdStringOrArray(element, "@type"),
            Id = HtmlModernParserUtilities.GetJsonString(element, "@id"),
            RawJson = element.GetRawText()
        };
    }
}

/// <summary>Extracts embedded application state from common framework script payloads.</summary>
public static class HtmlAppStateParser {
    private static readonly string[] KnownNames = {
        "__NEXT_DATA__",
        "__NUXT__",
        "__INITIAL_STATE__",
        "__APOLLO_STATE__",
        "__RELAY_STORE__",
        "__REDUX_STATE__",
        "__PRELOADED_STATE__",
        "__URQL_DATA__",
        "__SVELTEKIT_DATA__"
    };

    public static IReadOnlyList<HtmlAppStateEntry> Parse(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<HtmlAppStateEntry> entries = new();
        int scriptIndex = 0;
        foreach (IElement script in document.QuerySelectorAll("script")) {
            string id = script.GetAttribute("id") ?? string.Empty;
            string content = (script.TextContent ?? string.Empty).Trim();
            if (id.Equals("__NEXT_DATA__", StringComparison.OrdinalIgnoreCase) && content.Length > 0) {
                entries.Add(new HtmlAppStateEntry {
                    ScriptIndex = scriptIndex,
                    Index = entries.Count,
                    Name = "__NEXT_DATA__",
                    Framework = "Next.js",
                    SourceKind = "ScriptJson",
                    RawJson = NormalizeJson(content)
                });
            }

            if (content.Contains("__", StringComparison.Ordinal)) {
                AddAssignments(content, scriptIndex, entries);
            }

            scriptIndex++;
        }

        return entries;
    }

    public static async Task<IReadOnlyList<HtmlAppStateEntry>> ParseUrlAsync(string url, HttpClient? client = null) {
        string html = await HtmlModernParserUtilities.GetUrlStringAsync(url, client).ConfigureAwait(false);
        return Parse(html);
    }

    private static void AddAssignments(string script, int scriptIndex, List<HtmlAppStateEntry> entries) {
        AcornimaNode root;
        try {
            root = new Parser(new ParserOptions { Tolerant = true, AllowHashBang = true }).ParseScript(script, null, strict: false);
        } catch (ParseErrorException) {
            return;
        }

        foreach (AssignmentExpression assignment in HtmlModernParserUtilities.Walk(root).OfType<AssignmentExpression>()) {
            AddStateValue(GetStateName(assignment.Left), assignment.Right, scriptIndex, "Assignment", entries);
        }

        foreach (VariableDeclarator declarator in HtmlModernParserUtilities.Walk(root).OfType<VariableDeclarator>()) {
            AddStateValue(GetStateName(declarator.Id), declarator.Init, scriptIndex, "VariableDeclaration", entries);
        }
    }

    private static void AddStateValue(string? name, AcornimaNode? node, int scriptIndex, string sourceKind, List<HtmlAppStateEntry> entries) {
        if (name == null || node == null || !KnownNames.Contains(name, StringComparer.OrdinalIgnoreCase)) {
            return;
        }

        object? value = HtmlModernParserUtilities.EvaluateJavaScriptLiteral(node);
        if (value == null) {
            return;
        }

        entries.Add(new HtmlAppStateEntry {
            ScriptIndex = scriptIndex,
            Index = entries.Count,
            Name = name,
            Framework = GetFramework(name),
            SourceKind = sourceKind,
            RawJson = JsonSerializer.Serialize(value)
        });
    }

    private static string NormalizeJson(string json) {
        try {
            using JsonDocument document = JsonDocument.Parse(json, HtmlModernParserUtilities.JsonOptions);
            return document.RootElement.GetRawText();
        } catch (JsonException) {
            return json;
        }
    }

    private static string? GetStateName(AcornimaNode node) {
        if (node is Identifier identifier) {
            return identifier.Name;
        }

        if (node is MemberExpression member) {
            if (member.Property is Identifier propertyIdentifier) {
                return propertyIdentifier.Name;
            }

            if (member.Property is Literal literal) {
                return literal.Value?.ToString();
            }
        }

        return null;
    }

    private static string GetFramework(string name) {
        return name switch {
            "__NEXT_DATA__" => "Next.js",
            "__NUXT__" => "Nuxt",
            "__APOLLO_STATE__" => "Apollo",
            "__RELAY_STORE__" => "Relay",
            "__REDUX_STATE__" or "__PRELOADED_STATE__" => "Redux",
            "__URQL_DATA__" => "urql",
            "__SVELTEKIT_DATA__" => "SvelteKit",
            _ => "Generic"
        };
    }
}

/// <summary>Extracts structured head link and metadata relations.</summary>
public static class HtmlHeadLinkParser {
    public static IReadOnlyList<HtmlHeadLink> Parse(string html, Uri? baseUri = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        IElement scope = document.Head ?? document.DocumentElement;
        Uri? effectiveBaseUri = HtmlModernParserUtilities.GetEffectiveBaseUri(document, baseUri);
        List<HtmlHeadLink> links = new();

        foreach (IElement element in scope.QuerySelectorAll("link[href], meta[content]")) {
            string href = element.GetAttribute("href") ?? string.Empty;
            string content = element.GetAttribute("content") ?? string.Empty;
            string target = href.Length > 0 ? href : content;
            string resolved = href.Length > 0 || IsUrlValuedMeta(element)
                ? HtmlModernParserUtilities.ResolveUrl(target, effectiveBaseUri)
                : string.Empty;
            links.Add(new HtmlHeadLink {
                Index = links.Count,
                Element = element.TagName.ToLowerInvariant(),
                Rel = element.GetAttribute("rel") ?? string.Empty,
                Name = element.GetAttribute("name") ?? string.Empty,
                Property = element.GetAttribute("property") ?? string.Empty,
                Href = href,
                Url = resolved,
                Content = content,
                Type = element.GetAttribute("type") ?? string.Empty,
                Media = element.GetAttribute("media") ?? string.Empty,
                HrefLang = element.GetAttribute("hreflang") ?? string.Empty,
                Sizes = element.GetAttribute("sizes") ?? string.Empty,
                IsExternal = HtmlModernParserUtilities.IsExternal(resolved, baseUri)
            });
        }

        return links;
    }

    public static async Task<IReadOnlyList<HtmlHeadLink>> ParseUrlAsync(string url, HttpClient? client = null) {
        string html = await HtmlModernParserUtilities.GetUrlStringAsync(url, client).ConfigureAwait(false);
        return Parse(html, new Uri(url));
    }

    private static bool IsUrlValuedMeta(IElement element) {
        string name = element.GetAttribute("name") ?? string.Empty;
        string property = element.GetAttribute("property") ?? string.Empty;
        string itemprop = element.GetAttribute("itemprop") ?? string.Empty;

        return IsKnownUrlMetaName(name)
            || IsKnownUrlMetaName(property)
            || IsKnownUrlMetaName(itemprop);
    }

    private static bool IsKnownUrlMetaName(string value) {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0) {
            return false;
        }

        return normalized.Equals("url", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("image", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("thumbnail", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("contentUrl", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("embedUrl", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("msapplication-starturl", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("msapplication-TileImage", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("msapplication-config", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("og:url", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("og:image", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("og:audio", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("og:video", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("twitter:url", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("twitter:image", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("twitter:player", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(":secure_url", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(":url", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Extracts likely CSRF, anti-forgery, nonce, and auth tokens from HTML.</summary>
public static class HtmlTokenParser {
    private static readonly Regex ScriptTokenRegex = new(
        @"(?<name>[A-Za-z0-9_\-]*(?:csrf|xsrf|token|nonce|authenticity|verification)[A-Za-z0-9_\-]*)\s*[:=]\s*['""](?<value>[^'""]+)['""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<HtmlToken> Parse(string html, string[]? names = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<HtmlToken> tokens = new();
        foreach (IElement element in document.QuerySelectorAll("input[name], meta[name], meta[property], [data-token], [nonce]")) {
            string name = element.GetAttribute("name")
                ?? element.GetAttribute("property")
                ?? (element.HasAttribute("data-token") ? "data-token" : null)
                ?? (element.HasAttribute("nonce") ? "nonce" : string.Empty);
            string value = element.GetAttribute("value")
                ?? element.GetAttribute("content")
                ?? element.GetAttribute("data-token")
                ?? element.GetAttribute("nonce")
                ?? string.Empty;
            AddToken(tokens, name, value, element.TagName.ToLowerInvariant(), BuildSelector(element), names);
        }

        foreach (IElement script in document.QuerySelectorAll("script")) {
            string content = script.TextContent ?? string.Empty;
            foreach (Match match in ScriptTokenRegex.Matches(content)) {
                AddToken(tokens, match.Groups["name"].Value, match.Groups["value"].Value, "script", "script", names);
            }
        }

        return tokens;
    }

    public static async Task<IReadOnlyList<HtmlToken>> ParseUrlAsync(string url, string[]? names = null, HttpClient? client = null) {
        string html = await HtmlModernParserUtilities.GetUrlStringAsync(url, client).ConfigureAwait(false);
        return Parse(html, names);
    }

    private static void AddToken(List<HtmlToken> tokens, string name, string value, string source, string selector, string[]? names) {
        if (string.IsNullOrWhiteSpace(value) || !LooksLikeTokenName(name, names)) {
            return;
        }

        tokens.Add(new HtmlToken {
            Index = tokens.Count,
            Name = name,
            Value = value,
            Source = source,
            Selector = selector
        });
    }

    private static bool LooksLikeTokenName(string name, string[]? names) {
        if (names != null && names.Length > 0) {
            return names.Any(item => name.Equals(item, StringComparison.OrdinalIgnoreCase));
        }

        return name.Contains("csrf", StringComparison.OrdinalIgnoreCase)
            || name.Contains("xsrf", StringComparison.OrdinalIgnoreCase)
            || name.Contains("token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("nonce", StringComparison.OrdinalIgnoreCase)
            || name.Contains("authenticity", StringComparison.OrdinalIgnoreCase)
            || name.Contains("verification", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSelector(IElement element) {
        string tag = element.TagName.ToLowerInvariant();
        string? id = element.GetAttribute("id");
        if (!string.IsNullOrEmpty(id)) {
            return $"{tag}#{id}";
        }

        string? name = element.GetAttribute("name");
        return !string.IsNullOrEmpty(name) ? $"{tag}[name='{name}']" : tag;
    }
}

/// <summary>Discovers likely endpoints from static JavaScript source.</summary>
public static class HtmlJavaScriptEndpointParser {
    private static readonly Regex EndpointStringRegex = new(
        @"['""](?<url>(?:https?:)?//[^'""]+|/[^'""]+|(?:api|graphql|rest|v[0-9])(?:[/?#][^'""]*)?|[A-Za-z0-9_\-./]+/(?:api|graphql|rest|v[0-9])[^'""]*)['""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<HtmlJavaScriptEndpoint> ParseJavaScript(string script) {
        if (script == null) {
            throw new ArgumentNullException(nameof(script));
        }

        List<HtmlJavaScriptEndpoint> endpoints = new();
        foreach (Match match in EndpointStringRegex.Matches(script)) {
            string url = match.Groups["url"].Value;
            if (!LooksLikeEndpoint(url)) {
                continue;
            }

            endpoints.Add(new HtmlJavaScriptEndpoint {
                Index = endpoints.Count,
                Url = url,
                Method = InferMethod(script, match.Index),
                Client = InferClient(script, match.Index),
                OperationName = InferGraphQlOperation(script, match.Index, url),
                Source = "StringLiteral"
            });
        }

        return endpoints
            .GroupBy(endpoint => $"{endpoint.Method}|{endpoint.Url}|{endpoint.Client}|{endpoint.OperationName}", StringComparer.OrdinalIgnoreCase)
            .Select(group => {
                HtmlJavaScriptEndpoint first = group.First();
                first.Index = endpoints.IndexOf(first);
                return first;
            })
            .ToArray();
    }

    public static IReadOnlyList<HtmlJavaScriptEndpoint> ParseHtml(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<HtmlJavaScriptEndpoint> endpoints = new();
        foreach (IElement script in document.QuerySelectorAll("script")) {
            if (!IsJavaScriptScript(script)) {
                continue;
            }

            foreach (HtmlJavaScriptEndpoint endpoint in ParseJavaScript(script.TextContent ?? string.Empty)) {
                endpoint.Index = endpoints.Count;
                endpoint.Source = "InlineScript";
                endpoints.Add(endpoint);
            }
        }

        return endpoints;
    }

    private static bool IsJavaScriptScript(IElement script) {
        string type = (script.GetAttribute("type") ?? string.Empty).Split(';')[0].Trim();
        if (type.Length == 0) {
            return true;
        }

        return type.Equals("module", StringComparison.OrdinalIgnoreCase)
            || type.Equals("text/javascript", StringComparison.OrdinalIgnoreCase)
            || type.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
            || type.Equals("application/ecmascript", StringComparison.OrdinalIgnoreCase)
            || type.Equals("text/ecmascript", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeEndpoint(string value) {
        if (value.Length < 2 || value.StartsWith("//#", StringComparison.Ordinal) || value.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".css", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return value.Contains("/api", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("api", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("rest", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(value, @"^v[0-9](?:/|$)", RegexOptions.IgnoreCase)
            || value.Contains("graphql", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/", StringComparison.Ordinal);
    }

    private static string InferMethod(string script, int index) {
        string before = script.Substring(Math.Max(0, index - 80), index - Math.Max(0, index - 80));
        string after = script.Substring(index, Math.Min(script.Length - index, 140));
        Match methodCall = Regex.Match(before, @"(?:axios|client|\$)\.(?<method>get|post|put|patch|delete|head|options)\s*\($", RegexOptions.IgnoreCase);
        if (methodCall.Success) {
            return methodCall.Groups["method"].Value.ToUpperInvariant();
        }

        Match xhrOpen = Regex.Match(before, @"\.open\s*\(\s*['""](?<method>GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)['""]\s*,\s*$", RegexOptions.IgnoreCase);
        if (xhrOpen.Success) {
            return xhrOpen.Groups["method"].Value.ToUpperInvariant();
        }

        Match methodOption = Regex.Match(after, @"\b(?:method|type)\s*:\s*['""](?<method>GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)['""]", RegexOptions.IgnoreCase);
        if (methodOption.Success) {
            return methodOption.Groups["method"].Value.ToUpperInvariant();
        }

        return string.Empty;
    }

    private static string InferClient(string script, int index) {
        int start = Math.Max(0, index - 80);
        string before = script.Substring(start, index - start);
        if (Regex.IsMatch(before, @"axios\.(?:get|post|put|patch|delete|head|options)\s*\($", RegexOptions.IgnoreCase)) return "axios";
        if (Regex.IsMatch(before, @"(?:^|[^\w])fetch\s*\($", RegexOptions.IgnoreCase)) return "fetch";
        if (Regex.IsMatch(before, @"\$\.(?:ajax|get|post)\s*\($", RegexOptions.IgnoreCase)) return "jQuery.ajax";
        if (Regex.IsMatch(before, @"\$\.\s*ajax\s*\(\s*\{[^;]*$", RegexOptions.IgnoreCase)) return "jQuery.ajax";
        if (Regex.IsMatch(before, @"\.open\s*\(\s*['""](?:GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)['""]\s*,\s*$", RegexOptions.IgnoreCase)) return "XMLHttpRequest";
        if (Regex.IsMatch(before, @"(?:XMLHttpRequest|\.open\s*\()$", RegexOptions.IgnoreCase)) return "XMLHttpRequest";
        if (Regex.IsMatch(before, @"[A-Za-z0-9_$]+\.(?:get|post|put|patch|delete|head|options)\s*\($", RegexOptions.IgnoreCase)) return "client";
        return string.Empty;
    }

    private static string InferGraphQlOperation(string script, int index, string url) {
        if (!url.Contains("graphql", StringComparison.OrdinalIgnoreCase)) {
            return string.Empty;
        }

        string window = script.Substring(index, Math.Min(script.Length - index, 220));
        Match operation = Regex.Match(window, @"\b(query|mutation)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
        return operation.Success ? operation.Groups["name"].Value : string.Empty;
    }
}

/// <summary>Parses robots.txt directives.</summary>
public static class HtmlRobotsParser {
    public static IReadOnlyList<HtmlRobotsRule> Parse(string content, Uri? baseUri = null) {
        if (content == null) {
            throw new ArgumentNullException(nameof(content));
        }

        List<HtmlRobotsRule> rules = new();
        List<string> currentAgents = new();
        int groupIndex = -1;
        string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int index = 0; index < lines.Length; index++) {
            string line = StripComment(lines[index]).Trim();
            if (line.Length == 0) {
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon < 0) {
                continue;
            }

            string directive = line.Substring(0, colon).Trim();
            string value = line.Substring(colon + 1).Trim();
            if (directive.Equals("User-agent", StringComparison.OrdinalIgnoreCase)) {
                if (currentAgents.Count == 0 || rules.Any(rule => rule.GroupIndex == groupIndex && !rule.Directive.Equals("User-agent", StringComparison.OrdinalIgnoreCase))) {
                    groupIndex++;
                    currentAgents.Clear();
                }

                currentAgents.Add(value);
                rules.Add(CreateRobotsRule(rules.Count, Math.Max(groupIndex, 0), directive, value, value, index + 1, baseUri));
                continue;
            }

            IEnumerable<string> agents = currentAgents.Count == 0 ? new[] { string.Empty } : currentAgents;
            foreach (string agent in agents) {
                rules.Add(CreateRobotsRule(rules.Count, Math.Max(groupIndex, 0), directive, value, agent, index + 1, baseUri));
            }
        }

        return rules;
    }

    public static async Task<IReadOnlyList<HtmlRobotsRule>> ParseUrlAsync(string url, HttpClient? client = null) {
        string content = await HtmlModernParserUtilities.GetUrlStringAsync(url, client).ConfigureAwait(false);
        return Parse(content, new Uri(url));
    }

    private static string StripComment(string line) {
        int index = line.IndexOf('#');
        return index >= 0 ? line.Substring(0, index) : line;
    }

    private static bool IsPathDirective(string directive) {
        return directive.Equals("Allow", StringComparison.OrdinalIgnoreCase)
            || directive.Equals("Disallow", StringComparison.OrdinalIgnoreCase);
    }

    private static HtmlRobotsRule CreateRobotsRule(int index, int groupIndex, string directive, string value, string agent, int lineNumber, Uri? baseUri) {
        return new HtmlRobotsRule {
            Index = index,
            GroupIndex = groupIndex,
            Directive = directive,
            Value = value,
            UserAgent = agent,
            Path = IsPathDirective(directive) ? value : string.Empty,
            Url = directive.Equals("Sitemap", StringComparison.OrdinalIgnoreCase) ? HtmlModernParserUtilities.ResolveUrl(value, baseUri) : string.Empty,
            CrawlDelay = directive.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal delay) ? delay : null,
            LineNumber = lineNumber
        };
    }
}

internal static class HtmlModernParserUtilities {
    internal static readonly JsonDocumentOptions JsonOptions = new() {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    internal static async Task<string> GetUrlStringAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) {
            throw new ArgumentException("The URL must be an absolute URI.", nameof(url));
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        return await HtmlUtilities.GetStringWithProperEncodingAsync(http, uri.ToString()).ConfigureAwait(false);
    }

    internal static string ResolveUrl(string value, Uri? baseUri) {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        if (baseUri != null && Uri.TryCreate(baseUri, trimmed.Replace('\\', '/'), out Uri? resolved)) {
            return resolved.AbsoluteUri;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute) ? absolute.AbsoluteUri : trimmed;
    }

    internal static Uri? GetEffectiveBaseUri(IDocument document, Uri? baseUri) {
        if (baseUri == null) {
            return null;
        }

        string href = document.QuerySelector("base[href]")?.GetAttribute("href") ?? string.Empty;
        string resolved = ResolveUrl(href, baseUri);
        return Uri.TryCreate(resolved, UriKind.Absolute, out Uri? effectiveBaseUri) ? effectiveBaseUri : baseUri;
    }

    internal static bool IsExternal(string value, Uri? baseUri) {
        if (baseUri == null || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) {
            return false;
        }

        return !string.Equals(uri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || uri.Port != baseUri.Port;
    }

    internal static string? GetJsonString(JsonElement element, string propertyName) {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    internal static string? GetJsonLdStringOrArray(JsonElement element, string propertyName) {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value)) {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String) {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Array) {
            return string.Join(",", value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText()));
        }

        return value.GetRawText();
    }

    internal static IEnumerable<AcornimaNode> Walk(AcornimaNode root) {
        Stack<AcornimaNode> stack = new();
        stack.Push(root);
        while (stack.Count > 0) {
            AcornimaNode node = stack.Pop();
            yield return node;
            List<AcornimaNode> children = node.ChildNodes.ToList();
            for (int index = children.Count - 1; index >= 0; index--) {
                stack.Push(children[index]);
            }
        }
    }

    internal static object? EvaluateJavaScriptLiteral(AcornimaNode? node) {
        if (node == null) {
            return null;
        }

        if (node is Literal literal) {
            return literal.Value;
        }

        if (node is ArrayExpression array) {
            return array.Elements.Select(EvaluateJavaScriptLiteral).ToArray();
        }

        if (node is ObjectExpression objectExpression) {
            Dictionary<string, object?> values = new(StringComparer.Ordinal);
            foreach (Acornima.Ast.Property property in objectExpression.Properties.OfType<Acornima.Ast.Property>()) {
                string? key = property.Key switch {
                    Identifier identifier => identifier.Name,
                    Literal keyLiteral => keyLiteral.Value?.ToString(),
                    _ => null
                };
                if (!string.IsNullOrEmpty(key)) {
                    values[key!] = EvaluateJavaScriptLiteral(property.Value);
                }
            }
            return values;
        }

        if (node is UnaryExpression unary && unary.Operator.ToString() == "-" && EvaluateJavaScriptLiteral(unary.Argument) is IConvertible convertible) {
            return -convertible.ToDouble(CultureInfo.InvariantCulture);
        }

        return null;
    }
}
