using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates parsing cookies and adding them to a browser session.
/// </summary>
public static class ParseCookiesExample {
    /// <summary>
    /// Executes the example logic.
    /// </summary>
    public static async Task RunAsync() {
        string line = "example.com\tFALSE\t/\tTRUE\t1704067199\tSessionId\tabc123xyz";
        List<HtmlCookie> cookies = HtmlCookieParser.ParseNetscapeFile(line);

        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank").ConfigureAwait(false);
        await HtmlBrowser.SetCookiesAsync(session, cookies).ConfigureAwait(false);
        List<HtmlCookie> result = await HtmlBrowser.GetCookiesAsync(session).ConfigureAwait(false);
        Console.WriteLine($"Retrieved {result.Count} cookie(s) from session.");
        string netscape = HtmlCookieParser.ToNetscapeFile(result);
        Console.WriteLine(netscape);
        await HtmlBrowser.CloseSessionAsync(session).ConfigureAwait(false);

        string header = "Set-Cookie: id=abc; Path=/; Secure";
        HtmlCookie cookie = HtmlCookieParser.ParseSetCookieHeader(header);
        Console.WriteLine($"{cookie.Name}={cookie.Value}; Secure={cookie.Secure}");
    }
}