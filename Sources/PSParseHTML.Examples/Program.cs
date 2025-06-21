using System;
using System.IO;
using System.Threading.Tasks;

namespace PSParseHTML.Examples;

public static class Program
{
    public static async Task Main()
    {
        string har = Path.Combine("..", "..", "..", "Examples", "example.har");
        Har data = await HtmlHarViewer.ReadHarAsync(har);
        string html = HtmlHarViewer.BuildViewerHtml(data);
        string outfile = Path.Combine("viewer.html");
        await File.WriteAllTextAsync(outfile, html);
        Console.WriteLine($"Viewer written to {outfile}");
    }
}
