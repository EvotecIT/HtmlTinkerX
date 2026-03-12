using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Provides reusable crawl profiles for known sites and site families.
/// </summary>
public static class HtmlCrawlProfiles {
    private static readonly IReadOnlyList<HtmlCrawlProfile> BuiltInProfiles = new[] {
        new HtmlCrawlProfile {
            Name = "wordpress-content",
            Selector = "main",
            ExcludeSelectors = {
                ".wpml-ls",
                ".wpml-ls-statics-footer",
                ".sharing-popup",
                ".post-footer-sharing",
                ".socials-sharing",
                ".language-switcher",
                ".related-posts",
                ".related-posts-list",
                ".breadcrumbs",
                ".breadcrumb",
                ".comments-area",
                ".comment-respond",
                ".newsletter",
                ".cookie-banner"
            },
            DismissTexts = {
                "Accept",
                "I agree"
            },
            ClickTexts = {
                "Load more",
                "Show more"
            },
            InteractionRepeatCount = 2
        },
        new HtmlCrawlProfile {
            Name = "evotec-xyz",
            Hosts = {
                "evotec.xyz",
                "www.evotec.xyz"
            },
            Selector = "main",
            WaitForSelector = "#main",
            ExcludeSelectors = {
                ".wpml-ls",
                ".wpml-ls-statics-footer",
                ".sharing-popup",
                ".post-footer-sharing",
                ".socials-sharing",
                ".language-switcher",
                ".related-posts",
                ".related-posts-list",
                ".breadcrumbs",
                ".breadcrumb",
                ".comment-respond",
                ".comments-area"
            },
            DismissTexts = {
                "Accept",
                "I agree"
            },
            ClickTexts = {
                "Load more",
                "Show more"
            },
            InteractionRepeatCount = 2
        }
    };

    /// <summary>Gets the built-in crawl profiles.</summary>
    public static IReadOnlyList<HtmlCrawlProfile> Defaults => BuiltInProfiles;

    internal static HtmlCrawlProfile? Resolve(string? profileName, Uri startUri, bool autoProfile) {
        if (!string.IsNullOrWhiteSpace(profileName)) {
            return BuiltInProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!autoProfile) {
            return null;
        }

        string host = startUri.Host;
        return BuiltInProfiles.FirstOrDefault(profile =>
            profile.Hosts.Any(profileHost => string.Equals(profileHost, host, StringComparison.OrdinalIgnoreCase)));
    }

    internal static void Apply(HtmlCrawlOptions options, HtmlCrawlProfile profile) {
        HtmlCrawlOptions defaults = new();

        if (string.IsNullOrWhiteSpace(options.Selector) && !string.IsNullOrWhiteSpace(profile.Selector)) {
            options.Selector = profile.Selector;
        }
        if (string.IsNullOrWhiteSpace(options.WaitForSelector) && !string.IsNullOrWhiteSpace(profile.WaitForSelector)) {
            options.WaitForSelector = profile.WaitForSelector;
        }
        if (string.IsNullOrWhiteSpace(options.PathPrefix) && !string.IsNullOrWhiteSpace(profile.PathPrefix)) {
            options.PathPrefix = profile.PathPrefix;
        }
        if (!options.AutoRender && profile.AutoRender) {
            options.AutoRender = true;
        }
        if (!options.AutoScroll && profile.AutoScroll) {
            options.AutoScroll = true;
        }
        if (options.InteractionRepeatCount == defaults.InteractionRepeatCount && profile.InteractionRepeatCount.HasValue && profile.InteractionRepeatCount.Value > 0) {
            options.InteractionRepeatCount = profile.InteractionRepeatCount.Value;
        }

        AppendDistinct(options.ExcludeSelectors, profile.ExcludeSelectors);
        AppendDistinct(options.ClickSelectors, profile.ClickSelectors);
        AppendDistinct(options.ClickTexts, profile.ClickTexts);
        AppendDistinct(options.DismissSelectors, profile.DismissSelectors);
        AppendDistinct(options.DismissTexts, profile.DismissTexts);
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
