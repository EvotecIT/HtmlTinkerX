using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Discovers static, app-state, and endpoint data sources that can be extracted without starting a browser.
/// </summary>
/// <example>
///   <summary>Find browserless data sources in a downloaded page</summary>
///   <code>Find-HtmlDataSource -Url https://example.org/products -IncludeLinkedScripts</code>
/// </example>
/// <example>
///   <summary>Find only sources that can be extracted directly</summary>
///   <code>Invoke-HtmlPageWorkbench -Content $html -BaseUrl https://example.org | Find-HtmlDataSource -DirectOnly</code>
/// </example>
[Cmdlet(VerbsCommon.Find, "HtmlDataSource", DefaultParameterSetName = ParameterSetWorkbench)]
[OutputType(typeof(HtmlBrowserlessDataSource))]
public sealed class CmdletFindHtmlDataSource : AsyncPSCmdlet {
    private const string ParameterSetWorkbench = "Workbench";
    private const string ParameterSetContent = "Content";
    private const string ParameterSetPath = "Path";
    private const string ParameterSetUrl = "Url";
    private Uri? downloadedResponseUri;

    /// <summary>Page workbench result to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetWorkbench, ValueFromPipeline = true, Position = 0)]
    public HtmlPageWorkbenchResult? Workbench { get; set; }

    /// <summary>HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of the page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl, Position = 0)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Path to a local HTML file to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath, Position = 0)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Base URL used to resolve relative endpoints when Content or Path is used.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetPath)]
    public Uri? BaseUrl { get; set; }

    /// <summary>Downloads same-origin linked JavaScript files and inspects them for endpoints.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetPath)]
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public SwitchParameter IncludeLinkedScripts { get; set; }

    /// <summary>Allows linked-script inspection to download cross-origin scripts.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetPath)]
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public SwitchParameter IncludeExternalLinkedScripts { get; set; }

    /// <summary>Returns only sources that HtmlTinkerX can extract directly.</summary>
    [Parameter]
    public SwitchParameter DirectOnly { get; set; }

    /// <summary>Maximum number of sources to return.</summary>
    [Parameter]
    public int MaxSources { get; set; }

    /// <summary>Proxy server address used when downloading by URL or inspecting linked scripts.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetPath)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetPath)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        HtmlBrowserlessDiscoveryOptions options = new() {
            DirectOnly = DirectOnly.IsPresent,
            MaxSources = MaxSources,
            IncludeLinkedScripts = IncludeLinkedScripts.IsPresent || IncludeExternalLinkedScripts.IsPresent,
            IncludeExternalLinkedScripts = IncludeExternalLinkedScripts.IsPresent
        };

        if (ParameterSetName == ParameterSetWorkbench) {
            WriteObject(HtmlBrowserlessExtraction.Discover(Workbench!, options), true);
            return;
        }

        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync(client).ConfigureAwait(false);
        options.BaseUri = ParameterSetName == ParameterSetUrl ? downloadedResponseUri ?? Url : BaseUrl;
        WriteObject((await HtmlBrowserlessExtraction.DiscoverAsync(html, options, client, CancelToken).ConfigureAwait(false)), true);
    }

    private async Task<string> ReadHtmlAsync(HttpClient client) {
        if (ParameterSetName == ParameterSetUrl) {
            using CancellationTokenSource requestTimeout = HtmlUtilities.CreateRequestTimeoutTokenSource(client, CancelToken);
            CancellationToken requestToken = requestTimeout.Token;
            using HttpResponseMessage response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            downloadedResponseUri = response.RequestMessage?.RequestUri ?? Url;
            return await HtmlUtilities.ReadResponseContentWithProperEncodingAsync(response, fetchOptions: null, cancellationToken: requestToken).ConfigureAwait(false);
        }

        if (ParameterSetName == ParameterSetPath) {
            string fullPath = Path!.ToFullPath();
            return await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        }

        return Content;
    }
}
