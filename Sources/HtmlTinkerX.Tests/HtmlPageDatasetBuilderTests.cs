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
        Assert.DoesNotContain(chunk.Provenance, entry => (entry.Url ?? string.Empty).Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Build_ChunksMarkdownInsteadOfRepeatingFullDocument() {
        string paragraph = string.Join(" ", Enumerable.Range(1, 130).Select(index => $"word{index}"));
        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            $"<html><body><main><h1>Long</h1><p>{paragraph}</p></main></body></html>",
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/long")
            });

        IReadOnlyList<HtmlPageDatasetChunk> chunks = HtmlPageDatasetBuilder.Build(
            workbench,
            new HtmlPageDatasetOptions {
                MaxChunkWords = 50
            });

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Markdown.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length <= 55));
        Assert.NotEqual(chunks[0].Markdown, chunks[1].Markdown);
    }

    [Fact]
    public void Build_RedactsSensitiveMarkdownLinkTargets() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            Title = "Markdown",
            AnalysisMode = "Static",
            Markdown = "[Reset](https://example.org/reset?token=abc123) ![Chart](https://example.org/chart.png?access_token=def456)",
            ReadableText = new HtmlReadableTextResult {
                Text = "Markdown link page.",
                Title = "Markdown"
            }
        };

        HtmlPageDatasetChunk chunk = Assert.Single(HtmlPageDatasetBuilder.Build(workbench));

        Assert.Contains("token=<redacted>", chunk.Markdown);
        Assert.Contains("access_token=<redacted>", chunk.Markdown);
        Assert.DoesNotContain("abc123", chunk.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("def456", chunk.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_RedactsSensitiveScriptStateProvenance() {
        string html = """
<html>
<head>
<script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"sessionToken":"abc123","redirectUrl":"https:\/\/user:pass@example.org\/callback","next":"/callback?access_token=url456","safe":"ok"}}}</script>
</head>
<body><main><h1>State</h1><p>State payload page.</p></main></body>
</html>
""";
        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/state")
            });

        HtmlPageDatasetChunk chunk = Assert.Single(HtmlPageDatasetBuilder.Build(workbench));
        string provenanceText = string.Join(" ", chunk.Provenance.Select(entry => entry.Url));

        Assert.Contains("<redacted>", provenanceText);
        Assert.DoesNotContain("abc123", provenanceText, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", provenanceText, StringComparison.Ordinal);
        Assert.DoesNotContain("url456", provenanceText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsMicrodataStructuredProvenance() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            Title = "Microdata",
            AnalysisMode = "Static",
            ReadableText = new HtmlReadableTextResult {
                Text = "Microdata page.",
                Title = "Microdata"
            },
            Data = new[] {
                new HtmlDataItem {
                    Kind = "Microdata",
                    Name = "Profile",
                    RawValue = "{\"url\":[\"https://user:pass@example.org/profile\"]}",
                    Source = "Microdata"
                }
            }
        };

        HtmlPageDatasetChunk chunk = Assert.Single(HtmlPageDatasetBuilder.Build(workbench));
        string provenanceText = string.Join(" ", chunk.Provenance.Select(entry => entry.Url));

        Assert.Contains("<redacted>@example.org", provenanceText);
        Assert.DoesNotContain("user:pass", provenanceText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsJsonEscapedStructuredUrlSeparators() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            Title = "App State",
            AnalysisMode = "Static",
            ReadableText = new HtmlReadableTextResult {
                Text = "App state page.",
                Title = "App State"
            },
            Data = new[] {
                new HtmlDataItem {
                    Kind = "AppState",
                    Name = "__NEXT_DATA__",
                    RawValue = "{\"redirectUrl\":\"/callback?state=ok\\u0026access_token=escaped789\"}",
                    Source = "AppState"
                }
            }
        };

        HtmlPageDatasetChunk chunk = Assert.Single(HtmlPageDatasetBuilder.Build(workbench));
        string provenanceText = string.Join(" ", chunk.Provenance.Select(entry => entry.Url));

        Assert.Contains("access_token=<redacted>", provenanceText);
        Assert.DoesNotContain("escaped789", provenanceText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsSingleQuotedSensitiveStructuredKeys() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            Title = "App State",
            AnalysisMode = "Static",
            ReadableText = new HtmlReadableTextResult {
                Text = "App state page.",
                Title = "App State"
            },
            Data = new[] {
                new HtmlDataItem {
                    Kind = "AppState",
                    Name = "__APP_STATE__",
                    RawValue = "{'access_token':'abc123','safe':'ok'}",
                    Source = "AppState"
                }
            }
        };

        HtmlPageDatasetChunk chunk = Assert.Single(HtmlPageDatasetBuilder.Build(workbench));
        string provenanceText = string.Join(" ", chunk.Provenance.Select(entry => entry.Url));

        Assert.Contains("'access_token':\"<redacted>\"", provenanceText);
        Assert.DoesNotContain("abc123", provenanceText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsSensitiveChunkUrlsAndProvenanceNames() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page?access_token=abc123",
            FinalUrl = "https://example.org/final?token=def456",
            Title = "Sensitive URLs",
            AnalysisMode = "Static",
            ReadableText = new HtmlReadableTextResult {
                Text = "Sensitive URL page.",
                Title = "Sensitive URLs"
            },
            Data = new[] {
                new HtmlDataItem {
                    Kind = "Link",
                    Name = "https://example.org/reset?token=abc123",
                    RawValue = "https://example.org/reset?token=abc123",
                    Source = "Anchor"
                }
            }
        };

        HtmlPageDatasetChunk chunk = Assert.Single(HtmlPageDatasetBuilder.Build(workbench));
        string provenanceText = string.Join(" ", chunk.Provenance.Select(entry => string.Join(" ", entry.Name, entry.Url)));

        Assert.Contains("access_token=<redacted>", chunk.SourceUrl);
        Assert.Contains("token=<redacted>", chunk.FinalUrl);
        Assert.Contains("token=<redacted>", provenanceText);
        Assert.DoesNotContain("abc123", chunk.SourceUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("def456", chunk.FinalUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", provenanceText, StringComparison.Ordinal);
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
