using HtmlTinkerX;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlFormSubmitterTests {
    private static TestServer CreateServer() {
        return TestServerCompat.CreateFormTestServer();
    }

    [Fact]
    public async Task SubmitAsync_PostsForm() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();

        var fields = new Dictionary<string, string> {
            ["user"] = "admin",
            ["pass"] = "secret"
        };

        string result = await HtmlFormSubmitter.SubmitAsync(server.BaseAddress + "login", FormMethod.Post, fields, client);
        Assert.Equal("admin:secret", result);
    }

    [Fact]
    public async Task SubmitAsync_GetsFormWithExistingQuery() {
        using var server = TestServerCompat.CreateTestServer(async context => {
            string user = context.Request.Query["user"].ToString();
            string pass = context.Request.Query["pass"].ToString();
            string existing = context.Request.Query["existing"].ToString();
            await context.Response.WriteAsync($"{user}:{pass}:{existing}");
        }, "/login", "GET");
        using HttpClient client = server.CreateClient();

        var fields = new Dictionary<string, string> {
            ["user"] = "admin",
            ["pass"] = "secret"
        };

        string action = server.BaseAddress + "login?existing=value";
        string result = await HtmlFormSubmitter.SubmitAsync(action, FormMethod.Get, fields, client);
        Assert.Equal("admin:secret:value", result);
    }

    [Fact]
    public async Task SubmitAsync_CanceledToken_Throws() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();

        var fields = new Dictionary<string, string> {
            ["user"] = "admin",
            ["pass"] = "secret"
        };

        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            HtmlFormSubmitter.SubmitAsync(server.BaseAddress + "login", FormMethod.Post, fields, client, cts.Token));
    }
}