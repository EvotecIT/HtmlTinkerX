using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Discovers likely endpoints from static JavaScript source or inline HTML scripts.</summary>
[Cmdlet(VerbsData.ConvertFrom, "JavaScriptEndpoint", DefaultParameterSetName = ParameterSetContent)]
[Alias("ConvertFrom-JSEndpoint")]
[OutputType(typeof(HtmlJavaScriptEndpoint))]
public sealed class CmdletConvertFromJavaScriptEndpoint : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>JavaScript or HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a JavaScript or HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of a JavaScript or HTML document to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Treat the input as HTML and inspect inline script content.</summary>
    [Parameter]
    public SwitchParameter Html { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string content = await ReadContentAsync().ConfigureAwait(false);
        object[] endpoints = Html.IsPresent
            ? HtmlJavaScriptEndpointParser.ParseHtml(content).Cast<object>().ToArray()
            : HtmlJavaScriptEndpointParser.ParseJavaScript(content).Cast<object>().ToArray();
        WriteObject(endpoints, true);
    }

    private async Task<string> ReadContentAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), CancelToken).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }
}
