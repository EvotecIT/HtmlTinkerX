using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlResourceParserTests {
    [Fact]
    public void Parse_StripsQueryAndFragmentFromNames() {
        string html = "<script src=\"/scripts/app.js?v=1#frag\"></script>" +
                      "<link rel=\"stylesheet\" href=\"/styles/main.css?x=1#frag\" />";

        List<HtmlResourceLink> links = HtmlResourceParser.Parse(html, includeCss: true);

        Assert.Equal(2, links.Count);
        Assert.Equal("app.js", links[0].Name);
        Assert.Equal("main.css", links[1].Name);
    }
}
