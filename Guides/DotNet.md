# Use HtmlTinkerX from .NET

[Back to the project overview](../README.MD)

### C# Example
```csharp
using HtmlTinkerX;

// Read semantic page objects and inferred collections
string html = await File.ReadAllTextAsync("page.html");
HtmlPageDocument page = HtmlPageReader.Read(
    html,
    new HtmlPageReaderOptions {
        BaseUri = new Uri("https://example.org/catalog")
    });

Console.WriteLine(page.Headings[0].Text);
Console.WriteLine(page.Tables.Count);
Console.WriteLine(page.Collections[0].Items[0]["Title"]);

// Audit or use lower-level parsers when you need them
HtmlDocumentAuditResult audit = HtmlDocumentAudit.Analyze(html);

// Format and optimize resources
string formatted = HtmlFormatter.FormatHtml(html);
string minified = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);

// Browser automation
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");
await HtmlBrowser.CaptureScreenshotAsync(session.Page, "screenshot.png");

// Offline crawl
var crawl = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    MaxDepth = 1,
    MaxPages = 10,
    MaximumTotalPageResponseBytes = 128L * 1024 * 1024,
    MaximumTotalAssetResponseBytes = 256L * 1024 * 1024,
    UseSitemaps = true,
    RespectRobotsTxt = true,
    DeduplicatePages = true,
    OutputPath = "crawl-output"
});

Console.WriteLine(crawl.Summary.ToReportText(crawl.SitemapUrls));
Console.WriteLine(crawl.PagesCsvPath);

// AI-ready offline dataset with Markdown alongside HTML/text
var aiReady = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Selector = "main",
    IncludeMarkdown = true,
    OutputPath = "crawl-output"
});
Console.WriteLine(aiReady.Pages[0].MarkdownPath);

// Self-contained JSON document export for downstream automation
var structured = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Selector = "main",
    IncludeStructuredJson = true,
    OutputPath = "crawl-output"
});
Console.WriteLine(structured.StructuredJsonPagesJsonlPath);
Console.WriteLine(structured.OpenApiLikePath);
Console.WriteLine(structured.OpenApiPath);
Console.WriteLine(structured.OpenApiDocument["openapi"]);
Console.WriteLine(structured.OpenApiLike.StrictOpenApiEligibleOperationCount);
Console.WriteLine(structured.OpenApiLike.StrictOpenApiSkippedOperationCount);
Console.WriteLine(structured.OpenApiDocument.ContainsKey("x-htmltinkerx-promotion"));
Console.WriteLine(structured.Pages[0].StructuredJson!.Document.Summary);
Console.WriteLine(structured.Pages[0].StructuredJson!.Document.Markdown);
Console.WriteLine(structured.Pages[0].StructuredJson!.Metadata.Description);
Console.WriteLine(structured.Pages[0].StructuredJson!.Layout.NavigationCount);
Console.WriteLine(structured.Pages[0].StructuredJson!.CodeBlocks.Count);
Console.WriteLine(structured.Pages[0].StructuredJson!.CodeSamples.Count);
Console.WriteLine(structured.Pages[0].StructuredJson!.ApiEndpoints.Count);
Console.WriteLine(structured.Pages[0].StructuredJson!.Breadcrumbs.Count);
Console.WriteLine(structured.Pages[0].StructuredJson!.FaqItems.Count);
Console.WriteLine(structured.Pages[0].StructuredJson!.SpecTables.Count);
Console.WriteLine(structured.Pages[0].StructuredJson!.Callouts.Count);
Console.WriteLine(structured.Pages[0].StructuredJson!.PrimaryActions.Count);

// Built-in preset fields for docs/article/product pages, with auto mode available too
var docsJson = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Selector = "main",
    IncludeStructuredJson = true,
    StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Docs,
    OutputPath = "crawl-output"
});
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ResolvedPreset);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["mainHeading"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["navigationLinks"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["codeSamples"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["apiEndpoints"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["apiCatalog"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["apiTags"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["apiResources"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["operationIds"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["openApiLike"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["openApiPaths"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["openApiServers"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["authenticationSchemes"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["rateLimitHeaders"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["operationId"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["resource"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["tags"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["requestExamples"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["requestHeaders"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["responseHeaders"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["errorResponses"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["errorCatalog"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["successResponseSchema"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["errorResponseSchema"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["requestBodyFields"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["successResponseFields"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["errorResponseFields"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].Parameters.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].OperationId);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].Resource);
Console.WriteLine(string.Join(", ", docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].Tags));
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].BodyParameters.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].HeaderParameters.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].RequestBodySchema["name"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].RequestBodyFields[0].Path);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].RequestBodyFields[0].Provenance[0].Kind);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].RequestBodyFields[0].ConfidenceScore);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].BodyParameters[0].Format);
Console.WriteLine(string.Join(", ", docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].BodyParameters[0].EnumValues));
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].BodyParameters[0].ExampleValue);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].Authentication.Required);
Console.WriteLine(string.Join(", ", docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].Authentication.Schemes));
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].RateLimit.Limit);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].RequestExamples.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].RequestHeaders.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].ResponseHeaders.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].ErrorResponses.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].ErrorCatalog.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].SuccessResponseSchema["id"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].ErrorResponseSchema["error"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].SuccessResponseFields[0].Path);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].SuccessResponseFields[0].Provenance[0].Kind);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].SuccessResponseFields[0].ConfidenceScore);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].ErrorResponseFields[0].Path);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].SuccessResponseFields[0].Kind);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].SuccessResponseFields[0].ChildPaths.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiEndpoints[0].ResponseExamples.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.ApiCatalog.OperationCount);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.OpenApiLike.Paths["/v1/widgets"].Operations["post"].OperationId);
Console.WriteLine(docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].OperationId);
Console.WriteLine(docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].AuthenticationRef);
Console.WriteLine(docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ParametersRef);
Console.WriteLine(docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].RequestHeadersRef);
Console.WriteLine(docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ResponseExamplesRef);
Console.WriteLine(docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].StrictOpenApiScore);
Console.WriteLine(docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].StrictOpenApiEligible);
Console.WriteLine(string.Join(", ", docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].Provenance.PageUrls));
Console.WriteLine(string.Join(", ", docsJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].Provenance.SourceKinds));
Console.WriteLine(docsJson.OpenApiLike.Components.AuthProfiles.Count);
Console.WriteLine(docsJson.OpenApiLike.Components.FieldSets.Count);
Console.WriteLine(docsJson.OpenApiLike.Components.ParameterSets.Count);
Console.WriteLine(docsJson.OpenApiLike.Components.RequestHeaderSets.Count);
Console.WriteLine(docsJson.OpenApiLike.Components.ResponseExampleSets.Count);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["breadcrumbs"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["faqItems"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["callouts"]);
Console.WriteLine(docsJson.Pages[0].StructuredJson!.Extracted["primaryActions"]);

// Caller-defined JSON fields layered on top of the built-in structured model
var extracted = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Selector = "main",
    StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Docs,
    StructuredJsonSchema = """
    {
      "title": "Metadata.Title",
      "description": "Metadata.Description",
      "navLinks": { "selector": "nav a", "source": "page", "mode": "text", "all": true },
      "mainHeading": { "selector": "h1", "source": "selected", "mode": "text" }
    }
    """
});
Console.WriteLine(extracted.Pages[0].StructuredJson!.Extracted["title"]);
Console.WriteLine(extracted.Pages[0].StructuredJson!.Extracted["mainHeading"]);

// One-call dataset mode turns on Markdown + structured JSON defaults
var dataset = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Scenario = HtmlCrawlScenario.Dataset,
    OutputPath = "crawl-output"
});
Console.WriteLine(dataset.Pages[0].MarkdownPath);
Console.WriteLine(dataset.Pages[0].StructuredJsonPath);
Console.WriteLine(dataset.Pages[0].StructuredJson!.ResolvedPreset);

// Keep full tracking query strings when a site uses them as real page identity
var exactUrls = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    IgnoreTrackingQueryParameters = false
});

// Allow non-HTML responses when you intentionally want them in the crawl
var permissive = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    SkipKnownAssetUrls = false,
    RestrictToAllowedContentTypes = false
});

// Download discovered images/documents into the offline dataset
var richSnapshot = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    MaxDepth = 1,
    DownloadAssets = true,
    OutputPath = "crawl-output"
});
// Stored HTML now points at local ../assets/... paths by default

// Hybrid crawl for JavaScript-heavy pages with cleanup of known noisy blocks
var hybrid = await HtmlCrawler.CrawlAsync("https://example.com/app", new HtmlCrawlOptions {
    AutoRender = true,
    WaitForSelector = "#main",
    DismissSelectors = { ".cookie-banner button", "#consent-accept" },
    DismissTexts = { "Accept", "I agree" },
    ClickSelectors = { ".load-more", ".expand-details" },
    ClickTexts = { "Load more", "Show more" },
    InteractionRepeatCount = 2,
    AutoScroll = true,
    ExcludeSelectors = { ".language-switcher", ".share-links", ".related-posts" }
});
Console.WriteLine($"{hybrid.Pages[0].RenderMode}: {hybrid.Pages[0].RenderReason}");
Console.WriteLine(string.Join(", ", hybrid.Pages[0].AppliedInteractions));
Console.WriteLine(hybrid.Summary.ToReportText(hybrid.SitemapUrls));

// Choose how content is selected before cleanup
var raw = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Selector = "#main",
    ContentMode = HtmlCrawlContentMode.Raw
});
var focused = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Selector = "main",
    ContentMode = HtmlCrawlContentMode.Focused
});
var reader = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    ContentMode = HtmlCrawlContentMode.Reader,
    CompareContentModes = true,
    ReaderMinimumWordCount = 30,
    ReaderMinimumScore = 40
});
Console.WriteLine($"{reader.Pages[0].ContentModeUsed} / {reader.Pages[0].ContentSelectionReasonCode}");
Console.WriteLine(reader.Pages[0].ContentElementSelectorHint);
Console.WriteLine($"{reader.Pages[0].ContentSelectionScore} across {reader.Pages[0].ReaderCandidateCount} reader candidates");
Console.WriteLine(string.Join(", ", reader.Pages[0].ContentComparisons.Select(c => $"{c.Mode}:{c.WordCount}")));
Console.WriteLine(reader.Pages[0].ContentComparisonDeltaSummary);
Console.WriteLine(reader.Pages[0].ContentComparisonPreviewSummary);
Console.WriteLine(reader.Summary.ContentComparisonWinnerPreviewSamples["Reader"]);

// Remove noisy blocks by class or id while keeping smart cleanup enabled
var clean = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    Selector = "main",
    ExcludeClasses = { "promo-box", "doc-tools" },
    ExcludeIds = { "reader-tools" }
});

// Start from an intent-focused scenario instead of hand-tuning many knobs
var docsScenario = await HtmlCrawler.CrawlAsync("https://docs.example.com/", new HtmlCrawlOptions {
    Scenario = HtmlCrawlScenario.Docs
});
Console.WriteLine(docsScenario.AppliedScenario);
Console.WriteLine(docsScenario.Pages[0].ContentModeUsed);

// Reuse a built-in profile for a common site family
var profiled = await HtmlCrawler.CrawlAsync("https://docs.example.com/", new HtmlCrawlOptions {
    ProfileName = "docs-content"
});
Console.WriteLine(profiled.AppliedProfileName);
Console.WriteLine(profiled.AppliedProfileReasonCode);
Console.WriteLine(profiled.Pages[0].ContentComparisonDeltaSummary);
```

