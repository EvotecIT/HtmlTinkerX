using HtmlTinkerX;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Converts a page workbench result or HTML input into LLM-ready dataset JSON Lines.
/// </summary>
/// <example>
/// <code>Invoke-HtmlPageWorkbench -Url https://example.com | ConvertTo-HtmlDatasetJsonL</code>
/// </example>
[Cmdlet(VerbsData.ConvertTo, "HtmlDatasetJsonL", DefaultParameterSetName = ParameterSetWorkbench)]
[OutputType(typeof(string), typeof(HtmlPageDatasetChunk))]
public sealed class CmdletConvertToHtmlDatasetJsonL : AsyncPSCmdlet {
    private const string ParameterSetWorkbench = "Workbench";
    private const string ParameterSetContent = "Content";
    private const string ParameterSetPath = "Path";
    private const string ParameterSetUrl = "Url";

    /// <summary>Page workbench result to convert.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetWorkbench, ValueFromPipeline = true, Position = 0)]
    public HtmlPageWorkbenchResult? Workbench { get; set; }

    /// <summary>HTML content to inspect and convert.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of the page to download and convert.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl, Position = 0)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Path to a local HTML file to convert.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath, Position = 0)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Base URL used to resolve relative links and provenance when Content or Path is used.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetPath)]
    public Uri? BaseUrl { get; set; }

    /// <summary>Maximum number of words per dataset chunk.</summary>
    [Parameter]
    [ValidateRange(50, int.MaxValue)]
    public int MaxChunkWords { get; set; } = 350;

    /// <summary>Omits markdown content from dataset chunks.</summary>
    [Parameter]
    public SwitchParameter NoMarkdown { get; set; }

    /// <summary>Omits provenance entries from dataset chunks.</summary>
    [Parameter]
    public SwitchParameter NoProvenance { get; set; }

    /// <summary>Omits redaction hints from dataset chunks.</summary>
    [Parameter]
    public SwitchParameter NoRedactionHints { get; set; }

    /// <summary>Returns chunk objects instead of JSON Lines.</summary>
    [Parameter]
    public SwitchParameter AsObject { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        HtmlPageWorkbenchResult workbench = await GetWorkbenchAsync().ConfigureAwait(false);
        var chunks = HtmlPageDatasetBuilder.Build(workbench, new HtmlPageDatasetOptions {
            MaxChunkWords = MaxChunkWords,
            IncludeMarkdown = !NoMarkdown.IsPresent,
            IncludeProvenance = !NoProvenance.IsPresent,
            IncludeRedactionHints = !NoRedactionHints.IsPresent
        });

        if (AsObject.IsPresent) {
            WriteObject(chunks.ToArray(), true);
            return;
        }

        WriteObject(HtmlPageDatasetBuilder.ToJsonLines(chunks));
    }

    private async Task<HtmlPageWorkbenchResult> GetWorkbenchAsync() {
        if (ParameterSetName == ParameterSetWorkbench) {
            return Workbench!;
        }

        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync(client).ConfigureAwait(false);
        Uri? baseUri = ParameterSetName == ParameterSetUrl ? Url : BaseUrl;
        return await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = baseUri
            },
            client,
            CancelToken).ConfigureAwait(false);
    }

    private async Task<string> ReadHtmlAsync(HttpClient client) {
        if (ParameterSetName == ParameterSetUrl) {
            return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), CancelToken).ConfigureAwait(false);
        }

        if (ParameterSetName == ParameterSetPath) {
            string fullPath = Path!.ToFullPath();
            return await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        }

        return Content;
    }
}
