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

public static partial class HtmlCrawler {
    private static string BuildIndexHtml(HtmlCrawlResult result, HtmlCrawlSummary summary, string indexHtmlPath) {
        List<HtmlCrawlPage> skippedContentPages = result.SkippedPages
            .Where(page => page.SkipReason != HtmlCrawlSkipReason.AssetPath)
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<HtmlCrawlPage> skippedAssetPages = result.SkippedPages
            .Where(page => page.SkipReason == HtmlCrawlSkipReason.AssetPath)
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StringBuilder builder = new();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("  <title>Crawl Index - ")
            .Append(HtmlEncode(result.StartUrl))
            .AppendLine("</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    :root { color-scheme: light dark; }");
        builder.AppendLine("    body { font-family: Segoe UI, Arial, sans-serif; margin: 2rem; line-height: 1.5; }");
        builder.AppendLine("    h1, h2 { margin-bottom: 0.5rem; }");
        builder.AppendLine("    .meta { color: #666; margin-bottom: 1rem; }");
        builder.AppendLine("    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: 0.75rem; margin: 1rem 0 2rem; }");
        builder.AppendLine("    .stat { border: 1px solid #ccc; border-radius: 0.5rem; padding: 0.75rem; }");
        builder.AppendLine("    .stat strong { display: block; font-size: 1.35rem; }");
        builder.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 1rem 0 2rem; }");
        builder.AppendLine("    th, td { border: 1px solid #ccc; padding: 0.5rem; vertical-align: top; text-align: left; }");
        builder.AppendLine("    th { background: rgba(127, 127, 127, 0.12); }");
        builder.AppendLine("    code { font-family: Consolas, monospace; }");
        builder.AppendLine("    ul { padding-left: 1.25rem; }");
        builder.AppendLine("    .muted { color: #666; }");
        builder.AppendLine("    .summary { max-width: 32rem; }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.Append("  <h1>Offline Crawl Index</h1>").AppendLine();
        builder.Append("  <p class=\"meta\">Start URL: <a href=\"")
            .Append(HtmlEncode(result.StartUrl))
            .Append("\">")
            .Append(HtmlEncode(result.StartUrl))
            .AppendLine("</a></p>");
        if (result.AppliedScenario != HtmlCrawlScenario.Custom) {
            builder.Append("  <p class=\"meta\">Scenario: <code>")
                .Append(HtmlEncode(result.AppliedScenario.ToString()))
                .AppendLine("</code></p>");
        }
        if (!string.IsNullOrWhiteSpace(result.AppliedProfileName)) {
            builder.Append("  <p class=\"meta\">Profile: <code>")
                .Append(HtmlEncode(result.AppliedProfileName))
                .Append("</code>");
            if (result.AppliedProfileReasonCode != HtmlCrawlProfileSelectionReasonCode.None) {
                builder.Append(" <span class=\"muted\">(")
                    .Append(HtmlEncode(result.AppliedProfileReasonCode.ToString()))
                    .Append(")</span>");
            }
            builder.AppendLine("</p>");
            if (!string.IsNullOrWhiteSpace(result.AppliedProfileReason)) {
                builder.Append("  <p class=\"meta\">Profile reason: ")
                    .Append(HtmlEncode(result.AppliedProfileReason))
                    .AppendLine("</p>");
            }
        }

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Overview</h2>");
        builder.AppendLine("    <div class=\"stats\">");
        AppendStatCard(builder, "Pages", summary.PageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Successful", summary.SuccessfulPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Failed", summary.FailedPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Auto-Rendered", summary.AutoRenderedPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Skipped Pages", summary.SkippedContentPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Skipped Assets", summary.SkippedAssetCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Assets", summary.AssetCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Chunks", summary.ChunkCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Offline Grade", summary.OfflineReadinessGrade);
        AppendStatCard(builder, "Offline-Risk Pages", summary.OfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "High-Risk Pages", summary.HighOfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Offline Findings", summary.OfflineRiskDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Interactions", summary.InteractionCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Graph Edges", summary.GraphEdgeCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "External Nodes", summary.GraphExternalNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Links", summary.TotalDiscoveredLinks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("    </div>");
        builder.AppendLine("  </section>");

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Extraction Settings</h2>");
        builder.AppendLine("    <ul>");
        builder.Append("      <li>Hidden-content mode: <code>")
            .Append(HtmlEncode(summary.HiddenContentMode.ToString()))
            .AppendLine("</code></li>");
        builder.Append("      <li>Markdown profile: <code>")
            .Append(HtmlEncode(summary.MarkdownProfile.ToString()))
            .AppendLine("</code></li>");
        builder.Append("      <li>Markdown image mode: <code>")
            .Append(HtmlEncode(summary.MarkdownImageMode.ToString()))
            .AppendLine("</code></li>");
        builder.Append("      <li>Listing-card metadata mode: <code>")
            .Append(HtmlEncode(summary.ListingCardMetadataMode.ToString()))
            .AppendLine("</code></li>");
        builder.AppendLine("    </ul>");
        builder.AppendLine("  </section>");

        if (summary.GuidanceNotes.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Guidance</h2>");
            builder.AppendLine("    <ul>");
            foreach (string note in summary.GuidanceNotes.Where(note => !string.IsNullOrWhiteSpace(note))) {
                builder.Append("      <li>")
                    .Append(HtmlEncode(note))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Artifacts</h2>");
        builder.AppendLine("    <ul>");
        AppendArtifactLink(builder, indexHtmlPath, "Manifest JSON", result.ManifestPath);
        AppendArtifactLink(builder, indexHtmlPath, "Pages JSONL", result.PagesJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Pages CSV", result.PagesCsvPath);
        AppendArtifactLink(builder, indexHtmlPath, "Skipped Pages JSONL", result.SkippedPagesJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Skipped Assets JSONL", result.SkippedAssetsJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Links JSONL", result.LinksJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Assets JSONL", result.AssetsJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Structured Pages JSONL", result.StructuredJsonPagesJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "OpenAPI-Like JSON", result.OpenApiLikePath);
        AppendArtifactLink(builder, indexHtmlPath, "OpenAPI JSON", result.OpenApiPath);
        AppendArtifactLink(builder, indexHtmlPath, "Chunks JSONL", result.ChunksJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Graph JSON", result.GraphJsonPath);
        AppendArtifactLink(builder, indexHtmlPath, "Summary JSON", result.SummaryPath);
        AppendArtifactLink(builder, indexHtmlPath, "Summary Text", result.SummaryTextPath);
        builder.AppendLine("    </ul>");
        builder.AppendLine("  </section>");

        if (result.SitemapUrls.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Sitemaps</h2>");
            builder.AppendLine("    <ul>");
            foreach (string sitemapUrl in result.SitemapUrls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li><a href=\"")
                    .Append(HtmlEncode(sitemapUrl))
                    .Append("\">")
                    .Append(HtmlEncode(sitemapUrl))
                    .AppendLine("</a></li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.RenderModeCounts.Count > 0 || summary.RenderReasonCounts.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Render Summary</h2>");
            builder.AppendLine("    <ul>");
            foreach (var item in summary.RenderModeCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Render mode <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.RenderReasonCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Render reason <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.ContentModeCounts.Count > 0 || summary.ContentSelectionCounts.Count > 0 || summary.ContentComparisonWinnerCounts.Count > 0 || summary.ContentComparisonWinnerPreviewSamples.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Content Summary</h2>");
            builder.AppendLine("    <ul>");
            foreach (var item in summary.ContentModeCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Content mode <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.ContentSelectionCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Content selection <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.ContentComparisonWinnerCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Best comparison mode <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            if (summary.AverageBestContentComparisonWordDelta > 0) {
                builder.Append("      <li>Average best-comparison delta: ")
                    .Append(HtmlEncode(summary.AverageBestContentComparisonWordDelta.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine(" words</li>");
            }
            foreach (var item in summary.ContentComparisonWinnerPreviewSamples.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Best comparison sample <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: <span class=\"muted\">")
                    .Append(HtmlEncode(item.Value))
                    .AppendLine("</span></li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.InteractionCounts.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Interaction Summary</h2>");
            builder.AppendLine("    <ul>");
            builder.Append("      <li>Pages with interactions: ")
                .Append(HtmlEncode(summary.InteractedPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            builder.Append("      <li>Applied interactions: ")
                .Append(HtmlEncode(summary.InteractionCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            foreach (var item in summary.InteractionCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li><code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.OfflineReadinessCounts.Count > 0 || summary.OfflineDependencyKinds.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Offline Readiness</h2>");
            builder.AppendLine("    <ul>");
            builder.Append("      <li>Pages with offline-runtime risk signals: ")
                .Append(HtmlEncode(summary.OfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            builder.Append("      <li>Offline readiness grade: <code>")
                .Append(HtmlEncode(summary.OfflineReadinessGrade))
                .AppendLine("</code></li>");
            builder.Append("      <li>Total offline-runtime diagnostics: ")
                .Append(HtmlEncode(summary.OfflineRiskDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            builder.Append("      <li>Pages with high-severity offline-runtime signals: ")
                .Append(HtmlEncode(summary.HighOfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            foreach (var item in summary.OfflineReadinessCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline grade <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.OfflineReadinessCountsByState.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline state <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.OfflineDependencySeverityCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline severity <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.OfflineDependencyKinds.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline dependency <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.GraphNodeCategories.Count > 0 || summary.GraphEdgeRelations.Count > 0 || summary.GraphSkippedNodeReasons.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Graph Summary</h2>");
            builder.AppendLine("    <ul>");
            foreach (var item in summary.GraphNodeCategories.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Node category <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.GraphEdgeRelations.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Edge relation <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.GraphSkippedNodeReasons.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Skipped-node reason <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Pages</h2>");
        builder.AppendLine("    <table>");
        builder.AppendLine("      <thead><tr><th>Title</th><th>URL</th><th>Status</th><th>Search</th><th>Files</th></tr></thead>");
        builder.AppendLine("      <tbody>");
        foreach (HtmlCrawlPage page in result.Pages.OrderBy(page => page.Depth).ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)) {
            PageSearchMetadata searchMetadata = BuildPageSearchMetadata(page);
            builder.AppendLine("        <tr>");
            builder.Append("          <td>")
                .Append(HtmlEncode(string.IsNullOrWhiteSpace(page.Title) ? "(untitled)" : page.Title))
                .AppendLine("</td>");
            builder.Append("          <td><a href=\"")
                .Append(HtmlEncode(page.Url))
                .Append("\">")
                .Append(HtmlEncode(page.Url))
                .AppendLine("</a></td>");
            builder.Append("          <td><code>")
                .Append(HtmlEncode(page.Status.ToString()))
                .Append("</code>");
            if (page.StatusCode.HasValue) {
                builder.Append(" <span class=\"muted\">")
                    .Append(HtmlEncode(page.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .Append("</span>");
            }
            builder.Append("<br><span class=\"muted\">")
                .Append(HtmlEncode(page.RenderMode.ToString()));
            if (page.RenderReasonCode != HtmlCrawlRenderReasonCode.None) {
                builder.Append(" / ")
                    .Append(HtmlEncode(page.RenderReasonCode.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(page.RenderReason)) {
                builder.Append(": ")
                    .Append(HtmlEncode(page.RenderReason));
            }
            if (page.AppliedScenario != HtmlCrawlScenario.Custom) {
                builder.Append("<br>scenario: <code>")
                    .Append(HtmlEncode(page.AppliedScenario.ToString()))
                    .Append("</code>");
            }
            if (!string.IsNullOrWhiteSpace(page.AppliedProfileName)) {
                builder.Append("<br>profile: <code>")
                    .Append(HtmlEncode(page.AppliedProfileName))
                    .Append("</code>");
                if (page.AppliedProfileReasonCode != HtmlCrawlProfileSelectionReasonCode.None) {
                    builder.Append(" <span class=\"muted\">(")
                        .Append(HtmlEncode(page.AppliedProfileReasonCode.ToString()))
                        .Append(")</span>");
                }
                if (!string.IsNullOrWhiteSpace(page.AppliedProfileReason)) {
                    builder.Append("<br>")
                        .Append(HtmlEncode(page.AppliedProfileReason));
                }
            }
            if (page.AppliedInteractions.Count > 0) {
                builder.Append("<br>interactions: ")
                    .Append(HtmlEncode(string.Join(" | ", page.AppliedInteractions)));
            }
            if (page.OfflineDependencyDiagnostics.Count > 0) {
                builder.Append("<br>offline risk: ")
                    .Append(HtmlEncode(page.OfflineDependencyDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .Append(" <span class=\"muted\">(")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("; ")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .Append("; ")
                    .Append(HtmlEncode(page.OfflineDependencyKindsSummary))
                    .Append(")</span>");
            }
            builder.Append("<br>content: ")
                .Append(HtmlEncode(page.ContentModeUsed.ToString()));
            if (page.ContentSelectionReasonCode != HtmlCrawlContentSelectionReasonCode.None) {
                builder.Append(" / ")
                    .Append(HtmlEncode(page.ContentSelectionReasonCode.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(page.ContentElementSelectorHint)) {
                builder.Append(" <code>")
                    .Append(HtmlEncode(page.ContentElementSelectorHint))
                    .Append("</code>");
            }
            if (!string.IsNullOrWhiteSpace(page.ContentSelectionReason)) {
                builder.Append("<br>")
                    .Append(HtmlEncode(page.ContentSelectionReason));
            }
            if (page.ContentSelectionScore.HasValue) {
                builder.Append("<br>score: ")
                    .Append(HtmlEncode(page.ContentSelectionScore.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
                if (page.ReaderCandidateCount > 0) {
                    builder.Append(" <span class=\"muted\">(")
                        .Append(HtmlEncode(page.ReaderCandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        .Append(" candidates)</span>");
                }
            }
            if (!string.IsNullOrWhiteSpace(page.ReaderRootElementSelectorHint)) {
                builder.Append("<br>reader root: <code>")
                    .Append(HtmlEncode(page.ReaderRootElementSelectorHint))
                    .Append("</code>");
            }
            if (page.ContentComparisons.Count > 0) {
                builder.Append("<br>comparisons: ")
                    .Append(HtmlEncode(string.Join(" | ", page.ContentComparisons.Select(comparison => comparison.Mode.ToString()))));
                if (!string.IsNullOrWhiteSpace(page.ContentComparisonDeltaSummary)) {
                    builder.Append("<br>deltas: ")
                        .Append(HtmlEncode(page.ContentComparisonDeltaSummary));
                }
                if (!string.IsNullOrWhiteSpace(page.ContentComparisonPreviewSummary)) {
                    builder.Append("<br>preview: ")
                        .Append(HtmlEncode(page.ContentComparisonPreviewSummary));
                }
            }
            if (page.BestContentComparisonMode.HasValue) {
                builder.Append("<br>best comparison: <code>")
                    .Append(HtmlEncode(page.BestContentComparisonMode.Value.ToString()))
                    .Append("</code>");
                if (page.BestContentComparisonWordCount.HasValue) {
                    builder.Append(" ")
                        .Append(HtmlEncode(page.BestContentComparisonWordCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        .Append(" words");
                }
                if (page.BestContentComparisonReasonCode.HasValue) {
                    builder.Append(" <span class=\"muted\">(")
                        .Append(HtmlEncode(page.BestContentComparisonReasonCode.Value.ToString()))
                        .Append(")</span>");
                }
                if (page.RunnerUpContentComparisonMode.HasValue && page.BestContentComparisonWordDelta.HasValue) {
                    builder.Append("<br>delta vs <code>")
                        .Append(HtmlEncode(page.RunnerUpContentComparisonMode.Value.ToString()))
                        .Append("</code>: +")
                        .Append(HtmlEncode(page.BestContentComparisonWordDelta.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        .Append(" words");
                }
            }
            builder.Append("</span>");
            builder.AppendLine("</td>");
            builder.Append("          <td class=\"summary\"><strong>")
                .Append(HtmlEncode(searchMetadata.WordCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Append("</strong> words");
            builder.Append(" <span class=\"muted\">(")
                .Append(HtmlEncode(searchMetadata.ChunkCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Append(" chunks)</span>");
            if (searchMetadata.Headings.Length > 0) {
                builder.Append("<br><span class=\"muted\">")
                    .Append(HtmlEncode(string.Join(" | ", searchMetadata.Headings.Take(3))))
                    .Append("</span>");
            }
            if (searchMetadata.Keywords.Length > 0) {
                builder.Append("<br><span class=\"muted\">keywords: ")
                    .Append(HtmlEncode(string.Join(", ", searchMetadata.Keywords.Take(6))))
                    .Append("</span>");
            }
            if (!string.IsNullOrWhiteSpace(searchMetadata.Summary)) {
                builder.Append("<br>")
                    .Append(HtmlEncode(searchMetadata.Summary));
            }
            builder.AppendLine("</td>");
            builder.Append("          <td>");
            AppendOptionalFileLink(builder, indexHtmlPath, "HTML", page.HtmlPath);
            if (!string.IsNullOrWhiteSpace(page.HtmlPath) && HasAnyFollowingFile(page.TextPath, page.MarkdownPath, page.StructuredJsonPath, page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Text", page.TextPath);
            if (!string.IsNullOrWhiteSpace(page.TextPath) && HasAnyFollowingFile(page.MarkdownPath, page.StructuredJsonPath, page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Markdown", page.MarkdownPath);
            if (!string.IsNullOrWhiteSpace(page.MarkdownPath) && HasAnyFollowingFile(page.StructuredJsonPath, page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Structured JSON", page.StructuredJsonPath);
            if (!string.IsNullOrWhiteSpace(page.StructuredJsonPath) && HasAnyFollowingFile(page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Manifest", page.ManifestPath);
            builder.AppendLine("</td>");
            builder.AppendLine("        </tr>");
        }
        builder.AppendLine("      </tbody>");
        builder.AppendLine("    </table>");
        builder.AppendLine("  </section>");

        if (result.Assets.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Assets</h2>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Source Page</th><th>Type</th><th>File</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlAsset asset in result.Assets.OrderBy(asset => asset.PageUrl, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(asset.Url))
                    .Append("\">")
                    .Append(HtmlEncode(asset.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td>");
                if (!string.IsNullOrWhiteSpace(asset.PageUrl)) {
                    builder.Append("<a href=\"")
                        .Append(HtmlEncode(asset.PageUrl))
                        .Append("\">")
                        .Append(HtmlEncode(asset.PageUrl))
                        .Append("</a>");
                } else {
                    builder.Append("&nbsp;");
                }
                builder.AppendLine("</td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(asset.ContentType ?? string.Empty))
                    .AppendLine("</td>");
                builder.Append("          <td>");
                AppendOptionalFileLink(builder, indexHtmlPath, Path.GetFileName(asset.FilePath), asset.FilePath);
                builder.AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        if (skippedContentPages.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Skipped Pages</h2>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Reason</th><th>Offline</th><th>Depth</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlPage page in skippedContentPages) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(page.Url))
                    .Append("\">")
                    .Append(HtmlEncode(page.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.SkipReason.ToString()))
                    .AppendLine("</code></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("</code> <span class=\"muted\">(")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .AppendLine(")</span></td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        if (skippedAssetPages.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Skipped Assets</h2>");
            builder.AppendLine("    <p class=\"muted\">These URLs were discovered as asset candidates and intentionally not crawled as content pages.</p>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Reason</th><th>Offline</th><th>Depth</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlPage page in skippedAssetPages) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(page.Url))
                    .Append("\">")
                    .Append(HtmlEncode(page.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.SkipReason.ToString()))
                    .AppendLine("</code></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("</code> <span class=\"muted\">(")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .AppendLine(")</span></td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        if (result.PendingPages.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Pending Pages</h2>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Parent</th><th>Offline</th><th>Depth</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlPendingItem page in result.PendingPages.OrderBy(page => page.Depth).ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(page.Url))
                    .Append("\">")
                    .Append(HtmlEncode(page.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td>");
                if (!string.IsNullOrWhiteSpace(page.ParentUrl)) {
                    builder.Append("<a href=\"")
                        .Append(HtmlEncode(page.ParentUrl))
                        .Append("\">")
                        .Append(HtmlEncode(page.ParentUrl))
                        .Append("</a>");
                } else {
                    builder.Append("&nbsp;");
                }
                builder.AppendLine("</td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("</code> <span class=\"muted\">(")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .AppendLine(")</span></td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void AppendStatCard(StringBuilder builder, string label, string value) {
        builder.Append("      <div class=\"stat\"><strong>")
            .Append(HtmlEncode(value))
            .Append("</strong><span>")
            .Append(HtmlEncode(label))
            .AppendLine("</span></div>");
    }

    private static void AppendArtifactLink(StringBuilder builder, string indexHtmlPath, string label, string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        builder.Append("      <li>");
        AppendOptionalFileLink(builder, indexHtmlPath, label, path);
        builder.AppendLine("</li>");
    }

    private static void AppendOptionalFileLink(StringBuilder builder, string indexHtmlPath, string? label, string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            builder.Append("&nbsp;");
            return;
        }

        string relative = BuildRelativePath(indexHtmlPath, path!);
        builder.Append("<a href=\"")
            .Append(HtmlEncode(relative))
            .Append("\">")
            .Append(HtmlEncode(string.IsNullOrWhiteSpace(label) ? Path.GetFileName(path) : label))
            .Append("</a>");
    }

    private static bool HasAnyFollowingFile(params string?[] paths) => paths.Any(path => !string.IsNullOrWhiteSpace(path));

    private static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string BuildPageSlug(HtmlCrawlPage page, string prefix) {
        string source = !string.IsNullOrWhiteSpace(page.Title)
            ? page.Title!
            : page.Url;
        string slug = Regex.Replace(source, @"[^A-Za-z0-9\-]+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug)) {
            slug = "page";
        }
        if (slug.Length > 48) {
            slug = slug.Substring(0, 48).Trim('-');
        }

        return $"{prefix}-{slug}";
    }

    private static CrawlArtifactPaths ResolveArtifactPaths(string path) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        bool treatAsDirectory = Directory.Exists(fullPath) || !Path.HasExtension(fullPath);

        if (treatAsDirectory) {
            Directory.CreateDirectory(fullPath);
            string pagesDirectory = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "pages"), fullPath);
            string assetsDirectory = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "assets"), fullPath);
            Directory.CreateDirectory(pagesDirectory);
            Directory.CreateDirectory(assetsDirectory);
            return new CrawlArtifactPaths {
                ManifestPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "crawl-result.json"), fullPath),
                PagesDirectory = pagesDirectory,
                AssetsDirectory = assetsDirectory,
                PagesJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "pages.jsonl"), fullPath),
                PagesCsvPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "pages.csv"), fullPath),
                SkippedPagesJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "skipped-pages.jsonl"), fullPath),
                SkippedAssetsJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "skipped-assets.jsonl"), fullPath),
                LinksJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "links.jsonl"), fullPath),
                AssetsJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "assets.jsonl"), fullPath),
                StructuredJsonPagesJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "structured-pages.jsonl"), fullPath),
                OpenApiLikeJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "openapi-like.json"), fullPath),
                OpenApiJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "openapi.json"), fullPath),
                ChunksJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "chunks.jsonl"), fullPath),
                GraphJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "graph.json"), fullPath),
                SummaryJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "summary.json"), fullPath),
                SummaryTextPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "summary.txt"), fullPath),
                IndexHtmlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "index.html"), fullPath)
            };
        }

        string manifestPath = HtmlUtilities.EnsureDirectoryExists(fullPath);
        string baseDirectory = Path.GetDirectoryName(manifestPath) ?? Path.GetDirectoryName(fullPath) ?? fullPath;
        string pagesDirectoryForFile = EnsurePathIsWithinDirectory(Path.Combine(baseDirectory, Path.GetFileNameWithoutExtension(manifestPath) + ".pages"), baseDirectory);
        string assetsDirectoryForFile = EnsurePathIsWithinDirectory(Path.Combine(baseDirectory, Path.GetFileNameWithoutExtension(manifestPath) + ".assets"), baseDirectory);
        Directory.CreateDirectory(pagesDirectoryForFile);
        Directory.CreateDirectory(assetsDirectoryForFile);
        string stem = EnsurePathIsWithinDirectory(Path.Combine(baseDirectory, Path.GetFileNameWithoutExtension(manifestPath)), baseDirectory);
        return new CrawlArtifactPaths {
            ManifestPath = EnsurePathIsWithinDirectory(manifestPath, baseDirectory),
            PagesDirectory = pagesDirectoryForFile,
            AssetsDirectory = assetsDirectoryForFile,
            PagesJsonlPath = EnsurePathIsWithinDirectory(stem + ".pages.jsonl", baseDirectory),
            PagesCsvPath = EnsurePathIsWithinDirectory(stem + ".pages.csv", baseDirectory),
            SkippedPagesJsonlPath = EnsurePathIsWithinDirectory(stem + ".skipped.jsonl", baseDirectory),
            SkippedAssetsJsonlPath = EnsurePathIsWithinDirectory(stem + ".skipped-assets.jsonl", baseDirectory),
            LinksJsonlPath = EnsurePathIsWithinDirectory(stem + ".links.jsonl", baseDirectory),
            AssetsJsonlPath = EnsurePathIsWithinDirectory(stem + ".assets.jsonl", baseDirectory),
            StructuredJsonPagesJsonlPath = EnsurePathIsWithinDirectory(stem + ".structured-pages.jsonl", baseDirectory),
            OpenApiLikeJsonPath = EnsurePathIsWithinDirectory(stem + ".openapi-like.json", baseDirectory),
            OpenApiJsonPath = EnsurePathIsWithinDirectory(stem + ".openapi.json", baseDirectory),
            ChunksJsonlPath = EnsurePathIsWithinDirectory(stem + ".chunks.jsonl", baseDirectory),
            GraphJsonPath = EnsurePathIsWithinDirectory(stem + ".graph.json", baseDirectory),
            SummaryJsonPath = EnsurePathIsWithinDirectory(stem + ".summary.json", baseDirectory),
            SummaryTextPath = EnsurePathIsWithinDirectory(stem + ".summary.txt", baseDirectory),
            IndexHtmlPath = EnsurePathIsWithinDirectory(stem + ".index.html", baseDirectory)
        };
    }

    private static string ResolveManifestPath(string path) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        if (Directory.Exists(fullPath) || !Path.HasExtension(fullPath)) {
            return Path.Combine(fullPath, "crawl-result.json");
        }

        return fullPath;
    }

    private static async Task<IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField>> LoadStructuredSchemaAsync(HtmlCrawlOptions options, CancellationToken cancellationToken) {
        string? schemaJson = options.StructuredJsonSchema;
        if (string.IsNullOrWhiteSpace(schemaJson) && !string.IsNullOrWhiteSpace(options.StructuredJsonSchemaPath)) {
            schemaJson = await HtmlUtilities.ReadFileCheckedAsync(options.StructuredJsonSchemaPath!, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(schemaJson)) {
            return new Dictionary<string, HtmlCrawlJsonSchemaField>(StringComparer.OrdinalIgnoreCase);
        }

        return ParseStructuredSchema(schemaJson!);
    }

    private static IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> ParseStructuredSchema(string schemaJson) {
        using JsonDocument document = JsonDocument.Parse(schemaJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) {
            throw new ArgumentException("Structured JSON schema must be an object.", nameof(schemaJson));
        }

        if (root.TryGetProperty("fields", out JsonElement fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Object) {
            root = fieldsElement;
        }

        Dictionary<string, HtmlCrawlJsonSchemaField> fields = new(StringComparer.OrdinalIgnoreCase);
        JsonSerializerOptions options = new() {
            PropertyNameCaseInsensitive = true
        };
        foreach (JsonProperty property in root.EnumerateObject()) {
            fields[property.Name] = property.Value.ValueKind switch {
                JsonValueKind.String => new HtmlCrawlJsonSchemaField {
                    Path = property.Value.GetString()
                },
                JsonValueKind.Object => JsonSerializer.Deserialize<HtmlCrawlJsonSchemaField>(property.Value.GetRawText(), options) ?? new HtmlCrawlJsonSchemaField(),
                _ => throw new ArgumentException($"Structured JSON schema field '{property.Name}' must be a string path or an object rule.", nameof(schemaJson))
            };
        }

        return fields;
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        JsonSerializerOptions options = new() {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken) {
        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
#if NETSTANDARD2_0 || NETFRAMEWORK
        await Task.Run(() => File.WriteAllText(fullPath, content), cancellationToken).ConfigureAwait(false);
#else
        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
#endif
    }

    private static async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken) {
        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
#if NETSTANDARD2_0 || NETFRAMEWORK
        await Task.Run(() => File.WriteAllBytes(fullPath, bytes), cancellationToken).ConfigureAwait(false);
#else
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);
#endif
    }

    private static string EscapeCsv(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }

        string normalized = value!.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.IndexOfAny(new[] { ',', '"', '\n' }) >= 0) {
            return "\"" + normalized.Replace("\"", "\"\"") + "\"";
        }

        return normalized;
    }

}
