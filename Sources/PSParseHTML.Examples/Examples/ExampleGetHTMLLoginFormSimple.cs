using PSParseHTML;
using Microsoft.Playwright;

public static class ExampleGetHTMLLoginFormSimple {
    public static async Task RunAsync() {
        string html = File.ReadAllText(Path.Combine("Input", "sample_form.html"));
        using IPlaywright pw = await Playwright.CreateAsync();
        await using IBrowser browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);

        HtmlFormLogin? form = await HtmlBrowser.DetectLoginFormAsync(page);
        Console.WriteLine($"User selector: {form?.UsernameSelector}");
    }
}
