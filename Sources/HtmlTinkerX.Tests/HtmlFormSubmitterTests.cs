using HtmlTinkerX;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlFormSubmitterTests {
    private static TestServer CreateServer() {
        var builder = new WebHostBuilder()
            .ConfigureServices(s => s.AddRouting())
            .Configure(app => {
                app.UseRouting();
                app.UseEndpoints(endpoints => {
                    endpoints.MapPost("/login", async context => {
                        var form = await context.Request.ReadFormAsync();
                        string user = form["user"];
                        string pass = form["pass"];
                        await context.Response.WriteAsync($"{user}:{pass}");
                    });
                });
            });
        return new TestServer(builder);
    }

    [Fact]
    public async Task SubmitAsync_PostsForm() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();

        var fields = new Dictionary<string, string> {
            ["user"] = "admin",
            ["pass"] = "secret"
        };

        string result = await HtmlFormSubmitter.SubmitAsync(server.BaseAddress + "login", "POST", fields, client);
        Assert.Equal("admin:secret", result);
    }
}