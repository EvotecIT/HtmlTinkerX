using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Applies intent-focused crawl defaults that make common workflows easier to start without hand-tuning many switches.
/// </summary>
public static class HtmlCrawlScenarios {
    /// <summary>
    /// Applies scenario defaults to the provided options while preserving any values the caller already changed away from the library defaults.
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

        HtmlCrawlOptions defaults = new();
        switch (scenario) {
            case HtmlCrawlScenario.Content:
                if (options.ContentMode == defaults.ContentMode) {
                    options.ContentMode = HtmlCrawlContentMode.Reader;
                }
                if (options.StructuredJsonPreset == defaults.StructuredJsonPreset) {
                    options.StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Article;
                }
                if (!options.UseCanonicalUrls && options.UseCanonicalUrls == defaults.UseCanonicalUrls) {
                    options.UseCanonicalUrls = true;
                }
                if (!options.DeduplicatePages && options.DeduplicatePages == defaults.DeduplicatePages) {
                    options.DeduplicatePages = true;
                }
                if (options.ReaderMinimumWordCount == defaults.ReaderMinimumWordCount) {
                    options.ReaderMinimumWordCount = 30;
                }
                if (Math.Abs(options.ReaderMinimumScore - defaults.ReaderMinimumScore) < 0.0001) {
                    options.ReaderMinimumScore = 30;
                }
                break;

            case HtmlCrawlScenario.Archive:
                if (!options.DownloadAssets && options.DownloadAssets == defaults.DownloadAssets) {
                    options.DownloadAssets = true;
                }
                if (!options.UseCanonicalUrls && options.UseCanonicalUrls == defaults.UseCanonicalUrls) {
                    options.UseCanonicalUrls = true;
                }
                if (options.SmartContentCleanup == defaults.SmartContentCleanup) {
                    options.SmartContentCleanup = false;
                }
                break;

            case HtmlCrawlScenario.Docs:
                HtmlCrawlProfile? docsProfile = HtmlCrawlProfiles.ResolveByName("docs-content");
                if (docsProfile != null) {
                    HtmlCrawlProfiles.Apply(options, docsProfile);
                }
                if (!options.UseCanonicalUrls && options.UseCanonicalUrls == defaults.UseCanonicalUrls) {
                    options.UseCanonicalUrls = true;
                }
                if (!options.DeduplicatePages && options.DeduplicatePages == defaults.DeduplicatePages) {
                    options.DeduplicatePages = true;
                }
                if (options.StructuredJsonPreset == defaults.StructuredJsonPreset) {
                    options.StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Docs;
                }
                break;

            case HtmlCrawlScenario.Dataset:
                if (options.ContentMode == defaults.ContentMode) {
                    options.ContentMode = HtmlCrawlContentMode.Reader;
                }
                if (!options.IncludeMarkdown && options.IncludeMarkdown == defaults.IncludeMarkdown) {
                    options.IncludeMarkdown = true;
                }
                if (!options.IncludeStructuredJson && options.IncludeStructuredJson == defaults.IncludeStructuredJson) {
                    options.IncludeStructuredJson = true;
                }
                if (options.StructuredJsonPreset == defaults.StructuredJsonPreset) {
                    options.StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Auto;
                }
                if (!options.CompareContentModes && options.CompareContentModes == defaults.CompareContentModes) {
                    options.CompareContentModes = true;
                }
                if (!options.UseCanonicalUrls && options.UseCanonicalUrls == defaults.UseCanonicalUrls) {
                    options.UseCanonicalUrls = true;
                }
                if (!options.DeduplicatePages && options.DeduplicatePages == defaults.DeduplicatePages) {
                    options.DeduplicatePages = true;
                }
                if (options.ReaderMinimumWordCount == defaults.ReaderMinimumWordCount) {
                    options.ReaderMinimumWordCount = 30;
                }
                if (Math.Abs(options.ReaderMinimumScore - defaults.ReaderMinimumScore) < 0.0001) {
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
