using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using HtmlAgilityPack;

namespace PSParseHTML;

/// <summary>
/// Provides helpers for parsing HTML content using either AngleSharp or HtmlAgilityPack.
/// </summary>
public static class HtmlParser {
    private static readonly HttpClient _client = new();

    /// <summary>
    /// Metadata about a parsed table.
    /// </summary>
    public class TableMetadata {
        public int TableIndex { get; set; }
        public string? Id { get; set; }
        public string? Classes { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new();
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<string> Headers { get; set; } = new();
        public bool IsVisible { get; set; } = true;
    }

    /// <summary>
    /// Result of table parsing with metadata.
    /// </summary>
    public class TableParseResult {
        public TableMetadata Metadata { get; set; } = new();
        public List<Dictionary<string, string?>> Data { get; set; } = new();
    }

    /// <summary>
    /// Parses HTML markup from a string using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content to parse.</param>
    /// <returns>The parsed <see cref="IDocument"/>.</returns>
    public static IDocument ParseWithAngleSharp(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        var parser = new global::AngleSharp.Html.Parser.HtmlParser();
        return parser.ParseDocument(html);
    }

    /// <summary>
    /// Downloads and parses HTML markup from a URL using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <returns>The parsed <see cref="IDocument"/>.</returns>
    public static async Task<IDocument> ParseUrlWithAngleSharpAsync(string url) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseWithAngleSharp(content);
    }

    /// <summary>
    /// Parses HTML markup from a string using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content to parse.</param>
    /// <returns>The parsed <see cref="HtmlDocument"/>.</returns>
    public static HtmlDocument ParseWithHtmlAgilityPack(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        return doc;
    }

