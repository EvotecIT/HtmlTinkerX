using HtmlTinkerX;
using System.Linq;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlDiffViewerTests {
    [Fact]
    public void BuildViewerHtml_ReturnsHtml() {
        var diffs = HtmlDiffer.Compare("<p>a</p>", "<p>b</p>");
        string html = HtmlDiffViewer.BuildViewerHtml(diffs);
        Assert.Contains("<table>", html);
    }

    [Fact]
    public void BuildViewerHtml_EncodesScriptTags() {
        var diffs = HtmlDiffer.Compare("<p></p>", "<script>alert('x')</script>");
        string html = HtmlDiffViewer.BuildViewerHtml(diffs);
        Assert.DoesNotContain("<script>", html);
    }
}
