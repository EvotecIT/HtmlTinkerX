using HtmlTinkerX;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserTests {
    private static TestServer CreateServer() {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                const string html = "<!DOCTYPE html><html><head><title>Example Domain</title></head><body></body></html>";
                await ctx.Response.WriteAsync(html);
            }));
        return new TestServer(builder);
    }
    [Fact]
    public void ParseWithAngleSharp_FromString() {
        const string html = "<html><body><p>Test</p></body></html>";
        var doc = HtmlParser.ParseWithAngleSharp(html);
        Assert.Equal("html", doc.DocumentElement.NodeName.ToLower());
    }

    [Fact]
    public void ParseWithHtmlAgilityPack_FromString() {
        const string html = "<html><body><p>Test</p></body></html>";
        var doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        Assert.Equal("#document", doc.DocumentNode.Name.ToLower());
    }

    [Fact]
    public async Task ParseUrlWithAngleSharpAsync_FromExample() {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var doc = await HtmlParser.ParseUrlWithAngleSharpAsync(server.BaseAddress.ToString(), client);
        Assert.Contains("Example Domain", doc.Title);
    }

    [Fact]
    public async Task ParseUrlWithHtmlAgilityPackAsync_FromExample() {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var doc = await HtmlParser.ParseUrlWithHtmlAgilityPackAsync(server.BaseAddress.ToString(), client);
        Assert.NotNull(doc.DocumentNode);
    }

    [Fact]
    public void ParseTablesWithAngleSharpDetailed_NullHtml_Throws() {
        var method = typeof(HtmlParser).GetMethod(nameof(HtmlParser.ParseTablesWithAngleSharpDetailed))
            ?? throw new MissingMethodException();
        Assert.Throws<ArgumentNullException>(() => method.Invoke(null, new object?[] { null, null, null, false, false, false, null }));
    }
}