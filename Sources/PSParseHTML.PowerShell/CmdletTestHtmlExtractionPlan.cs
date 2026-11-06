using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Recommends the most useful PSParseHTML extraction workflow for a page.
/// </summary>
/// <example>
/// <code>Test-HtmlExtractionPlan -Url https://example.com</code>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "HtmlExtractionPlan", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlExtractionPlan))]
public sealed class CmdletTestHtmlExtractionPlan : AsyncUrlCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetPath = "Path";

    /// <summary>HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of the page to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl, Position = 0)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Path to a local HTML file to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath, Position = 0)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateUrlParameters();
        string html = await GetHtmlAsync().ConfigureAwait(false);
        Uri? sourceUri = ParameterSetName == ParameterSetUrl ? Url : null;
        WriteObject(HtmlExtractionPlanner.Analyze(html, sourceUri));
    }

    private async Task<string> GetHtmlAsync() {
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = await CreateUrlClientAsync().ConfigureAwait(false);
            return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
        }

        if (ParameterSetName == ParameterSetPath) {
            string fullPath = Path!.ToFullPath();
            return await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        }

        return Content;
    }
}
