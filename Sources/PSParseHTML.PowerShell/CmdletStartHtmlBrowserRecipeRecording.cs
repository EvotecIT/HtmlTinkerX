using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Starts recording successful HtmlTinkerX browser actions into a replayable browser recipe.
/// </summary>
/// <example>
///   <summary>Start recording actions on the default browser session</summary>
///   <code>Start-HtmlBrowserRecipeRecording -Name MailboxProof -IncludeCurrentUrl</code>
/// </example>
[Cmdlet(VerbsLifecycle.Start, "HtmlBrowserRecipeRecording")]
[OutputType(typeof(HtmlBrowserRecipe))]
public sealed class CmdletStartHtmlBrowserRecipeRecording : AsyncPSCmdlet {
    /// <summary>Browser session to record. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Friendly recipe name.</summary>
    [Parameter(Position = 1)]
    public string? Name { get; set; }

    /// <summary>Explicit recipe start URL. When omitted with <see cref="IncludeCurrentUrl"/>, the current page URL is used.</summary>
    [Parameter]
    public string? StartUrl { get; set; }

    /// <summary>Use the current page URL as the recipe StartUrl.</summary>
    [Parameter]
    public SwitchParameter IncludeCurrentUrl { get; set; }

    /// <summary>Replace an active recording on the same session.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Default timeout in milliseconds stored on the recipe.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Do not automatically capture stable selector alternates for recorded selector-based steps.</summary>
    [Parameter]
    public SwitchParameter NoSelectorAlternates { get; set; }

    /// <summary>Maximum selector alternates captured for each recorded selector-based step.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int SelectorAlternateLimit { get; set; } = 5;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        CancellationToken.ThrowIfCancellationRequested();
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        HtmlBrowserRecipe recipe = HtmlBrowser.StartRecipeRecording(
            session,
            Name,
            StartUrl,
            IncludeCurrentUrl.IsPresent || string.IsNullOrWhiteSpace(StartUrl),
            Force.IsPresent,
            Timeout,
            !NoSelectorAlternates.IsPresent,
            SelectorAlternateLimit);
        WriteObject(recipe);
        return Task.CompletedTask;
    }
}
