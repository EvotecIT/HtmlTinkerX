using AngleSharp.Dom;
using Microsoft.Playwright;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HtmlTinkerX;
/// <summary>
/// Provides a simple offline-first website crawler for .NET and PowerShell.
/// </summary>
public static partial class HtmlCrawler {
    internal sealed class AutoRenderDecision {
        public bool ShouldRender { get; set; }
        public string Reason { get; set; } = string.Empty;
        public HtmlCrawlRenderReasonCode ReasonCode { get; set; }
    }

    internal sealed class ProfileSelectionDecision {
        public HtmlCrawlProfile? Profile { get; set; }
        public HtmlCrawlProfileSelectionReasonCode ReasonCode { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class ContentSelectionResult {
        public HtmlCrawlContentMode ModeUsed { get; set; } = HtmlCrawlContentMode.Focused;
        public HtmlCrawlContentSelectionReasonCode ReasonCode { get; set; }
        public string Reason { get; set; } = string.Empty;
        public IElement? Element { get; set; }
        public string Html { get; set; } = string.Empty;
        public double? Score { get; set; }
        public int ReaderCandidateCount { get; set; }
        public string? ReaderRootElementSelectorHint { get; set; }
    }

    private sealed class PageSearchMetadata {
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public int ChunkCount { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string[] Headings { get; set; } = Array.Empty<string>();
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }

    private sealed class PageChunkRecord {
        public string ChunkId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int Depth { get; set; }
        public int ChunkIndex { get; set; }
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string[] Headings { get; set; } = Array.Empty<string>();
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string Text { get; set; } = string.Empty;
        public string? HtmlPath { get; set; }
        public string? TextPath { get; set; }
        public string? ManifestPath { get; set; }
        public string OfflineReadinessGrade { get; set; } = "ready";
        public string HighestOfflineRiskSeverity { get; set; } = "none";
        public int OfflineDependencyDiagnosticCount { get; set; }
        public string OfflineDependencyKindsSummary { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
    }

    private sealed class GraphNodeRecord {
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int Depth { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? SkipReason { get; set; }
        public string OfflineReadinessGrade { get; set; } = "not-assessed";
        public string HighestOfflineRiskSeverity { get; set; } = "none";
        public int OfflineDependencyDiagnosticCount { get; set; }
        public string OfflineDependencyKindsSummary { get; set; } = string.Empty;
        public int InDegree { get; set; }
        public int OutDegree { get; set; }
        public int InternalOutDegree { get; set; }
        public string? HtmlPath { get; set; }
        public string? ManifestPath { get; set; }
    }

    private sealed class GraphEdgeRecord {
        public string SourceUrl { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
        public bool TargetKnown { get; set; }
        public bool Internal { get; set; }
        public string Relation { get; set; } = string.Empty;
    }

    private sealed class CrawlRequest {
        public Uri Uri { get; set; } = null!;
        public string? ParentUrl { get; set; }
        public int Depth { get; set; }
    }

    private sealed class FetchedPageData {
        public HtmlCrawlPage Page { get; set; } = null!;
        public string? RawHtml { get; set; }
    }

    private sealed class CrawlArtifactPaths {
        public string ManifestPath { get; set; } = string.Empty;
        public string PagesDirectory { get; set; } = string.Empty;
        public string AssetsDirectory { get; set; } = string.Empty;
        public string PagesJsonlPath { get; set; } = string.Empty;
        public string PagesCsvPath { get; set; } = string.Empty;
        public string SkippedPagesJsonlPath { get; set; } = string.Empty;
        public string SkippedAssetsJsonlPath { get; set; } = string.Empty;
        public string LinksJsonlPath { get; set; } = string.Empty;
        public string AssetsJsonlPath { get; set; } = string.Empty;
        public string StructuredJsonPagesJsonlPath { get; set; } = string.Empty;
        public string OpenApiLikeJsonPath { get; set; } = string.Empty;
        public string OpenApiJsonPath { get; set; } = string.Empty;
        public string ChunksJsonlPath { get; set; } = string.Empty;
        public string GraphJsonPath { get; set; } = string.Empty;
        public string SummaryJsonPath { get; set; } = string.Empty;
        public string SummaryTextPath { get; set; } = string.Empty;
        public string IndexHtmlPath { get; set; } = string.Empty;
    }

    private sealed class RobotsDocument {
        public List<RobotsRule> Rules { get; } = new();
        public List<string> SitemapUrls { get; } = new();
        public int? CrawlDelayMs { get; set; }
    }

    private sealed class RobotsGroup {
        public List<string> UserAgents { get; } = new();
        public List<RobotsRule> Rules { get; } = new();
        public int? CrawlDelayMs { get; set; }
    }

    private sealed class RobotsRule {
        public bool Allow { get; set; }
        public string Path { get; set; } = string.Empty;
    }

    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase) {
        "a", "an", "and", "are", "as", "at", "be", "been", "but", "by", "for", "from", "had", "has", "have",
        "he", "her", "him", "his", "i", "if", "in", "into", "is", "it", "its", "me", "more", "most", "my",
        "no", "not", "of", "on", "or", "our", "ours", "she", "so", "some", "than", "that", "the", "their",
        "them", "then", "there", "these", "they", "this", "those", "to", "too", "up", "us", "was", "we",
        "were", "what", "when", "where", "which", "who", "will", "with", "you", "your"
    };

    private static readonly string[] ContentFallbackSelectors = {
        "main",
        "[role='main']",
        "#main",
        "#main-content",
        ".main-content",
        ".site-main",
        "#content",
        ".content",
        ".entry-content",
        ".post-content",
        "article"
    };

    private static readonly string[] BoilerplateSignalTokens = {
        "site-header",
        "primary-navigation",
        "nav-menu",
        "menu-item-search",
        "footer-nav",
        "footer-site-info",
        "wpml-ls",
        "sharing-popup",
        "post-footer-sharing",
        "socials-sharing",
        "gem-pagination",
        "skip-link",
        "language-switcher",
        "locale-switcher",
        "related-post",
        "related-articles",
        "related-content",
        "breadcrumbs",
        "breadcrumb",
        "comment-respond",
        "comments-area",
        "newsletter",
        "subscribe",
        "promo",
        "cookie-banner",
        "table-of-contents",
        "toc",
        "sidebar",
        "share",
        "social",
        "pagination",
        "pager"
    };

    private static readonly (string Kind, string Selector)[] StructuredRegionSelectors = {
        ("Header", "header,[role='banner'],.site-header,.page-header,#header"),
        ("Navigation", "nav,[role='navigation'],.navbar,.navigation,.site-nav,.site-navigation,.menu,#nav,#menu"),
        ("Main", "main,[role='main'],#main,#main-content,.main-content,.site-main,#content,.content"),
        ("Article", "article,[itemtype*='Article'],[itemtype*='BlogPosting']"),
        ("Aside", "aside,[role='complementary'],.sidebar,.side-nav,.toc,.table-of-contents,.on-this-page"),
        ("Footer", "footer,[role='contentinfo'],.site-footer,.page-footer,#footer")
    };

    private const int StrictOpenApiPromotionThreshold = 45;

}
