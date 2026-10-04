using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    public async Task CrawlAsync_UsesBomBeforeConflictingHeaders(string charset) {
        Encoding encoding = Encoding.GetEncoding(charset);
        const string html = "<main>Zażółć</main>";
        byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes(html)).ToArray();
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.ContentType = "text/html; charset=windows-1252";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }, out string root);
        var page = Assert.Single((await HtmlCrawler.CrawlAsync(root, StaticOptions(1))).Pages);
        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Contains("Zażółć", page.Html);
        Assert.DoesNotContain("\uFEFF", page.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CrawlAsync_UsesMetaEncodingWithoutACharsetHeader() {
        // windows-1250 bytes for the Polish text, avoiding process-global encoding setup in the fixture.
        byte[] bytes = Encoding.ASCII.GetBytes("<meta charset='windows-1250'><main>Za")
            .Concat(new byte[] { 0xBF, 0xF3, 0xB3, 0xE6 }).Concat(Encoding.ASCII.GetBytes("</main>")).ToArray();
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }, out string root);
        var page = Assert.Single((await HtmlCrawler.CrawlAsync(root, StaticOptions(1))).Pages);
        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Contains("Zażółć", page.Html);
    }
}
