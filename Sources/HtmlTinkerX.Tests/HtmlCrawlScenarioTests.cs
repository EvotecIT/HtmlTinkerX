using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlCrawlScenarioTests {
    [Fact]
    public void ApplyArchiveDocsAndDatasetScenarios_UsesIntentDefaults() {
        HtmlCrawlOptions archive = new();
        HtmlCrawlScenarios.Apply(archive, HtmlCrawlScenario.Archive);
        Assert.True(archive.DownloadAssets);
        Assert.True(archive.UseCanonicalUrls);
        Assert.False(archive.SmartContentCleanup);

        HtmlCrawlOptions docs = new();
        HtmlCrawlScenarios.Apply(docs, HtmlCrawlScenario.Docs);
        Assert.Equal(HtmlCrawlContentMode.Reader, docs.ContentMode);
        Assert.True(docs.CompareContentModes);
        Assert.True(docs.DeduplicatePages);
        Assert.Equal(HtmlCrawlStructuredJsonPreset.Docs, docs.StructuredJsonPreset);
        Assert.Contains(".theme-doc-toc-desktop", docs.ExcludeSelectors);

        HtmlCrawlOptions dataset = new();
        HtmlCrawlScenarios.Apply(dataset, HtmlCrawlScenario.Dataset);
        Assert.Equal(HtmlCrawlContentMode.Reader, dataset.ContentMode);
        Assert.True(dataset.IncludeMarkdown);
        Assert.True(dataset.IncludeStructuredJson);
        Assert.Equal(HtmlCrawlStructuredJsonPreset.Auto, dataset.StructuredJsonPreset);
        Assert.True(dataset.CompareContentModes);
        Assert.True(dataset.UseCanonicalUrls);
        Assert.True(dataset.DeduplicatePages);
        Assert.Contains(".breadcrumbs", dataset.ExcludeSelectors);
    }

    [Fact]
    public void Apply_PreservesExplicitValuesEqualToLibraryDefaults() {
        HtmlCrawlOptions dataset = new HtmlCrawlOptions {
            ContentMode = HtmlCrawlContentMode.Focused,
            IncludeMarkdown = false,
            IncludeStructuredJson = false,
            StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.None,
            CompareContentModes = false,
            UseCanonicalUrls = false,
            DeduplicatePages = false,
            ReaderMinimumWordCount = 20,
            ReaderMinimumScore = 25
        }.Clone();

        HtmlCrawlScenarios.Apply(dataset, HtmlCrawlScenario.Dataset);

        Assert.Equal(HtmlCrawlContentMode.Focused, dataset.ContentMode);
        Assert.False(dataset.IncludeMarkdown);
        Assert.False(dataset.IncludeStructuredJson);
        Assert.Equal(HtmlCrawlStructuredJsonPreset.None, dataset.StructuredJsonPreset);
        Assert.False(dataset.CompareContentModes);
        Assert.False(dataset.UseCanonicalUrls);
        Assert.False(dataset.DeduplicatePages);
        Assert.Equal(20, dataset.ReaderMinimumWordCount);
        Assert.Equal(25, dataset.ReaderMinimumScore);

        HtmlCrawlOptions archive = new() {
            DownloadAssets = false,
            UseCanonicalUrls = false,
            SmartContentCleanup = true
        };
        HtmlCrawlScenarios.Apply(archive, HtmlCrawlScenario.Archive);

        Assert.False(archive.DownloadAssets);
        Assert.False(archive.UseCanonicalUrls);
        Assert.True(archive.SmartContentCleanup);

        HtmlCrawlOptions docs = new() {
            ContentMode = HtmlCrawlContentMode.Focused,
            CompareContentModes = false
        };
        HtmlCrawlScenarios.Apply(docs, HtmlCrawlScenario.Docs);

        Assert.Equal(HtmlCrawlContentMode.Focused, docs.ContentMode);
        Assert.False(docs.CompareContentModes);
    }
}
