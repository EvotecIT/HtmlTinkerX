using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

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
[OutputType(typeof(PSObject[]), typeof(DataTable), typeof(DataSet))]
public sealed class CmdletConvertFromHtmlTable : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content containing tables.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file containing tables.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile, ValueFromPipelineByPropertyName = true)]
    public string Path { get; set; } = string.Empty;

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
    public HtmlParserEngine Engine { get; set; } = HtmlParserEngine.AgilityPack;

    /// <summary>Interpret table rows as key/value pairs.</summary>
    [Parameter]
    public SwitchParameter ReverseTable { get; set; }

    /// <summary>Include table metadata information.</summary>
    [Parameter]
    public SwitchParameter IncludeMetadata { get; set; }

    /// <summary>Return each parsed HTML table as a DataTable.</summary>
    [Parameter]
    public SwitchParameter AsDataTable { get; set; }

    /// <summary>Return all parsed HTML tables as a DataSet.</summary>
    [Parameter]
    public SwitchParameter AsDataSet { get; set; }

    /// <summary>Name to use when returning a single DataTable.</summary>
    [Parameter]
    public string? TableName { get; set; }

    /// <summary>Name to use when returning a DataSet.</summary>
    [Parameter]
    public string? DataSetName { get; set; }

    /// <summary>Infer simple .NET column types when returning DataTable/DataSet output.</summary>
    [Parameter]
    public SwitchParameter InferTypes { get; set; }

    /// <summary>Add companion URL columns for linked table cells.</summary>
    [Parameter]
    public SwitchParameter IncludeLinkUrls { get; set; }

    /// <summary>Zero-based table indexes to include.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int[]? TableIndex { get; set; }

    /// <summary>Table id to include.</summary>
    [Parameter]
    public string? TableId { get; set; }

    /// <summary>CSS class token to include.</summary>
    [Parameter]
    public string? TableClass { get; set; }

    /// <summary>Caption text fragment to include.</summary>
    [Parameter]
    public string? Caption { get; set; }

    /// <summary>Header name to include.</summary>
    [Parameter]
    public string? Header { get; set; }

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

    /// <summary>Controls how cell text is extracted (Compact, Lines, Markdown).</summary>
    [Parameter]
    [ValidateSet("Compact", "Lines", "Markdown")]
    public string CellTextFormat { get; set; } = nameof(HtmlCellTextFormat.Compact);

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
        if (AsDataTable.IsPresent && AsDataSet.IsPresent) {
            throw new PSArgumentException("Specify either -AsDataTable or -AsDataSet, but not both.");
        }

        if (IncludeMetadata.IsPresent && (AsDataTable.IsPresent || AsDataSet.IsPresent)) {
            throw new PSArgumentException("-IncludeMetadata cannot be combined with -AsDataTable or -AsDataSet.");
        }

        var detailedTables = SelectTables(await GetTablesDetailedAsync().ConfigureAwait(false));
        if (detailedTables.Count == 0 && HasTableSelection() && !AsDataSet.IsPresent) {
            return;
        }

        if (AsDataSet.IsPresent) {
            WriteObject(detailedTables.ToDataSet(DataSetName, InferTypes.IsPresent), false);
            return;
        }

        if (AsDataTable.IsPresent) {
            if (detailedTables.Count > 1) {
                WriteWarning($"{detailedTables.Count} tables found. Returning one DataTable per parsed table.");
            }

            bool useRequestedName = detailedTables.Count == 1 && !string.IsNullOrWhiteSpace(TableName);
            foreach (var tableResult in detailedTables) {
                WriteObject(tableResult.ToDataTable(useRequestedName ? TableName : null, InferTypes.IsPresent), false);
            }
            return;
        }

        if (IncludeMetadata.IsPresent) {
            foreach (var tableResult in detailedTables) {
                WriteObject(CreateTableObject(tableResult), false);
            }
            return;
        }

        var tableArrays = new List<PSObject[]>();
        foreach (var tableResult in detailedTables) {
            tableArrays.Add(ConvertRows(tableResult.Data));
        }

        if (tableArrays.Count == 1) {
            WriteObject(tableArrays[0], false);
            return;
        }

        if (tableArrays.Count > 1) {
            WriteWarning($"{tableArrays.Count} tables found. Returning array of tables.");
        }
        WriteObject(tableArrays.ToArray(), false);
    }

    private async Task<List<HtmlTableResult>> GetTablesDetailedAsync() {
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            if (Engine == HtmlParserEngine.AngleSharp && !ReverseTable.IsPresent) {
                string content = (await HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString(), client).ConfigureAwait(false)).DocumentElement.OuterHtml;
                return HtmlParser.ParseTablesWithAngleSharpDetailed(content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder, GetCellTextFormat(), IncludeLinkUrls.IsPresent);
            }

            var doc = await HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString(), client).ConfigureAwait(false);
            return HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder, GetCellTextFormat(), IncludeLinkUrls.IsPresent);
        }

        if (ParameterSetName == ParameterSetFile) {
            if (Engine == HtmlParserEngine.AngleSharp && !ReverseTable.IsPresent) {
                return HtmlParser.ParseFileTablesWithAngleSharpDetailed(Path.ToFullPath(), Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder, GetCellTextFormat(), IncludeLinkUrls.IsPresent);
            }

            return HtmlParser.ParseFileTablesWithHtmlAgilityPackDetailed(Path.ToFullPath(), ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder, GetCellTextFormat(), IncludeLinkUrls.IsPresent);
        }

        if (Engine == HtmlParserEngine.AngleSharp && !ReverseTable.IsPresent) {
            return HtmlParser.ParseTablesWithAngleSharpDetailed(Content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder, GetCellTextFormat(), IncludeLinkUrls.IsPresent);
        }

        return HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent, CleanHeaders.IsPresent, EmptyValuePlaceholder, GetCellTextFormat(), IncludeLinkUrls.IsPresent);
    }

    private bool HasTableSelection() =>
        TableIndex != null ||
        !string.IsNullOrWhiteSpace(TableId) ||
        !string.IsNullOrWhiteSpace(TableClass) ||
        !string.IsNullOrWhiteSpace(Caption) ||
        !string.IsNullOrWhiteSpace(Header);

    private HtmlCellTextFormat GetCellTextFormat() =>
        Enum.TryParse<HtmlCellTextFormat>(CellTextFormat, true, out var fmt) ? fmt : HtmlCellTextFormat.Compact;

    private static PSObject[] ConvertRows(IEnumerable<Dictionary<string, string?>> rows) {
        var list = new List<PSObject>();
        foreach (var row in rows) {
            PSObject obj = new();
            foreach (var kv in row) {
                obj.Properties.Add(new PSNoteProperty(kv.Key, kv.Value));
            }
            list.Add(obj);
        }
        return list.ToArray();
    }

    private PSObject CreateTableObject(HtmlTableResult tableResult) {
        PSObject tableObject = new();
        tableObject.Properties.Add(new PSNoteProperty("Data", ConvertRows(tableResult.Data)));

        tableObject.Properties.Add(new PSNoteProperty("TableIndex", tableResult.Metadata.TableIndex));
        tableObject.Properties.Add(new PSNoteProperty("TableId", tableResult.Metadata.Id ?? string.Empty));
        tableObject.Properties.Add(new PSNoteProperty("TableClasses", tableResult.Metadata.Classes ?? string.Empty));
        tableObject.Properties.Add(new PSNoteProperty("Caption", tableResult.Metadata.Caption ?? string.Empty));
        tableObject.Properties.Add(new PSNoteProperty("TableAttributes", tableResult.Metadata.Attributes));
        tableObject.Properties.Add(new PSNoteProperty("RowCount", tableResult.Metadata.RowCount));
        tableObject.Properties.Add(new PSNoteProperty("ColumnCount", tableResult.Metadata.ColumnCount));
        tableObject.Properties.Add(new PSNoteProperty("Headers", tableResult.Metadata.Headers.ToArray()));
        tableObject.Properties.Add(new PSNoteProperty("IsVisible", tableResult.Metadata.IsVisible));

        return tableObject;
    }

    private List<HtmlTableResult> SelectTables(List<HtmlTableResult> tables) {
        if ((TableIndex == null || TableIndex.Length == 0) &&
            string.IsNullOrWhiteSpace(TableId) &&
            string.IsNullOrWhiteSpace(TableClass) &&
            string.IsNullOrWhiteSpace(Caption) &&
            string.IsNullOrWhiteSpace(Header)) {
            return tables;
        }

        var options = new HtmlTableSelectionOptions {
            Id = TableId,
            ClassName = TableClass,
            CaptionContains = Caption,
            Header = Header
        };

        if (TableIndex != null) {
            foreach (int index in TableIndex) {
                options.TableIndexes.Add(index);
            }
        }

        return tables.SelectTables(options);
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
