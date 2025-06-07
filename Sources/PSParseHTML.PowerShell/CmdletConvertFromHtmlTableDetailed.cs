using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that converts HTML tables into PowerShell objects with detailed metadata.
/// </summary>
/// <example>
/// <code>ConvertFrom-HtmlTableDetailed -Url https://example.com</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlTableDetailed", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject[]))]
public sealed class CmdletConvertFromHtmlTableDetailed : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content containing tables.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with tables.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Replacements to apply to table cell contents.</summary>
    [Parameter]
    public IDictionary? ReplaceContent { get; set; }

    /// <summary>Replacements to apply to table headers.</summary>
    [Parameter]
    public IDictionary? ReplaceHeaders { get; set; }

    /// <summary>Selects parsing engine.</summary>
    [Parameter]
    [ValidateSet("AngleSharp", "AgilityPack")]
    public string Engine { get; set; } = "AgilityPack";

    /// <summary>Interpret table rows as key/value pairs.</summary>
    [Parameter]
    public SwitchParameter ReverseTable { get; set; }

    /// <summary>Include table metadata information.</summary>
    [Parameter]
    public SwitchParameter IncludeMetadata { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        List<HtmlParser.TableParseResult> tables;

        if (ParameterSetName == ParameterSetUrl) {
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                // For URL parsing, we need to get the content first
                string content = HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString()).GetAwaiter().GetResult().TextContent;
                tables = HtmlParser.ParseTablesWithAngleSharpDetailed(content, Cast(ReplaceContent), Cast(ReplaceHeaders));
            } else {
                var doc = HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString()).GetAwaiter().GetResult();
                tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders));
            }
        } else {
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                tables = HtmlParser.ParseTablesWithAngleSharpDetailed(Content, Cast(ReplaceContent), Cast(ReplaceHeaders));
            } else {
                tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders));
            }
        }

        foreach (var tableResult in tables) {
            PSObject tableObject = new();

            if (IncludeMetadata.IsPresent) {
                // Add metadata properties
                tableObject.Properties.Add(new PSNoteProperty("TableIndex", tableResult.Metadata.TableIndex));
                tableObject.Properties.Add(new PSNoteProperty("TableId", tableResult.Metadata.Id ?? string.Empty));
                tableObject.Properties.Add(new PSNoteProperty("TableClasses", tableResult.Metadata.Classes ?? string.Empty));
                tableObject.Properties.Add(new PSNoteProperty("TableAttributes", tableResult.Metadata.Attributes));
                tableObject.Properties.Add(new PSNoteProperty("RowCount", tableResult.Metadata.RowCount));
                tableObject.Properties.Add(new PSNoteProperty("ColumnCount", tableResult.Metadata.ColumnCount));
                tableObject.Properties.Add(new PSNoteProperty("Headers", tableResult.Metadata.Headers.ToArray()));
                tableObject.Properties.Add(new PSNoteProperty("IsVisible", tableResult.Metadata.IsVisible));
            }

            var dataList = new List<PSObject>();
            foreach (var row in tableResult.Data) {
                PSObject rowObject = new();
                foreach (var kv in row) {
                    rowObject.Properties.Add(new PSNoteProperty(kv.Key, kv.Value));
                }
                dataList.Add(rowObject);
            }

            tableObject.Properties.Add(new PSNoteProperty("Data", dataList.ToArray()));
            WriteObject(tableObject);
        }
    }

    private static IDictionary<string, string>? Cast(IDictionary? data) {
        if (data == null) {
            return null;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in data) {
            dict[entry.Key.ToString() ?? string.Empty] = entry.Value?.ToString() ?? string.Empty;
        }
        return dict;
    }
}