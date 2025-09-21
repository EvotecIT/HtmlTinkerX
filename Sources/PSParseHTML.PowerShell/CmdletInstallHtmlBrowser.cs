using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Installs or updates the Playwright browser runtime used by HtmlTinkerX.
/// Works on Windows, macOS, and Linux (including CI).
/// </summary>
[Cmdlet(VerbsLifecycle.Install, "HTMLBrowserRuntime")]
public sealed class CmdletInstallHtmlBrowser : AsyncPSCmdlet
{
    /// <summary>Browser engine to install.</summary>
    [Parameter]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Install OS dependencies on Linux using Playwright's --with-deps.</summary>
    [Parameter]
    public SwitchParameter WithDeps { get; set; }

    /// <summary>Clean existing runtime caches prior to install.</summary>
    [Parameter]
    public SwitchParameter Clean { get; set; }

    /// <summary>Optional custom browsers path (PLAYWRIGHT_BROWSERS_PATH). Use '0' for hermetic install.</summary>
    [Parameter]
    public string? BrowsersPath { get; set; }

    /// <summary>Skip the post-install smoke launch (for restricted CI environments).</summary>
    [Parameter]
    public SwitchParameter SkipSmoke { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        string? prevBrowsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? prevInstallDeps = Environment.GetEnvironmentVariable("HTMLINKERX_INSTALL_DEPS");
        string? prevSkipSmoke = Environment.GetEnvironmentVariable("HTMLINKERX_SKIP_SMOKE");
        try
        {
            if (!string.IsNullOrWhiteSpace(BrowsersPath))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", BrowsersPath);
            if (WithDeps.IsPresent)
                Environment.SetEnvironmentVariable("HTMLINKERX_INSTALL_DEPS", "1");
            if (SkipSmoke.IsPresent)
                Environment.SetEnvironmentVariable("HTMLINKERX_SKIP_SMOKE", "1");

            if (Clean.IsPresent)
                typeof(HtmlBrowser).GetMethod("CleanInstallDir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .Invoke(null, null);

            await HtmlBrowser.EnsureInstalledAsync(Browser).ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", prevBrowsers);
            Environment.SetEnvironmentVariable("HTMLINKERX_INSTALL_DEPS", prevInstallDeps);
            Environment.SetEnvironmentVariable("HTMLINKERX_SKIP_SMOKE", prevSkipSmoke);
        }
    }
}

