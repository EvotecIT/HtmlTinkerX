using System;
using PSParseHTML;
using Microsoft.Playwright;

public static class ExampleGetHTMLLoginFormAdvanced {
    public static async Task RunAsync() {
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
