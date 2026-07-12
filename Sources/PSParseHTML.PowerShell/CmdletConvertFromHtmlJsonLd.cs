using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Extracts JSON-LD structured data from HTML.</summary>
/// <example>
///   <summary>Extract JSON-LD from static HTML content</summary>
///   <code>ConvertFrom-HtmlJsonLd -Content $html</code>
/// </example>
/// <example>
///   <summary>Download a page and extract JSON-LD structured data</summary>
///   <code>ConvertFrom-HtmlJsonLd -Url https://example.org/article</code>
/// </example>
/// <example>
///   <summary>Inspect only selected JSON-LD script nodes from an HtmlAgilityPack pipeline</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//script[@type="application/ld+json"]' | ConvertFrom-HtmlJsonLd</code>
/// </example>
/// <example>
///   <summary>Return only Product JSON-LD items</summary>
///   <code>ConvertFrom-HtmlJsonLd -Content $html -Type Product</code>
/// </example>
/// <example>
///   <summary>Emit parsed JSON payload objects instead of metadata records</summary>
///   <code>ConvertFrom-HtmlJsonLd -Content $html -Type Product -AsObject</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlJsonLd", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlJsonLdItem))]
public sealed class CmdletConvertFromHtmlJsonLd : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetNode = "Node";
    private const string ParameterSetUrl = "Url";
    private static readonly JsonDocumentOptions JsonOptions = new() {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>HtmlAgilityPack node to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNode, ValueFromPipeline = true, Position = 0)]
    [Alias("Node", "InputObject")]
    public HtmlNode HtmlNode { get; set; } = null!;

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Filters results to one or more JSON-LD @type values.</summary>
    [Parameter]
    public string[]? Type { get; set; }

    /// <summary>Emits parsed JSON payloads instead of HtmlJsonLdItem metadata objects.</summary>
    [Parameter]
    public SwitchParameter AsObject { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        IReadOnlyList<HtmlJsonLdItem> items;
        if (ParameterSetName == ParameterSetNode) {
            items = HtmlJsonLdParser.ParseScriptContents(GetJsonLdScriptContents());
        } else {
            string html = await ReadHtmlAsync().ConfigureAwait(false);
            items = HtmlJsonLdParser.Parse(html);
        }

        HtmlJsonLdItem[] filteredItems = FilterByType(items).ToArray();
        if (AsObject.IsPresent) {
            WriteObject(filteredItems.Select(ConvertRawJsonToObject).ToArray(), true);
        } else {
            WriteObject(filteredItems, true);
        }
    }

    private IEnumerable<KeyValuePair<int, string>> GetJsonLdScriptContents() {
        if (IsJsonLdScript(HtmlNode)) {
            yield return new KeyValuePair<int, string>(GetSourceScriptIndex(HtmlNode, 0), HtmlNode.InnerHtml);
            yield break;
        }

        int scriptIndex = 0;
        foreach (HtmlNode script in HtmlNode.Descendants("script")) {
            if (IsJsonLdScript(script)) {
                yield return new KeyValuePair<int, string>(GetSourceScriptIndex(script, scriptIndex), script.InnerHtml);
            }

            scriptIndex++;
        }
    }

    private static int GetSourceScriptIndex(HtmlNode script, int fallbackIndex) {
        HtmlNode? documentNode = script.OwnerDocument?.DocumentNode;
        if (documentNode != null) {
            int index = 0;
            foreach (HtmlNode candidate in documentNode.Descendants("script")) {
                if (ReferenceEquals(candidate, script)) {
                    return index;
                }

                index++;
            }
        }

        return fallbackIndex;
    }

    private static bool IsJsonLdScript(HtmlNode node) {
        if (!string.Equals(node.Name, "script", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        string type = node.GetAttributeValue("type", string.Empty);
        return type.Contains("ld+json", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<HtmlJsonLdItem> FilterByType(IEnumerable<HtmlJsonLdItem> items) {
        if (Type == null || Type.Length == 0) {
            return items;
        }

        HashSet<string> types = new(Type.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.OrdinalIgnoreCase);
        if (types.Count == 0) {
            return items;
        }

        return items.Where(item => item.Type != null && item.Type.Split(',').Any(value => types.Contains(value.Trim())));
    }

    private static object? ConvertRawJsonToObject(HtmlJsonLdItem item) {
        try {
            using JsonDocument document = JsonDocument.Parse(item.RawJson, JsonOptions);
            return ConvertJsonElement(document.RootElement);
        } catch (JsonException) {
            return item.RawJson;
        }
    }

    private static object? ConvertJsonElement(JsonElement element) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                PSObject obj = new();
                foreach (JsonProperty property in element.EnumerateObject()) {
                    obj.Properties.Add(new PSNoteProperty(property.Name, ConvertJsonElement(property.Value)));
                }

                return obj;
            case JsonValueKind.Array:
                return element.EnumerateArray().Select(ConvertJsonElement).ToArray();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long longValue)) {
                    return longValue;
                }

                if (element.TryGetDecimal(out decimal decimalValue)) {
                    return decimalValue;
                }

                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }
}
