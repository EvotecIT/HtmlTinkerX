using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Represents a headless browser session consisting of Playwright objects.
/// </summary>
public sealed class HtmlBrowserSession : IAsyncDisposable {
    public IPlaywright Playwright { get; }
    public IBrowser Browser { get; }
    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public IVideo? Video { get; }
    public string? VideoPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlBrowserSession"/> class.
    /// /// </summary>
    public HtmlBrowserSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page, IVideo? video = null, string? videoPath = null) {
        Playwright = playwright;
        Browser = browser;
        Context = context;
        Page = page;
        Video = video;
        VideoPath = videoPath;
    }

    /// <summary>
    /// Asynchronously disposes of the browser session, closing the page, context, and browser.
    /// /// </summary>
    public async ValueTask DisposeAsync() {
        await Context.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}
