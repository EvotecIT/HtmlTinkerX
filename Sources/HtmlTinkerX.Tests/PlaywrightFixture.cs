using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// xUnit fixture that installs Playwright once for the entire test suite.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime {
    private string? _startupCacheDirectory;
    private string? _originalStartupCache;
    private bool _startupCacheConfigured;

    /// <summary>
    /// Performs one-time initialization of Playwright.
    /// </summary>
    public async Task InitializeAsync() {
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        _originalStartupCache = Environment.GetEnvironmentVariable("MOZ_STARTUP_CACHE");
        _startupCacheDirectory = Path.Combine(Path.GetTempPath(), "htmltinkerx-firefox-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_startupCacheDirectory);
        Environment.SetEnvironmentVariable("MOZ_STARTUP_CACHE", Path.Combine(_startupCacheDirectory, "startupCache"));
        _startupCacheConfigured = true;
        try {
            // A fresh Firefox profile writes its startup cache while handling the first page.
            // Prepare only that cache; every test still creates its own browser and page state.
            await using HtmlBrowserSession warmup = await HtmlBrowser.OpenSessionAsync("about:blank",
                new HtmlBrowserLaunchOptions { Browser = HtmlBrowserEngine.Firefox });
            await warmup.Page.EvaluateAsync<bool>("true");
        } catch {
            await DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Restores the environment and removes the fixture's Firefox startup cache.
    /// </summary>
    public Task DisposeAsync() {
        if (_startupCacheConfigured) {
            Environment.SetEnvironmentVariable("MOZ_STARTUP_CACHE", _originalStartupCache);
            _startupCacheConfigured = false;
        }
        if (_startupCacheDirectory != null && Directory.Exists(_startupCacheDirectory)) {
            Directory.Delete(_startupCacheDirectory, recursive: true);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Collection definition for Playwright dependent tests.
/// </summary>
[CollectionDefinition("Playwright collection", DisableParallelization = true)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }
