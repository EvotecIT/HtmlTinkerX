using HtmlTinkerX;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests asynchronous form parsing using <see cref="HtmlParser"/>.
/// </summary>
public class HtmlParserFormAsyncTests {
    private static TestServer CreateServer() {
        var builder = new WebHostBuilder()
            .ConfigureServices(s => s.AddRouting())
            .Configure(app => {
                app.UseRouting();
                app.UseEndpoints(endpoints => {
                    endpoints.MapGet("/form", async context => {
                        string html = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "sample_form.html"));
                        await context.Response.WriteAsync(html);
                    });
                });
            });
        return new TestServer(builder);
    }

    [Fact]
    public async Task ParseUrlFormsWithAngleSharpAsync_ReturnsForms() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();

        var forms = await HtmlParser.ParseUrlFormsWithAngleSharpAsync(server.BaseAddress + "form", client);
        Assert.Equal(2, forms.Count);
        Assert.Equal("user", forms[0].Fields[0].Name);
    }
}