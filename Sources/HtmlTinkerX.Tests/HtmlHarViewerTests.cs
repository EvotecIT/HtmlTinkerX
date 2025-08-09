using HtmlTinkerX;
using System.Text.Json;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests for <see cref="HtmlHarViewer"/> helper methods.
/// </summary>
public class HtmlHarViewerTests {
    private static string GetHarPath() {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Tests", "Documents", "sample.har"));
    }

    private static string GetMinimalHarPath() {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Tests", "Documents", "minimal.har"));
    }

    [Fact]
    /// <summary>
    /// Generates HTML viewer content from a HAR file.
    /// </summary>
    public async Task BuildViewerHtml_ReturnsHtml() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetHarPath());
        string html = HtmlHarViewer.BuildViewerHtml(har);
        Assert.Contains("<table>", html);
    }

    [Fact]
    /// <summary>
    /// Ensures the generated viewer embeds valid JSON data.
    /// </summary>
    public async Task BuildViewerHtml_EmbedsValidJson() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetMinimalHarPath());
        string html = HtmlHarViewer.BuildViewerHtml(har);
        string marker = "<script type='application/json' id='har-data'>";
        int start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.NotEqual(-1, start);
        start += marker.Length;
        int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        string json = html.Substring(start, end - start);
        var opts = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Har? parsed = JsonSerializer.Deserialize<Har>(json, opts);
        Assert.NotNull(parsed);
    }

    [Fact]
    /// <summary>
    /// Ensures malicious input is safely encoded in the viewer.
    /// </summary>
    public void BuildViewerHtml_EncodesMaliciousInput() {
        var har = new Har {
            Log = new HarLog {
                Entries = new[] {
                    new HarEntry {
                        StartedDateTime = DateTime.UtcNow,
                        Request = new HarRequest {
                            Method = "GET",
                            Url = "</script><script>alert('x')</script>"
                        },
                        Response = new HarResponse {
                            Status = 200
                        }
                    }
                }
            }
        };

        string html = HtmlHarViewer.BuildViewerHtml(har);
        Assert.DoesNotContain("</script><script>alert('x')</script>", html, StringComparison.Ordinal);

        string marker = "<script type='application/json' id='har-data'>";
        int start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        int end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        string json = html.Substring(start, end - start);

        var opts = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Har? parsed = JsonSerializer.Deserialize<Har>(json, opts);
        Assert.NotNull(parsed);
        Assert.Equal("</script><script>alert('x')</script>", parsed.Log!.Entries![0].Request!.Url);
    }

    [Fact]
    /// <summary>
    /// Reads a minimal HAR file and populates entries.
    /// </summary>
    public async Task ReadHarAsync_PopulatesEntries() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetMinimalHarPath());
        Assert.NotNull(har.Log);
        Assert.NotNull(har.Log!.Entries);
        Assert.NotEmpty(har.Log.Entries);
    }

    [Fact]
    /// <summary>
    /// Verifies invalid JSON causes <see cref="InvalidDataException"/>.
    /// </summary>
    public async Task ReadHarAsync_InvalidJsonThrows() {
        string path = Path.GetTempFileName();
        try {
#if FRAMEWORK
            await WriteAllTextAsync(path, "{ invalid ");
#else
            await File.WriteAllTextAsync(path, "{ invalid ");
#endif
            await Assert.ThrowsAsync<InvalidDataException>(() => HtmlHarViewer.ReadHarAsync(path));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    /// <summary>
    /// Ensures missing files throw <see cref="FileNotFoundException"/>.
    /// </summary>
    public async Task ReadHarAsync_FileNotFoundThrows() {
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".har");
        await Assert.ThrowsAsync<FileNotFoundException>(() => HtmlHarViewer.ReadHarAsync(missingPath));
    }

    [Fact]
    /// <summary>
    /// Serializes a HAR to a stream.
    /// </summary>
    public async Task WriteHarAsync_WritesJson() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetMinimalHarPath());
        using var ms = new MemoryStream();
        await HtmlHarViewer.WriteHarAsync(har, ms);
        ms.Position = 0;
        using var reader = new StreamReader(ms);
        string json = await reader.ReadToEndAsync();
        var opts = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Har? parsed = JsonSerializer.Deserialize<Har>(json, opts);
        Assert.NotNull(parsed);
        Assert.Equal(har.Log?.Entries?.Length, parsed.Log?.Entries?.Length);
    }

    [Fact]
    public async Task WriteHarAsync_AllowsNullLog() {
        var har = new Har { Log = null };
        using var ms = new MemoryStream();
        await HtmlHarViewer.WriteHarAsync(har, ms);
        ms.Position = 0;
        using var reader = new StreamReader(ms);
        string json = await reader.ReadToEndAsync();
        var opts = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Har? parsed = JsonSerializer.Deserialize<Har>(json, opts);
        Assert.NotNull(parsed);
        Assert.NotNull(parsed.Log);
    }
}