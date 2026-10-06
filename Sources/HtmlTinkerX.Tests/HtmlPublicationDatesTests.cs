using System;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlPublicationDatesTests {
    [Fact]
    public void CanonicalPageIdentityPreservesDatesForLanguageQueryUrls() {
        var dates = HtmlPublicationDates.Parse("""
            <link rel="canonical" href="https://example.test/article?id=123">
            <script type="application/ld+json">[{"@type":"Article","url":"https://example.test/article?id=456","datePublished":"1999-01-01"},
            {"@type":"Article","url":"https://example.test/article?id=123","datePublished":"2026-01-02"}]</script>
            """,new Uri("https://example.test/article?id=123&lang=PL"));
        Assert.Equal(new DateTimeOffset(2026,1,2,0,0,0,TimeSpan.Zero),dates.Published);
    }

    [Fact]
    public void ArticleDateSurvivesOrganizationAndSiteMetadata() {
        var dates = HtmlPublicationDates.Parse("""
            <script type="application/ld+json">{"@type":"Organization","datePublished":"2000-01-01"}</script>
            <script type="application/ld+json">{"@type":"WebSite","datePublished":"2001-01-01"}</script>
            <script type="application/ld+json">{"@type":"Article","url":"https://example.test/notice","datePublished":"2025-05-16T08:24:14.000365Z","dateModified":"2026-10-05T17:00:00+02:00"}</script>
            """,new Uri("https://example.test/notice"));
        Assert.Equal(new DateTimeOffset(2025,5,16,8,24,14,TimeSpan.Zero).AddTicks(3650),dates.Published);
        Assert.Equal(new DateTimeOffset(2026,10,5,17,0,0,TimeSpan.FromHours(2)),dates.Modified);
    }

    [Fact]
    public void MatchingGraphArticleWinsAndMetaDatesRetainPriority() {
        const string html = """
            <meta property="article:published_time" content="2026-01-02T10:00:00Z">
            <script type="application/ld+json">{"@graph":[
              {"@type":"NewsArticle","url":"https://example.test/other","datePublished":"1999-01-01","dateModified":"1999-01-02"},
              {"@type":["CreativeWork","NewsArticle"],"mainEntityOfPage":{"@id":"https://example.test/notice"},"datePublished":"2026-01-01","dateModified":"2026-02-03T12:00:00Z"}
            ]}</script>
            """;
        var dates = HtmlPublicationDates.Parse(html,new Uri("https://example.test/notice"));
        Assert.Equal(new DateTimeOffset(2026,1,2,10,0,0,TimeSpan.Zero),dates.Published);
        Assert.Equal(new DateTimeOffset(2026,2,3,12,0,0,TimeSpan.Zero),dates.Modified);
    }

    [Theory]
    [InlineData("https://example.test/notice#article")]
    [InlineData("#article")]
    public void MatchingIdentityAndAgreeingGraphRecordsPreserveIndependentDates(string identity) {
        string html = "<script type='application/ld+json'>{\"@graph\":["+
            "{\"@type\":\"WebPage\",\"url\":\"https://example.test/notice\"},"+
            "{\"@type\":\"Article\",\"@id\":\""+identity+"\",\"datePublished\":\"2026-01-02\",\"dateModified\":\"2026-01-04\"},"+
            "{\"@type\":\"Article\",\"url\":\"https://example.test/notice\",\"datePublished\":\"2026-01-02\",\"dateModified\":\"2026-01-05\"}]}"+
            "</script>";
        var dates = HtmlPublicationDates.Parse(html,new Uri("https://example.test/notice"));
        Assert.Equal(new DateTimeOffset(2026,1,2,0,0,0,TimeSpan.Zero),dates.Published);
        Assert.Null(dates.Modified);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"@type\":\"Article\",\"datePublished\":\"not a date\",\"dateModified\":\"2026-10-05\"}")]
    [InlineData("[{\"@type\":\"Article\",\"datePublished\":\"2020-01-01\"},{\"@type\":\"Article\",\"datePublished\":\"2021-01-01\"}]")]
    [InlineData("{\"@type\":\"Article\",\"url\":\"https://example.test/other\",\"datePublished\":\"2020-01-01\"}")]
    [InlineData("{\"@type\":\"Article\",\"@id\":\"https://example.test/other#article\",\"datePublished\":\"2020-01-01\"}")]
    public void MissingInvalidOrAmbiguousPublicationIsNotInvented(string json) {
        Assert.Null(HtmlPublicationDates.Parse("<script type='application/ld+json'>"+json+"</script>",new Uri("https://example.test/notice")).Published);
    }
}
