using HtmlTinkerX;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace HtmlTinkerX.Tests;

public class HtmlUtilitiesTests {
    private readonly ITestOutputHelper _output;

    public HtmlUtilitiesTests(ITestOutputHelper output) => _output = output;
    private static TestServer CreateServer(string content, string charset) {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                ctx.Response.ContentType = $"text/plain; charset={charset}";
                var bytes = System.Text.Encoding.GetEncoding(charset).GetBytes(content);
                await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length);
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
    public void ToFullPath_ReturnsSameAsResolvePath() {
        string temp = Path.GetTempPath();
        Environment.SetEnvironmentVariable("TMP_TEST", temp);
        string expected = HtmlUtilities.ResolvePath("%TMP_TEST%");
        string actual = "%TMP_TEST%".ToFullPath();
        Assert.Equal(expected, actual);
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
#if FRAMEWORK
            await WriteAllTextAsync(path, "async");
#else
            await File.WriteAllTextAsync(path, "async");
#endif
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
                await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length);
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
                await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            }));
        using var server = new TestServer(builder);
        using HttpClient client = server.CreateClient();
        string result = await HtmlUtilities.GetStringWithProperEncodingAsync(client, server.BaseAddress.ToString());
        Assert.Equal("<meta charset=\"UTF-16\">Hello", result);
    }

    [Fact]
    public void EnsureDirectoryExists_CreatesMissingDirectory() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try {
            string full = HtmlUtilities.EnsureDirectoryExists(dir);
            Assert.True(Directory.Exists(full));
        } finally {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void EnsureDirectoryExists_CreatesParentForFile() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "test.txt");
        try {
            string full = HtmlUtilities.EnsureDirectoryExists(file);
            Assert.True(Directory.Exists(Path.GetDirectoryName(full)!));
            Assert.Equal(Path.GetFullPath(file), full);
        } finally {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RemoveRedundantWhitespace_CollapsesWhitespace() {
        const string html = "<div>   Hello  </div>  <span>  World</span>";
        string result = HtmlUtilities.RemoveRedundantWhitespace(html);
        Assert.Equal("<div> Hello </div><span> World</span>", result);
    }

    [Fact]
    public void RemoveRedundantWhitespace_PerformanceBenchmark() {
        const string html = "<div>   Hello  </div>  <span>  World</span>";
        const int iterations = 10000;

        static string OldRemove(string h) {
            string collapsed = Regex.Replace(h, "\\s+", " ");
            collapsed = Regex.Replace(collapsed, @">\s+<", "><");
            return collapsed.Trim();
        }

        OldRemove(html);
        HtmlUtilities.RemoveRedundantWhitespace(html);

        var oldWatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) {
            OldRemove(html);
        }
        oldWatch.Stop();

        var newWatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) {
            HtmlUtilities.RemoveRedundantWhitespace(html);
        }
        newWatch.Stop();

        _output.WriteLine($"Old: {oldWatch.ElapsedMilliseconds} ms, New: {newWatch.ElapsedMilliseconds} ms");

        TimeSpan tolerance = TimeSpan.FromMilliseconds(oldWatch.Elapsed.TotalMilliseconds * 0.05 + 1);
        Assert.True(newWatch.Elapsed <= oldWatch.Elapsed + tolerance);
    }
}