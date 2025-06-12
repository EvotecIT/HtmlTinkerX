using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Converts HTML lists into PowerShell objects by default.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlList", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject[]))]
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
    protected override void ProcessRecord() {
        List<ListParseResult> results;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase)) {
                results = HtmlParser.ParseUrlListsWithAngleSharpDetailedAsync(Url.ToString(), TagPlaceholder, client).GetAwaiter().GetResult();
            } else {
                results = HtmlParser.ParseUrlListsWithHtmlAgilityPackDetailedAsync(Url.ToString(), TagPlaceholder, client).GetAwaiter().GetResult();
            }
        } else {
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase)) {
                results = HtmlParser.ParseListsWithAngleSharpDetailed(Content, TagPlaceholder);
            } else {
                results = HtmlParser.ParseListsWithHtmlAgilityPackDetailed(Content, TagPlaceholder);
            }
        }

        bool returnObjects = !AsString.IsPresent;

        if (IncludeMetadata.IsPresent) {
            foreach (var result in results) {
                WriteObject(CreateListObject(result, returnObjects));
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
            WriteObject(output.ToArray(), false);
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

    private PSObject CreateListObject(ListParseResult result, bool returnObjects) {
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
