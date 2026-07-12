using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Applies intent-focused crawl defaults that make common workflows easier to start without hand-tuning many switches.
/// </summary>
public static class HtmlCrawlScenarios {
    /// <summary>
    /// Applies scenario defaults to the provided options while preserving values the caller explicitly supplied, including values equal to library defaults.
    /// </summary>
    /// <param name="options">Options to update.</param>
    /// <param name="scenario">Scenario whose defaults should be applied.</param>
    public static void Apply(HtmlCrawlOptions options, HtmlCrawlScenario scenario) {
        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }
        if (scenario == HtmlCrawlScenario.Custom) {
            return;
        }

        options.ApplyScenarioDefaults(() => {
            switch (scenario) {
                case HtmlCrawlScenario.Content:
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.ContentMode))) {
                        options.ContentMode = HtmlCrawlContentMode.Reader;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.StructuredJsonPreset))) {
                        options.StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Article;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.UseCanonicalUrls))) {
                        options.UseCanonicalUrls = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.DeduplicatePages))) {
                        options.DeduplicatePages = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.ReaderMinimumWordCount))) {
                        options.ReaderMinimumWordCount = 30;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.ReaderMinimumScore))) {
                        options.ReaderMinimumScore = 30;
                    }
                    break;

                case HtmlCrawlScenario.Archive:
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.DownloadAssets))) {
                        options.DownloadAssets = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.UseCanonicalUrls))) {
                        options.UseCanonicalUrls = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.SmartContentCleanup))) {
                        options.SmartContentCleanup = false;
                    }
                    break;

                case HtmlCrawlScenario.Docs:
                    HtmlCrawlProfile? docsProfile = HtmlCrawlProfiles.ResolveByName("docs-content");
                    if (docsProfile != null) {
                        HtmlCrawlProfiles.Apply(options, docsProfile);
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.UseCanonicalUrls))) {
                        options.UseCanonicalUrls = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.DeduplicatePages))) {
                        options.DeduplicatePages = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.StructuredJsonPreset))) {
                        options.StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Docs;
                    }
                    break;

                case HtmlCrawlScenario.Dataset:
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.ContentMode))) {
                        options.ContentMode = HtmlCrawlContentMode.Reader;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.IncludeMarkdown))) {
                        options.IncludeMarkdown = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.IncludeStructuredJson))) {
                        options.IncludeStructuredJson = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.StructuredJsonPreset))) {
                        options.StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Auto;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.CompareContentModes))) {
                        options.CompareContentModes = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.UseCanonicalUrls))) {
                        options.UseCanonicalUrls = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.DeduplicatePages))) {
                        options.DeduplicatePages = true;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.ReaderMinimumWordCount))) {
                        options.ReaderMinimumWordCount = 30;
                    }
                    if (!options.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.ReaderMinimumScore))) {
                        options.ReaderMinimumScore = 30;
                    }
                    AppendDistinct(options.ExcludeSelectors, new[] {
                        ".breadcrumbs",
                        ".breadcrumb",
                        ".related-posts",
                        ".comments-area"
                    });
                    break;
            }
        });
    }

    private static void AppendDistinct(IList<string> target, IEnumerable<string> source) {
        HashSet<string> existing = new(target.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
        foreach (string value in source.Where(value => !string.IsNullOrWhiteSpace(value))) {
            if (existing.Add(value)) {
                target.Add(value);
            }
        }
    }
}
