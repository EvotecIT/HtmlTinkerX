using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserListAsyncTests {
    private static TestServer CreateServer() {
        var builder = new WebHostBuilder()
            .ConfigureServices(s => s.AddRouting())
            .Configure(app => {
                app.UseRouting();
                app.UseEndpoints(endpoints => {
                    endpoints.MapGet("/lists", async context => {
                        string html = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "sample_lists.html"));
                        await context.Response.WriteAsync(html);
                    });
                });
            });
        return new TestServer(builder);
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
