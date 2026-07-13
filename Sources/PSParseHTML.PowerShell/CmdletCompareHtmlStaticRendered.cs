using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Compares static HTML with browser-rendered HTML using parsing-friendly signatures.</summary>
/// <example>
///   <summary>Compare captured static and rendered HTML strings</summary>
///   <code>Compare-HtmlStaticRendered -StaticContent $staticHtml -RenderedContent $renderedHtml</code>
/// </example>
/// <example>
///   <summary>Compare two saved HTML files</summary>
///   <code>Compare-HtmlStaticRendered -StaticPath .\static.html -RenderedPath .\rendered.html</code>
/// </example>
/// <example>
///   <summary>Download a page, render it in Chromium, and compare added links, forms, and structured data</summary>
///   <code>Compare-HtmlStaticRendered -Url https://example.org/app -Browser Chromium -Timeout 15000</code>
/// </example>
[Cmdlet(VerbsData.Compare, "HtmlStaticRendered", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlStaticRenderedComparison))]
public sealed class CmdletCompareHtmlStaticRendered : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>Original static HTML content before browser execution.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string StaticContent { get; set; } = string.Empty;

    /// <summary>Rendered HTML content after browser execution.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string RenderedContent { get; set; } = string.Empty;

    /// <summary>Path to the original static HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [ValidateNotNullOrEmpty]
    public string StaticPath { get; set; } = string.Empty;

    /// <summary>Path to the browser-rendered HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [ValidateNotNullOrEmpty]
    public string RenderedPath { get; set; } = string.Empty;

    /// <summary>URL of a page to download statically and render with a browser before comparing.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Base URL used to resolve relative links in comparison signatures. Defaults to Url when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>Browser engine used for rendered URL comparisons.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes before rendering.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Timeout in milliseconds for browser rendering operations.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Proxy server address used when downloading and rendering by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Credentials used when accessing authenticated pages.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? Credential { get; set; }

    /// <summary>Username for pages secured with basic authentication.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Username { get; set; }

    /// <summary>Password for pages secured with basic authentication.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Password { get; set; }

    /// <summary>Token used to cancel the browser-rendering operation.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        (string StaticHtml, string RenderedHtml) html = await ReadHtmlPairAsync().ConfigureAwait(false);
        Uri? baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? Url : null);
        WriteObject(HtmlParsingToolbox.CompareStaticRendered(html.StaticHtml, html.RenderedHtml, baseUri));
    }

    private async Task<(string StaticHtml, string RenderedHtml)> ReadHtmlPairAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return (
                    await HtmlUtilities.ReadFileCheckedAsync(StaticPath.ToFullPath()).ConfigureAwait(false),
                    await HtmlUtilities.ReadFileCheckedAsync(RenderedPath.ToFullPath()).ConfigureAwait(false));
            case ParameterSetUrl:
                string? user = Credential?.UserName ?? Username;
                string? pass = Credential?.GetNetworkCredential().Password ?? Password;
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential, Credential, Username, Password)) {
                    string staticHtml = await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
                    string? proxyUser = ProxyCredential?.UserName;
                    string? proxyPass = ProxyCredential?.GetNetworkCredential().Password;
                    using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
                    string renderedHtml = await HtmlBrowser.GetPageContentAsync(
                        Url.ToString(),
                        Browser,
                        Clean.IsPresent,
                        user,
                        pass,
                        formLogin: null,
                        headless: !Visible.IsPresent,
                        slowMo: 0,
                        userAgent: null,
                        viewportWidth: null,
                        viewportHeight: null,
                        deviceScaleFactor: null,
                        proxy: Proxy,
                        proxyUsername: proxyUser,
                        proxyPassword: proxyPass,
                        geoLatitude: null,
                        geoLongitude: null,
                        timezone: null,
                        timeout: Timeout,
                        cancellationToken: linkedCts.Token).ConfigureAwait(false);
                    return (staticHtml, renderedHtml);
                }
            default:
                return (StaticContent, RenderedContent);
        }
    }
}
