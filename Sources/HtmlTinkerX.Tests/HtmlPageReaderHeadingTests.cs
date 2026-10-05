using OfficeIMO.Html;

namespace HtmlTinkerX.Tests;

public class HtmlPageReaderHeadingTests {
    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    public void Read_RetainsImplicitTitleHeadingWithAndWithoutOptionalAnalyses(int level, bool analyses) {
        HtmlPageDocument page = HtmlPageReader.Read(
            $"<main><h{level} id='intro'><em>Intro</em></h{level}><p>Body</p></main>",
            new HtmlPageReaderOptions {
                IncludeReadableText = analyses,
                IncludeMarkdown = analyses,
                IncludeWebData = analyses,
                IncludeCollections = analyses
            });

        HtmlSemanticBlock heading = Assert.Single(page.Headings);
        Assert.Equal("Intro", heading.Text);
        Assert.Equal(level, heading.Level);
        Assert.Equal("h" + level, heading.SourceLocation!.ElementName);
        Assert.Equal("Body", Assert.Single(page.Paragraphs).Text);
        Assert.Same(heading, page.Blocks[0]);
        Assert.Equal(2, page.Blocks.Count);
    }

    [Fact]
    public void Read_ReturnsPromotedAndBodyHeadingsOnceInSourceOrder() {
        HtmlPageDocument page = HtmlPageReader.Read(
            "<main><h1>First</h1><p>One</p><h2>Second</h2><p>Two</p><h3>Details</h3></main>");

        Assert.Equal(new[] { "First", "Second", "Details" }, page.Headings.Select(heading => heading.Text));
        Assert.Equal(new[] { 1, 2, 3 }, page.Headings.Select(heading => heading.Level));
        Assert.Equal(new[] { "First", "One", "Second", "Two", "Details" }, page.Blocks.Select(block => block.Text));
    }
}
