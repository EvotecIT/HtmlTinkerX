using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using System.Threading;

namespace HtmlTinkerX;

/// <summary>
/// Provides specialized functionality for parsing HTML tables.
/// </summary>
public static partial class HtmlParserFromTable {
    private const string DefaultColumnNamePrefix = "Column";
    private const string LinkUrlSuffix = "Url";

    /// <summary>
    /// Extracts table data from HTML markup using AngleSharp with detailed metadata.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells (case-insensitive).</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells (case-insensitive).</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <param name="skipFooter">Whether to skip HTML table footer elements.</param>
    /// <param name="cleanHeaders">Whether to automatically clean special characters from header names.</param>
    /// <param name="emptyValuePlaceholder">Value to use for empty cells.</param>
    /// <param name="cellTextFormat">Controls how cell text is flattened (compact, lines, markdown).</param>
    /// <returns>List of table parse results with metadata.</returns>
    /// <example>
    /// <code>
    /// var tables = HtmlParserFromTable.ParseTablesWithAngleSharpDetailed(html);
    /// </code>
    /// </example>
    public static List<HtmlTableResult> ParseTablesWithAngleSharpDetailed(
        string html,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        bool skipFooter = false,
        bool cleanHeaders = false,
        string? emptyValuePlaceholder = null,
        HtmlCellTextFormat cellTextFormat = HtmlCellTextFormat.Compact) {
        return ParseTablesWithAngleSharpDetailed(html, replaceContent, replaceHeaders, allProperties, skipFooter, cleanHeaders, emptyValuePlaceholder, cellTextFormat, includeLinkUrls: false);
    }

    /// <summary>
    /// Extracts table data from HTML markup using AngleSharp with detailed metadata.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells (case-insensitive).</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells (case-insensitive).</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <param name="skipFooter">Whether to skip HTML table footer elements.</param>
    /// <param name="cleanHeaders">Whether to automatically clean special characters from header names.</param>
    /// <param name="emptyValuePlaceholder">Value to use for empty cells.</param>
    /// <param name="cellTextFormat">Controls how cell text is flattened (compact, lines, markdown).</param>
    /// <param name="includeLinkUrls">Whether to add companion URL columns for linked cells.</param>
    /// <returns>List of table parse results with metadata.</returns>
    public static List<HtmlTableResult> ParseTablesWithAngleSharpDetailed(
        string html,
        IDictionary<string, string>? replaceContent,
        IDictionary<string, string>? replaceHeaders,
        bool allProperties,
        bool skipFooter,
        bool cleanHeaders,
        string? emptyValuePlaceholder,
        HtmlCellTextFormat cellTextFormat,
        bool includeLinkUrls) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        replaceContent = replaceContent != null ? new Dictionary<string, string>(replaceContent, StringComparer.OrdinalIgnoreCase) : null;
        replaceHeaders = replaceHeaders != null ? new Dictionary<string, string>(replaceHeaders, StringComparer.OrdinalIgnoreCase) : null;

        var document = HtmlParser.ParseWithAngleSharp(html);
        var tables = document.QuerySelectorAll("table");
        List<HtmlTableResult> results = new();

        for (int tableIndex = 0; tableIndex < tables.Length; tableIndex++) {
            var table = tables[tableIndex];
            var result = new HtmlTableResult();
            var (metadata, rows, startIndex) = ReadTableMetadata(table, tableIndex, replaceHeaders, skipFooter, cleanHeaders);
            var dataValueLookup = BuildDataValueLookup(table);

            if (rows.Length == 0 || metadata.Headers.Count == 0) {
                continue;
            }

            var linkHeaderNames = includeLinkUrls ? BuildLinkHeaderNames(metadata.Headers) : null;
            var tableRows = ParseTableRows(rows, startIndex, metadata.Headers, replaceContent, allProperties, emptyValuePlaceholder, cellTextFormat, dataValueLookup, linkHeaderNames);
            if (tableRows.Count > 0) {
                AppendUsedLinkHeaders(metadata.Headers, tableRows, linkHeaderNames);
                metadata.ColumnCount = metadata.Headers.Count;
                result.Metadata = metadata;
                result.Data = tableRows;
                results.Add(result);
            }
        }

