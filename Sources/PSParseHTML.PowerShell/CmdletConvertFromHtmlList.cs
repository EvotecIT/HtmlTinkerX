using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Converts HTML lists into PowerShell objects by default.
/// </summary>
/// <example>
/// <code>ConvertFrom-HtmlList -Content $html</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlList", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject[]))]
public sealed class CmdletConvertFromHtmlList : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content containing lists.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with lists.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri? Url { get; set; }

    /// <summary>Selects parsing engine.</summary>
    [Parameter]
    public HtmlParserEngine Engine { get; set; } = HtmlParserEngine.AgilityPack;

    /// <summary>Include list metadata information.</summary>
    [Parameter]
    public SwitchParameter IncludeMetadata { get; set; }

    /// <summary>Return list items as strings.</summary>
    [Parameter]
    public SwitchParameter AsString { get; set; }

    /// <summary>Placeholder inserted between text segments when joining item text.</summary>
    [Parameter]
    public string TagPlaceholder { get; set; } = " ";

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
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        List<HtmlListResult> results;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            string url = (Url ?? throw new PSArgumentNullException(nameof(Url))).ToString();
            if (Engine == HtmlParserEngine.AngleSharp) {
                results = await HtmlParser.ParseUrlListsWithAngleSharpDetailedAsync(url, TagPlaceholder, client).ConfigureAwait(false);
            } else {
                results = await HtmlParser.ParseUrlListsWithHtmlAgilityPackDetailedAsync(url, TagPlaceholder, client).ConfigureAwait(false);
            }
        } else {
            if (Engine == HtmlParserEngine.AngleSharp) {
                results = HtmlParser.ParseListsWithAngleSharpDetailed(Content, TagPlaceholder);
            } else {
                results = HtmlParser.ParseListsWithHtmlAgilityPackDetailed(Content, TagPlaceholder);
            }
        }

        bool returnObjects = !AsString.IsPresent;

        if (IncludeMetadata.IsPresent) {
            var listObjects = new List<PSObject>();
            foreach (var result in results) {
                listObjects.Add(CreateListObject(result, returnObjects));
            }

            if (listObjects.Count == 1) {
                WriteObject(listObjects[0], false);
            } else {
                if (listObjects.Count > 1) {
                    WriteWarning($"{listObjects.Count} lists found. Returning array of lists.");
                }
                WriteObject(listObjects.ToArray(), false);
            }
        } else {
            var output = new List<object>();
            foreach (var result in results) {
                if (returnObjects) {
                    output.Add(ConvertItems(result.Items));
                } else {
                    output.Add(result.Items.Select(i => string.Join(TagPlaceholder, i)).ToArray());
                }
            }
            if (output.Count == 1) {
                WriteObject(output[0], false);
            } else {
                if (output.Count > 1) {
                    WriteWarning($"{output.Count} lists found. Returning array of lists.");
                }
                WriteObject(output.ToArray(), false);
            }
        }

    }

    private PSObject[] ConvertItems(List<List<string>> items) {
        var list = new List<PSObject>();
        int max = items.Count == 0 ? 0 : items.Max(i => i.Count);
        foreach (var item in items) {
            PSObject obj = new();
            for (int i = 0; i < max; i++) {
                string name = $"Column{i + 1}";
                string? value = i < item.Count ? item[i] : null;
                obj.Properties.Add(new PSNoteProperty(name, value));
            }
            list.Add(obj);
        }
        return list.ToArray();
    }

    private PSObject CreateListObject(HtmlListResult result, bool returnObjects) {
        PSObject listObject = new();
        if (returnObjects) {
            listObject.Properties.Add(new PSNoteProperty("Data", ConvertItems(result.Items)));
        } else {
            listObject.Properties.Add(new PSNoteProperty("Data", result.Items.Select(i => string.Join(TagPlaceholder, i)).ToArray()));
        }

        listObject.Properties.Add(new PSNoteProperty("ListIndex", result.Metadata.ListIndex));
        listObject.Properties.Add(new PSNoteProperty("ListId", result.Metadata.Id ?? string.Empty));
        listObject.Properties.Add(new PSNoteProperty("ListClasses", result.Metadata.Classes ?? string.Empty));
        listObject.Properties.Add(new PSNoteProperty("ListAttributes", result.Metadata.Attributes));
        listObject.Properties.Add(new PSNoteProperty("ItemCount", result.Metadata.ItemCount));
        listObject.Properties.Add(new PSNoteProperty("IsOrdered", result.Metadata.IsOrdered));
        listObject.Properties.Add(new PSNoteProperty("IsVisible", result.Metadata.IsVisible));
        return listObject;
    }
}