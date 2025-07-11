using System;
using System.IO;

namespace PSParseHTML.Examples;

/// <summary>
/// Demonstrates generating an HTML viewer from diff results.
/// </summary>
public static class ShowHtmlDiffExample
{
    /// <summary>Executes the example logic.</summary>
    public static void Run()
    {
        var diffs = HtmlDiffer.Compare("<p>a</p>", "<p>b</p>");
        string html = HtmlDiffViewer.BuildViewerHtml(diffs);
        string outFile = "diff_viewer.html";
        File.WriteAllText(outFile, html);
        Console.WriteLine($"Viewer written to {outFile}");
    }
}
