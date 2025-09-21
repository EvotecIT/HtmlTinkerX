using HtmlTinkerX;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserInstallerTests
{
    [Fact]
    public async Task EnsureInstalledAsync_InstallsDepsOnLinux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
        // Opt into dependency installation for this test to verify flag propagation
        Environment.SetEnvironmentVariable("HTMLINKERX_INSTALL_DEPS", "1");
        // Skip smoke launch in CI/unit test
        Environment.SetEnvironmentVariable("HTMLINKERX_SKIP_SMOKE", "1");

        try
        {
            // prepare fake driver so IsDriverPresent returns true
            string baseDir = Path.Combine(tempDriver, ".playwright");
            string platformId = PlatformExtensions.GetCurrentPlatform().ToPlatformId();
            string nodeDir = Path.Combine(baseDir, "node", platformId);
            Directory.CreateDirectory(nodeDir);
            var pkgDir = Path.Combine(baseDir, "package");
            Directory.CreateDirectory(pkgDir);
            // create a fake cli.js to satisfy completeness checks without network
            File.WriteAllText(Path.Combine(pkgDir, "cli.js"), "// fake");
            File.WriteAllText(Path.Combine(nodeDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), "");
            File.WriteAllText(Path.Combine(baseDir, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version?.ToString(3) ?? "1.52.0");

            string[]? captured = null;
            HtmlBrowser.PlaywrightInstaller = args => { captured = args; return 0; };

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            Assert.NotNull(captured);
            Assert.Contains("--with-deps", captured);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = args => Microsoft.Playwright.Program.Main(args);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            Environment.SetEnvironmentVariable("HTMLINKERX_INSTALL_DEPS", null);
            Environment.SetEnvironmentVariable("HTMLINKERX_SKIP_SMOKE", null);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }
}

