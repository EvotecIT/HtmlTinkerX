using HtmlTinkerX;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlUtilitiesTests {
    private static TestServer CreateServer(string content, string charset) {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                ctx.Response.ContentType = $"text/plain; charset={charset}";
                var bytes = System.Text.Encoding.GetEncoding(charset).GetBytes(content);
                await ctx.Response.Body.WriteAsync(bytes);
            }));
        return new TestServer(builder);
    }

    [Fact]
    public void ResolvePath_ExpandsEnvironmentVariables() {
        string temp = Path.GetTempPath();
        Environment.SetEnvironmentVariable("TMP_TEST", temp);
        string result = HtmlUtilities.ResolvePath("%TMP_TEST%");
        Assert.Equal(Path.GetFullPath(temp), result);
    }

    [Fact]
    public void ResolvePath_RelativePathToAbsolute() {
        string relative = "..";
        string result = HtmlUtilities.ResolvePath(relative);
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void ReadFileChecked_ReturnsContent() {
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, "data");
            string content = HtmlUtilities.ReadFileChecked(path);
            Assert.Equal("data", content);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFileChecked_MissingThrows() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
        Assert.Throws<FileNotFoundException>(() => HtmlUtilities.ReadFileChecked(path));
    }

    [Fact]
    public async Task ReadFileCheckedAsync_ReturnsContent() {
        string path = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(path, "async");
            string content = await HtmlUtilities.ReadFileCheckedAsync(path);
            Assert.Equal("async", content);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadFileCheckedAsync_MissingThrows() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
        await Assert.ThrowsAsync<FileNotFoundException>(() => HtmlUtilities.ReadFileCheckedAsync(path));
    }

    [Fact]
    public async Task GetStringWithProperEncodingAsync_UsesHeaderEncoding() {
        using var server = CreateServer("Hello", "utf-16");
        using HttpClient client = server.CreateClient();
        string result = await HtmlUtilities.GetStringWithProperEncodingAsync(client, server.BaseAddress.ToString());
        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task GetStringWithProperEncodingAsync_TrimsQuotedCharset() {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                ctx.Response.ContentType = "text/plain; charset=\"utf-8\"";
                var bytes = System.Text.Encoding.UTF8.GetBytes("Hello");
                await ctx.Response.Body.WriteAsync(bytes);
            }));
        using var server = new TestServer(builder);
        using HttpClient client = server.CreateClient();
        string result = await HtmlUtilities.GetStringWithProperEncodingAsync(client, server.BaseAddress.ToString());
        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task GetStringWithProperEncodingAsync_DetectsMetaCharset() {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                ctx.Response.ContentType = "text/html";
                const string html = "<meta charset=\"UTF-16\">Hello";
                byte[] preamble = System.Text.Encoding.Unicode.GetPreamble();
                byte[] data = System.Text.Encoding.Unicode.GetBytes(html);
                byte[] bytes = new byte[preamble.Length + data.Length];
                preamble.CopyTo(bytes, 0);
                data.CopyTo(bytes, preamble.Length);
                await ctx.Response.Body.WriteAsync(bytes);
            }));
        using var server = new TestServer(builder);
        using HttpClient client = server.CreateClient();
        string result = await HtmlUtilities.GetStringWithProperEncodingAsync(client, server.BaseAddress.ToString());
        Assert.Equal("<meta charset=\"UTF-16\">Hello", result);
    }
}
