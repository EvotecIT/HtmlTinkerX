using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Provides helpers for parsing HTML content using either AngleSharp or HtmlAgilityPack.
/// </summary>
public static class HtmlParser {

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
    public static async Task<IDocument> ParseUrlWithAngleSharpAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
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
    public static async Task<HtmlDocument> ParseUrlWithHtmlAgilityPackAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return ParseWithHtmlAgilityPack(content);
    }

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
    public static List<HtmlTableResult> ParseTablesWithAngleSharpDetailed(
        string html,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        bool skipFooter = false,
        bool cleanHeaders = false,
        string? emptyValuePlaceholder = null) {
        return HtmlParserFromTable.ParseTablesWithAngleSharpDetailed(html, replaceContent, replaceHeaders, allProperties, skipFooter, cleanHeaders, emptyValuePlaceholder);
    }

    /// <summary>
    /// Extracts table data from HTML markup using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing tables.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <param name="clientFactory">Factory used to create a temporary <see cref="HttpClient"/> when one is not supplied.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static List<List<Dictionary<string, string?>>> ParseTablesWithAngleSharp(
        string html,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false) {
        return HtmlParserFromTable.ParseTablesWithAngleSharp(html, replaceContent, replaceHeaders, allProperties);
    }

    /// <summary>
    /// Extracts table data from a web page using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="replaceContent">Dictionary of text replacements for table cells.</param>
    /// <param name="replaceHeaders">Dictionary of text replacements for header cells.</param>
    /// <param name="clientFactory">Factory used to create a temporary <see cref="HttpClient"/> when one is not supplied.</param>
    /// <returns>List of tables with rows represented as dictionaries.</returns>
    public static async Task<List<List<Dictionary<string, string?>>>> ParseUrlTablesWithAngleSharpAsync(
        string url,
        IDictionary<string, string>? replaceContent = null,
        IDictionary<string, string>? replaceHeaders = null,
        bool allProperties = false,
        HttpClient? client = null,
        Func<HttpClient>? clientFactory = null) {
        return await HtmlParserFromTable.ParseUrlTablesWithAngleSharpAsync(url, replaceContent, replaceHeaders, allProperties, client, clientFactory);
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
        return HtmlParserFromTable.ParseTablesWithHtmlAgilityPackDetailed(html, reverseTable, replaceContent, replaceHeaders, allProperties, skipFooter, cleanHeaders, emptyValuePlaceholder);
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
        return HtmlParserFromTable.ParseTablesWithHtmlAgilityPack(html, reverseTable, replaceContent, replaceHeaders, allProperties);
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
        bool allProperties = false,
        HttpClient? client = null,
        Func<HttpClient>? clientFactory = null) {
        return await HtmlParserFromTable.ParseUrlTablesWithHtmlAgilityPackAsync(url, reverseTable, replaceContent, replaceHeaders, allProperties, client, clientFactory);
    }

    /// <summary>
    /// Extracts list items from HTML markup using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with item texts.</returns>
    public static List<List<string>> ParseListsWithAngleSharp(string html, string tagPlaceholder = " ") {
        return HtmlParserFromList.ParseListsWithAngleSharp(html, tagPlaceholder);
    }

    /// <summary>
    /// Extracts list items from a web page using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with item texts.</returns>
    public static async Task<List<List<string>>> ParseUrlListsWithAngleSharpAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        return await HtmlParserFromList.ParseUrlListsWithAngleSharpAsync(url, tagPlaceholder, client);
    }

    /// <summary>
    /// Extracts list items from HTML markup using HtmlAgilityPack.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with item texts.</returns>
    public static List<List<string>> ParseListsWithHtmlAgilityPack(string html, string tagPlaceholder = " ") {
        return HtmlParserFromList.ParseListsWithHtmlAgilityPack(html, tagPlaceholder);
    }

    /// <summary>
    /// Extracts list items from a web page using HtmlAgilityPack.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List of lists with item texts.</returns>
    public static async Task<List<List<string>>> ParseUrlListsWithHtmlAgilityPackAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        return await HtmlParserFromList.ParseUrlListsWithHtmlAgilityPackAsync(url, tagPlaceholder, client);
    }

    /// <summary>
    /// Extracts list items from HTML markup using AngleSharp with metadata.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    public static List<HtmlListResult> ParseListsWithAngleSharpDetailed(string html, string tagPlaceholder = " ") {
        return HtmlParserFromList.ParseListsWithAngleSharpDetailed(html, tagPlaceholder);
    }

    /// <summary>
    /// Extracts list items from a web page using AngleSharp with metadata.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    public static async Task<List<HtmlListResult>> ParseUrlListsWithAngleSharpDetailedAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        return await HtmlParserFromList.ParseUrlListsWithAngleSharpDetailedAsync(url, tagPlaceholder, client);
    }

    /// <summary>
    /// Extracts list items from HTML markup using HtmlAgilityPack with metadata.
    /// </summary>
    /// <param name="html">HTML content containing lists.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    public static List<HtmlListResult> ParseListsWithHtmlAgilityPackDetailed(string html, string tagPlaceholder = " ") {
        return HtmlParserFromList.ParseListsWithHtmlAgilityPackDetailed(html, tagPlaceholder);
    }

    /// <summary>
    /// Extracts list items from a web page using HtmlAgilityPack with metadata.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="tagPlaceholder">Placeholder inserted between text segments.</param>
    /// <returns>List parse results with metadata.</returns>
    public static async Task<List<HtmlListResult>> ParseUrlListsWithHtmlAgilityPackDetailedAsync(string url, string tagPlaceholder = " ", HttpClient? client = null) {
        return await HtmlParserFromList.ParseUrlListsWithHtmlAgilityPackDetailedAsync(url, tagPlaceholder, client);
    }

    /// <summary>
    /// Extracts form definitions from HTML markup using AngleSharp.
    /// </summary>
    /// <param name="html">HTML content containing forms.</param>
    /// <returns>List of form parse results.</returns>
    public static List<HtmlFormResult> ParseFormsWithAngleSharp(string html) {
        return HtmlParserFromForm.ParseFormsWithAngleSharp(html);
    }

    /// <summary>
    /// Extracts form definitions from a web page using AngleSharp.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>List of form parse results.</returns>
    public static async Task<List<HtmlFormResult>> ParseUrlFormsWithAngleSharpAsync(string url, HttpClient? client = null) {
        return await HtmlParserFromForm.ParseUrlFormsWithAngleSharpAsync(url, client);
    }

    /// <summary>
    /// Extracts <meta> tag information from HTML markup.
    /// </summary>
    /// <param name="html">HTML content containing meta tags.</param>
    /// <returns>List of meta tag name/content pairs.</returns>
    public static List<HtmlMetaTag> ParseMetaTags(string html) {
        return HtmlParserFromMeta.ParseMetaTags(html);
    }

    /// <summary>
    /// Extracts <meta> tag information from a web page.
    /// </summary>
    /// <param name="url">URL of the page to download.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>List of meta tag name/content pairs.</returns>
    public static async Task<List<HtmlMetaTag>> ParseUrlMetaTagsAsync(string url, HttpClient? client = null) {
        return await HtmlParserFromMeta.ParseUrlMetaTagsAsync(url, client).ConfigureAwait(false);
    }



    /// <summary>
    /// Clean the header name to remove problematic characters that can cause PowerShell formatting issues.
    /// This method is useful for both PowerShell and C# consumers to ensure header names are safe for property access.
    /// </summary>
    /// <param name="headerName">The header name to clean.</param>
    /// <returns>The cleaned header name.</returns>
    public static string CleanHeaderName(string headerName) {
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
    /// Ensure all names are unique by appending an index when duplicates are detected.
    /// </summary>
    /// <param name="names">Collection of names to sanitize.</param>
    public static void EnsureUniqueNames(IList<string> names) {
        if (names == null) {
            throw new ArgumentNullException(nameof(names));
        }

        Dictionary<string, int> counters = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < names.Count; i++) {
            string name = names[i];
            if (string.IsNullOrEmpty(name)) {
                continue;
            }

            if (counters.TryGetValue(name, out int count)) {
                count++;
                counters[name] = count;
                string unique = name + count;
                LoggingMessages.Logger.WriteWarning($"Duplicate header '{name}' detected. Renaming to '{unique}'.");
                names[i] = unique;
            } else {
                counters[name] = 0;
            }
        }
    }
}
