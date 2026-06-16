using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Saves rendered HTML or text content from an active browser session.
/// </summary>
/// <example>
///   <summary>Save rendered main content to disk</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/app -Session
/// Save-HtmlBrowserContent -Session $session -Selector main -OutFile .\rendered-main.html
///   </code>
/// </example>
[Cmdlet(VerbsData.Save, "HtmlBrowserContent")]
[OutputType(typeof(string))]
[Alias("Save-HtmlContent")]
public sealed class CmdletSaveHtmlBrowserContent : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Output file path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string OutFile { get; set; } = string.Empty;

    /// <summary>Optional selector to save.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>Save inner HTML instead of outer HTML.</summary>
    [Parameter]
    public SwitchParameter InnerHtml { get; set; }

    /// <summary>Save text instead of HTML.</summary>
    [Parameter]
    public SwitchParameter AsText { get; set; }

    /// <summary>Timeout in milliseconds while waiting for the selector.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Write the saved path to the pipeline.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (InnerHtml.IsPresent && AsText.IsPresent) {
            ThrowTerminatingError(new ErrorRecord(
                new PSInvalidOperationException("Specify only one of -InnerHtml or -AsText."),
                "InvalidParameter",
                ErrorCategory.InvalidArgument,
                Selector));
            return;
        }

        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        string fullPath = OutFile.ToFullPath();
        await HtmlBrowser.SaveContentAsync(session, fullPath, Selector, InnerHtml.IsPresent, AsText.IsPresent, Timeout, linkedCts.Token).ConfigureAwait(false);
        if (PassThru.IsPresent) {
            WriteObject(fullPath);
        }
    }
}
