using HtmlTinkerX;
using System;
using System.Linq;
using System.Text.Json;

if (args.Length == 0 || args[0] != "doctor" || args.Skip(1).Any(arg => arg != "--repair" && !arg.StartsWith("--browser=", StringComparison.Ordinal))) {
    Console.Error.WriteLine("Usage: htmltinkerx doctor [--repair] [--browser=Chromium|Firefox|WebKit]");
    return 1;
}
HtmlBrowserEngine? engine = null;
string? browser = args.FirstOrDefault(arg => arg.StartsWith("--browser=", StringComparison.Ordinal));
if (browser != null) {
    if (!Enum.TryParse(browser.Substring("--browser=".Length), true, out HtmlBrowserEngine parsed) || !Enum.IsDefined(typeof(HtmlBrowserEngine), parsed)) {
        Console.Error.WriteLine("Unknown browser. Choose Chromium, Firefox, or WebKit.");
        return 1;
    }
    engine = parsed;
}
try {
    HtmlBrowserCacheReport report = await HtmlBrowser.TestBrowserCacheAsync(engine, args.Contains("--repair"));
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return report.ExitCode;
} catch (Exception exception) {
    Console.Error.WriteLine(JsonSerializer.Serialize(new { ExitCode = 2, Message = exception.Message }));
    return 2;
}
