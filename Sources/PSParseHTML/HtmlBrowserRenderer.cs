using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static class HtmlBrowserRenderer {
    /// <summary>
    /// Retrieves the fully rendered HTML from the specified URL after executing JavaScript.
    /// </summary>
    /// <param name="url">The URL to load.</param>
    /// <returns>The rendered HTML markup.</returns>
    public static async Task<string> GetPageContentAsync(string url) {
        Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return await page.ContentAsync();
    }

    /// <summary>
    /// Saves the rendered HTML to a file.
    /// </summary>
    /// <param name="url">URL to load.</param>
    /// <param name="path">File path to write.</param>
    public static async Task SavePageContentAsync(string url, string path) {
        string content = await GetPageContentAsync(url).ConfigureAwait(false);
        File.WriteAllText(path, content);
    }
}
