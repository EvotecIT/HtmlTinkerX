using AngleSharp.Diffing.Core;
using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that compares HTML content and returns differences.
/// </summary>
/// <example>
/// <code>Compare-Html -Reference $file1 -Difference $file2</code>
/// </example>
[Cmdlet(VerbsData.Compare, "Html")]
[OutputType(typeof(IDiff))]
public sealed class CmdletCompareHtml : AsyncPSCmdlet {
    /// <summary>Reference HTML markup, file path or URL.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Reference { get; set; } = string.Empty;

    /// <summary>HTML to compare against the reference.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Difference { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string referenceContent = await GetContentAsync(Reference).ConfigureAwait(false);
        string differenceContent = await GetContentAsync(Difference).ConfigureAwait(false);

        foreach (var diff in HtmlDiffer.Compare(referenceContent, differenceContent)) {
            WriteObject(diff);
        }
    }

    private async Task<string> GetContentAsync(string input) {
        if (TryReadFile(input, out string fileContent)) {
            return fileContent;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            return await HtmlUtilities.GetStringWithProperEncodingAsync(HtmlHttpClientFactory.Shared, input, fetchOptions: null, cancellationToken: CancelToken)
                .ConfigureAwait(false);
        }

        return input;
    }

    private static bool TryReadFile(string path, out string content) {
        content = string.Empty;
        try {
            string fullPath = path.ToFullPath();
            if (File.Exists(fullPath)) {
                content = File.ReadAllText(fullPath);
                return true;
            }
        } catch {
        }
        return false;
    }
}
