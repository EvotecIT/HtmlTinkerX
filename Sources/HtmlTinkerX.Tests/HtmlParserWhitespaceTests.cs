using AngleSharp.Dom;
using HtmlTinkerX;
using System.Linq;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserWhitespaceTests {
    [Fact]
    public void Parsers_HandleWhitespaceAndComments_Consistently() {
        const string html = "<div>\n  <p> <!--comment--> Hello </p>\n  <!-- another -->\n</div>";

        var angleDoc = HtmlParser.ParseWithAngleSharp(html);
        var angleText = angleDoc.QuerySelector("p")!.TextContent.Trim();
        var angleComments = angleDoc.Descendants<IComment>().Count();

        var agilityDoc = HtmlParser.ParseWithHtmlAgilityPack(html);
        var agilityText = agilityDoc.DocumentNode.SelectSingleNode("//p")!.InnerText.Trim();
        var agilityComments = agilityDoc.DocumentNode.SelectNodes("//comment()")?.Count ?? 0;

        Assert.Equal(angleComments, agilityComments);
        Assert.Equal(angleText, agilityText);
        Assert.Equal("Hello", angleText);
    }
}