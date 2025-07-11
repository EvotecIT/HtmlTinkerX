using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlResourceDownloadTests {
    private static TestServer CreateServer() {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                string content = ctx.Request.Path.Value switch {
                    "/file1.txt" => "file1",
                    "/file2.js" => "file2",
                    _ => string.Empty
                };
                await ctx.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(content));
            }));
        return new TestServer(builder);
    }

    [Fact]
    public async Task SaveAsync_DownloadsFile() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();
        HtmlResourceLink link = new() { Source = server.BaseAddress + "file1.txt" };
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        string path = await link.SaveAsync(dir, client: client);

        Assert.True(File.Exists(path));
        Assert.Equal("file1", File.ReadAllText(path));
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task DownloadResourcesAsync_DownloadsAllFiles() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();
        var links = new List<HtmlResourceLink> {
            new() { Source = "file1.txt" },
            new() { Source = "file2.js" }
        };
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        List<string> paths = await HtmlResourceParser.DownloadResourcesAsync(links, server.BaseAddress!, dir, client);

        Assert.Equal(2, paths.Count);
        foreach (string path in paths) {
            Assert.True(File.Exists(path));
        }
        string c1 = File.ReadAllText(Path.Combine(dir, "file1.txt"));
        string c2 = File.ReadAllText(Path.Combine(dir, "file2.js"));
        Assert.Equal("file1", c1);
        Assert.Equal("file2", c2);
        Directory.Delete(dir, true);
    }
}
