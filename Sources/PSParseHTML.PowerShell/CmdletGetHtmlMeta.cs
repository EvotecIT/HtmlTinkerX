using System;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that extracts <meta> tags from HTML content or pages.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLMeta", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlMetaTag))]
public sealed class CmdletGetHtmlMeta : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of the page to download.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Selects parsing engine.</summary>
    [Parameter]
    [ValidateSet("AngleSharp", "AgilityPack")]
    public string Engine { get; set; } = "AgilityPack";

    /// <summary>Proxy server for downloading when using <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string? html = null;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            html = await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString()).ConfigureAwait(false);
        } else if (ParameterSetName == ParameterSetFile) {
            html = await HtmlUtilities.ReadFileCheckedAsync(Path).ConfigureAwait(false);
        } else {
            html = Content;
        }

        var results = Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase)
            ? HtmlParser.ParseMetaTagsWithAngleSharp(html)
            : HtmlParser.ParseMetaTagsWithHtmlAgilityPack(html);

        WriteObject(results, true);
    }
}
