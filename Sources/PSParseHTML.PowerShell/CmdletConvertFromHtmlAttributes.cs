using AngleSharp.Dom;
using System.Management.Automation;
using System.Net.Http;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that extracts elements from HTML by tag, class, id or name attributes.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlAttributes", DefaultParameterSetName = ParameterSetContent)]
[Alias("ConvertFrom-HTMLTag", "ConvertFrom-HTMLClass")]
[OutputType(typeof(string), typeof(IElement))]
public sealed class CmdletConvertFromHtmlAttributes : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of HTML page to download.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Tag name to search for.</summary>
    [Parameter]
    public string? Tag { get; set; }

    /// <summary>Class name to search for.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>ID attribute to search for.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Name attribute to search for.</summary>
    [Parameter]
    public string? Name { get; set; }

    [Parameter]
    public string? Proxy { get; set; }

    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Return matching <see cref="IElement"/> objects instead of text.</summary>
    [Parameter]
    public SwitchParameter ReturnObject { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string html = ParameterSetName == ParameterSetUrl ? DownloadHtml() : Content;

        IEnumerable<IElement> elements = HtmlParserExtensions.GetElements(html, Tag, Class, Id, Name);

        if (ReturnObject.IsPresent) {
            foreach (var e in elements) {
                WriteObject(e);
            }
        } else {
            foreach (var e in elements) {
                WriteObject(e.TextContent);
            }
        }
    }

    private string DownloadHtml() {
        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        return GetStringWithProperEncodingAsync(client, Url.ToString()).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Downloads content from a URL with proper encoding detection.
    /// </summary>
    /// <param name="client">HttpClient to use for the request.</param>
    /// <param name="url">URL to download from.</param>
    /// <returns>Content as a string with proper encoding.</returns>
    private static async Task<string> GetStringWithProperEncodingAsync(HttpClient client, string url) {
        using var response = await client.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        // Try to get encoding from Content-Type header
        var contentType = response.Content.Headers.ContentType;
        if (contentType?.CharSet != null) {
            try {
                var encoding = System.Text.Encoding.GetEncoding(contentType.CharSet);
                return encoding.GetString(bytes);
            } catch {
                // If the specified encoding is not supported, fall through to detection
            }
        }

        // Try to detect encoding from byte order mark (BOM)
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
            return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
            return System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // Try to detect encoding from HTML meta tag
        var asciiContent = System.Text.Encoding.ASCII.GetString(bytes);
        var metaMatch = System.Text.RegularExpressions.Regex.Match(
            asciiContent,
            @"<meta[^>]+charset\s*=\s*[""']?([^""'>\s]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (metaMatch.Success) {
            try {
                var encoding = System.Text.Encoding.GetEncoding(metaMatch.Groups[1].Value);
                return encoding.GetString(bytes);
            } catch {
                // If the detected encoding is not supported, fall through to UTF-8
            }
        }

        // Default to UTF-8 if no encoding could be determined
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
