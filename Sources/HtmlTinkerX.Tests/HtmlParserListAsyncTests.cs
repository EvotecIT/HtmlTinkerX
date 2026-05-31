using HtmlTinkerX;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserListAsyncTests {
    private static TestServerFixture CreateServer() {
        return TestServerCompat.CreateListParsingTestServer();
    }

    [Fact]
    public async Task ParseUrlListsWithHtmlAgilityPackAsync_ReturnsItems() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();

        var lists = await HtmlParser.ParseUrlListsWithHtmlAgilityPackAsync(server.BaseAddress + "lists", " ", client);
        Assert.Equal(2, lists.Count);
        Assert.Equal(new[] { "Item1", "Item2" }, lists[0]);
    }

    [Fact]
    public async Task ParseUrlListsWithAngleSharpAsync_ReturnsItems() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();

        var lists = await HtmlParser.ParseUrlListsWithAngleSharpAsync(server.BaseAddress + "lists", " ", client);
        Assert.Equal(2, lists.Count);
        Assert.Equal(new[] { "Item1", "Item2" }, lists[0]);
    }
}
