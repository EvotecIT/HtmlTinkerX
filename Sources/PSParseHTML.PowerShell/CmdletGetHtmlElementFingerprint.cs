using AngleSharp.Dom;
using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Records the structural identity of elements so they can be found again after a page layout change.</summary>
/// <remarks>A fingerprint keeps the tag path, identity attributes, stable classes, child and sibling shape, and a hash of
/// the element's text. Save it next to the selector and pass it to Find-HtmlRelocatedElement when the selector stops matching.</remarks>
/// <example>
///   <summary>Fingerprint the element a saved selector matches today</summary>
///   <code>$fingerprint = Get-HtmlElementFingerprint -Path .\release-notes.html -Selector 'div.release-entry' | Select-Object -First 1</code>
/// </example>
/// <example>
///   <summary>Fingerprint an element selected earlier in the pipeline</summary>
///   <code>Select-HtmlElement -Content $html -Selector 'main h1' -First | Get-HtmlElementFingerprint</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlElementFingerprint", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlElementFingerprint))]
public sealed class CmdletGetHtmlElementFingerprint : AsyncPSCmdlet {
    private const string ParameterSetElement = "Element";
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>Element to fingerprint, for example from Select-HtmlElement. Its position in the parsed document is recorded.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetElement, ValueFromPipeline = true, Position = 0)]
    public IElement Element { get; set; } = null!;

    /// <summary>HTML content containing the elements.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file containing the elements.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile, Position = 0)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>CSS selector whose matches are fingerprinted, one fingerprint per match in document order.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, Position = 1)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Selector { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (ParameterSetName == ParameterSetElement) {
            WriteObject(HtmlElementFingerprint.Capture(Element));
            return;
        }
        string html = ParameterSetName == ParameterSetFile
            ? await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false)
            : Content;
        using IDocument document = HtmlParser.ParseWithAngleSharp(html);
        foreach (IElement element in document.QuerySelectorAll(Selector)) {
            WriteObject(HtmlElementFingerprint.Capture(element));
        }
    }
}
