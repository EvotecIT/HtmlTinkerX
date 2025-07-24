using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns basic statistics for an HTML document.
/// </summary>
/// <example>
///   <summary>Analyze a string</summary>
///   <code>Measure-HTMLDocument -Content "&lt;html&gt;...&lt;/html&gt;"</code>
/// </example>
/// <example>
///   <summary>Analyze a file</summary>
///   <code>Measure-HTMLDocument -Path ./page.html</code>
/// </example>
[Cmdlet(VerbsDiagnostic.Measure, "HtmlDocumentStructure", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlDocumentStatistics))]
[Alias("Measure-HTMLDocument")]
public sealed class CmdletMeasureHtmlDocumentStructure : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>HTML string to analyze.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string html = ParameterSetName == ParameterSetFile
#if NETSTANDARD2_0 || NETFRAMEWORK
            ? await Task.Run(() => File.ReadAllText(HtmlUtilities.ResolvePath(Path)), CancelToken).ConfigureAwait(false)
#else
            ? await File.ReadAllTextAsync(HtmlUtilities.ResolvePath(Path), CancelToken).ConfigureAwait(false)
#endif
            : Content;

        HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        string text = HtmlParserToText.ConvertToText(html);
        int wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        int linkCount = doc.DocumentNode.SelectNodes("//a")?.Count ?? 0;
        int imageCount = doc.DocumentNode.SelectNodes("//img")?.Count ?? 0;

        HtmlDocumentStatistics stats = new() {
            WordCount = wordCount,
            LinkCount = linkCount,
            ImageCount = imageCount
        };

        WriteObject(stats);
    }
}