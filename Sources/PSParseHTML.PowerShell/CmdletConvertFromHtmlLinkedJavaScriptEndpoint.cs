using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Downloads linked JavaScript files from HTML and discovers likely endpoints.</summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlLinkedJavaScriptEndpoint", DefaultParameterSetName = ParameterSetContent)]
[Alias("ConvertFrom-HtmlLinkedJSEndpoint")]
[OutputType(typeof(HtmlLinkedJavaScriptEndpoint))]
public sealed class CmdletConvertFromHtmlLinkedJavaScriptEndpoint : AsyncUrlCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
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

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Base URL used to resolve linked script URLs. Defaults to Url when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>Download and inspect cross-origin linked scripts. Same-origin scripts are inspected by default.</summary>
    [Parameter]
    public SwitchParameter IncludeExternal { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateUrlParameters();
        using (HttpClient client = await CreateUrlClientAsync().ConfigureAwait(false)) {
            string html = await ReadHtmlAsync(client).ConfigureAwait(false);
            Uri baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? Url : throw new InvalidOperationException("BaseUrl is required when parsing linked scripts from Content or Path."));
            var endpoints = await HtmlLinkedJavaScriptEndpointParser.ParseAsync(html, baseUri, IncludeExternal.IsPresent, client).ConfigureAwait(false);
            WriteObject(endpoints.ToArray(), true);
        }
    }

    private async Task<string> ReadHtmlAsync(HttpClient client) {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
            default:
                return Content;
        }
    }
}
