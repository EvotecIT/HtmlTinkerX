using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Follows deterministic hidden-form relay pages without launching a browser.
/// </summary>
/// <example>
/// <code>Invoke-HtmlFormRelay -Url https://example.org/login/callback -AllowCrossHost</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlFormRelay", DefaultParameterSetName = ParameterSetUrl)]
[Alias("Invoke-HtmlAutoSubmitForm")]
[OutputType(typeof(HtmlFormRelayResult))]
public sealed class CmdletInvokeHtmlFormRelay : AsyncPSCmdlet {
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetContent = "Content";
    private const string ParameterSetPath = "Path";
    private Uri? initialResponseUri;

    /// <summary>URL returning the first possible relay form.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl, Position = 0)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>HTML content containing the first possible relay form.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Base URL used to resolve form actions when Content or Path is used.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath)]
    public Uri BaseUrl { get; set; } = null!;

    /// <summary>Path to an HTML file containing the first possible relay form.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath, Position = 0)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Maximum number of relay forms to submit.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxRelayCount { get; set; } = 5;

    /// <summary>Allow relay form actions to post to another host.</summary>
    [Parameter]
    public SwitchParameter AllowCrossHost { get; set; }

    /// <summary>Specific hosts allowed for cross-host relay actions.</summary>
    [Parameter]
    public string[] AllowedHost { get; set; } = Array.Empty<string>();

    /// <summary>Proxy server address used when downloading or submitting relay forms.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        using HttpClient client = HttpClientHelper.CreateWithCookies(Proxy, ProxyCredential, credential: null, username: null, password: null, out CookieContainer cookieContainer);
        string html = await GetInitialHtmlAsync(client).ConfigureAwait(false);
        Uri baseUri = ParameterSetName == ParameterSetUrl ? initialResponseUri ?? Url : BaseUrl;
        using HttpClient relayClient = HttpClientHelper.CreateWithCookies(cookieContainer, Proxy, ProxyCredential, allowAutoRedirect: false);
        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            html,
            baseUri,
            relayClient,
            new HtmlFormRelayOptions {
                MaxRelayCount = MaxRelayCount,
                AllowCrossHost = AllowCrossHost.IsPresent,
                AllowedHosts = AllowedHost
            },
            CancelToken).ConfigureAwait(false);

        WriteObject(result);
    }

    private async Task<string> GetInitialHtmlAsync(HttpClient client) {
        if (ParameterSetName == ParameterSetUrl) {
            using HttpResponseMessage response = await client.GetAsync(Url, CancelToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            initialResponseUri = response.RequestMessage?.RequestUri ?? Url;
            return await HtmlUtilities.ReadResponseContentWithProperEncodingAsync(response, fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
        }

        if (ParameterSetName == ParameterSetPath) {
            string fullPath = Path!.ToFullPath();
            return await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        }

        return Content;
    }
}
