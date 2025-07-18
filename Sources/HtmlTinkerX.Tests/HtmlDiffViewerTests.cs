using HtmlTinkerX;
using System.Linq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlDiffViewerTests
{
    [Fact]
    public void BuildViewerHtml_ReturnsHtml()
    {
        var diffs = HtmlDiffer.Compare("<p>a</p>", "<p>b</p>");
        string html = HtmlDiffViewer.BuildViewerHtml(diffs);
        Assert.Contains("<table>", html);
    }
}
