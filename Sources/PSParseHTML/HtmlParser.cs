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
    /// Extracts table data from HTML markup using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static List<List<Dictionary<string, string>>> ParseTablesWithAngleSharp(
        string html,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        var document = ParseWithAngleSharp(html);
        var tables = document.QuerySelectorAll("table");
        List<List<Dictionary<string, string>>> result = new();

        foreach (var table in tables) {
            var rows = table.QuerySelectorAll("tr");
            if (rows.Length == 0) {
                continue;
            }

            var headerCells = rows[0].QuerySelectorAll("th,td");
            List<string> headers = new();
            foreach (var cell in headerCells) {
                string header = cell.TextContent.Trim();
                if (replaceHeaders != null) {
                    foreach (var kv in replaceHeaders) {
                        header = header.Replace(kv.Key, kv.Value);
                    }
                }
                headers.Add(header);
            }

            if (headers.Count == 0) {
                continue;
            }

            List<Dictionary<string, string>> tableRows = new();
            foreach (var row in rows.Skip(1)) {
                var cells = row.QuerySelectorAll("th,td");
                Dictionary<string, string> dict = new();
                for (int i = 0; i < headers.Count && i < cells.Length; i++) {
                    string value = cells[i].TextContent.Trim();
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = value.Replace(kv.Key, kv.Value);
                        }
                    }
                    string header = headers[i];
                    dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = value;
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
    public static async Task<List<List<Dictionary<string, string>>>> ParseUrlTablesWithAngleSharpAsync(
        string url,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseTablesWithAngleSharp(content, replaceContent, replaceHeaders);
    }

    /// <summary>
    /// Extracts table data from HTML markup using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="reverseTable">Whether to treat rows as key/value pairs.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static List<List<Dictionary<string, string>>> ParseTablesWithHtmlAgilityPack(
        string html,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlDocument doc = ParseWithHtmlAgilityPack(html);
        var tables = doc.DocumentNode.SelectNodes("//table");
        List<List<Dictionary<string, string>>> result = new();
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
                    result.Add(new List<Dictionary<string, string>> { obj });
                }
                continue;
            }

            var headerCells = rows[0].SelectNodes("th|td");
            if (headerCells == null) {
                continue;
            }

            List<string> headers = new();
            foreach (var cell in headerCells) {
                string header = HtmlEntity.DeEntitize(cell.InnerText).Trim();
                if (replaceHeaders != null) {
                    foreach (var kv in replaceHeaders) {
                        header = header.Replace(kv.Key, kv.Value);
                    }
                }
                headers.Add(header);
            }

            List<Dictionary<string, string>> tableRows = new();
            foreach (var row in rows.Skip(1)) {
                var cells = row.SelectNodes("th|td");
                if (cells == null) {
                    continue;
                }
                Dictionary<string, string> dict = new();
                for (int i = 0; i < headers.Count && i < cells.Count; i++) {
                    string value = HtmlEntity.DeEntitize(cells[i].InnerText).Trim();
                    if (replaceContent != null) {
                        foreach (var kv in replaceContent) {
                            value = value.Replace(kv.Key, kv.Value);
                        }
                    }
                    string header = headers[i];
                    dict[string.IsNullOrEmpty(header) ? i.ToString() : header] = value;
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
    public static async Task<List<List<Dictionary<string, string>>>> ParseUrlTablesWithHtmlAgilityPackAsync(
        string url,
        bool reverseTable = false,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        string content = await _client.GetStringAsync(url).ConfigureAwait(false);
        return ParseTablesWithHtmlAgilityPack(content, reverseTable, replaceContent, replaceHeaders);
    }
}
