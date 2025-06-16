using System;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using AngleSharp.Diffing.Core;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that compares HTML content and returns differences.
/// </summary>
[Cmdlet(VerbsData.Compare, "HTML")]
[OutputType(typeof(IDiff))]
public sealed class CmdletCompareHtml : PSCmdlet {
    /// <summary>Reference HTML markup, file path or URL.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Reference { get; set; } = string.Empty;

    /// <summary>HTML to compare against the reference.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Difference { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string referenceContent = GetContent(Reference);
        string differenceContent = GetContent(Difference);

        foreach (var diff in HtmlDiffer.Compare(referenceContent, differenceContent)) {
            WriteObject(diff);
        }
    }

    private static string GetContent(string input) {
        if (TryReadFile(input, out string fileContent)) {
            return fileContent;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            return HtmlUtilities.GetStringWithProperEncodingAsync(HtmlHttpClientFactory.Shared, input)
                .GetAwaiter().GetResult();
        }

        return input;
    }

    private static bool TryReadFile(string path, out string content) {
        content = string.Empty;
        try {
            string fullPath = HtmlUtilities.ResolvePath(path);
            if (File.Exists(fullPath)) {
                content = File.ReadAllText(fullPath);
                return true;
            }
        } catch {
        }
        return false;
    }
}
