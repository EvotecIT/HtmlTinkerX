using HtmlTinkerX;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserMetaTests {
    private static string GetSampleMetaHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "sample_meta.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ParseMetaTags_ReturnsPairs() {
        string html = GetSampleMetaHtml();
        var tags = HtmlParser.ParseMetaTags(html);
        Assert.Equal(2, tags.Count);
        Assert.Equal("description", tags[0].Name);
        Assert.Equal("name", tags[0].SourceAttribute);
        Assert.Equal("Example site", tags[0].Content);
        Assert.Equal("og:title", tags[1].Name);
        Assert.Equal("property", tags[1].SourceAttribute);
        Assert.Equal("Meta Example", tags[1].Content);
    }
}
