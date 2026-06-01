using HtmlTinkerX;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests for parsing HTML documents retrieved from a URL.
/// </summary>
public class HtmlParserUrlDocumentTests {
    private static TestServerFixture CreateServer() {
        return TestServerCompat.CreateTestServer(async ctx => {
            const string html = "<!DOCTYPE html><html><head><title>Server Page</title></head><body><p>Hello world</p></body></html>";
            await ctx.Response.WriteAsync(html);
        }, "/", "GET");
    }

    [Fact]
    public async Task ParseUrlWithAngleSharpAsync_ReturnsExpectedElements() {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var doc = await HtmlParser.ParseUrlWithAngleSharpAsync(server.BaseAddress.ToString(), client);

        Assert.Equal("Server Page", doc.Title);
        Assert.Equal("Hello world", doc.QuerySelector("p")?.TextContent);
    }

    [Fact]
    public async Task ParseUrlWithHtmlAgilityPackAsync_ReturnsExpectedElements() {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var doc = await HtmlParser.ParseUrlWithHtmlAgilityPackAsync(server.BaseAddress.ToString(), client);
        var paragraph = doc.DocumentNode.SelectSingleNode("//p");
        var title = doc.DocumentNode.SelectSingleNode("//title");

        Assert.Equal("Server Page", title?.InnerText);
        Assert.Equal("Hello world", paragraph?.InnerText);
    }
}
