using Microsoft.Playwright;
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
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
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
