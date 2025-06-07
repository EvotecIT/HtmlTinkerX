using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that converts HTML content to plain text.
/// </summary>
/// <example>
/// <code>Convert-HTMLToText -Content "&lt;p&gt;Hello&lt;/p&gt;"</code>
/// </example>
[Cmdlet(VerbsData.Convert, "HTMLToText", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletConvertHtmlToText : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>
    /// HTML content to convert.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Path to a HTML file.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// URL of a HTML page to download and convert.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>
    /// Optional path to write the resulting text.
    /// </summary>
    [Parameter]
    public string? OutputFile { get; set; }

    [Parameter]
    public string? Proxy { get; set; }

    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string html = GetHtmlContent();
        string text = HtmlUtilities.ConvertToText(html);

        if (!string.IsNullOrEmpty(OutputFile)) {
            System.IO.File.WriteAllText(OutputFile, text);
        } else {
            WriteObject(text);
        }
    }

    private string GetHtmlContent() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                if (!System.IO.File.Exists(File)) {
                    ThrowTerminatingError(new ErrorRecord(new FileNotFoundException($"HTML file not found: {File}", File), "FileNotFound", ErrorCategory.InvalidArgument, File));
                }
                return System.IO.File.ReadAllText(File);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return GetStringWithProperEncodingAsync(client, Url.ToString()).GetAwaiter().GetResult();
                }
            case ParameterSetContent:
            default:
                return Content;
        }
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
