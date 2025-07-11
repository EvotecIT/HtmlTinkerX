using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that saves files downloaded while rendering a web page.
/// </summary>
/// <example>
///   <code>Save-HTMLAttachment -Url https://example.com/download.html -Path C:\temp</code>
/// </example>
[Cmdlet(VerbsData.Save, "HTMLAttachment", DefaultParameterSetName = ParameterSetDefault)]
[Alias("Save-HTMLDownload")]
[OutputType(typeof(string))]
public sealed class CmdletSaveHtmlAttachment : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";
    private const string ParameterSetSession = "Session";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetDefault)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Directory where downloads will be saved.</summary>
    [Parameter(Mandatory = true)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    public SwitchParameter Visible { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Optional filter applied to download URLs or file names.</summary>
    [Parameter]
    public string? Filter { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession? session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        IAsyncEnumerable<string> files = ParameterSetName switch {
            ParameterSetSession => HtmlBrowser.SavePageDownloadsAsync(
                (session ?? throw new PSInvalidOperationException("No session provided and no default session found.")).Page,
                HtmlUtilities.ResolvePath(Path),
                Filter,
                token),
            _ => HtmlBrowser.SavePageDownloadsAsync(
                Url,
                HtmlUtilities.ResolvePath(Path),
                Browser,
                Clean.IsPresent,
                Filter,
                headless: !Visible.IsPresent,
                slowMo: SlowMo,
                cancellationToken: token)
        };

        await foreach (string file in files.WithCancellation(token).ConfigureAwait(false)) {
            WriteObject(file);
        }
    }
}
