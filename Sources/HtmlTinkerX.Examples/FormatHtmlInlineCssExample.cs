using System;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates inlining CSS in HTML asynchronously.
/// </summary>
public static class FormatHtmlInlineCssExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        const string html = "<html><head><style>h1{color:red}</style></head><body><h1>Hello</h1></body></html>";
        string result = await HtmlFormatter.FormatHtmlInlineCssAsync(
            html,
            new PreMailerOptions { RemoveStyleElements = true });
        Console.WriteLine(result);
    }
}
