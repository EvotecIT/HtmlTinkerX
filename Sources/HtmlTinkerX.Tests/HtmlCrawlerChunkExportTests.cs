using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlCrawlerChunkExportTests {
    [Fact]
    public async Task SaveResultAsync_ChunkRecordsKeepOrderOverlapAndGlobalDeduplication() {
        string[] firstWords = Enumerable.Range(0, 400).Select(index => "first-" + index).ToArray();
        string[] lastWords = Enumerable.Range(0, 160).Select(index => "last-" + index).ToArray();
        HtmlCrawlResult result = new() { StartUrl = "https://example.test/" };
        result.Pages.Add(new HtmlCrawlPage { Url = "https://example.test/first", Text = string.Join(" ", firstWords) });
        result.Pages.Add(new HtmlCrawlPage { Url = "https://example.test/duplicate", Text = string.Join(" ", firstWords) });
        result.Pages.Add(new HtmlCrawlPage { Url = "https://example.test/last", Text = string.Join(" ", lastWords) });
        result.Pages.Add(new HtmlCrawlPage { Url = "https://example.test/failed", Text = "not exported", Status = HtmlCrawlPageStatus.Failed });
        string output = Path.Combine(Path.GetTempPath(), "HtmlCrawlerChunkExport", Guid.NewGuid().ToString("N"));
        try {
            await HtmlCrawler.SaveResultAsync(result, output);
            string[] records = File.ReadAllLines(result.ChunksJsonlPath!);
            Assert.Equal(6, records.Length);
            for (int index = 0; index < records.Length; index++) {
                using JsonDocument record = JsonDocument.Parse(records[index]);
                JsonElement chunk = record.RootElement;
                Assert.Equal($"chunk-{index + 1:D5}", chunk.GetProperty("ChunkId").GetString());
                bool first = index < 4;
                int pageIndex = first ? index : index - 4;
                string[] words = first ? firstWords : lastWords;
                string expected = string.Join(" ", words.Skip(pageIndex * 110).Take(140));
                Assert.Equal(first ? result.Pages[0].Url : result.Pages[2].Url, chunk.GetProperty("Url").GetString());
                Assert.Equal(pageIndex + 1, chunk.GetProperty("ChunkIndex").GetInt32());
                Assert.Equal(expected, chunk.GetProperty("Text").GetString());
            }
            Assert.Equal(6, result.ChunkCount);
            Assert.Equal(6, result.Summary.ChunkCount);
            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(output);
            Assert.Equal(6, loaded.ChunkCount);
            Assert.Equal(6, loaded.Summary.ChunkCount);
        } finally {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }
}
