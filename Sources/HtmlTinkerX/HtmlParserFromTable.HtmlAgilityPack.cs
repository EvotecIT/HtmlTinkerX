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
        HtmlCellTextFormat cellTextFormat = HtmlCellTextFormat.Compact) {
        return ParseTablesWithHtmlAgilityPackDetailed(html, reverseTable, replaceContent, replaceHeaders, allProperties, skipFooter, cleanHeaders, emptyValuePlaceholder, cellTextFormat, includeLinkUrls: false);
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
        bool reverseTable,
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
                Dictionary<string, string?> linkValues = new(StringComparer.OrdinalIgnoreCase);
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
                    if (includeLinkUrls && cells.Count > 1) {
                        string? linkUrl = ExtractLinkUrls(cells[1]);
                        if (!string.IsNullOrWhiteSpace(linkUrl)) {
                            var used = new HashSet<string>(obj.Keys.Concat(linkValues.Keys), StringComparer.OrdinalIgnoreCase);
                            string linkHeader = CreateUniqueLinkHeaderName(header, index, used);
                            linkValues[linkHeader] = linkUrl;
                        }
                    }
                }

                if (obj.Count > 0) {
                    foreach (var linkValue in linkValues) {
                        obj[linkValue.Key] = linkValue.Value;
                    }

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
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static async Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithHtmlAgilityPackAsync(
        string url,
        bool reverseTable = false,
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
            return ParseTablesWithHtmlAgilityPack(content, reverseTable, replaceContent, replaceHeaders, allProperties);
        } finally {
            if (disposeClient) {
                http.Dispose();
            }
        }
    }

}
