using Microsoft.Playwright;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserLoginDetectionTests {
    private static string GetDocument(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", name));

    [Fact]
    public async Task DetectLoginForm_ReturnsSelectors() {
        string html = GetDocument("sample_form.html");
        using IPlaywright pw = await Playwright.CreateAsync();
        await using IBrowser browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);

        HtmlFormLogin? result = await HtmlBrowser.DetectLoginFormAsync(page);

        Assert.NotNull(result);
        Assert.Equal("input[name='user']", result!.UsernameSelector);
        Assert.Equal("input[name='pass']", result.PasswordSelector);
        Assert.Equal("button[type='submit']", result.SubmitSelector);
    }

    [Fact]
    public async Task DetectLoginForm_NoFormReturnsNull() {
        string html = GetDocument("headless_table.html");
        using IPlaywright pw = await Playwright.CreateAsync();
        await using IBrowser browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);

        HtmlFormLogin? result = await HtmlBrowser.DetectLoginFormAsync(page);

        Assert.Null(result);
    }
}
