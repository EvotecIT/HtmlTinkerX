using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
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
///   <code>ConvertFrom-HTML -Content $html | Select-HtmlNode -XPath '//script[@type="application/ld+json"]' | ConvertFrom-HtmlJsonLd</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlJsonLd", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlJsonLdItem))]
public sealed class CmdletConvertFromHtmlJsonLd : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetNode = "Node";
    private const string ParameterSetUrl = "Url";

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

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        if (ParameterSetName == ParameterSetNode) {
            WriteObject(HtmlJsonLdParser.ParseScriptContents(GetJsonLdScriptContents()).ToArray(), true);
            return;
        }

        string html = await ReadHtmlAsync().ConfigureAwait(false);
        WriteObject(HtmlJsonLdParser.Parse(html).ToArray(), true);
    }

    private IEnumerable<KeyValuePair<int, string>> GetJsonLdScriptContents() {
        if (IsJsonLdScript(HtmlNode)) {
            yield return new KeyValuePair<int, string>(0, HtmlNode.InnerHtml);
            yield break;
        }

        int scriptIndex = 0;
        foreach (HtmlNode script in HtmlNode.Descendants("script")) {
            if (IsJsonLdScript(script)) {
                yield return new KeyValuePair<int, string>(scriptIndex, script.InnerHtml);
            }

            scriptIndex++;
        }
    }

    private static bool IsJsonLdScript(HtmlNode node) {
        if (!string.Equals(node.Name, "script", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        string type = node.GetAttributeValue("type", string.Empty);
        return type.Contains("ld+json", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString()).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }
}
