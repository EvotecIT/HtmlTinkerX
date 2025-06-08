using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Converts HTML lists into PowerShell string arrays.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlList", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string[]))]
public sealed class CmdletConvertFromHtmlList : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content containing lists.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with lists.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Selects parsing engine.</summary>
    [Parameter]
    [ValidateSet("AngleSharp", "AgilityPack")]
    public string Engine { get; set; } = "AgilityPack";

    /// <summary>
    /// Proxy server address used when <see cref="Url"/> is specified.
    /// Include protocol and port if required.
    /// </summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>
    /// Credentials used for authenticating with the specified <see cref="Proxy"/> server.
    /// </summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        List<List<string>> lists;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase)) {
                lists = HtmlParser.ParseUrlListsWithAngleSharpAsync(Url.ToString(), client).GetAwaiter().GetResult();
            } else {
                lists = HtmlParser.ParseUrlListsWithHtmlAgilityPackAsync(Url.ToString(), client).GetAwaiter().GetResult();
            }
        } else {
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase)) {
                lists = HtmlParser.ParseListsWithAngleSharp(Content);
            } else {
                lists = HtmlParser.ParseListsWithHtmlAgilityPack(Content);
            }
        }

        var arrays = new List<string[]>();
        foreach (var list in lists) {
            arrays.Add(list.ToArray());
        }
        WriteObject(arrays.ToArray(), false);
    }
}
