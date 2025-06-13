using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;

namespace PSParseHTML.PowerShell;

/// <summary>Converts HTML tables into PowerShell objects.</summary>
/// <para>
/// The cmdlet accepts raw HTML or downloads a page using <c>-Url</c>. When
/// downloading you can specify <c>-Proxy</c> and <c>-ProxyCredential</c>.
/// </para>
/// <example>
///   <summary>Parse the first table from a URL</summary>
///   <code>ConvertFrom-HtmlTable -Url https://example.com</code>
/// </example>
/// <example>
///   <summary>Download through a proxy server</summary>
///   <code>ConvertFrom-HtmlTable -Url https://example.com -Proxy http://proxy:8080</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlTable", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject[]))]
public sealed class CmdletConvertFromHtmlTable : PSCmdlet {
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

    /// <summary>Pad rows with missing cells.</summary>
    [Parameter]
    public SwitchParameter AllProperties { get; set; }

    /// <summary>Value to use for empty cells to improve PowerShell formatting compatibility.</summary>
    [Parameter]
    public string? EmptyValuePlaceholder { get; set; }

    /// <summary>Automatically clean special characters from header names that can cause PowerShell formatting issues.</summary>
    [Parameter]
    public SwitchParameter CleanHeaders { get; set; }
    /// <summary>Skip HTML table footer (&lt;tfoot&gt;) elements when parsing tables.</summary>
    [Parameter]
    public SwitchParameter SkipFooter { get; set; }

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
        if (IncludeMetadata.IsPresent) {
            List<HtmlTableResult> tables;
            if (ParameterSetName == ParameterSetUrl) {
                using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    string content = HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString(), client).GetAwaiter().GetResult().DocumentElement.OuterHtml;
                    tables = HtmlParser.ParseTablesWithAngleSharpDetailed(content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                } else {
                    var doc = HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString(), client).GetAwaiter().GetResult();
                    tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                }
            } else {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    tables = HtmlParser.ParseTablesWithAngleSharpDetailed(Content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                } else {
                    tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                }
            }

            foreach (var tableResult in tables) {
                WriteObject(CreateTableObject(tableResult));
            }
        } else {
            // Use the detailed parsing methods but extract only the Data part
            List<HtmlTableResult> detailedTables;
            if (ParameterSetName == ParameterSetUrl) {
                using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    string content = HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString(), client).GetAwaiter().GetResult().DocumentElement.OuterHtml;
                    detailedTables = HtmlParser.ParseTablesWithAngleSharpDetailed(content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                } else {
                    var doc = HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString(), client).GetAwaiter().GetResult();
                    detailedTables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                }
            } else {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    detailedTables = HtmlParser.ParseTablesWithAngleSharpDetailed(Content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                } else {
                    detailedTables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder);
                }
            }

            // Return array of arrays - each table as a separate array (extract Data from detailed results)
            var tableArrays = new List<PSObject[]>();
            foreach (var tableResult in detailedTables) {
                tableArrays.Add(ConvertRows(tableResult.Data));
            }

            if (tableArrays.Count == 1) {
                WriteObject(tableArrays[0], false);
            } else {
                if (tableArrays.Count > 1) {
                    WriteWarning($"{tableArrays.Count} tables found. Returning array of tables.");
                }
                WriteObject(tableArrays.ToArray(), false);
            }
        }
    }

    /// <summary>
    /// Convert the rows to PowerShell objects.
    /// </summary>
    /// <param name="rows">The rows to convert.</param>
    /// <returns>The converted rows.</returns>
    private PSObject[] ConvertRows(IEnumerable<Dictionary<string, string?>> rows) {
        var list = new List<PSObject>();
        foreach (var row in rows) {
            PSObject obj = new();
            foreach (var kv in row) {
                // Core library now handles CleanHeaders and EmptyValuePlaceholder processing
                obj.Properties.Add(new PSNoteProperty(kv.Key, kv.Value));
            }
            list.Add(obj);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Creates a PSObject with flattened table metadata and data.
    /// </summary>
    /// <param name="tableResult">The table parse result containing metadata and data.</param>
    /// <returns>A PSObject with flattened structure.</returns>
    private PSObject CreateTableObject(HtmlTableResult tableResult) {
        PSObject tableObject = new();
        tableObject.Properties.Add(new PSNoteProperty("Data", ConvertRows(tableResult.Data)));

        if (IncludeMetadata.IsPresent) {
            tableObject.Properties.Add(new PSNoteProperty("TableIndex", tableResult.Metadata.TableIndex));
            tableObject.Properties.Add(new PSNoteProperty("TableId", tableResult.Metadata.Id ?? string.Empty));
            tableObject.Properties.Add(new PSNoteProperty("TableClasses", tableResult.Metadata.Classes ?? string.Empty));
            tableObject.Properties.Add(new PSNoteProperty("TableAttributes", tableResult.Metadata.Attributes));
            tableObject.Properties.Add(new PSNoteProperty("RowCount", tableResult.Metadata.RowCount));
            tableObject.Properties.Add(new PSNoteProperty("ColumnCount", tableResult.Metadata.ColumnCount));
            tableObject.Properties.Add(new PSNoteProperty("Headers", tableResult.Metadata.Headers.ToArray()));
            tableObject.Properties.Add(new PSNoteProperty("IsVisible", tableResult.Metadata.IsVisible));
        }

        return tableObject;
    }



    /// <summary>
    /// Cast the dictionary to a dictionary of strings.
    /// </summary>
    /// <param name="data">The dictionary to cast.</param>
    /// <returns>The casted dictionary.</returns>
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
