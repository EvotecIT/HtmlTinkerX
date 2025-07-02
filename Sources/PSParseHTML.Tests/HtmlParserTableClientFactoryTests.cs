using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserTableClientFactoryTests {
    [Fact]
    public async Task ParseUrlTablesWithAngleSharpAsync_NullUrl_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => HtmlParser.ParseUrlTablesWithAngleSharpAsync(null!, null, null, false, null, () => new HttpClient()));
    }

    [Fact]
    public async Task ParseUrlTablesWithAngleSharpAsync_NullClientFactory_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => HtmlParser.ParseUrlTablesWithAngleSharpAsync("http://example.com", null, null, false, null, null!));
    }

    [Fact]
    public async Task ParseUrlTablesWithHtmlAgilityPackAsync_NullUrl_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => HtmlParser.ParseUrlTablesWithHtmlAgilityPackAsync(null!, false, null, null, false, null, () => new HttpClient()));
    }

    [Fact]
    public async Task ParseUrlTablesWithHtmlAgilityPackAsync_NullClientFactory_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => HtmlParser.ParseUrlTablesWithHtmlAgilityPackAsync("http://example.com", false, null, null, false, null, null!));
    }
}
