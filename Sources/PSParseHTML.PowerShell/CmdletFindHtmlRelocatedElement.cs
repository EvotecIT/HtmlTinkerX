using AngleSharp.Dom;
using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Suggests where content moved when a saved CSS selector stops matching after a page layout change.</summary>
/// <remarks>Candidates are ranked by similarity to a fingerprint from Get-HtmlElementFingerprint and carry a High, Medium or
/// Low confidence with the reasons behind it. They are suggestions to review; no selector or recipe is changed.</remarks>
/// <example>
///   <summary>Find the new location of a release list after a redesign</summary>
///   <code>Find-HtmlRelocatedElement -Path .\release-notes.html -Selector 'div.release-entry' -Fingerprint $fingerprint</code>
/// </example>
/// <example>
///   <summary>Accept only a high-confidence suggestion</summary>
///   <code>$candidate = Find-HtmlRelocatedElement -Content $html -Selector $selector -Fingerprint $fingerprint | Where-Object Confidence -eq 'High' | Select-Object -First 1</code>
/// </example>
[Cmdlet(VerbsCommon.Find, "HtmlRelocatedElement", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlSelectorRelocationCandidate))]
public sealed class CmdletFindHtmlRelocatedElement : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>Current HTML content to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to the current HTML file to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile, Position = 0)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Saved selector that no longer finds the expected content. A candidate with the same selector is omitted.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Fingerprint captured while the selector still matched.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public HtmlElementFingerprint Fingerprint { get; set; } = null!;

    /// <summary>Maximum number of ranked candidates to return.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Limit { get; set; } = 5;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string html = ParameterSetName == ParameterSetFile
            ? await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false)
            : Content;
        using IDocument document = HtmlParser.ParseWithAngleSharp(html);
        WriteObject(HtmlSelectorRelocator.FindCandidates(document, Selector, Fingerprint, Limit), true);
    }
}
