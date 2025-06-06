using System;
using System.Collections;
using System.Collections.Generic;
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

    /// <inheritdoc />
    protected override void ProcessRecord() {
        List<List<Dictionary<string, string>>> tables;
        if (ParameterSetName == ParameterSetUrl) {
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                tables = HtmlParser.ParseUrlTablesWithAngleSharpAsync(Url.ToString(), Cast(ReplaceContent), Cast(ReplaceHeaders)).GetAwaiter().GetResult();
            } else {
                tables = HtmlParser.ParseUrlTablesWithHtmlAgilityPackAsync(Url.ToString(), ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders)).GetAwaiter().GetResult();
            }
        } else {
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase) && !ReverseTable.IsPresent) {
                tables = HtmlParser.ParseTablesWithAngleSharp(Content, Cast(ReplaceContent), Cast(ReplaceHeaders));
            } else {
                tables = HtmlParser.ParseTablesWithHtmlAgilityPack(Content, ReverseTable.IsPresent, Cast(ReplaceContent), Cast(ReplaceHeaders));
            }
        }

        foreach (var table in tables) {
            var list = new List<PSObject>();
            foreach (var row in table) {
                PSObject obj = new();
                foreach (var kv in row) {
                    obj.Properties.Add(new PSNoteProperty(kv.Key, kv.Value));
                }
                list.Add(obj);
            }
            WriteObject(list.ToArray());
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
