using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Sends keyboard input to an element in a browser session before extraction.
/// </summary>
/// <example>
///   <summary>Submit a search input with Enter</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/search -Session
/// Set-HtmlBrowserInput -Session $session -Selector 'input[type=search]' -Value 'HtmlTinkerX' -Type
/// Invoke-HtmlBrowserKey -Session $session -Selector 'input[type=search]' -Key Enter
/// Wait-HtmlBrowserContent -Session $session -Text 'Results' -Selector 'main'
///   </code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserKey")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Invoke-HtmlKey", "Press-HtmlKey")]
public sealed class CmdletInvokeHtmlBrowserKey : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the focused element.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Key or key chord to send, such as Enter, Control+A, or ArrowDown.</summary>
    [Parameter(Mandatory = true, Position = 2)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        await HtmlBrowser.PressKeysAsync(session, Selector, Key, Timeout, linkedCts.Token).ConfigureAwait(false);
        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