        return results;
    }

    internal static (HtmlTableMetadata Metadata, IElement[] Rows, int StartIndex) ReadTableMetadata(
        IElement table,
        int tableIndex,
        IDictionary<string, string>? replaceHeaders,
        bool skipFooter,
        bool cleanHeaders) {
        var metadata = new HtmlTableMetadata {
            TableIndex = tableIndex,
            Id = table.Id,
            Classes = table.ClassName,
            Caption = HtmlEntity.DeEntitize(GetDirectCaptionText(table)).Trim()
        };

        foreach (var attr in table.Attributes) {
            metadata.Attributes[attr.Name] = attr.Value ?? string.Empty;
        }

        var style = table.GetAttribute("style") ?? string.Empty;
        var containsDisplayNone = style.IndexOf("display:none", StringComparison.OrdinalIgnoreCase) >= 0;
        var containsDisplaySpaceNone = style.IndexOf("display: none", StringComparison.OrdinalIgnoreCase) >= 0;
        metadata.IsVisible = !(containsDisplayNone || containsDisplaySpaceNone);

        IElement[] rows = table.QuerySelectorAll("tr").ToArray();
        if (skipFooter) {
            rows = rows.Where(r => r.Closest("tfoot") is null).ToArray();
        }
        metadata.RowCount = rows.Length;

        if (rows.Length == 0) {
            return (metadata, rows, 0);
        }

        int headerRowIndex = -1;
        bool hasHeader = false;
        IElement? headerRow = SelectBestHeaderRow(table, rows);

        if (headerRow != null) {
            headerRowIndex = Array.IndexOf(rows, headerRow);
            hasHeader = headerRow.QuerySelectorAll("th").Length > 0;
        }

        if (!hasHeader) {
            for (int i = 0; i < rows.Length; i++) {
                if (rows[i].QuerySelectorAll("th").Length > 0) {
                    headerRowIndex = i;
                    headerRow = rows[i];
                    hasHeader = true;
                    break;
                }
            }
        }

        if (headerRow == null) {
            // No <thead> and no <th> detected. Use first non-empty row to determine column count and emit default headers.
            for (int i = 0; i < rows.Length; i++) {
                if (rows[i].QuerySelectorAll("th,td").Length > 0) {
                    headerRowIndex = i;
                    headerRow = rows[i];
                    break;
                }
            }
            if (headerRow == null) {
                return (metadata, rows, 0);
            }
        }
        var headerCells = headerRow.QuerySelectorAll("th,td");
        List<string> headers = new();
        if (hasHeader) {
            foreach (var cell in headerCells) {
                if (cell == null) {
                    continue;
                }
                string header = GetHeaderText(cell);
                if (replaceHeaders != null) {
                    foreach (var kv in replaceHeaders) {
                        header = ReplaceCaseInsensitive(header, kv.Key, kv.Value);
                    }
                }
                if (cleanHeaders) {
                    header = HtmlParser.CleanHeaderName(header);
                }
                int colspan = 1;
                if (int.TryParse(cell.GetAttribute("colspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs)) {
                    colspan = cs;
                }
                for (int c = 0; c < colspan; c++) {
                    headers.Add(header);
                }
            }
        } else {
            int columnCount = SumColSpans(headerCells);
            for (int i = 0; i < columnCount; i++) {
                headers.Add($"Column{i + 1}");
            }
        }

        HtmlParser.EnsureUniqueNames(headers);
        metadata.Headers = headers;
        metadata.ColumnCount = headers.Count;

        int startIndex = hasHeader ? headerRowIndex + 1 : 0;
        return (metadata, rows, startIndex);
    }

    internal static List<Dictionary<string, string?>> ParseTableRows(
        IElement[] rows,
        int startIndex,
        List<string> headers,
        IDictionary<string, string>? replaceContent,
        bool allProperties,
        string? emptyValuePlaceholder,
        HtmlCellTextFormat cellTextFormat,
        IDictionary<string, string>? dataValueLookup = null,
        IReadOnlyDictionary<int, string>? linkHeaderNames = null) {
        List<Dictionary<string, string?>> tableRows = new();
        Dictionary<int, (string? Value, int Remaining)> rowSpans = new();
        Dictionary<int, (string? Value, int Remaining)> rowSpanLinks = new();
        int categoryIndex = headers.FindIndex(h => h.Equals("Category", StringComparison.OrdinalIgnoreCase));
        int severityIndex = headers.FindIndex(h => h.Equals("Severity", StringComparison.OrdinalIgnoreCase));
        foreach (var row in OrderDataRows(rows, startIndex)) {
            if (row == null) {
                continue;
            }
            var cells = row.QuerySelectorAll("th,td");
            string?[] rowValues = new string?[headers.Count];
            Dictionary<string, string?> linkValues = new(StringComparer.OrdinalIgnoreCase);
            int col = 0;
            int cellIndex = 0;
            while (col < headers.Count) {
                if (rowSpans.TryGetValue(col, out var span)) {
                    rowValues[col] = span.Value;
                    if (--span.Remaining == 0) {
                        rowSpans.Remove(col);
                    } else {
                        rowSpans[col] = span;
                    }
                    if (linkHeaderNames != null && rowSpanLinks.TryGetValue(col, out var linkSpan)) {
                        if (!string.IsNullOrWhiteSpace(linkSpan.Value)) {
                            linkValues[linkHeaderNames[col]] = linkSpan.Value;
                        }
                        if (--linkSpan.Remaining == 0) {
                            rowSpanLinks.Remove(col);
                        } else {
                            rowSpanLinks[col] = linkSpan;
                        }
                    }
                    col++;
                    continue;
                }

                if (cellIndex < cells.Length) {
                    var cell = cells[cellIndex++];
                    string? value = FormatCellText(cell, cellTextFormat);
                    string? linkUrl = linkHeaderNames != null ? ExtractLinkUrls(cell) : null;
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = ReplaceCaseInsensitive(value, kv.Key, kv.Value);
                        }
                    }
                    if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(emptyValuePlaceholder)) {
                        value = emptyValuePlaceholder ?? string.Empty;
                    }
                    int colspan = 1;
                    int rowspan = 1;
                    if (int.TryParse(cell.GetAttribute("colspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs)) {
                        colspan = cs;
                    }
                    if (int.TryParse(cell.GetAttribute("rowspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rs)) {
                        rowspan = rs;
                    }
                    for (int c = 0; c < colspan && col < headers.Count; c++, col++) {
                        rowValues[col] = FillFromDataAttributes(headers[col], value, row, dataValueLookup);
                        if (linkHeaderNames != null && !string.IsNullOrWhiteSpace(linkUrl)) {
                            linkValues[linkHeaderNames[col]] = linkUrl;
                        }
                        if (rowspan > 1) {
                            rowSpans[col] = (rowValues[col], rowspan - 1);
                            if (linkHeaderNames != null) {
                                rowSpanLinks[col] = (linkUrl, rowspan - 1);
                            }
                        }
                    }
                } else {
                    if (allProperties) {
                        rowValues[col] = string.IsNullOrEmpty(emptyValuePlaceholder) ? null : emptyValuePlaceholder;
                    }
                    col++;
                }
            }

            Dictionary<string, string?> dict = new();
            for (int i = 0; i < headers.Count; i++) {
                string header = headers[i];
                dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = rowValues[i];
            }
            foreach (var linkValue in linkValues) {
                dict[linkValue.Key] = linkValue.Value;
            }
            // Override Category/Severity from row-level data attributes when present.
            if (categoryIndex >= 0) {
                string key = (row.GetAttribute("data-category") ?? string.Empty).Trim();
                if (key.Length > 0) {
                    string val = key;
                    if (dataValueLookup != null && dataValueLookup.TryGetValue(key, out var label)) {
                        val = label;
                    }
                    dict["Category"] = val;
                }
            }
            if (severityIndex >= 0) {
                string key = (row.GetAttribute("data-severity") ?? string.Empty).Trim();
                if (key.Length > 0) {
                    string val = key;
                    if (dataValueLookup != null && dataValueLookup.TryGetValue(key, out var label)) {
                        val = label;
                    }
                    dict["Severity"] = val;
                }
            }
            // Second chance: fill Category/Severity from data-* even if a non-empty placeholder was present.
            if (dict.TryGetValue("Category", out var catVal) && string.IsNullOrWhiteSpace(catVal)) {
                dict["Category"] = FillFromDataAttributes("Category", catVal, row, dataValueLookup);
            }
            if (dict.TryGetValue("Severity", out var sevVal) && string.IsNullOrWhiteSpace(sevVal)) {
                dict["Severity"] = FillFromDataAttributes("Severity", sevVal, row, dataValueLookup);
            }
            if (dict.Count > 0) {
                tableRows.Add(dict);
            }
        }
        return tableRows;
    }

    /// <summary>
    /// Extracts table data from HTML markup using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells (case-insensitive).</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells (case-insensitive).</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static List<List<Dictionary<string, string?>>> ParseTablesWithAngleSharp(
        string html,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        replaceContent = replaceContent != null ? new Dictionary<string, string>(replaceContent, StringComparer.OrdinalIgnoreCase) : null;
        replaceHeaders = replaceHeaders != null ? new Dictionary<string, string>(replaceHeaders, StringComparer.OrdinalIgnoreCase) : null;

        var document = HtmlParser.ParseWithAngleSharp(html);
        var tables = document.QuerySelectorAll("table");
        List<List<Dictionary<string, string?>>> result = new();

        foreach (var table in tables) {
            var rows = table.QuerySelectorAll("tr");
            if (rows.Length == 0) {
                continue;
            }
            var dataValueLookup = BuildDataValueLookup(table);

            int headerRowIndex = 0;
            bool hasHeader = false;
            for (int i = 0; i < rows.Length; i++) {
                if (rows[i].QuerySelectorAll("th").Length > 0) {
                    headerRowIndex = i;
                    hasHeader = true;
                    break;
                }
            }
            var headerRow = rows[headerRowIndex];
            var headerCells = headerRow.QuerySelectorAll("th,td");
            List<string> headers = new();
            if (hasHeader) {
                foreach (var cell in headerCells) {
                    if (cell == null) {
                        continue;
                    }
                    string header = cell.TextContent.Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = ReplaceCaseInsensitive(header, kv.Key, kv.Value);
                        }
                    }
                    int colspan = 1;
                    if (int.TryParse(cell.GetAttribute("colspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs)) {
                        colspan = cs;
                    }
                    for (int c = 0; c < colspan; c++) {
                        headers.Add(header);
                    }
                }
            } else {
                int columnCount = SumColSpans(headerCells);
                for (int i = 0; i < columnCount; i++) {
                    headers.Add($"Column{i + 1}");
                }
            }

            int startIndex = hasHeader ? headerRowIndex + 1 : 0;
            List<Dictionary<string, string?>> tableRows = new();
            Dictionary<int, (string? Value, int Remaining)> rowSpans = new();
            int categoryIndex = headers.FindIndex(h => h.Equals("Category", StringComparison.OrdinalIgnoreCase));
            int severityIndex = headers.FindIndex(h => h.Equals("Severity", StringComparison.OrdinalIgnoreCase));
            foreach (var row in rows.Skip(startIndex)) {
                if (row == null) {
                    continue;
                }
                var cells = row.QuerySelectorAll("th,td");
                string?[] rowValues = new string?[headers.Count];
                int col = 0;
                int cellIndex = 0;
                while (col < headers.Count) {
                    if (rowSpans.TryGetValue(col, out var span)) {
                        rowValues[col] = span.Value;
                        if (--span.Remaining == 0) {
                            rowSpans.Remove(col);
                        } else {
                            rowSpans[col] = span;
                        }
                        col++;
                        continue;
                    }

                    if (cellIndex < cells.Length) {
                        var cell = cells[cellIndex++];
                        string value = FormatCellText(cell, HtmlCellTextFormat.Compact);
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = ReplaceCaseInsensitive(value, kv.Key, kv.Value);
                            }
                        }
                        int colspan = 1;
                        int rowspan = 1;
                        if (int.TryParse(cell.GetAttribute("colspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs2)) {
                            colspan = cs2;
                        }
                        if (int.TryParse(cell.GetAttribute("rowspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rs2)) {
                            rowspan = rs2;
                        }
                        for (int c = 0; c < colspan && col < headers.Count; c++, col++) {
                            rowValues[col] = value;
                            if (rowspan > 1) {
                                rowSpans[col] = (value, rowspan - 1);
                            }
                        }
                    } else {
                        if (allProperties) {
                            rowValues[col] = null;
                        }
                        col++;
                    }
                }

                Dictionary<string, string?> dict = new();
                for (int i = 0; i < headers.Count; i++) {
                    string header = headers[i];
                    dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = rowValues[i];
                }
                if (categoryIndex >= 0) {
                    string key = (row.GetAttribute("data-category") ?? string.Empty).Trim();
                    if (key.Length > 0) {
                        string val = key;
                        if (dataValueLookup.TryGetValue(key, out var label)) {
                            val = label;
                        }
                        dict["Category"] = val;
                    }
                }
                if (severityIndex >= 0) {
                    string key = (row.GetAttribute("data-severity") ?? string.Empty).Trim();
                    if (key.Length > 0) {
                        string val = key;
                        if (dataValueLookup.TryGetValue(key, out var label)) {
                            val = label;
                        }
                        dict["Severity"] = val;
                    }
                }
                if (dict.TryGetValue("Category", out var catVal) && string.IsNullOrWhiteSpace(catVal)) {
                    dict["Category"] = FillFromDataAttributes("Category", catVal, row, dataValueLookup);
                }
                if (dict.TryGetValue("Severity", out var sevVal) && string.IsNullOrWhiteSpace(sevVal)) {
                    dict["Severity"] = FillFromDataAttributes("Severity", sevVal, row, dataValueLookup);
                }
                if (dict.Count > 0) {
                    tableRows.Add(dict);
                }
            }

            if (tableRows.Count > 0) {
                result.Add(tableRows);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts table data from a web page using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells (case-insensitive).</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells (case-insensitive).</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <param name="client">Optional HTTP client used for downloading the page.</param>
    /// <param name="clientFactory">Factory used to create a temporary <see cref="HttpClient"/> when one is not supplied.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static async Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithAngleSharpAsync(
        string url,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        HttpClient? client = null,
        Func<HttpClient>? clientFactory = null,
        HtmlHttpFetchOptions? fetchOptions = null,
        CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        replaceContent = replaceContent != null ? new Dictionary<string, string>(replaceContent, StringComparer.OrdinalIgnoreCase) : null;
        replaceHeaders = replaceHeaders != null ? new Dictionary<string, string>(replaceHeaders, StringComparer.OrdinalIgnoreCase) : null;

        bool disposeClient = false;
        HttpClient http;
        if (client != null) {
            http = client;
        } else if (clientFactory != null) {
            http = clientFactory();
            disposeClient = true;
        } else {
            http = HtmlHttpClientFactory.Shared;
        }

        try {
            string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url, fetchOptions, cancellationToken).ConfigureAwait(false);
            return ParseTablesWithAngleSharp(content, replaceContent, replaceHeaders, allProperties);
        } finally {
            if (disposeClient) {
                http.Dispose();
            }
        }
    }
}
