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

    /// <summary>Filter out rows that appear to be footnotes or metadata (contain symbols like †, ‡, §, *, etc.).</summary>
    [Parameter]
    public SwitchParameter FilterFootnotes { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (IncludeMetadata.IsPresent) {
            List<HtmlParser.TableParseResult> tables;
            if (ParameterSetName == ParameterSetUrl) {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    string content = HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString()).GetAwaiter().GetResult().DocumentElement.OuterHtml;
                    tables = HtmlParser.ParseTablesWithAngleSharpDetailed(content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent);
                } else {
                    var doc = HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString()).GetAwaiter().GetResult();
                    tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent);
                }
            } else {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    tables = HtmlParser.ParseTablesWithAngleSharpDetailed(Content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent);
                } else {
                    tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent);
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
            List<List<Dictionary<string, string?>>> tables;
            if (ParameterSetName == ParameterSetUrl) {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    tables = HtmlParser.ParseUrlTablesWithAngleSharpAsync(Url.ToString(), Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent).GetAwaiter().GetResult();
                } else {
                    tables = HtmlParser.ParseUrlTablesWithHtmlAgilityPackAsync(Url.ToString(), ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent).GetAwaiter().GetResult();
                }
            } else {
                if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                    tables = HtmlParser.ParseTablesWithAngleSharp(Content, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent);
                } else {
                    tables = HtmlParser.ParseTablesWithHtmlAgilityPack(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders), AllProperties.IsPresent);
                }
            }

            foreach (var table in tables) {
                WriteObject(ConvertRows(table), true);
            }
        }
    }

        private PSObject[] ConvertRows(IEnumerable<Dictionary<string, string?>> rows) {
        var list = new List<PSObject>();
        foreach (var row in rows) {
            // Filter out footnote rows if requested
            if (FilterFootnotes.IsPresent && IsFootnoteRow(row)) {
                continue;
            }

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

    private static bool IsFootnoteRow(Dictionary<string, string?> row) {
        // Check if the first cell (usually "Products and services") contains footnote indicators
        var firstValue = row.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(firstValue)) {
            return false;
        }

        // Look for common footnote symbols and patterns
        return firstValue.Contains("†") ||      // Dagger
               firstValue.Contains("‡") ||      // Double dagger
               firstValue.Contains("§") ||      // Section sign
               firstValue.Contains("*Note") ||  // Note indicators
               firstValue.Contains("View the") || // Azure status footnotes
               firstValue.Contains("To learn more") || // Help text
               firstValue.Contains("regions are available to") || // Regional disclaimers
               (firstValue.StartsWith("*") && firstValue.Length > 10); // Asterisk footnotes
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
