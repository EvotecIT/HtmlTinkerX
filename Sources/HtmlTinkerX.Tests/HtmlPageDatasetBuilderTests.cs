using System.Text.Json;

namespace HtmlTinkerX.Tests;

public class HtmlPageDatasetBuilderTests {
    [Fact]
    public async Task Build_CreatesChunksWithProvenanceAndRedactionHints() {
        string html = """
<html>
<head>
<title>Dataset Demo</title>
<meta property="og:title" content="Dataset Demo">
<script>fetch("/api/items");</script>
</head>
<body>
<main>
<h1>Dataset Demo</h1>
<p>One two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty.</p>
<p>Twenty one twenty two twenty three twenty four twenty five twenty six twenty seven twenty eight twenty nine thirty.</p>
<form method="post" action="/submit"><input type="hidden" name="csrf" value="secret"><input name="user"></form>
</main>
</body>
</html>
""";
        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/dataset")
            });

        IReadOnlyList<HtmlPageDatasetChunk> chunks = HtmlPageDatasetBuilder.Build(
            workbench,
            new HtmlPageDatasetOptions {
                MaxChunkWords = 50
            });

        HtmlPageDatasetChunk chunk = Assert.Single(chunks);
        Assert.Equal("page-chunk-0001", chunk.ChunkId);
        Assert.Equal("https://example.org/dataset", chunk.SourceUrl);
        Assert.Equal("Dataset Demo", chunk.Title);
        Assert.Contains("Dataset Demo", chunk.Headings);
        Assert.Contains("OpenGraph", chunk.DataKinds);
        Assert.Equal(1, chunk.FormCount);
        Assert.True(chunk.EndpointCount > 0);
        Assert.Contains("hidden-form-fields", chunk.RedactionHints);
        Assert.Contains(chunk.Provenance, entry => entry.Kind == "ReadableText");
        Assert.Contains(chunk.Provenance, entry => entry.Kind == "Endpoint");
    }

    [Fact]
    public async Task ToJsonLines_SerializesOneChunkPerLine() {
        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            "<html><body><main><h1>JSONL</h1><p>This page is small but still becomes one dataset record.</p></main></body></html>",
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/jsonl")
            });
        IReadOnlyList<HtmlPageDatasetChunk> chunks = HtmlPageDatasetBuilder.Build(workbench);

        string jsonl = HtmlPageDatasetBuilder.ToJsonLines(chunks);
        string[] lines = jsonl.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        string line = Assert.Single(lines);
        using JsonDocument document = JsonDocument.Parse(line);
        Assert.Equal("page-chunk-0001", document.RootElement.GetProperty("ChunkId").GetString());
        Assert.Equal("https://example.org/jsonl", document.RootElement.GetProperty("SourceUrl").GetString());
    }
}
