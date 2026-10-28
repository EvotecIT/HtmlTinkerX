using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Checks browser cache hashes, launch readiness, lock ownership, disk space, and install history.</summary>
/// <example><summary>Check Chromium and repair its cache when needed.</summary>
/// <code>Test-HtmlTinkerXBrowser -Browser Chromium -Repair</code></example>
/// <example><summary>Export machine-readable diagnostics for every engine.</summary>
/// <code>Test-HtmlTinkerXBrowser | ConvertTo-Json -Depth 8</code></example>
[Cmdlet(VerbsDiagnostic.Test, "HtmlTinkerXBrowser")]
[OutputType(typeof(HtmlBrowserCacheReport))]
public sealed class CmdletTestHtmlTinkerXBrowser : AsyncPSCmdlet {
    /// <summary>One browser engine; omit to diagnose all engines.</summary>
    [Parameter]
    public HtmlBrowserEngine? Browser { get; set; }

    /// <summary>Download verified replacement generations for broken or missing revisions.</summary>
    [Parameter]
    public SwitchParameter Repair { get; set; }

    /// <summary>Runs the engine diagnosis and writes its structured report.</summary>
    protected override async Task ProcessRecordAsync() =>
        WriteObject(await HtmlBrowser.TestBrowserCacheAsync(Browser, Repair, CancelToken).ConfigureAwait(false));
}