## 🎯 C# API Reference

### Core Classes

#### HtmlParser
```csharp
// Parse with different engines
var doc = HtmlParser.ParseWithAngleSharp(html);
var doc2 = HtmlParser.ParseWithHtmlAgilityPack(html);

// Extract tables with detailed information
var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html);
var tables2 = HtmlParser.ParseTablesWithHtmlAgilityPack(html);

// Parse from URLs
var urlDoc = await HtmlParser.ParseUrlWithAngleSharpAsync("https://example.com");
```

#### HtmlFormatter
```csharp
// Format different resource types
string formattedHtml = HtmlFormatter.FormatHtml(html);
string formattedCss = HtmlFormatter.FormatCss(css);
string formattedJs = HtmlFormatter.FormatJavaScript(javascript);

// Custom JavaScript formatting options
var options = new BeautifierOptions {
    IndentSize = 2,
    BraceStyle = BraceStyle.Expand
};
string customJs = HtmlFormatter.FormatJavaScript(javascript, options);

// Async operations
string formatted = await HtmlFormatter.FormatHtmlAsync(html);
```

#### HtmlOptimizer
```csharp
// Minify resources
string minifiedHtml = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);
string minifiedCss = HtmlOptimizer.OptimizeCss(css);
string minifiedJs = HtmlOptimizer.OptimizeJavaScript(javascript);

// File operations
string optimizedFile = await HtmlOptimizer.OptimizeHtmlFileAsync(
    "input.html",
    cssDecodeEscapes: false);
await File.WriteAllTextAsync("output.html", optimizedFile);
```

