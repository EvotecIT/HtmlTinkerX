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
}