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
        int idx = html.IndexOf("const har =", StringComparison.Ordinal);
        Assert.NotEqual(-1, idx);
        int start = html.IndexOf('{', idx);
        int end = html.IndexOf("};", start, StringComparison.Ordinal);
        string json = html.Substring(start, end - start + 1);
        var opts = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Har? parsed = JsonSerializer.Deserialize<Har>(json, opts);
        Assert.NotNull(parsed);
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
}