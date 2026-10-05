using System.Collections.Generic;

namespace HtmlTinkerX;

public static partial class HtmlParser {
    /// <summary>Parses AngleSharp table data with explicit allocation limits.</summary>
    public static List<List<Dictionary<string, string?>>> ParseTablesWithAngleSharp(string html, IDictionary<string, string>? replaceContent, IDictionary<string, string>? replaceHeaders, bool allProperties, HtmlTableParseLimits? limits) =>
        HtmlParserFromTable.ParseTablesWithAngleSharp(html, replaceContent, replaceHeaders, allProperties, limits);

    /// <summary>Parses HtmlAgilityPack table data with explicit allocation limits.</summary>
    public static List<List<Dictionary<string, string?>>> ParseTablesWithHtmlAgilityPack(string html, bool reverseTable, IDictionary<string, string>? replaceContent, IDictionary<string, string>? replaceHeaders, bool allProperties, HtmlTableParseLimits? limits) =>
        HtmlParserFromTable.ParseTablesWithHtmlAgilityPack(html, reverseTable, replaceContent, replaceHeaders, allProperties, limits);

    /// <summary>Parses detailed AngleSharp table data with explicit allocation limits.</summary>
    public static List<HtmlTableResult> ParseTablesWithAngleSharpDetailed(string html, IDictionary<string, string>? replaceContent, IDictionary<string, string>? replaceHeaders, bool allProperties, bool skipFooter, bool cleanHeaders, string? emptyValuePlaceholder, HtmlCellTextFormat cellTextFormat, bool includeLinkUrls, HtmlTableParseLimits? limits) =>
        HtmlParserFromTable.ParseTablesWithAngleSharpDetailed(html, replaceContent, replaceHeaders, allProperties, skipFooter, cleanHeaders, emptyValuePlaceholder, cellTextFormat, includeLinkUrls, limits);

    /// <summary>Parses detailed HtmlAgilityPack table data with explicit allocation limits.</summary>
    public static List<HtmlTableResult> ParseTablesWithHtmlAgilityPackDetailed(string html, bool reverseTable, IDictionary<string, string>? replaceContent, IDictionary<string, string>? replaceHeaders, bool allProperties, bool skipFooter, bool cleanHeaders, string? emptyValuePlaceholder, HtmlCellTextFormat cellTextFormat, bool includeLinkUrls, HtmlTableParseLimits? limits) =>
        HtmlParserFromTable.ParseTablesWithHtmlAgilityPackDetailed(html, reverseTable, replaceContent, replaceHeaders, allProperties, skipFooter, cleanHeaders, emptyValuePlaceholder, cellTextFormat, includeLinkUrls, limits);
}
