using System;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates retrieving console messages from a page.
/// </summary>
public static class ConsoleLogExample
{
    /// <summary>
    /// Executes the example logic.
    /// </summary>
    public static async Task RunAsync()
    {
        string path = Path.Combine("..", "..", "..", "Examples", "Input", "console_page.html");
        string url = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(url).ConfigureAwait(false);
        foreach (HtmlConsoleEntry entry in HtmlBrowser.GetConsoleLog(session))
        {
            Console.WriteLine($"{entry.Type}: {entry.Text}");
        }
    }
}