    /// <summary>
    /// Downloads and parses HTML markup from a URL using HtmlAgilityPack.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <returns>The parsed <see cref="HtmlDocument"/>.</returns>
    public static async Task<HtmlDocument> ParseUrlWithHtmlAgilityPackAsync(string url) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseWithHtmlAgilityPack(content);
    }

    /// <summary>
    /// Extracts table data from HTML markup using AngleSharp with detailed metadata.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <returns>List of table parse results with metadata.</returns>
    public static List<TableParseResult> ParseTablesWithAngleSharpDetailed(
        string html,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        var document = ParseWithAngleSharp(html);
        var tables = document.QuerySelectorAll("table");
        List<TableParseResult> results = new();

        for (int tableIndex = 0; tableIndex < tables.Length; tableIndex++) {
            var table = tables[tableIndex];
            var result = new TableParseResult();
            var metadata = result.Metadata;

            // Extract metadata
            metadata.TableIndex = tableIndex;
            metadata.Id = table.Id;
            metadata.Classes = table.ClassName;

            // Extract all attributes
            foreach (var attr in table.Attributes) {
                metadata.Attributes[attr.Name] = attr.Value ?? string.Empty;
            }

            // Check visibility (simple check for display:none)
            var style = table.GetAttribute("style") ?? string.Empty;
            metadata.IsVisible = !style.ToLowerInvariant().Contains("display:none") &&
                                !style.ToLowerInvariant().Contains("display: none");

            var rows = table.QuerySelectorAll("tr");
            metadata.RowCount = rows.Length;

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
                    string header = cell.TextContent.Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    headers.Add(header);
                }
            } else {
                for (int i = 0; i < headerCells.Length; i++) {
                    headers.Add($"Column{ i + 1 }");
                }
            }

            metadata.Headers = headers;
            metadata.ColumnCount = headers.Count;

            if (headers.Count == 0) {
                continue;
            }

            int startIndex = hasHeader ? headerRowIndex + 1 : 0;
            List<Dictionary<string, string?>> tableRows = new();
            foreach (var row in rows.Skip(startIndex)) {
                var cells = row.QuerySelectorAll("th,td");
                Dictionary<string, string?> dict = new();
                for (int i = 0; i < headers.Count; i++) {
                    string header = headers[i];
                    if (i < cells.Length) {
                        string value = cells[i].TextContent.Trim();
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = value.Replace(kv.Key, kv.Value);
                            }
                        }
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = value;
                    } else if (allProperties) {
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = null;
                    }
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
    /// Extracts table data from HTML markup using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static List<List<Dictionary<string, string?>>> ParseTablesWithAngleSharp(
        string html,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        var document = ParseWithAngleSharp(html);
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
                    string header = cell.TextContent.Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    headers.Add(header);
                }
            } else {
                for (int i = 0; i < headerCells.Length; i++) {
                    headers.Add($"Column{ i + 1 }");
                }
            }

            if (headers.Count == 0) {
                continue;
            }

            int startIndex = hasHeader ? headerRowIndex + 1 : 0;
            List<Dictionary<string, string?>> tableRows = new();
            foreach (var row in rows.Skip(startIndex)) {
                var cells = row.QuerySelectorAll("th,td");
                Dictionary<string, string?> dict = new();
                for (int i = 0; i < headers.Count; i++) {
                    string header = headers[i];
                    if (i < cells.Length) {
                        string value = cells[i].TextContent.Trim();
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = value.Replace(kv.Key, kv.Value);
                            }
                        }
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = value;
                    } else if (allProperties) {
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = null;
                    }
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
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static async Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithAngleSharpAsync(
        string url,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseTablesWithAngleSharp(content, replaceContent, replaceHeaders, allProperties);
    }

    /// <summary>
    /// Extracts table data from HTML markup using HtmlAgilityPack with detailed metadata.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="reverseTable">Whether to treat rows as key/value pairs.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <returns>List of table parse results with metadata.</returns>
    public static List<TableParseResult> ParseTablesWithHtmlAgilityPackDetailed(
        string html,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlDocument doc = ParseWithHtmlAgilityPack(html);
        var tables = doc.DocumentNode.SelectNodes("//table");
        List<TableParseResult> results = new();

        if (tables == null) {
            return results;
        }

        for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++) {
            var table = tables[tableIndex];
            var result = new TableParseResult();
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
            metadata.IsVisible = !style.ToLowerInvariant().Contains("display:none") &&
                                !style.ToLowerInvariant().Contains("display: none");

            var rows = table.SelectNodes(".//tr");
            metadata.RowCount = rows?.Count ?? 0;

            if (rows == null || rows.Count == 0) {
                continue;
            }

            if (reverseTable) {
                Dictionary<string, string> obj = new();
                int index = 0;
                foreach (var row in rows) {
                    var cells = row.SelectNodes("th|td");
                    if (cells == null || cells.Count == 0) {
                        continue;
                    }
                    string header = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    string value = cells.Count > 1 ? HtmlEntity.DeEntitize(cells[1].InnerText).Trim() : string.Empty;
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
                    string header = HtmlEntity.DeEntitize(cell.InnerText).Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    headers.Add(header);
                }
            } else {
                for (int i = 0; i < headerCells.Count; i++) {
                    headers.Add($"Column{ i + 1 }");
                }
            }

            metadata.Headers = headers;
            metadata.ColumnCount = headers.Count;

            int startIndex = hasHeader ? headerRowIndex + 1 : 0;
            List<Dictionary<string, string?>> tableRows = new();
            foreach (var row in rows.Skip(startIndex)) {
                var cells = row.SelectNodes("th|td");
                if (cells == null) {
                    continue;
                }
                Dictionary<string, string?> dict = new();
                for (int i = 0; i < headers.Count; i++) {
                    string header = headers[i];
                    if (i < cells.Count) {
                        string value = HtmlEntity.DeEntitize(cells[i].InnerText).Trim();
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = value.Replace(kv.Key, kv.Value);
                            }
                        }
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = value;
                    } else if (allProperties) {
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = null;
                    }
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

        HtmlDocument doc = ParseWithHtmlAgilityPack(html);
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
                Dictionary<string, string> obj = new();
                int index = 0;
                foreach (var row in rows) {
                    var cells = row.SelectNodes("th|td");
                    if (cells == null || cells.Count == 0) {
                        continue;
                    }
                    string header = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    string value = cells.Count > 1 ? HtmlEntity.DeEntitize(cells[1].InnerText).Trim() : string.Empty;
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
                    string header = HtmlEntity.DeEntitize(cell.InnerText).Trim();
                    if (replaceHeaders != null) {
                        foreach (var kv in replaceHeaders) {
                            header = header.Replace(kv.Key, kv.Value);
                        }
                    }
                    headers.Add(header);
                }
            } else {
                for (int i = 0; i < headerCells.Count; i++) {
                    headers.Add($"Column{ i + 1 }");
                }
            }

            int startIndex = hasHeader ? headerRowIndex + 1 : 0;
            List<Dictionary<string, string?>> tableRows = new();
            foreach (var row in rows.Skip(startIndex)) {
                var cells = row.SelectNodes("th|td");
                if (cells == null) {
                    continue;
                }
                Dictionary<string, string?> dict = new();
                for (int i = 0; i < headers.Count; i++) {
                    string header = headers[i];
                    if (i < cells.Count) {
                        string value = HtmlEntity.DeEntitize(cells[i].InnerText).Trim();
                        if (replaceContent != null) {
                            foreach (var kv in replaceContent) {
                                value = value.Replace(kv.Key, kv.Value);
                            }
                        }
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = value;
                    } else if (allProperties) {
                        dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = null;
                    }
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
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static async Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithHtmlAgilityPackAsync(
        string url,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseTablesWithHtmlAgilityPack(content, reverseTable, replaceContent, replaceHeaders, allProperties);
    }
}
