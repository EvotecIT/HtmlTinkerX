using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Provides built-in extraction workflow profiles that connect planning, rendering, crawling, and dataset output.
/// </summary>
public static class HtmlExtractionProfiles {
    private static readonly IReadOnlyList<HtmlExtractionProfile> BuiltInProfiles = new[] {
        new HtmlExtractionProfile {
            Name = "static-page",
            DisplayName = "Static page",
            Description = "First-pass extraction for pages where static HTML already contains the useful content.",
            RecommendedMode = HtmlExtractionPlanMode.Static,
            DatasetReady = true,
            SuggestedCommand = "Invoke-HtmlPageWorkbench -Content $html | ConvertTo-HtmlDatasetJsonL",
            ReasonCodes = new[] { "static-readable-text", "structured-data" }
        },
        new HtmlExtractionProfile {
            Name = "docs-content",
            DisplayName = "Documentation content",
            Description = "Documentation pages with reusable navigation, sidebars, and article-like content.",
            RecommendedMode = HtmlExtractionPlanMode.Crawl,
            CrawlProfileName = "docs-content",
            DatasetReady = true,
            SuggestedCommand = "Invoke-HtmlCrawl -Url '<start-url>' -Scenario Dataset -Profile docs-content",
            ReasonCodes = new[] { "many-links", "documentation-markers", "reader-content" }
        },
        new HtmlExtractionProfile {
            Name = "api-docs-content",
            DisplayName = "API documentation",
            Description = "API reference pages, OpenAPI-style docs, Swagger, Redoc, and endpoint catalogs.",
            RecommendedMode = HtmlExtractionPlanMode.Crawl,
            CrawlProfileName = "api-docs-content",
            DatasetReady = true,
            SuggestedCommand = "Invoke-HtmlCrawl -Url '<start-url>' -Scenario Dataset -Profile api-docs-content",
            ReasonCodes = new[] { "api-docs-markers", "endpoint-catalog", "structured-api" }
        },
        new HtmlExtractionProfile {
            Name = "app-shell",
            DisplayName = "Rendered app shell",
            Description = "Thin JavaScript shells or SPA pages where rendered snapshots should drive extraction.",
            RecommendedMode = HtmlExtractionPlanMode.RenderedSnapshot,
            RenderProfile = HtmlRenderProfile.HeavyDynamicPage,
            DatasetReady = true,
            SuggestedCommand = "Invoke-HtmlRendering -Url '<page-url>' -Snapshot -RenderProfile HeavyDynamicPage | Invoke-HtmlPageWorkbench",
            ReasonCodes = new[] { "javascript-shell", "app-state", "low-static-text" }
        },
        new HtmlExtractionProfile {
            Name = "auth-relay-page",
            DisplayName = "Browserless auth relay",
            Description = "WS-Federation, SAML, and similar deterministic hidden-form relay pages.",
            RecommendedMode = HtmlExtractionPlanMode.BrowserlessRelayCandidate,
            SuggestedCommand = "Invoke-HtmlFormRelay -Url '<relay-url>'",
            ReasonCodes = new[] { "auto-submit-form", "hidden-fields", "saml-or-wsfed" }
        },
        new HtmlExtractionProfile {
            Name = "login-protected-page",
            DisplayName = "Login-protected page",
            Description = "Pages that require interactive browser/session handling before extraction.",
            RecommendedMode = HtmlExtractionPlanMode.AuthRequired,
            RenderProfile = HtmlRenderProfile.HeavyDynamicPage,
            SuggestedCommand = "Invoke-HtmlRendering -Url '<login-protected-url>' -Snapshot -Session",
            ReasonCodes = new[] { "login-form", "password-field", "session-required" }
        },
        new HtmlExtractionProfile {
            Name = "dataset-page",
            DisplayName = "Single-page dataset",
            Description = "One-page extraction into compact JSONL chunks with provenance and redaction hints.",
            RecommendedMode = HtmlExtractionPlanMode.Static,
            DatasetReady = true,
            SuggestedCommand = "ConvertTo-HtmlDatasetJsonL -Content $html -BaseUrl '<page-url>'",
            ReasonCodes = new[] { "single-page-dataset", "llm-ready-output", "provenance" }
        }
    };

    private static readonly IReadOnlyList<string> BuiltInProfileNames = BuiltInProfiles
        .Select(profile => profile.Name)
        .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Gets the built-in extraction profiles.</summary>
    public static IReadOnlyList<HtmlExtractionProfile> Defaults => BuiltInProfiles;

    /// <summary>Gets the built-in extraction profile names.</summary>
    public static IReadOnlyList<string> Names => BuiltInProfileNames;

    /// <summary>
    /// Resolves an extraction profile by name.
    /// </summary>
    /// <param name="profileName">Profile name to resolve.</param>
    /// <returns>The matching profile or <c>null</c>.</returns>
    public static HtmlExtractionProfile? ResolveByName(string? profileName) {
        if (string.IsNullOrWhiteSpace(profileName)) {
            return null;
        }

        string normalizedName = profileName!.Trim();
        return BuiltInProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Recommends an extraction profile for a static extraction plan.
    /// </summary>
    /// <param name="plan">Extraction plan to classify.</param>
    /// <param name="url">Optional source URL used for page-family hints.</param>
    /// <returns>The matching extraction profile.</returns>
    public static HtmlExtractionProfile Recommend(HtmlExtractionPlan plan, Uri? url = null) {
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }

        string profileName = ChooseProfileName(plan, url);
        return ResolveByName(profileName) ?? BuiltInProfiles[0];
    }

    private static string ChooseProfileName(HtmlExtractionPlan plan, Uri? url) {
        if (plan.HasAutoSubmitForm || plan.RecommendedMode == HtmlExtractionPlanMode.BrowserlessRelayCandidate) {
            return "auth-relay-page";
        }

        if (plan.HasLoginForm || plan.RecommendedMode == HtmlExtractionPlanMode.AuthRequired) {
            return "login-protected-page";
        }

        if (plan.LooksLikeJavaScriptShell || plan.RecommendedMode == HtmlExtractionPlanMode.RenderedSnapshot) {
            return "app-shell";
        }

        if (LooksLikeApiDocs(plan, url)) {
            return "api-docs-content";
        }

        if (plan.RecommendedMode == HtmlExtractionPlanMode.Crawl || plan.LinkCount >= 20) {
            return "docs-content";
        }

        return plan.HasStructuredData || plan.WordCount >= 40
            ? "dataset-page"
            : "static-page";
    }

    private static bool LooksLikeApiDocs(HtmlExtractionPlan plan, Uri? url) {
        string combined = string.Join(" ", plan.Title, url?.AbsoluteUri ?? string.Empty);
        return ContainsAny(combined, "api", "openapi", "swagger", "redoc", "reference")
            || plan.Reasons.Any(reason => ContainsAny(reason, "api", "endpoint", "openapi", "swagger", "redoc"));
    }

    private static bool ContainsAny(string value, params string[] markers) =>
        markers.Any(marker => value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
}
