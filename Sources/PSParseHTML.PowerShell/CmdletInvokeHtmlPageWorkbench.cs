using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Builds a one-page extraction workbench result with text, Markdown, data, forms, endpoints, and guidance.
/// </summary>
/// <example>
/// <code>Invoke-HtmlPageWorkbench -Url https://example.com</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlPageWorkbench", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlPageWorkbenchResult))]
public sealed class CmdletInvokeHtmlPageWorkbench : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetPath = "Path";
    private const string ParameterSetUrl = "Url";
    private Uri? downloadedResponseUri;

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

    /// <summary>Base URL used to resolve relative links and assets. Defaults to Url when downloading.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetPath)]
    public Uri? BaseUrl { get; set; }

    /// <summary>Downloads same-origin linked JavaScript files and inspects them for endpoints.</summary>
    [Parameter]
    public SwitchParameter IncludeLinkedScripts { get; set; }

    /// <summary>Allows linked-script endpoint inspection to download cross-origin scripts.</summary>
    [Parameter]
    public SwitchParameter IncludeExternalLinkedScripts { get; set; }

    /// <summary>Rendered snapshot from Invoke-HtmlRendering -Snapshot used to enrich the workbench.</summary>
    [Parameter]
    public HtmlRenderedPageSnapshot? RenderedSnapshot { get; set; }

    /// <summary>Skips static-vs-rendered comparison when a rendered snapshot is supplied.</summary>
    [Parameter]
    public SwitchParameter NoStaticRenderedComparison { get; set; }

    /// <summary>Omits the original HTML from the workbench result.</summary>
    [Parameter]
    public SwitchParameter NoHtml { get; set; }

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
        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync(client).ConfigureAwait(false);
        Uri? baseUri = ParameterSetName == ParameterSetUrl ? downloadedResponseUri ?? Url : BaseUrl;
        HtmlPageWorkbenchResult result = await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = baseUri,
                IncludeHtml = !NoHtml.IsPresent,
                RenderedSnapshot = RenderedSnapshot,
                IncludeStaticRenderedComparison = !NoStaticRenderedComparison.IsPresent,
                IncludeLinkedScripts = IncludeLinkedScripts.IsPresent || IncludeExternalLinkedScripts.IsPresent,
                IncludeExternalLinkedScripts = IncludeExternalLinkedScripts.IsPresent
            },
            client,
            CancelToken).ConfigureAwait(false);

        WriteObject(result);
    }

    private async Task<string> ReadHtmlAsync(HttpClient client) {
        if (ParameterSetName == ParameterSetUrl) {
            using HttpResponseMessage response = await client.GetAsync(Url, CancelToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            downloadedResponseUri = response.RequestMessage?.RequestUri ?? Url;
            return await HtmlUtilities.ReadResponseContentWithProperEncodingAsync(response, CancelToken).ConfigureAwait(false);
        }

        if (ParameterSetName == ParameterSetPath) {
            string fullPath = Path!.ToFullPath();
            return await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        }

        return Content;
    }
}
