using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlEntityUtilityTests {
    [Fact]
    public void DeEntitize_DecodesHtmlEntities() {
        string decoded = HtmlEntityUtility.DeEntitize("A&amp;B &lt;tag&gt; &#x141;");

        Assert.Equal("A&B <tag> Ł", decoded);
    }

    [Fact]
    public void Entitize_EncodesHtmlEntities() {
        string encoded = HtmlEntityUtility.Entitize("A&B <tag>");

        Assert.Contains("A&amp;B", encoded);
        Assert.Contains("&lt;tag&gt;", encoded);
    }
}
