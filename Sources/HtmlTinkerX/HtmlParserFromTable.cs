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

namespace HtmlTinkerX;

/// <summary>
/// Provides specialized functionality for parsing HTML tables.
/// </summary>
public static class HtmlParserFromTable {
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
    /// <param name="includeLinkUrls">Whether to add companion URL columns for linked cells.</param>
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
        HtmlCellTextFormat cellTextFormat = HtmlCellTextFormat.Compact,
        bool includeLinkUrls = false) {
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
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static async Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithAngleSharpAsync(
        string url,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        HttpClient? client = null,
        Func<HttpClient>? clientFactory = null) {
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
            string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
            return ParseTablesWithAngleSharp(content, replaceContent, replaceHeaders, allProperties);
        } finally {
            if (disposeClient) {
                http.Dispose();
            }
        }
    }

    /// <summary>
    /// Extracts table data from HTML markup using HtmlAgilityPack with detailed metadata.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="reverseTable">Whether to treat rows as key/value pairs.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells (case-insensitive).</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells (case-insensitive).</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <param name="skipFooter">Whether to skip HTML table footer elements.</param>
    /// <param name="cleanHeaders">Whether to automatically clean special characters from header names.</param>
    /// <param name="emptyValuePlaceholder">Value to use for empty cells.</param>
    /// <param name="cellTextFormat">Controls how cell text is flattened (compact, lines, markdown).</param>
    /// <param name="includeLinkUrls">Whether to add companion URL columns for linked cells.</param>
    /// <returns>List of table parse results with metadata.</returns>
    public static List<HtmlTableResult> ParseTablesWithHtmlAgilityPackDetailed(
        string html,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        bool skipFooter = false,
        bool cleanHeaders = false,
        string? emptyValuePlaceholder = null,
        HtmlCellTextFormat cellTextFormat = HtmlCellTextFormat.Compact,
        bool includeLinkUrls = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        replaceContent = replaceContent != null ? new Dictionary<string, string>(replaceContent, StringComparer.OrdinalIgnoreCase) : null;
        replaceHeaders = replaceHeaders != null ? new Dictionary<string, string>(replaceHeaders, StringComparer.OrdinalIgnoreCase) : null;

        HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        var tables = doc.DocumentNode.SelectNodes("//table");
        List<HtmlTableResult> results = new();

        if (tables == null) {
            return results;
        }

        for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++) {
            var table = tables[tableIndex];
            var result = new HtmlTableResult();
            var metadata = result.Metadata;
            var dataValueLookup = BuildDataValueLookup(table);

            // Extract metadata
            metadata.TableIndex = tableIndex;
            metadata.Id = table.GetAttributeValue("id", string.Empty);
            metadata.Classes = table.GetAttributeValue("class", string.Empty);
            metadata.Caption = HtmlEntity.DeEntitize(GetDirectCaptionText(table)).Trim();

            // Extract all attributes
            foreach (var attr in table.Attributes) {
                metadata.Attributes[attr.Name] = attr.Value ?? string.Empty;
            }

            // Check visibility (simple check for display:none)
            var style = table.GetAttributeValue("style", string.Empty);
            var containsDisplayNone = style.IndexOf("display:none", StringComparison.OrdinalIgnoreCase) >= 0;
            var containsDisplaySpaceNone = style.IndexOf("display: none", StringComparison.OrdinalIgnoreCase) >= 0;
            metadata.IsVisible = !(containsDisplayNone || containsDisplaySpaceNone);

            var rows = skipFooter ?
                table.SelectNodes(".//tr[not(ancestor::tfoot)]") :
                table.SelectNodes(".//tr");
            metadata.RowCount = rows?.Count ?? 0;

            if (rows == null || rows.Count == 0) {
                continue;
            }

            if (reverseTable) {
                Dictionary<string, string?> obj = new();
                int index = 0;
                foreach (var row in rows) {
                    if (row == null) {
                        continue;
                    }
                    var cells = row.SelectNodes("th|td");
                    if (cells == null || cells.Count == 0) {
                        continue;
                    }
                    string header = HtmlEntity.DeEntitize(cells[0].InnerText ?? string.Empty)!.Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = ReplaceCaseInsensitive(header, kv.Key, kv.Value);
                        }
                    }
                    string value = cells.Count > 1 ? HtmlEntity.DeEntitize(cells[1].InnerText ?? string.Empty)!.Trim() : string.Empty;
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = ReplaceCaseInsensitive(value, kv.Key, kv.Value);
                        }
                    }
                    if (string.IsNullOrEmpty(header)) {
                        header = (++index).ToString();
                    }
                    obj[header] = value;
                }

                if (obj.Count > 0) {
                    result.Data = new List<Dictionary<string, string?>> { obj };
                    metadata.Headers = obj.Keys.ToList();
                    metadata.ColumnCount = obj.Count;
                }
                results.Add(result);
                continue;
            }

            int headerRowIndex = 0;
            bool hasHeader = false;
            HtmlNode? headerRow = SelectBestHeaderRow(rows);
            if (headerRow != null) {
                headerRowIndex = rows.IndexOf(headerRow);
                hasHeader = headerRow.SelectNodes("th")?.Count > 0;
            }

            if (!hasHeader) {
                for (int i = 0; i < rows.Count; i++) {
                    if (rows[i].SelectNodes("th")?.Count > 0) {
                        headerRowIndex = i;
                        headerRow = rows[i];
                        hasHeader = true;
                        break;
                    }
                }
            }
            if (headerRow == null) {
                // No <thead> and no <th> detected. Use first non-empty row to determine column count and emit default headers.
                for (int i = 0; i < rows.Count; i++) {
                    if (rows[i].SelectNodes("th|td")?.Count > 0) {
                        headerRowIndex = i;
                        headerRow = rows[i];
                        break;
                    }
                }
                if (headerRow == null) {
                    continue;
                }
            }
            var headerCells = headerRow.SelectNodes("th|td");
            if (headerCells == null) {
                continue;
            }

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
                    if (int.TryParse(cell.GetAttributeValue("colspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs)) {
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
            List<Dictionary<string, string?>> tableRows = new();
            Dictionary<int, (string? Value, int Remaining)> rowSpans = new();
            Dictionary<int, (string? Value, int Remaining)> rowSpanLinks = new();
            var linkHeaderNames = includeLinkUrls ? BuildLinkHeaderNames(headers) : null;
            int categoryIndex = headers.FindIndex(h => h.Equals("Category", StringComparison.OrdinalIgnoreCase));
            int severityIndex = headers.FindIndex(h => h.Equals("Severity", StringComparison.OrdinalIgnoreCase));
            foreach (var row in OrderDataRows(rows, startIndex)) {
                if (row == null) {
                    continue;
                }
                var cells = row.SelectNodes("th|td");
                if (cells == null) {
                    continue;
                }
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

                    if (cellIndex < cells.Count) {
                        var cell = cells[cellIndex++];
                        string value = FormatCellText(cell, cellTextFormat);
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
                        if (int.TryParse(cell.GetAttributeValue("colspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs2)) {
                            colspan = cs2;
                        }
                        if (int.TryParse(cell.GetAttributeValue("rowspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rs2)) {
                            rowspan = rs2;
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
                if (categoryIndex >= 0) {
                    string key = row.GetAttributeValue("data-category", string.Empty).Trim();
                    if (!string.IsNullOrEmpty(key)) {
                        string val = key;
                        if (dataValueLookup != null && dataValueLookup.TryGetValue(key, out var label)) {
                            val = label;
                        }
                        dict["Category"] = val;
                    }
                }
                if (severityIndex >= 0) {
                    string key = row.GetAttributeValue("data-severity", string.Empty).Trim();
                    if (!string.IsNullOrEmpty(key)) {
                        string val = key;
                        if (dataValueLookup != null && dataValueLookup.TryGetValue(key, out var label)) {
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
                AppendUsedLinkHeaders(metadata.Headers, tableRows, linkHeaderNames);
                metadata.ColumnCount = metadata.Headers.Count;
                result.Data = tableRows;
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts table data from HTML markup using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="reverseTable">Whether to treat rows as key/value pairs.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells (case-insensitive).</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells (case-insensitive).</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static List<List<Dictionary<string, string?>>> ParseTablesWithHtmlAgilityPack(
        string html,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
       IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        replaceContent = replaceContent != null ? new Dictionary<string, string>(replaceContent, StringComparer.OrdinalIgnoreCase) : null;
        replaceHeaders = replaceHeaders != null ? new Dictionary<string, string>(replaceHeaders, StringComparer.OrdinalIgnoreCase) : null;

        HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        var tables = doc.DocumentNode.SelectNodes("//table");
        List<List<Dictionary<string, string?>>> result = new();
        if (tables == null) {
            return result;
        }

        foreach (var table in tables) {
            var rows = table.SelectNodes(".//tr");
            if (rows == null || rows.Count == 0) {
                continue;
            }
            if (reverseTable) {
                Dictionary<string, string?> obj = new();
                int index = 0;
                foreach (var row in rows) {
                    if (row == null) {
                        continue;
                    }
                    var cells = row.SelectNodes("th|td");
                    if (cells == null || cells.Count == 0) {
                        continue;
                    }
                    string header = HtmlEntity.DeEntitize(cells[0].InnerText ?? string.Empty)!.Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = ReplaceCaseInsensitive(header, kv.Key, kv.Value);
                        }
                    }
                    string value = cells.Count > 1 ? HtmlEntity.DeEntitize(cells[1].InnerText ?? string.Empty)!.Trim() : string.Empty;
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = ReplaceCaseInsensitive(value, kv.Key, kv.Value);
                        }
                    }
                    if (string.IsNullOrEmpty(header)) {
                        header = (++index).ToString();
                    }
                    obj[header] = value;
                }

                if (obj.Count > 0) {
                    result.Add(new List<Dictionary<string, string?>> { obj });
                }
                continue;
            }

            var dataValueLookup = BuildDataValueLookup(table);

            int headerRowIndex = 0;
            bool hasHeader = false;
            for (int i = 0; i < rows.Count; i++) {
                if (rows[i].SelectNodes("th")?.Count > 0) {
                    headerRowIndex = i;
                    hasHeader = true;
                    break;
                }
            }
            var headerRow = rows[headerRowIndex];
            var headerCells = headerRow.SelectNodes("th|td");
            if (headerCells == null) {
                continue;
            }
            List<string> headers = new();
            if (hasHeader) {
                foreach (var cell in headerCells) {
                    if (cell == null) {
                        continue;
                    }
                    string header = HtmlEntity.DeEntitize(cell.InnerText ?? string.Empty)!.Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = ReplaceCaseInsensitive(header, kv.Key, kv.Value);
                        }
                    }
                    int colspan = 1;
                    if (int.TryParse(cell.GetAttributeValue("colspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs)) {
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
            foreach (var row in rows.Skip(startIndex)) {
                if (row == null) {
                    continue;
                }
                var cells = row.SelectNodes("th|td");
                if (cells == null) {
                    continue;
                }
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

                    if (cellIndex < cells.Count) {
                        var cell = cells[cellIndex++];
                        string value = FormatCellText(cell, HtmlCellTextFormat.Compact);
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = ReplaceCaseInsensitive(value, kv.Key, kv.Value);
                            }
                        }
                        int colspan = 1;
                        int rowspan = 1;
                        if (int.TryParse(cell.GetAttributeValue("colspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs2)) {
                            colspan = cs2;
                        }
                        if (int.TryParse(cell.GetAttributeValue("rowspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rs2)) {
                            rowspan = rs2;
                        }
                        for (int c = 0; c < colspan && col < headers.Count; c++, col++) {
                            rowValues[col] = FillFromDataAttributes(headers[col], value, row, dataValueLookup);
                            if (rowspan > 1) {
                                rowSpans[col] = (rowValues[col], rowspan - 1);
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
    /// Extracts table data from a web page using HtmlAgilityPack.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="reverseTable">Whether to treat rows as key/value pairs.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells (case-insensitive).</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells (case-insensitive).</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <param name="client">Optional HTTP client used for downloading the page.</param>
    /// <param name="clientFactory">Factory used to create a temporary <see cref="HttpClient"/> when one is not supplied.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static async Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithHtmlAgilityPackAsync(
        string url,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        HttpClient? client = null,
        Func<HttpClient>? clientFactory = null) {
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
            string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
            return ParseTablesWithHtmlAgilityPack(content, reverseTable, replaceContent, replaceHeaders, allProperties);
        } finally {
            if (disposeClient) {
                http.Dispose();
            }
        }
    }

    private static IElement? SelectBestHeaderRow(IElement table, IElement[] allRows) {
        // Look at thead rows first – prefer the one with the largest effective column span, then most cells, then the last occurrence.
        var theadRows = table.QuerySelectorAll("thead tr").ToArray();
        if (theadRows.Length == 0) {
            return null;
        }

        return theadRows
            .Select((r, idx) => new {
                Row = r,
                EffectiveCols = SumColSpans(r.QuerySelectorAll("th,td")),
                CellCount = r.QuerySelectorAll("th,td").Length,
                Index = idx
            })
            .OrderByDescending(x => x.EffectiveCols)
            .ThenByDescending(x => x.CellCount)
            .ThenByDescending(x => x.Index)
            .First().Row;
    }

    private static HtmlNode? SelectBestHeaderRow(HtmlNodeCollection rows) {
        if (rows == null || rows.Count == 0) {
            return null;
        }

        // Prefer thead rows when present; otherwise rows is already scoped.
        var candidates = rows.Where(r => r.ParentNode?.Name.Equals("thead", StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (candidates.Count == 0) {
            return null;
        }

        return candidates
            .Select((r, idx) => new {
                Row = r,
                EffectiveCols = SumColSpans(r.SelectNodes("th|td")),
                CellCount = r.SelectNodes("th|td")?.Count ?? 0,
                Index = idx
            })
            .OrderByDescending(x => x.EffectiveCols)
            .ThenByDescending(x => x.CellCount)
            .ThenByDescending(x => x.Index)
            .First().Row;
    }

    private static IEnumerable<IElement> OrderDataRows(IElement[] rows, int startIndex) =>
        rows.Skip(startIndex)
            .OrderBy(r => r.Closest("tfoot") != null ? 1 : 0); // ensure tfoot rows come last

    private static IEnumerable<HtmlNode> OrderDataRows(HtmlNodeCollection rows, int startIndex) {
        if (rows == null) {
            yield break;
        }
        foreach (var row in rows.Cast<HtmlNode>().Skip(startIndex).OrderBy(r => r.Ancestors("tfoot").Any() ? 1 : 0)) {
            yield return row;
        }
    }

    private static string GetHeaderText(IElement cell) {
        // Prefer explicit dropdown header text if present
        var dropdownHead = cell.QuerySelector(".table-dropdown-head");
        if (dropdownHead != null) {
            return dropdownHead.TextContent.Trim();
        }

        // Otherwise, collect text but skip menus/lists that often live in header filters
        var sb = new StringBuilder();
        AppendHeaderNodeText(cell, sb);
        return CleanupHeader(sb.ToString());
    }

    private static string GetHeaderText(HtmlNode cell) {
        var dropdownHead = cell.SelectSingleNode(".//*[contains(@class,'table-dropdown-head')]");
        if (dropdownHead != null) {
            return HtmlEntity.DeEntitize(dropdownHead.InnerText ?? string.Empty)!.Trim();
        }

        var sb = new StringBuilder();
        AppendHeaderNodeText(cell, sb);
        return CleanupHeader(sb.ToString());
    }

    private static readonly HashSet<string> HeaderSkipTags = new(StringComparer.OrdinalIgnoreCase) { "ul", "ol", "li", "script", "style", "select", "option" };

    private static void AppendHeaderNodeText(INode node, StringBuilder sb) {
        if (node is IText textNode) {
            sb.Append(textNode.Data);
            return;
        }
        if (node is IElement el) {
            if (HeaderSkipTags.Contains(el.TagName)) {
                return;
            }
            foreach (var child in el.ChildNodes) {
                AppendHeaderNodeText(child, sb);
            }
        }
    }

    private static void AppendHeaderNodeText(HtmlNode node, StringBuilder sb) {
        if (node is HtmlTextNode textNode) {
            sb.Append(textNode.Text);
            return;
        }
        if (node.NodeType != HtmlNodeType.Element) {
            foreach (var child in node.ChildNodes) {
                AppendHeaderNodeText(child, sb);
            }
            return;
        }
        if (HeaderSkipTags.Contains(node.Name)) {
            return;
        }
        foreach (var child in node.ChildNodes) {
            AppendHeaderNodeText(child, sb);
        }
    }

    private static string CleanupHeader(string raw) {
        return Regex.Replace(raw, @"\s+", " ").Trim();
    }

    private static IDictionary<string, string> BuildDataValueLookup(IElement table) {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var li in table.QuerySelectorAll("li[data-value]")) {
            string text = li.TextContent.Trim();
            string key = (li.GetAttribute("data-value") ?? string.Empty).Trim();
            if (key.Length > 0 && !dict.ContainsKey(key)) {
                dict[key] = text;
            }
        }
        return dict;
    }

    private static IDictionary<string, string> BuildDataValueLookup(HtmlNode table) {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var items = table.SelectNodes(".//li[@data-value]");
        if (items != null) {
            foreach (var li in items) {
                string key = li.GetAttributeValue("data-value", string.Empty).Trim();
                string text = HtmlEntity.DeEntitize(li.InnerText ?? string.Empty).Trim();
                if (key.Length > 0 && !dict.ContainsKey(key)) {
                    dict[key] = text;
                }
            }
        }
        return dict;
    }

    private static string? FillFromDataAttributes(string header, string? current, IElement row, IDictionary<string, string>? lookup) {
        if (!string.IsNullOrEmpty(current) || lookup == null) {
            return current;
        }
        if (header.Equals("Category", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttribute("data-category") ?? string.Empty;
            key = key.Trim();
            if (key.Length == 0) {
                return current;
            }
            if (lookup.TryGetValue(key, out var label)) {
                return label;
            }
            return key; // fallback to raw code
        }
        if (header.Equals("Severity", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttribute("data-severity") ?? string.Empty;
            key = key.Trim();
            if (key.Length == 0) {
                return current;
            }
            if (lookup.TryGetValue(key, out var label)) {
                return label;
            }
            return key;
        }
        return current;
    }

    private static string? FillFromDataAttributes(string header, string? current, HtmlNode row, IDictionary<string, string>? lookup) {
        if (!string.IsNullOrEmpty(current) || lookup == null) {
            return current;
        }
        if (header.Equals("Category", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttributeValue("data-category", string.Empty).Trim();
            if (!string.IsNullOrEmpty(key)) {
                if (lookup.TryGetValue(key, out var label)) {
                    return label;
                }
                return key;
            }
        }
        if (header.Equals("Severity", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttributeValue("data-severity", string.Empty).Trim();
            if (!string.IsNullOrEmpty(key)) {
                if (lookup.TryGetValue(key, out var label)) {
                    return label;
                }
                return key;
            }
        }
        return current;
    }

    private static int SumColSpans(IEnumerable<IElement> cells) {
        int total = 0;
        foreach (var cell in cells) {
            if (int.TryParse(cell.GetAttribute("colspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs) && cs > 0) {
                total += cs;
            } else {
                total += 1;
            }
        }
        return total;
    }

    private static int SumColSpans(HtmlNodeCollection? cells) {
        if (cells == null) {
            return 0;
        }
        int total = 0;
        foreach (var cell in cells) {
            if (int.TryParse(cell.GetAttributeValue("colspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs) && cs > 0) {
                total += cs;
            } else {
                total += 1;
            }
        }
        return total;
    }

    private static string FormatCellText(IElement cell, HtmlCellTextFormat format) {
        if (format == HtmlCellTextFormat.Compact) {
            return HtmlEntity.DeEntitize(cell.TextContent).Trim();
        }
        var sb = new StringBuilder();
        AppendNodeText(cell, sb);
        var cleaned = CleanupText(sb.ToString(), format);
        return HtmlEntity.DeEntitize(cleaned);
    }

    private static string FormatCellText(HtmlNode cell, HtmlCellTextFormat format) {
        if (format == HtmlCellTextFormat.Compact) {
            return HtmlEntity.DeEntitize(cell.InnerText ?? string.Empty).Trim();
        }
        var sb = new StringBuilder();
        AppendNodeText(cell, sb);
        var cleaned = CleanupText(sb.ToString(), format);
        return HtmlEntity.DeEntitize(cleaned);
    }

    private static Dictionary<int, string> BuildLinkHeaderNames(IReadOnlyList<string> headers) {
        var used = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<int, string>();
        for (int i = 0; i < headers.Count; i++) {
            string header = headers[i];
            string baseName = string.IsNullOrWhiteSpace(header)
                ? DefaultColumnNamePrefix + (i + 1).ToString(CultureInfo.InvariantCulture) + LinkUrlSuffix
                : header + LinkUrlSuffix;
            string candidate = baseName;
            int suffix = 2;
            while (!used.Add(candidate)) {
                candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            result[i] = candidate;
        }

        return result;
    }

    private static void AppendUsedLinkHeaders(
        IList<string> headers,
        IEnumerable<Dictionary<string, string?>> rows,
        IReadOnlyDictionary<int, string>? linkHeaderNames) {
        if (linkHeaderNames == null) {
            return;
        }

        var rowList = rows as IList<Dictionary<string, string?>> ?? rows.ToList();
        foreach (string linkHeader in linkHeaderNames.Values) {
            if (headers.Contains(linkHeader, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }

            if (rowList.Any(row => row.TryGetValue(linkHeader, out string? value) && !string.IsNullOrWhiteSpace(value))) {
                headers.Add(linkHeader);
                foreach (Dictionary<string, string?> row in rowList) {
                    if (!row.ContainsKey(linkHeader)) {
                        row[linkHeader] = null;
                    }
                }
            }
        }
    }

    private static string GetDirectCaptionText(IElement table) {
        return table.Children
            .FirstOrDefault(child => child.TagName.Equals("caption", StringComparison.OrdinalIgnoreCase))
            ?.TextContent ?? string.Empty;
    }

    private static string GetDirectCaptionText(HtmlNode table) {
        return table.ChildNodes
            .FirstOrDefault(child => child.Name.Equals("caption", StringComparison.OrdinalIgnoreCase))
            ?.InnerText ?? string.Empty;
    }

    private static string? ExtractLinkUrls(IElement cell) {
        var links = cell.QuerySelectorAll("a[href]")
            .Select(link => (link.GetAttribute("href") ?? string.Empty).Trim())
            .Where(href => href.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return links.Length == 0 ? null : string.Join("; ", links);
    }

    private static string? ExtractLinkUrls(HtmlNode cell) {
        var links = cell.SelectNodes(".//a[@href]")?
            .Select(link => link.GetAttributeValue("href", string.Empty).Trim())
            .Where(href => href.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return links == null || links.Length == 0 ? null : string.Join("; ", links);
    }

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase) { "p", "div", "section", "article", "header", "footer", "ul", "ol" };
    private static readonly HashSet<string> LineTags = new(StringComparer.OrdinalIgnoreCase) { "br", "tr", "li" };

    private static void AppendNodeText(INode node, StringBuilder sb) {
        switch (node) {
            case IText textNode:
                var text = textNode.Data;
                if (string.IsNullOrWhiteSpace(text)) {
                    return;
                }
                sb.Append(Regex.Replace(text, @"\s+", " "));
                break;
            case IElement element:
                bool addBullet = element.TagName.Equals("li", StringComparison.OrdinalIgnoreCase);
                int startLength = sb.Length;
                int beforeChildren = sb.Length;
                if (addBullet) {
                    AppendNewlineIfNeeded(sb);
                    sb.Append("- ");
                    beforeChildren = sb.Length;
                }
                foreach (var child in element.ChildNodes) {
                    AppendNodeText(child, sb);
                }
                bool wroteChildren = sb.Length > beforeChildren;
                if (addBullet) {
                    string addedText = sb.ToString(beforeChildren, sb.Length - beforeChildren);
                    if (string.IsNullOrWhiteSpace(addedText)) {
                        sb.Length = startLength; // remove stray bullet
                        wroteChildren = false;
                    }
                }
                if (wroteChildren && (BlockTags.Contains(element.TagName) || LineTags.Contains(element.TagName))) {
                    AppendNewlineIfNeeded(sb);
                }
                break;
        }
    }

    private static void AppendNodeText(HtmlNode node, StringBuilder sb) {
        if (node is HtmlTextNode textNode) {
            var text = textNode.Text;
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }
            sb.Append(Regex.Replace(text, @"\s+", " "));
            return;
        }

        if (node.NodeType != HtmlNodeType.Element) {
            foreach (var child in node.ChildNodes) {
                AppendNodeText(child, sb);
            }
            return;
        }

        var tag = node.Name;
        bool addBullet = tag.Equals("li", StringComparison.OrdinalIgnoreCase);
        int startLength = sb.Length;
        int beforeChildren = sb.Length;
        if (addBullet) {
            AppendNewlineIfNeeded(sb);
            sb.Append("- ");
            beforeChildren = sb.Length;
        }

        foreach (var child in node.ChildNodes) {
            AppendNodeText(child, sb);
        }

        bool wroteChildren = sb.Length > beforeChildren;
        if (addBullet) {
            string addedText = sb.ToString(beforeChildren, sb.Length - beforeChildren);
            if (string.IsNullOrWhiteSpace(addedText)) {
                sb.Length = startLength;
                wroteChildren = false;
            }
        }

        if (wroteChildren && (BlockTags.Contains(tag) || LineTags.Contains(tag))) {
            AppendNewlineIfNeeded(sb);
        }
    }

    private static void AppendNewlineIfNeeded(StringBuilder sb) {
        if (sb.Length == 0) {
            return;
        }
        if (sb[sb.Length - 1] != '\n') {
            sb.Append('\n');
        }
    }

    private static string CleanupText(string raw, HtmlCellTextFormat format) {
        if (format == HtmlCellTextFormat.Compact) {
            return raw.Trim();
        }

        // Collapse spaces but keep deliberate newlines.
        string normalized = Regex.Replace(raw, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");

        if (format == HtmlCellTextFormat.Markdown) {
            // Ensure list items are on their own lines; already prefixed with "- ".
            normalized = Regex.Replace(normalized, @"\n- ", "\n- ");
        }

        // Drop empty bullet lines that can appear from list scaffolding.
        var lines = normalized.Split('\n')
            .Where(l => !Regex.IsMatch(l, @"^\s*-\s*$"))
            .ToArray();
        normalized = string.Join("\n", lines);

        return normalized.Trim();
    }

    private static string ReplaceCaseInsensitive(string input, string oldValue, string newValue) =>
        Regex.Replace(input, Regex.Escape(oldValue), _ => newValue,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
