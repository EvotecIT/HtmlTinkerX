using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Represents a headless browser session consisting of Playwright objects.
/// </summary>
internal sealed class BrowserSession : IAsyncDisposable {
    public IPlaywright Playwright { get; }
    public IBrowser Browser { get; }
    public IBrowserContext Context { get; }
    public IPage Page { get; }

    public BrowserSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page) {
        Playwright = playwright;
        Browser = browser;
        Context = context;
        Page = page;
    }

    public async ValueTask DisposeAsync() {
        await Context.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}
