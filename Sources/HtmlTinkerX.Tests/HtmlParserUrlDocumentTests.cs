using HtmlTinkerX;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests for parsing HTML documents retrieved from a URL.
/// </summary>
public class HtmlParserUrlDocumentTests {
    private static TestServer CreateServer() {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                const string html = "<!DOCTYPE html><html><head><title>Server Page</title></head><body><p>Hello world</p></body></html>";
                await ctx.Response.WriteAsync(html);
            }));
        return new TestServer(builder);
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