#### HtmlBrowser (Browser Automation)
```csharp
// Create browser sessions
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");

// Form-based authentication
var formLogin = new HtmlFormLogin
{
    LoginUrl = "https://example.com/login",
    UsernameSelector = "#username",
    PasswordSelector = "#password",
    SubmitSelector = "button[type='submit']"
};
await using var authSession = await HtmlBrowser.OpenSessionAsync(
    "https://example.com/protected",
    username: "user",
    password: "pass",
    formLogin: formLogin
);

// Screenshots
await HtmlBrowser.CaptureScreenshotAsync(session.Page, "screenshot.png");
await HtmlBrowser.CaptureScreenshotAsync(
    session.Page,
    "full.png",
    new ScreenshotOptions { FullPage = true });

// PDF generation from an already-loaded page
await HtmlBrowser.SavePagePdfAsync(
    session.Page,
    "document.pdf",
    new HtmlBrowserPdfOptions(
        format: PdfPageFormat.A4,
        printBackground: true));

// High-throughput PDF generation with warm Chromium reuse and isolated contexts
await using var pdfRenderer = new HtmlBrowserPdfRenderer(
    new HtmlBrowserPdfRendererOptions(
        minimumBrowserInstances: 1,
        maximumBrowserInstances: 4,
        maximumQueuedCaptures: 32));
await pdfRenderer.PreWarmAsync();

var pdfRequest = new HtmlBrowserPdfRequest(
    HtmlBrowserPdfSource.FromHtml(
        "<main><h1>Quarterly report</h1></main>",
        new Uri("https://reports.example.com/assets/")),
    pdfOptions: new HtmlBrowserPdfOptions(
        format: PdfPageFormat.A4,
        printBackground: true,
        tagged: true,
        outline: true));

HtmlBrowserPdfResult pdfResult = await pdfRenderer.CaptureAsync(pdfRequest);
await File.WriteAllBytesAsync("quarterly.pdf", pdfResult.PdfBytes);

// Navigation
await HtmlBrowser.NavigateAsync(session, "https://example.com/page2");

// JavaScript execution
string? title = await HtmlBrowser.EvaluateAsync<string>(session, "document.title");

// Element interaction
await HtmlBrowser.ClickSelectorAsync(session, "#button");
await HtmlBrowser.FillInputAsync(session, "#username", "user");
await HtmlBrowser.ClickSelectorAsync(session, "#loginForm button[type='submit']");
```

