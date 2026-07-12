using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects JavaScript application configuration objects and framework state from inline HTML scripts.</summary>
/// <example>
///   <summary>Find common config and state objects</summary>
///   <code>Select-HtmlJavaScriptConfig -Content $html</code>
/// </example>
/// <example>
///   <summary>Read a nested API base URL from window.__CONFIG__</summary>
///   <code>Select-HtmlJavaScriptConfig -Content $html -Name window.__CONFIG__ -PropertyPath api.baseUrl</code>
/// </example>
/// <example>
///   <summary>Search selected HtmlAgilityPack markup for settings variables</summary>
///   <code>Select-HtmlNode -Content $html -XPath '//body' | Select-HtmlJavaScriptConfig -Name settings -Contains</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlJavaScriptConfig", DefaultParameterSetName = ParameterSetNode)]
[Alias("Select-HtmlJSConfig")]
[OutputType(typeof(HtmlJavaScriptConfigItem))]
public sealed class CmdletSelectHtmlJavaScriptConfig : AsyncPSCmdlet {
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

    /// <summary>HtmlAgilityPack node or document to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNode, ValueFromPipeline = true, Position = 0)]
    [Alias("Node", "InputObject")]
    public object HtmlNode { get; set; } = null!;

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Variable names or assignment paths to return. When omitted, common config and state names are searched.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? Name { get; set; }

    /// <summary>Matches variable names or full assignment paths that contain the provided Name values.</summary>
    [Parameter]
    public SwitchParameter Contains { get; set; }

    /// <summary>Matches variable names or full assignment paths that start with the provided Name values.</summary>
    [Parameter]
    public SwitchParameter StartsWith { get; set; }

    /// <summary>Returns a value from a dotted property path inside the matched JavaScript object or array literal.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? PropertyPath { get; set; }

    /// <summary>Skips known framework app-state payloads and returns only JavaScript variable matches.</summary>
    [Parameter]
    public SwitchParameter NoAppState { get; set; }

    /// <summary>Enables Acornima tolerant parsing for inline script content.</summary>
    [Parameter]
    public SwitchParameter Tolerant { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (Contains.IsPresent && StartsWith.IsPresent) {
            throw new PSArgumentException("Use either -Contains or -StartsWith, not both.");
        }

        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        WriteObject(HtmlParsingToolbox.SelectJavaScriptConfig(
            html,
            Name,
            Contains.IsPresent,
            StartsWith.IsPresent,
            PropertyPath,
            includeAppState: !NoAppState.IsPresent,
            tolerant: true).ToArray(), true);
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), CancelToken).ConfigureAwait(false);
                }
            case ParameterSetNode:
                return HtmlPipelineInput.ToHtmlMarkup(HtmlNode);
            default:
                return Content;
        }
    }
}
