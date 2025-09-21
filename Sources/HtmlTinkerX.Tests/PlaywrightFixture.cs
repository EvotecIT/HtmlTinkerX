using Microsoft.Playwright;
using System;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// xUnit fixture that installs Playwright once for the entire test suite.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime {
    /// <summary>
    /// Performs one-time initialization of Playwright.
    /// </summary>
    public async Task InitializeAsync() {
        var skip = (Environment.GetEnvironmentVariable("HTMLINKERX_SKIP_FIXTURE_INSTALL") ?? string.Empty)
            .Equals("1", StringComparison.OrdinalIgnoreCase);
        if (!skip) {
            // Emit logs to console in CI to help diagnose Playwright installation issues
            if ((Environment.GetEnvironmentVariable("CI") ?? string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase))
                HtmlBrowser.Logger = s => Console.WriteLine($"[HtmlTinkerX] {s}");
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        }
    }

    /// <summary>
    /// No cleanup required.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// Collection definition for Playwright dependent tests.
/// </summary>
[CollectionDefinition("Playwright collection")]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }
