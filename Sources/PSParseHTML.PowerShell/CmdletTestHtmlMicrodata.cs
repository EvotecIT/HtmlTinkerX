using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Validates microdata items against built-in schema definitions.
/// </summary>
/// <example>
/// <code>Test-HtmlMicrodata -Content $html</code>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "HtmlMicrodata", DefaultParameterSetName = ParameterSetItems)]
[OutputType(typeof(MicrodataSchemaMismatch))]
public sealed class CmdletTestHtmlMicrodata : AsyncPSCmdlet {
    private const string ParameterSetItems = "Items";
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>Microdata items to validate.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetItems)]
    public HtmlMicrodataItem[] Items { get; set; } = Array.Empty<HtmlMicrodataItem>();

    /// <summary>HTML markup containing microdata.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with microdata.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Proxy server address when downloading by URL.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    private readonly List<HtmlMicrodataItem> _items = new();

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        if (ParameterSetName == ParameterSetContent) {
            _items.AddRange(HtmlParser.ParseMicrodataItems(Content));
        } else if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            var list = await HtmlParser.ParseUrlMicrodataItemsAsync(Url.ToString(), client).ConfigureAwait(false);
            _items.AddRange(list);
        } else {
            _items.AddRange(Items);
        }
    }

    /// <inheritdoc />
    protected override Task EndProcessingAsync() {
        var mismatches = HtmlParser.ValidateMicrodataItems(_items);
        foreach (var mismatch in mismatches) {
            WriteObject(mismatch);
        }
        return Task.CompletedTask;
    }
}
