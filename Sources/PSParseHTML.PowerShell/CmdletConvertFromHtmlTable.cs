using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that converts HTML tables into PowerShell objects.
/// </summary>
/// <example>
/// <code>ConvertFrom-HtmlTable -Url https://example.com</code>
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

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (IncludeMetadata.IsPresent) {
            List<HtmlParser.TableParseResult> tables;
            if (ParameterSetName == ParameterSetUrl) {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    string content = HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString()).GetAwaiter().GetResult().DocumentElement.OuterHtml;
                    tables = HtmlParser.ParseTablesWithAngleSharpDetailed(content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                } else {
                    var doc = HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString()).GetAwaiter().GetResult();
                    tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                }
            } else {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    tables = HtmlParser.ParseTablesWithAngleSharpDetailed(Content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                } else {
                    tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                }
            }

            foreach (var tableResult in tables) {
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
                WriteObject(tableObject);
            }
        } else {
            // Use the detailed parsing methods but extract only the Data part
            List<HtmlParser.TableParseResult> detailedTables;
            if (ParameterSetName == ParameterSetUrl) {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    string content = HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString()).GetAwaiter().GetResult().DocumentElement.OuterHtml;
                    detailedTables = HtmlParser.ParseTablesWithAngleSharpDetailed(content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                } else {
                    var doc = HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString()).GetAwaiter().GetResult();
                    detailedTables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                }
            } else {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    detailedTables = HtmlParser.ParseTablesWithAngleSharpDetailed(Content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                } else {
                    detailedTables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent, SkipFooter.IsPresent);
                }
            }

            // Return array of arrays - each table as a separate array (extract Data from detailed results)
            var tableArrays = new List<PSObject[]>();
            foreach (var tableResult in detailedTables) {
                tableArrays.Add(ConvertRows(tableResult.Data));
            }
            WriteObject(tableArrays.ToArray(), false);
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
                var value = kv.Value;
                // If the value is empty and we have a placeholder, use it
                if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(EmptyValuePlaceholder)) {
                    value = EmptyValuePlaceholder;
                }

                var propertyName = CleanHeaders.IsPresent ? CleanHeaderName(kv.Key) : kv.Key;
                obj.Properties.Add(new PSNoteProperty(propertyName, value));
            }
            list.Add(obj);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Clean the header name to remove problematic characters that can cause PowerShell formatting issues.
    /// </summary>
    /// <param name="headerName">The header name to clean.</param>
    /// <returns>The cleaned header name.</returns>
    private static string CleanHeaderName(string headerName) {
        if (string.IsNullOrEmpty(headerName)) {
            return headerName;
        }

        // Remove or replace problematic characters that can cause PowerShell formatting issues
        return headerName
            .Replace("*", "")           // Remove asterisks
            .Replace("‡", "")           // Remove double dagger symbols
            .Replace("†", "")           // Remove dagger symbols
            .Replace("#", "")           // Remove hash symbols
            .Replace("$", "")           // Remove dollar signs
            .Replace("@", "")           // Remove at symbols
            .Replace("!", "")           // Remove exclamation marks
            .Replace("?", "")           // Remove question marks
            .Replace("%", "")           // Remove percent symbols
            .Replace("&", "and")        // Replace ampersand with "and"
            .Replace("(", "")           // Remove opening parenthesis
            .Replace(")", "")           // Remove closing parenthesis
            .Replace("[", "")           // Remove opening bracket
            .Replace("]", "")           // Remove closing bracket
            .Replace("{", "")           // Remove opening brace
            .Replace("}", "")           // Remove closing brace
            .Replace("|", "")           // Remove pipe symbols
            .Replace("\\", "")          // Remove backslashes
            .Replace("/", "")           // Remove forward slashes
            .Replace(":", "")           // Remove colons
            .Replace(";", "")           // Remove semicolons
            .Replace("\"", "")          // Remove quotes
            .Replace("'", "")           // Remove apostrophes
            .Replace("`", "")           // Remove backticks
            .Replace("~", "")           // Remove tildes
            .Replace("^", "")           // Remove carets
            .Replace("<", "")           // Remove less than
            .Replace(">", "")           // Remove greater than
            .Replace("=", "")           // Remove equals
            .Replace("+", "")           // Remove plus
            .Replace("-", "")           // Remove hyphens
            .Trim();                    // Remove leading/trailing whitespace
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
