using HtmlTinkerX;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace HtmlTinkerX.Tests;

public class HtmlResourceDownloadTests {
    private static TestServerFixture CreateServer() {
        return TestServerCompat.CreateTestServer(async ctx => {
            string content = ctx.Request.Path.Value switch {
                "/file1.txt" => "file1",
                "/file2.js" => "file2",
                _ => string.Empty
            };
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            await ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }, null, null);
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

    [Fact]
    public async Task DownloadResourcesAsync_StripsQueryAndFragmentFromFileName() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();
        var links = new List<HtmlResourceLink> {
            new() { Source = "file1.txt?x=1#frag" },
            new() { Source = "file2.js?y=2#frag" }
        };
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        List<string> paths = await HtmlResourceParser.DownloadResourcesAsync(links, server.BaseAddress!, dir, client);

        Assert.Contains(Path.Combine(dir, "file1.txt"), paths);
        Assert.Contains(Path.Combine(dir, "file2.js"), paths);
        string c1 = File.ReadAllText(Path.Combine(dir, "file1.txt"));
        string c2 = File.ReadAllText(Path.Combine(dir, "file2.js"));
        Assert.Equal("file1", c1);
        Assert.Equal("file2", c2);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task SaveAsync_RejectsResourceAboveConfiguredLimitWithoutLeavingPartialFile() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();
        HtmlResourceLink link = new() { Source = server.BaseAddress + "file1.txt" };
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<InvalidDataException>(() => link.SaveAsync(
            dir,
            client: client,
            fetchOptions: new HtmlHttpFetchOptions { MaximumResponseBytes = 4 }));

        Assert.False(File.Exists(Path.Combine(dir, "file1.txt")));
        if (Directory.Exists(dir)) {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsync_FailedDownloadPreservesExistingDestination() {
        using HttpClient client = new(new StaticResponseHandler(new UnknownLengthContent("file1")));
        HtmlResourceLink link = new() { Source = "https://example.test/file1.txt", Name = "file1.txt" };
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        string destination = Path.Combine(dir, "file1.txt");
        File.WriteAllText(destination, "existing");

        await Assert.ThrowsAsync<InvalidDataException>(() => link.SaveAsync(
            dir,
            client: client,
            fetchOptions: new HtmlHttpFetchOptions { MaximumResponseBytes = 4 }));

        Assert.Equal("existing", File.ReadAllText(destination));
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task DownloadResourcesAsync_HonorsCancellation() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();
        var links = new List<HtmlResourceLink> { new() { Source = "file1.txt" } };
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlResourceParser.DownloadResourcesAsync(
            links,
            server.BaseAddress!,
            dir,
            client,
            cancellationToken: cancellation.Token));

        if (Directory.Exists(dir)) {
            Directory.Delete(dir, true);
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler {
        private readonly HttpContent _content;

        public StaticResponseHandler(HttpContent content) {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = _content });
        }
    }

    private sealed class UnknownLengthContent : HttpContent {
        private readonly byte[] _content;

        public UnknownLengthContent(string content) {
            _content = Encoding.UTF8.GetBytes(content);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) {
            return stream.WriteAsync(_content, 0, _content.Length);
        }

        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }
    }
}
