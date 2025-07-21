using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides specialized functionality for parsing HTML tables.
/// </summary>
public static class HtmlParserFromTable {
    /// <summary>
    /// Extracts table data from HTML markup using AngleSharp with detailed metadata.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <param name="skipFooter">Whether to skip HTML table footer elements.</param>
    /// <param name="cleanHeaders">Whether to automatically clean special characters from header names.</param>
    /// <param name="emptyValuePlaceholder">Value to use for empty cells.</param>
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
        string? emptyValuePlaceholder = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        var document = HtmlParser.ParseWithAngleSharp(html);
        var tables = document.QuerySelectorAll("table");
        List<HtmlTableResult> results = new();

        for (int tableIndex = 0; tableIndex < tables.Length; tableIndex++) {
            var table = tables[tableIndex];
            var result = new HtmlTableResult();
            var (metadata, rows, startIndex) = ReadTableMetadata(table, tableIndex, replaceHeaders, skipFooter, cleanHeaders);

            if (rows.Length == 0 || metadata.Headers.Count == 0) {
                continue;
            }

            var tableRows = ParseTableRows(rows, startIndex, metadata.Headers, replaceContent, allProperties, emptyValuePlaceholder);
            if (tableRows.Count > 0) {
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
            Classes = table.ClassName
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
                        header = header.Replace(kv.Key, kv.Value);
                    }
                }
                if (cleanHeaders) {
                    header = HtmlParser.CleanHeaderName(header);
                }
                int colspan = 1;
                if (int.TryParse(cell.GetAttribute("colspan"), out int cs)) {
                    colspan = cs;
                }
                for (int c = 0; c < colspan; c++) {
                    headers.Add(header);
                }
            }
        } else {
            for (int i = 0; i < headerCells.Length; i++) {
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
        string? emptyValuePlaceholder) {
        List<Dictionary<string, string?>> tableRows = new();
        Dictionary<int, (string? Value, int Remaining)> rowSpans = new();
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
                    string? value = cell.TextContent.Trim();
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = value.Replace(kv.Key, kv.Value);
                        }
                    }
                    if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(emptyValuePlaceholder)) {
                        value = emptyValuePlaceholder ?? string.Empty;
                    }
                    int colspan = 1;
                    int rowspan = 1;
                    if (int.TryParse(cell.GetAttribute("colspan"), out int cs)) {
                        colspan = cs;
                    }
                    if (int.TryParse(cell.GetAttribute("rowspan"), out int rs)) {
                        rowspan = rs;
                    }
                    for (int c = 0; c < colspan && col < headers.Count; c++, col++) {
                        rowValues[col] = value;
                        if (rowspan > 1) {
                            rowSpans[col] = (value, rowspan - 1);
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
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
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

        var document = HtmlParser.ParseWithAngleSharp(html);
        var tables = document.QuerySelectorAll("table");
        List<List<Dictionary<string, string?>>> result = new();

        foreach (var table in tables) {
            var rows = table.QuerySelectorAll("tr");
            if (rows.Length == 0) {
                continue;
            }

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
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    int colspan = 1;
                    if (int.TryParse(cell.GetAttribute("colspan"), out int cs)) {
                        colspan = cs;
                    }
                    for (int c = 0; c < colspan; c++) {
                        headers.Add(header);
                    }
                }
            } else {
                for (int i = 0; i < headerCells.Length; i++) {
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
                        string value = cell.TextContent.Trim();
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = value.Replace(kv.Key, kv.Value);
                            }
                        }
                        int colspan = 1;
                        int rowspan = 1;
                        if (int.TryParse(cell.GetAttribute("colspan"), out int cs2)) {
                            colspan = cs2;
                        }
                        if (int.TryParse(cell.GetAttribute("rowspan"), out int rs2)) {
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
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
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
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <param name="allProperties">Whether to pad rows with missing cells.</param>
    /// <param name="skipFooter">Whether to skip HTML table footer elements.</param>
    /// <param name="cleanHeaders">Whether to automatically clean special characters from header names.</param>
    /// <param name="emptyValuePlaceholder">Value to use for empty cells.</param>
    /// <returns>List of table parse results with metadata.</returns>
    public static List<HtmlTableResult> ParseTablesWithHtmlAgilityPackDetailed(
        string html,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        bool skipFooter = false,
        bool cleanHeaders = false,
        string? emptyValuePlaceholder = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

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

            // Extract metadata
            metadata.TableIndex = tableIndex;
            metadata.Id = table.GetAttributeValue("id", string.Empty);
            metadata.Classes = table.GetAttributeValue("class", string.Empty);

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
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    string value = cells.Count > 1 ? HtmlEntity.DeEntitize(cells[1].InnerText ?? string.Empty)!.Trim() : string.Empty;
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = value.Replace(kv.Key, kv.Value);
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
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    if (cleanHeaders) {
                        header = HtmlParser.CleanHeaderName(header);
                    }
                    int colspan = 1;
                    if (int.TryParse(cell.GetAttributeValue("colspan", "1"), out int cs)) {
                        colspan = cs;
                    }
                    for (int c = 0; c < colspan; c++) {
                        headers.Add(header);
                    }
                }
            } else {
                for (int i = 0; i < headerCells.Count; i++) {
                    headers.Add($"Column{i + 1}");
                }
            }
            HtmlParser.EnsureUniqueNames(headers);

            metadata.Headers = headers;
            metadata.ColumnCount = headers.Count;

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
                        string value = HtmlEntity.DeEntitize(cell.InnerText ?? string.Empty)!.Trim();
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = value.Replace(kv.Key, kv.Value);
                            }
                        }
                        if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(emptyValuePlaceholder)) {
                            value = emptyValuePlaceholder ?? string.Empty;
                        }
                        int colspan = 1;
                        int rowspan = 1;
                        if (int.TryParse(cell.GetAttributeValue("colspan", "1"), out int cs2)) {
                            colspan = cs2;
                        }
                        if (int.TryParse(cell.GetAttributeValue("rowspan", "1"), out int rs2)) {
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
                if (dict.Count > 0) {
                    tableRows.Add(dict);
                }
            }

            if (tableRows.Count > 0) {
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
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
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
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    string value = cells.Count > 1 ? HtmlEntity.DeEntitize(cells[1].InnerText ?? string.Empty)!.Trim() : string.Empty;
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = value.Replace(kv.Key, kv.Value);
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
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    int colspan = 1;
                    if (int.TryParse(cell.GetAttributeValue("colspan", "1"), out int cs)) {
                        colspan = cs;
                    }
                    for (int c = 0; c < colspan; c++) {
                        headers.Add(header);
                    }
                }
            } else {
                for (int i = 0; i < headerCells.Count; i++) {
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
                        string value = HtmlEntity.DeEntitize(cell.InnerText ?? string.Empty)!.Trim();
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = value.Replace(kv.Key, kv.Value);
                            }
                        }
                        int colspan = 1;
                        int rowspan = 1;
                        if (int.TryParse(cell.GetAttributeValue("colspan", "1"), out int cs2)) {
                            colspan = cs2;
                        }
                        if (int.TryParse(cell.GetAttributeValue("rowspan", "1"), out int rs2)) {
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
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
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
}