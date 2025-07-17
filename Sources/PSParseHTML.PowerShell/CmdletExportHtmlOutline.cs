using System;
using System.Management.Automation;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Exports a hierarchical outline of headings in HTML content to a JSON file.
/// </summary>
/// <example>
/// <code>Export-HTMLOutline -Url https://example.com -Path outline.json</code>
/// </example>
[Cmdlet(VerbsData.Export, "HTMLOutline", DefaultParameterSetName = ParameterSetContent)]
public sealed class CmdletExportHtmlOutline : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML markup to analyze.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of the page to analyze.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl, Position = 0)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Destination path for the JSON outline.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Parsing engine used for processing HTML.</summary>
    [Parameter]
    public HtmlParserEngine Engine { get; set; } = HtmlParserEngine.AgilityPack;

    /// <summary>Proxy server address used when <see cref="Url"/> is specified.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the specified <see cref="Proxy"/>.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        List<HtmlOutlineItem> outline;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            outline = await HtmlOutlineBuilder.BuildFromUrlAsync(Url.ToString(), Engine, client).ConfigureAwait(false);
        } else {
            outline = HtmlOutlineBuilder.Build(Content, Engine);
        }

        var opts = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(outline, opts);
        string outPath = HtmlUtilities.ResolvePath(Path);
#if NETSTANDARD2_0 || NETFRAMEWORK
        System.IO.File.WriteAllText(outPath, json);
#else
        await System.IO.File.WriteAllTextAsync(outPath, json, CancelToken).ConfigureAwait(false);
#endif
    }
}
