using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides reusable crawl profiles for known sites and site families.
/// </summary>
public static class HtmlCrawlProfiles {
    private sealed class HtmlCrawlProfileFile {
        public List<HtmlCrawlProfile> Profiles { get; set; } = new();
    }

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

    private static readonly IReadOnlyList<string> BuiltInProfileNames = BuiltInProfiles
        .Select(profile => profile.Name)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Gets the built-in crawl profiles.</summary>
    public static IReadOnlyList<HtmlCrawlProfile> Defaults => BuiltInProfiles;

    /// <summary>Gets the built-in crawl profile names.</summary>
    public static IReadOnlyList<string> Names => BuiltInProfileNames;

    /// <summary>Loads custom crawl profiles from a JSON file.</summary>
    /// <param name="path">Path to a JSON file containing a single profile, an array of profiles, or an object with a <c>profiles</c> array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalized custom crawl profiles.</returns>
    public static async Task<IReadOnlyList<HtmlCrawlProfile>> LoadFromPathAsync(string path, CancellationToken cancellationToken = default) {
        string json = await HtmlUtilities.ReadFileCheckedAsync(path, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonSerializerOptions options = new() {
            PropertyNameCaseInsensitive = true
        };

        List<HtmlCrawlProfile>? profiles = null;
        if (document.RootElement.ValueKind == JsonValueKind.Array) {
            profiles = JsonSerializer.Deserialize<List<HtmlCrawlProfile>>(json, options);
        } else if (document.RootElement.ValueKind == JsonValueKind.Object) {
            if (document.RootElement.TryGetProperty("profiles", out JsonElement profilesElement) && profilesElement.ValueKind == JsonValueKind.Array) {
                profiles = JsonSerializer.Deserialize<List<HtmlCrawlProfile>>(profilesElement.GetRawText(), options);
            } else {
                HtmlCrawlProfile? singleProfile = JsonSerializer.Deserialize<HtmlCrawlProfile>(json, options);
                profiles = singleProfile == null ? new List<HtmlCrawlProfile>() : new List<HtmlCrawlProfile> { singleProfile };
            }
        }

        if (profiles == null || profiles.Count == 0) {
            return Array.Empty<HtmlCrawlProfile>();
        }

        return profiles
            .Select(NormalizeProfile)
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .ToArray();
    }

    internal static IReadOnlyList<string> GetNames(IEnumerable<HtmlCrawlProfile>? additionalProfiles) {
        List<string> names = BuiltInProfileNames.ToList();
        if (additionalProfiles != null) {
            foreach (string profileName in additionalProfiles
                .Select(profile => profile.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) {
                if (!names.Contains(profileName, StringComparer.OrdinalIgnoreCase)) {
                    names.Add(profileName);
                }
            }
        }

        return names;
    }

    internal static HtmlCrawlProfile? ResolveByName(string? profileName, IEnumerable<HtmlCrawlProfile>? additionalProfiles = null) {
        if (string.IsNullOrWhiteSpace(profileName)) {
            return null;
        }

        string normalizedName = profileName.Trim();
        if (additionalProfiles != null) {
            HtmlCrawlProfile? custom = additionalProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (custom != null) {
                return custom;
            }
        }

        return BuiltInProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    internal static HtmlCrawlProfile? Resolve(string? profileName, Uri startUri, bool autoProfile, IEnumerable<HtmlCrawlProfile>? additionalProfiles = null) {
        if (!string.IsNullOrWhiteSpace(profileName)) {
            return ResolveByName(profileName, additionalProfiles);
        }

        if (!autoProfile) {
            return null;
        }

        string host = startUri.Host;
        if (additionalProfiles != null) {
            HtmlCrawlProfile? custom = additionalProfiles.FirstOrDefault(profile =>
                profile.Hosts.Any(profileHost => string.Equals(profileHost, host, StringComparison.OrdinalIgnoreCase)));
            if (custom != null) {
                return custom;
            }
        }

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

    private static HtmlCrawlProfile NormalizeProfile(HtmlCrawlProfile profile) {
        return new HtmlCrawlProfile {
            Name = profile.Name?.Trim() ?? string.Empty,
            Hosts = profile.Hosts
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Selector = string.IsNullOrWhiteSpace(profile.Selector) ? null : profile.Selector.Trim(),
            WaitForSelector = string.IsNullOrWhiteSpace(profile.WaitForSelector) ? null : profile.WaitForSelector.Trim(),
            PathPrefix = string.IsNullOrWhiteSpace(profile.PathPrefix) ? null : profile.PathPrefix.Trim(),
            AutoRender = profile.AutoRender,
            AutoScroll = profile.AutoScroll,
            InteractionRepeatCount = profile.InteractionRepeatCount,
            ExcludeSelectors = profile.ExcludeSelectors
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ClickSelectors = profile.ClickSelectors
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ClickTexts = profile.ClickTexts
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            DismissSelectors = profile.DismissSelectors
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            DismissTexts = profile.DismissTexts
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
