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
    private static HtmlCrawlStructuredJson BuildStructuredJson(
        HtmlCrawlPage page,
        string fullHtml,
        string selectedHtml,
        string structuredText,
        string structuredMarkdown,
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema,
        HtmlCrawlStructuredJsonPreset structuredPreset) {
        IDocument document = HtmlParser.ParseWithAngleSharp(fullHtml);
        IDocument selectedDocument = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_structured_selected\">{selectedHtml}</div>");
        List<HtmlMetaTag> metaTags = HtmlParser.ParseMetaTags(fullHtml);
        HtmlOpenGraph openGraph = HtmlParser.ParseOpenGraph(fullHtml);
        HtmlCrawlStructuredMetadata metadata = BuildStructuredMetadata(page, document, metaTags, openGraph);
        PageSearchMetadata searchMetadata = BuildPageSearchMetadata(selectedHtml, structuredText, structuredMarkdown);
        List<HtmlTableResult> tables = HtmlParser.ParseTablesWithAngleSharpDetailed(selectedHtml);
        List<HtmlCrawlStructuredCodeBlock> codeBlocks = BuildStructuredCodeBlocks(selectedDocument);
        List<HtmlCrawlStructuredCodeSample> codeSamples = BuildStructuredCodeSamples(selectedDocument);
        List<HtmlCrawlStructuredBreadcrumbTrail> breadcrumbs = BuildStructuredBreadcrumbs(document, page.Url);
        List<HtmlCrawlStructuredFaqItem> faqItems = BuildStructuredFaqItems(selectedDocument);
        List<HtmlCrawlStructuredSpecTable> specTables = BuildStructuredSpecTables(selectedDocument, tables);
        List<HtmlCrawlStructuredCallout> callouts = BuildStructuredCallouts(selectedDocument);
        List<HtmlCrawlStructuredPrimaryAction> primaryActions = BuildStructuredPrimaryActions(selectedDocument, page.Url);
        List<HtmlCrawlStructuredApiEndpoint> apiEndpoints = BuildStructuredApiEndpoints(selectedDocument, codeSamples, page.Url);
        HtmlCrawlStructuredJson structuredJson = new() {
            Document = BuildStructuredDocument(page, searchMetadata, structuredText, structuredMarkdown),
            Content = new HtmlCrawlStructuredContent {
                WordCount = searchMetadata.WordCount,
                CharacterCount = searchMetadata.CharacterCount,
                ChunkCount = searchMetadata.ChunkCount,
                Summary = searchMetadata.Summary,
                Headings = new List<string>(searchMetadata.Headings),
                Keywords = new List<string>(searchMetadata.Keywords)
            },
            Metadata = metadata,
            Layout = BuildStructuredLayout(document),
            MetaTags = metaTags,
            OpenGraph = openGraph,
            MicrodataItems = HtmlParser.ParseMicrodataItems(fullHtml),
            Forms = HtmlParser.ParseFormsWithAngleSharp(fullHtml),
            Lists = HtmlParser.ParseListsWithAngleSharpDetailed(selectedHtml),
            Tables = tables,
            CodeBlocks = codeBlocks,
            CodeSamples = codeSamples,
            Breadcrumbs = breadcrumbs,
            FaqItems = faqItems,
            SpecTables = specTables,
            Callouts = callouts,
            PrimaryActions = primaryActions,
            ApiEndpoints = apiEndpoints,
            ApiCatalog = BuildStructuredApiCatalog(metadata, apiEndpoints),
            OpenApiLike = BuildStructuredOpenApiLike(page, metadata, apiEndpoints)
        };

        HtmlCrawlStructuredJsonPreset resolvedPreset = ResolveStructuredJsonPreset(structuredJson, document, selectedDocument, structuredPreset);
        structuredJson.ResolvedPreset = resolvedPreset;
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> effectiveStructuredSchema = BuildEffectiveStructuredSchema(resolvedPreset, structuredSchema);
        if (effectiveStructuredSchema.Count > 0) {
            structuredJson.Extracted = BuildStructuredSchemaExtraction(structuredJson, document, selectedDocument, effectiveStructuredSchema);
        }

        return structuredJson;
    }

    private static IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> BuildEffectiveStructuredSchema(
        HtmlCrawlStructuredJsonPreset structuredPreset,
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema) {
        Dictionary<string, HtmlCrawlJsonSchemaField> effectiveSchema = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlJsonSchemaField> field in GetPresetStructuredSchema(structuredPreset)) {
            effectiveSchema[field.Key] = field.Value;
        }

        foreach (KeyValuePair<string, HtmlCrawlJsonSchemaField> field in structuredSchema) {
            effectiveSchema[field.Key] = field.Value;
        }

        return effectiveSchema;
    }

    private static IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> GetPresetStructuredSchema(HtmlCrawlStructuredJsonPreset structuredPreset) {
        Dictionary<string, HtmlCrawlJsonSchemaField> schema = new(StringComparer.OrdinalIgnoreCase);
        if (structuredPreset == HtmlCrawlStructuredJsonPreset.None) {
            return schema;
        }

        void AddPath(string name, string path) {
            schema[name] = new HtmlCrawlJsonSchemaField {
                Path = path
            };
        }

        void AddSelector(string name, string selector, string source = "selected", string mode = "Text", string? attribute = null, bool all = false) {
            schema[name] = new HtmlCrawlJsonSchemaField {
                Selector = selector,
                Source = source,
                Mode = mode,
                Attribute = attribute,
                All = all
            };
        }

        switch (structuredPreset) {
            case HtmlCrawlStructuredJsonPreset.Docs:
                AddPath("title", "Metadata.Title");
                AddPath("description", "Metadata.Description");
                AddPath("canonicalUrl", "Metadata.CanonicalUrl");
                AddPath("language", "Metadata.Language");
                AddPath("siteName", "Metadata.SiteName");
                AddPath("summary", "Document.Summary");
                AddPath("content", "Document.Text");
                AddPath("markdown", "Document.Markdown");
                AddPath("headings", "Document.Headings");
                AddPath("keywords", "Document.Keywords");
                AddPath("codeBlocks", "CodeBlocks");
                AddPath("codeSamples", "CodeSamples");
                AddPath("apiEndpoints", "ApiEndpoints");
                AddPath("apiCatalog", "ApiCatalog");
                AddPath("apiTags", "ApiCatalog.Tags");
                AddPath("apiResources", "ApiCatalog.Resources");
                AddPath("operationIds", "ApiCatalog.OperationIds");
                AddPath("openApiLike", "OpenApiLike");
                AddPath("openApiPaths", "OpenApiLike.Paths");
                AddPath("openApiServers", "OpenApiLike.Servers");
                AddPath("authentication", "ApiEndpoints.0.Authentication");
                AddPath("authRequired", "ApiEndpoints.0.Authentication.Required");
                AddPath("authenticationSchemes", "ApiEndpoints.0.Authentication.Schemes");
                AddPath("authenticationHeaders", "ApiEndpoints.0.Authentication.Headers");
                AddPath("rateLimit", "ApiEndpoints.0.RateLimit");
                AddPath("rateLimitHeaders", "ApiEndpoints.0.RateLimit.Headers");
                AddPath("rateLimitStatusCode", "ApiEndpoints.0.RateLimit.StatusCode");
                AddPath("operationId", "ApiEndpoints.0.OperationId");
                AddPath("resource", "ApiEndpoints.0.Resource");
                AddPath("tags", "ApiEndpoints.0.Tags");
                AddPath("requestExamples", "ApiEndpoints.0.RequestExamples");
                AddPath("requestExampleCount", "ApiEndpoints.0.RequestExamples.Count");
                AddPath("requestHeaders", "ApiEndpoints.0.RequestHeaders");
                AddPath("responseHeaders", "ApiEndpoints.0.ResponseHeaders");
                AddPath("errorResponses", "ApiEndpoints.0.ErrorResponses");
                AddPath("errorResponseCount", "ApiEndpoints.0.ErrorResponses.Count");
                AddPath("errorCatalog", "ApiEndpoints.0.ErrorCatalog");
                AddPath("errorCatalogCount", "ApiEndpoints.0.ErrorCatalog.Count");
                AddPath("successResponseSchema", "ApiEndpoints.0.SuccessResponseSchema");
                AddPath("errorResponseSchema", "ApiEndpoints.0.ErrorResponseSchema");
                AddPath("requestBodyFields", "ApiEndpoints.0.RequestBodyFields");
                AddPath("successResponseFields", "ApiEndpoints.0.SuccessResponseFields");
                AddPath("errorResponseFields", "ApiEndpoints.0.ErrorResponseFields");
                AddPath("faqItems", "FaqItems");
                AddPath("callouts", "Callouts");
                AddPath("primaryActions", "PrimaryActions");
                AddSelector("mainHeading", "h1, h2");
                AddSelector("sectionHeadings", "h2, h3", all: true);
                AddSelector("navigationLinks", "header nav a, nav a, [role='navigation'] a", source: "page", all: true);
                AddSelector("navigationHrefs", "header nav a, nav a, [role='navigation'] a", source: "page", mode: "Attribute", attribute: "href", all: true);
                AddPath("breadcrumbs", "Breadcrumbs.0.Labels");
                AddPath("codeBlockCount", "CodeBlocks.Count");
                AddPath("codeSampleCount", "CodeSamples.Count");
                AddPath("apiEndpointCount", "ApiEndpoints.Count");
                AddSelector("faqCount", "details, [itemscope][itemtype*='Question' i], [itemprop='mainEntity'][itemscope]", mode: "Count");
                AddPath("calloutCount", "Callouts.Count");
                AddPath("primaryActionLabels", "PrimaryActions");
                AddSelector("tableCount", "table", mode: "Count");
                break;

            case HtmlCrawlStructuredJsonPreset.Article:
                AddPath("title", "Metadata.Title");
                AddPath("description", "Metadata.Description");
                AddPath("canonicalUrl", "Metadata.CanonicalUrl");
                AddPath("language", "Metadata.Language");
                AddPath("author", "Metadata.Author");
                AddPath("publishedTime", "Metadata.PublishedTime");
                AddPath("modifiedTime", "Metadata.ModifiedTime");
                AddPath("imageUrl", "Metadata.ImageUrl");
                AddPath("summary", "Document.Summary");
                AddPath("content", "Document.Text");
                AddPath("markdown", "Document.Markdown");
                AddPath("headings", "Document.Headings");
                AddPath("keywords", "Document.Keywords");
                AddPath("links", "Document.Links");
                AddPath("faqItems", "FaqItems");
                AddPath("callouts", "Callouts");
                AddPath("primaryActions", "PrimaryActions");
                AddSelector("mainHeading", "h1");
                AddSelector("lead", "p");
                AddSelector("sectionHeadings", "h2, h3", all: true);
                AddSelector("imageUrls", "article img[src], main img[src], img[src]", source: "page", mode: "Attribute", attribute: "src", all: true);
                break;

            case HtmlCrawlStructuredJsonPreset.Product:
                AddPath("title", "Metadata.Title");
                AddPath("description", "Metadata.Description");
                AddPath("canonicalUrl", "Metadata.CanonicalUrl");
                AddPath("language", "Metadata.Language");
                AddPath("siteName", "Metadata.SiteName");
                AddPath("imageUrl", "Metadata.ImageUrl");
                AddPath("summary", "Document.Summary");
                AddPath("content", "Document.Text");
                AddPath("markdown", "Document.Markdown");
                AddPath("headings", "Document.Headings");
                AddPath("breadcrumbs", "Breadcrumbs.0.Labels");
                AddPath("faqItems", "FaqItems");
                AddPath("specTables", "SpecTables");
                AddPath("primaryActions", "PrimaryActions");
                AddSelector("name", "[itemprop='name'], h1", source: "page");
                AddSelector("price", "[itemprop='price'], .price, .product-price, [data-price], [class*='price']", source: "page");
                AddSelector("priceMeta", "meta[property='product:price:amount'], meta[itemprop='price']", source: "page", mode: "Attribute", attribute: "content");
                AddSelector("currency", "meta[property='product:price:currency'], meta[itemprop='priceCurrency']", source: "page", mode: "Attribute", attribute: "content");
                AddSelector("sku", "[itemprop='sku'], [data-sku], .sku, [class*='sku']", source: "page");
                AddSelector("availability", "[itemprop='availability'], .availability, [data-stock-status], [class*='stock']", source: "page");
                AddSelector("imageUrls", "img[src]", source: "page", mode: "Attribute", attribute: "src", all: true);
                AddSelector("specTableCount", "table", mode: "Count");
                break;
        }

        return schema;
    }

    private static HtmlCrawlStructuredJsonPreset ResolveStructuredJsonPreset(
        HtmlCrawlStructuredJson structuredJson,
        IDocument document,
        IDocument selectedDocument,
        HtmlCrawlStructuredJsonPreset structuredPreset) {
        if (structuredPreset == HtmlCrawlStructuredJsonPreset.None) {
            return HtmlCrawlStructuredJsonPreset.None;
        }

        if (structuredPreset != HtmlCrawlStructuredJsonPreset.Auto) {
            return structuredPreset;
        }

        if (LooksLikeProductPage(document, structuredJson)) {
            return HtmlCrawlStructuredJsonPreset.Product;
        }

        if (LooksLikeDocsPage(document, selectedDocument, structuredJson)) {
            return HtmlCrawlStructuredJsonPreset.Docs;
        }

        return HtmlCrawlStructuredJsonPreset.Article;
    }

    private static bool LooksLikeDocsPage(IDocument document, IDocument selectedDocument, HtmlCrawlStructuredJson structuredJson) {
        string url = structuredJson.Document.Url ?? string.Empty;
        string title = structuredJson.Metadata.Title ?? structuredJson.Document.Title ?? string.Empty;
        bool docsUrl = url.IndexOf("/docs", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/documentation", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/reference", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/api", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/manual", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/guide", StringComparison.OrdinalIgnoreCase) >= 0;
        bool docsTitle = title.IndexOf("docs", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("documentation", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("reference", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("api", StringComparison.OrdinalIgnoreCase) >= 0;
        int codeBlockCount = selectedDocument.QuerySelectorAll("pre, code, samp, kbd").Length;
        int tocCount = document.QuerySelectorAll("[aria-label*='table of contents' i], nav.toc, .toc, .table-of-contents, [data-toc]").Length;
        bool strongNavigation = structuredJson.Layout.Regions.Any(region =>
            string.Equals(region.Kind, "Navigation", StringComparison.OrdinalIgnoreCase) &&
            region.LinkCount >= 4);
        return docsUrl || docsTitle || codeBlockCount > 0 || tocCount > 0 || (strongNavigation && structuredJson.Document.Headings.Count >= 2);
    }

    private static bool LooksLikeProductPage(IDocument document, HtmlCrawlStructuredJson structuredJson) {
        bool hasProductMicrodata = structuredJson.MicrodataItems.Any(item =>
            !string.IsNullOrWhiteSpace(item.Type) &&
            item.Type!.IndexOf("Product", StringComparison.OrdinalIgnoreCase) >= 0);
        bool hasPrice = document.QuerySelectorAll("[itemprop='price'], meta[property='product:price:amount'], .price, .product-price, [data-price], [class*='price']").Length > 0;
        bool hasSku = document.QuerySelectorAll("[itemprop='sku'], [data-sku], .sku, [class*='sku']").Length > 0;
        bool hasAvailability = document.QuerySelectorAll("[itemprop='availability'], .availability, [data-stock-status], [class*='stock']").Length > 0;
        bool hasCommerceAction = document.QuerySelectorAll("button, a")
            .Select(element => NormalizeWhitespace(element.TextContent))
            .Any(text =>
                text.IndexOf("add to cart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("buy now", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("checkout", StringComparison.OrdinalIgnoreCase) >= 0);
        return hasProductMicrodata || (hasPrice && (hasSku || hasAvailability || hasCommerceAction));
    }

    private static PageSearchMetadata BuildPageSearchMetadata(HtmlCrawlPage page) {
        return BuildPageSearchMetadata(page.Html, page.Text, page.Markdown);
    }

    private static PageSearchMetadata BuildPageSearchMetadata(string html, string text, string markdown) {
        string sourceText = GetSearchText(text, markdown, html);
        string[] headings = ExtractHeadings(html);
        List<string> chunkTexts = BuildPageChunkTexts(sourceText);
        return new PageSearchMetadata {
            WordCount = CountWords(sourceText),
            CharacterCount = sourceText.Length,
            ChunkCount = chunkTexts.Count,
            Summary = BuildSummary(sourceText),
            Headings = headings,
            Keywords = ExtractKeywords(sourceText)
        };
    }

    internal static int GetChunkCountForSummary(IEnumerable<HtmlCrawlPage> pages) =>
        BuildChunkRecords(pages).Count;

    internal static int GetStructuredAuthenticatedApiEndpointCount(HtmlCrawlStructuredJson? structuredJson) =>
        structuredJson?.ApiEndpoints.Count(endpoint =>
            endpoint.Authentication.Required.HasValue
            || endpoint.Authentication.Schemes.Count > 0
            || endpoint.Authentication.Headers.Count > 0
            || !string.IsNullOrWhiteSpace(endpoint.Authentication.Summary)) ?? 0;

    internal static int GetStructuredRateLimitedApiEndpointCount(HtmlCrawlStructuredJson? structuredJson) =>
        structuredJson?.ApiEndpoints.Count(endpoint =>
            endpoint.RateLimit.Mentioned
            || endpoint.RateLimit.StatusCode.HasValue
            || endpoint.RateLimit.Headers.Count > 0
            || !string.IsNullOrWhiteSpace(endpoint.RateLimit.Limit)
            || !string.IsNullOrWhiteSpace(endpoint.RateLimit.Summary)) ?? 0;

    internal static int GetStructuredApiErrorResponseCount(HtmlCrawlStructuredJson? structuredJson) =>
        structuredJson?.ApiEndpoints.Sum(endpoint => endpoint.ErrorResponses.Count) ?? 0;

    private static (object GraphDocument, int NodeCount, int EdgeCount, int FetchedNodeCount, int SkippedNodeCount, int ExternalNodeCount, Dictionary<string, int> NodeCategories, Dictionary<string, int> EdgeRelations, Dictionary<string, int> SkippedNodeReasons) BuildGraphDocument(
        IEnumerable<HtmlCrawlPage> pages,
        IEnumerable<HtmlCrawlPage> skippedPages,
        string graphJsonPath) {
        List<HtmlCrawlPage> pageList = pages
            .Where(page => !string.IsNullOrWhiteSpace(page.Url))
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<HtmlCrawlPage> skippedPageList = skippedPages
            .Where(page => !string.IsNullOrWhiteSpace(page.Url))
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<HtmlCrawlPage> skippedGraphPages = skippedPageList
            .Where(page => !IsExternalSkipReason(page.SkipReason))
            .ToList();
        List<HtmlCrawlPage> externalGraphPages = skippedPageList
            .Where(page => IsExternalSkipReason(page.SkipReason))
            .ToList();
        HashSet<string> fetchedUrls = new(pageList.Select(page => page.Url), StringComparer.OrdinalIgnoreCase);
        HashSet<string> skippedUrls = new(skippedGraphPages.Select(page => page.Url), StringComparer.OrdinalIgnoreCase);
        HashSet<string> externalSkippedUrls = new(externalGraphPages.Select(page => page.Url), StringComparer.OrdinalIgnoreCase);
        HashSet<string> edgeKeys = new(StringComparer.OrdinalIgnoreCase);
        List<GraphEdgeRecord> edges = new();
        Dictionary<string, int> incomingTotal = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> outgoingTotal = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> outgoingInternal = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> externalUrls = new(StringComparer.OrdinalIgnoreCase);

        foreach (HtmlCrawlPage page in pageList) {
            IEnumerable<string> pageLinks = page.Links
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string link in pageLinks) {
                string edgeKey = page.Url + "->" + link;
                if (!edgeKeys.Add(edgeKey)) {
                    continue;
                }

                bool targetKnown = fetchedUrls.Contains(link);
                bool targetSkipped = skippedUrls.Contains(link);
                bool targetExternal = externalSkippedUrls.Contains(link);
                string relation = targetKnown
                    ? "fetched"
                    : targetSkipped
                        ? "skipped"
                        : targetExternal
                            ? "external"
                            : "external";
                edges.Add(new GraphEdgeRecord {
                    SourceUrl = page.Url,
                    TargetUrl = link,
                    TargetKnown = targetKnown,
                    Internal = targetKnown,
                    Relation = relation
                });

                outgoingTotal[page.Url] = outgoingTotal.TryGetValue(page.Url, out int total) ? total + 1 : 1;
                incomingTotal[link] = incomingTotal.TryGetValue(link, out int incomingCount) ? incomingCount + 1 : 1;
                if (targetKnown) {
                    outgoingInternal[page.Url] = outgoingInternal.TryGetValue(page.Url, out int internalCount) ? internalCount + 1 : 1;
                } else if (!targetSkipped) {
                    externalUrls.Add(link);
                }
            }
        }

        List<GraphNodeRecord> nodes = pageList.Select(page => new GraphNodeRecord {
            Url = page.Url,
            Title = page.Title,
            Depth = page.Depth,
            Status = page.Status.ToString(),
            Category = "Fetched",
            OfflineReadinessGrade = page.OfflineReadinessGrade,
            HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
            OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
            OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
            InDegree = incomingTotal.TryGetValue(page.Url, out int inDegree) ? inDegree : 0,
            OutDegree = outgoingTotal.TryGetValue(page.Url, out int outDegree) ? outDegree : 0,
            InternalOutDegree = outgoingInternal.TryGetValue(page.Url, out int internalOutDegree) ? internalOutDegree : 0,
            HtmlPath = BuildRelativeOptionalPath(graphJsonPath, page.HtmlPath),
            ManifestPath = BuildRelativeOptionalPath(graphJsonPath, page.ManifestPath)
        }).ToList();

        nodes.AddRange(skippedGraphPages
            .Where(page => !fetchedUrls.Contains(page.Url))
            .Select(page => new GraphNodeRecord {
                Url = page.Url,
                Title = page.Title,
                Depth = page.Depth,
                Status = page.Status.ToString(),
                Category = "Skipped",
                SkipReason = page.SkipReason.ToString(),
                OfflineReadinessGrade = page.OfflineReadinessGrade,
                HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
                InDegree = incomingTotal.TryGetValue(page.Url, out int inDegree) ? inDegree : 0,
                OutDegree = 0,
                InternalOutDegree = 0
            }));

        nodes.AddRange(externalGraphPages
            .Where(page => !fetchedUrls.Contains(page.Url))
            .Select(page => new GraphNodeRecord {
                Url = page.Url,
                Title = page.Title,
                Depth = page.Depth,
                Status = page.Status.ToString(),
                Category = "External",
                SkipReason = page.SkipReason.ToString(),
                OfflineReadinessGrade = page.OfflineReadinessGrade,
                HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
                InDegree = incomingTotal.TryGetValue(page.Url, out int inDegree) ? inDegree : 0,
                OutDegree = 0,
                InternalOutDegree = 0
            }));

        nodes.AddRange(externalUrls
            .Where(url => !externalSkippedUrls.Contains(url))
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .Select(url => new GraphNodeRecord {
                Url = url,
                Depth = -1,
                Status = "Discovered",
                Category = "External",
                InDegree = incomingTotal.TryGetValue(url, out int inDegree) ? inDegree : 0,
                OutDegree = 0,
                InternalOutDegree = 0
            }));

        int fetchedNodeCount = nodes.Count(node => string.Equals(node.Category, "Fetched", StringComparison.OrdinalIgnoreCase));
        int skippedNodeCount = nodes.Count(node => string.Equals(node.Category, "Skipped", StringComparison.OrdinalIgnoreCase));
        int externalNodeCount = nodes.Count(node => string.Equals(node.Category, "External", StringComparison.OrdinalIgnoreCase));
        Dictionary<string, int> nodeCategories = nodes
            .GroupBy(node => node.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> edgeRelations = edges
            .GroupBy(edge => edge.Relation, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> skippedNodeReasons = nodes
            .Where(node => string.Equals(node.Category, "Skipped", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(node.SkipReason))
            .GroupBy(node => node.SkipReason!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> offlineReadinessCounts = nodes
            .GroupBy(node => node.OfflineReadinessGrade, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> offlineSeverityCounts = nodes
            .GroupBy(node => node.HighestOfflineRiskSeverity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> offlineDependencyKindCounts = nodes
            .SelectMany(node => string.IsNullOrWhiteSpace(node.OfflineDependencyKindsSummary)
                ? Array.Empty<string>()
                : node.OfflineDependencyKindsSummary.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(kind => kind.Trim())
                    .Where(kind => !string.IsNullOrWhiteSpace(kind)))
            .GroupBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        int offlineRiskNodeCount = nodes.Count(node => node.OfflineDependencyDiagnosticCount > 0);
        int highOfflineRiskNodeCount = nodes.Count(node => string.Equals(node.HighestOfflineRiskSeverity, "high", StringComparison.OrdinalIgnoreCase));

        object graphDocument = new {
            Summary = new {
                NodeCount = nodes.Count,
                EdgeCount = edges.Count,
                FetchedNodeCount = fetchedNodeCount,
                SkippedNodeCount = skippedNodeCount,
                ExternalNodeCount = externalNodeCount,
                OfflineRiskNodeCount = offlineRiskNodeCount,
                HighOfflineRiskNodeCount = highOfflineRiskNodeCount,
                OfflineReadinessCounts = offlineReadinessCounts,
                OfflineSeverityCounts = offlineSeverityCounts,
                OfflineDependencyKindCounts = offlineDependencyKindCounts,
                NodeCategories = nodeCategories,
                EdgeRelations = edgeRelations,
                SkippedNodeReasons = skippedNodeReasons
            },
            Nodes = nodes.OrderBy(node => node.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.Depth)
                .ThenBy(node => node.Url, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Edges = edges.OrderBy(edge => edge.SourceUrl, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.TargetUrl, StringComparer.OrdinalIgnoreCase)
                .Select(edge => new {
                    edge.SourceUrl,
                    edge.TargetUrl,
                    edge.TargetKnown,
                    edge.Internal,
                    edge.Relation
                })
                .ToArray()
        };
        return (graphDocument, nodes.Count, edges.Count, fetchedNodeCount, skippedNodeCount, externalNodeCount, nodeCategories, edgeRelations, skippedNodeReasons);
    }

    private static bool IsExternalSkipReason(HtmlCrawlSkipReason reason) =>
        reason == HtmlCrawlSkipReason.OutsideHost;

    private static List<PageChunkRecord> BuildChunkRecords(IEnumerable<HtmlCrawlPage> pages) {
        List<PageChunkRecord> chunks = new();
        HashSet<string> seenFingerprints = new(StringComparer.OrdinalIgnoreCase);
        int nextChunkId = 1;

        foreach (HtmlCrawlPage page in pages.Where(page => page.Status == HtmlCrawlPageStatus.Success)) {
            PageSearchMetadata searchMetadata = BuildPageSearchMetadata(page);
            List<string> pageChunks = BuildPageChunkTexts(page.Text, page.Markdown, page.Html);
            int chunkIndex = 1;
            foreach (string chunkText in pageChunks) {
                string fingerprint = ComputeContentFingerprint(chunkText);
                if (!seenFingerprints.Add(fingerprint)) {
                    continue;
                }

                chunks.Add(new PageChunkRecord {
                    ChunkId = $"chunk-{nextChunkId:D5}",
                    Url = page.Url,
                Title = page.Title,
                Depth = page.Depth,
                ChunkIndex = chunkIndex++,
                WordCount = CountWords(chunkText),
                CharacterCount = chunkText.Length,
                    Summary = BuildSummary(chunkText),
                    Headings = searchMetadata.Headings,
                    Keywords = ExtractKeywords(chunkText),
                Text = chunkText,
                HtmlPath = page.HtmlPath,
                TextPath = page.TextPath,
                ManifestPath = page.ManifestPath,
                OfflineReadinessGrade = page.OfflineReadinessGrade,
                HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
                Fingerprint = fingerprint
            });
                nextChunkId++;
            }
        }

        return chunks;
    }

    private static List<string> BuildPageChunkTexts(HtmlCrawlPage page) =>
        BuildPageChunkTexts(page.Text, page.Markdown, page.Html);

    private static List<string> BuildPageChunkTexts(string text, string markdown, string html) {
        string sourceText = GetSearchText(text, markdown, html);
        return BuildPageChunkTexts(sourceText);
    }

    private static List<string> BuildPageChunkTexts(string sourceText) {
        if (string.IsNullOrWhiteSpace(sourceText)) {
            return new List<string>();
        }

        const int targetWords = 140;
        const int overlapWords = 30;
        string[] words = sourceText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) {
            return new List<string>();
        }

        if (words.Length <= targetWords) {
            return new List<string> { sourceText };
        }

        List<string> chunks = new();
        int start = 0;
        while (start < words.Length) {
            int length = Math.Min(targetWords, words.Length - start);
            string chunk = string.Join(" ", words.Skip(start).Take(length)).Trim();
            if (!string.IsNullOrWhiteSpace(chunk)) {
                chunks.Add(chunk);
            }

            if (start + length >= words.Length) {
                break;
            }

            start += Math.Max(1, targetWords - overlapWords);
        }

        return chunks;
    }

    private static string GetSearchText(HtmlCrawlPage page) =>
        GetSearchText(page.Text, page.Markdown, page.Html);

    private static string GetSearchText(string text, string markdown, string html) {
        if (!string.IsNullOrWhiteSpace(text)) {
            return NormalizeWhitespace(text);
        }

        if (!string.IsNullOrWhiteSpace(markdown)) {
            return NormalizeWhitespace(markdown);
        }

        if (!string.IsNullOrWhiteSpace(html)) {
            return NormalizeWhitespace(HtmlParserToText.ConvertToText(html));
        }

        return string.Empty;
    }

}
