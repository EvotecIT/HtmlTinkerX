using PSParseHTML;
using Microsoft.Playwright;

public class Program {
    public static async Task Main() {
        await SimpleExample();
        await AdvancedExample();
    }

    private static async Task SimpleExample() {
        string html = File.ReadAllText(Path.Combine("Input", "sample_form.html"));
        using IPlaywright pw = await Playwright.CreateAsync();
        await using IBrowser browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);

        HtmlFormLogin? form = await HtmlBrowser.DetectLoginFormAsync(page);
        Console.WriteLine($"User selector: {form?.UsernameSelector}");
    }

    private static async Task AdvancedExample() {
        await Playwright.InstallAsync();
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(
            "https://example.com/login",
            HtmlBrowserEngine.Chromium,
            clean: false,
            username: null,
            password: null,
            formLogin: null,
            headless: true);
        HtmlFormLogin? form = await HtmlBrowser.DetectLoginFormAsync(session);
        Console.WriteLine($"Submit selector: {form?.SubmitSelector}");
    }
}