`HtmlBrowserPdfRenderer` accepts URL, HTML-string, and file sources. Each capture gets a fresh context while the bounded pool reuses Chromium processes, applies finite prewarm and pre-navigation setup deadlines, origin-scoped headers/storage, cookie-scoped authentication, viewport and device emulation, readiness and print options, streams PDF output through a configurable 128 MiB default limit, and returns lifecycle diagnostics with the PDF bytes. HTML-string requests need an HTTP/HTTPS base URI when using headers or web storage. Capture fails if Chromium rejects a requested storage entry instead of continuing with missing state. PDF capture is Chromium-only; Firefox and WebKit requests fail before launch.

The renderer validates HTTPS and blocks private-network HTTP(S)/WS(S) and arbitrary file requests by default. Its browser-slot proxy connects to the exact DNS address accepted by policy, while canonical path checks reject file symlink escapes. Configure `HtmlBrowserNetworkPolicy` with explicit internal hosts or file roots when the caller and resources are trusted. A caller-supplied proxy requires explicit private-network mode because that proxy owns DNS and outbound enforcement. These controls do not replace container or host egress policy for services that accept untrusted input.

#### HtmlUtilities
```csharp
// Convert HTML to plain text
string plainText = HtmlParserToText.ConvertToText(html);

// HTTP client operations
HttpClient httpClient = HtmlHttpClientFactory.Shared;
string content = await httpClient.GetStringAsync("https://example.com");
```

#### PreMailerClient
```csharp
// Email optimization
PreMailerResult inlineResult = PreMailerClient.MoveCssInline(emailHtml, new PreMailerOptions());
string inlinedHtml = inlineResult.Html;

var preMailerOptions = new PreMailerOptions { DownloadRemoteCss = true };
PreMailerResult remoteCssResult = await PreMailerClient.MoveCssInlineAsync(emailHtml, preMailerOptions);
string optimized = remoteCssResult.Html;
```

### Extension Methods

#### HtmlParserExtensions
```csharp
// Quick element queries
var elements = HtmlParserExtensions.GetElements(html, className: "class-name");
var byId = HtmlParserExtensions.GetElements(html, id: "element-id");
var byTag = HtmlParserExtensions.GetElements(html, tag: "p");
```
