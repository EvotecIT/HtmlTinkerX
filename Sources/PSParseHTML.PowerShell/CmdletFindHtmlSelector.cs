using HtmlTinkerX;
using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Finds repeated static HTML structures, likely fields, links, and replayable extraction commands or secure templates.</summary>
/// <example>
///   <summary>Discover product-card selectors containing visible product text</summary>
///   <code>Find-HtmlSelector -Url https://example.org/products -Query 'Product'</code>
/// </example>
/// <example>
///   <summary>Inspect the fields suggested for the highest-ranked repeated structure</summary>
///   <code>$candidate = Find-HtmlSelector -Content $html -Limit 1; $candidate.Fields</code>
/// </example>
[Cmdlet(VerbsCommon.Find, "HtmlSelector", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlDomSelectorCandidate))]
public sealed class CmdletFindHtmlSelector : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";
    private Uri? _effectiveUrl;

    /// <summary>HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile, Position = 0)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl, Position = 0)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Optional visible text, URL, id, class, or attribute fragment used to focus discovery.</summary>
    [Parameter(Position = 1)]
    public string? Query { get; set; }

    /// <summary>Base URL used to resolve relative link and image samples.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public Uri? BaseUrl { get; set; }

    /// <summary>Minimum number of repeated elements a candidate selector must match.</summary>
    [Parameter]
    [ValidateRange(2, int.MaxValue)]
    public int MinimumRepeatCount { get; set; } = 2;

    /// <summary>Maximum number of ranked candidates to return.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Limit { get; set; } = 10;

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>User-Agent header used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? UserAgent { get; set; }

    /// <summary>Additional or replacement HTTP headers used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Alias("Headers")]
    public Hashtable? Header { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        Uri? sourceUri = ParameterSetName == ParameterSetUrl ? _effectiveUrl ?? Url : BaseUrl;
        WriteObject(
            HtmlDomExtraction.DiscoverSelectors(
                html,
                Query,
                sourceUri,
                MinimumRepeatCount,
                Limit,
                CreateCommandSource()),
            true);
    }

    private async Task<string> ReadHtmlAsync() {
        if (ParameterSetName == ParameterSetFile) {
            return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
        }

        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential, UserAgent, Header);
            HtmlHttpTextResult result = await HtmlUtilities.GetTextWithProperEncodingAsync(
                client,
                Url.ToString(),
                fetchOptions: null,
                cancellationToken: CancelToken).ConfigureAwait(false);
            _effectiveUrl = result.FinalUri ?? Url;
            return result.Content;
        }

        return Content;
    }

    private HtmlDomCommandSource CreateCommandSource() {
        HtmlDomCommandSource source = new() {
            BaseUri = ParameterSetName == ParameterSetUrl ? null : BaseUrl
        };
        switch (ParameterSetName) {
            case ParameterSetUrl:
                source.Url = Url;
                source.UserAgent = UserAgent;
                source.Proxy = Proxy;
                source.UsesHeaders = Header != null;
                source.UsesProxyCredential = ProxyCredential != null;
                break;
            case ParameterSetFile:
                source.Path = Path.ToFullPath();
                break;
        }

        return source;
    }
}
