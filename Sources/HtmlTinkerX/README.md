# HtmlTinkerX

HtmlTinkerX is a .NET library for parsing and inspecting HTML, CSS, and JavaScript. It includes static extraction, website crawling, audit-oriented page analysis, email CSS inlining, and Playwright browser automation. PowerShell users can access the same engine through the `PSParseHTML` module.

## Install

```shell
dotnet add package HtmlTinkerX
```

Pin a stable HtmlTinkerX version when reproducible restores matter.

## Read a page as objects

`HtmlPageReader` uses the canonical `OfficeIMO.Html` semantic document and adds
web-specific links, resources, and repeated-collection inference. Callers do not
need CSS selectors to inspect common page content.

```csharp
using HtmlTinkerX;

string html = await File.ReadAllTextAsync("catalog.html");
HtmlPageDocument page = HtmlPageReader.Read(
    html,
    new HtmlPageReaderOptions {
        BaseUri = new Uri("https://example.org/catalog")
    });

foreach (var heading in page.Headings) {
    Console.WriteLine($"{heading.Level}: {heading.Text}");
}

foreach (var table in page.Tables) {
    Console.WriteLine($"{table.Caption}: {table.Rows.Count} rows");
}

foreach (var item in page.Collections.FirstOrDefault()?.Items
         ?? Array.Empty<HtmlPageCollectionItem>()) {
    Console.WriteLine(item["Title"]);
}
```

`Sections`, `Blocks`, `Headings`, `Paragraphs`, `Lists`, and `Tables` come from
the shared OfficeIMO semantic model. `Collections` are inferred by HtmlTinkerX
from repeated cards, rows, or listings. Each collection retains its selector as
provenance, but callers do not have to provide one.

Use `page.Markdown` when a text projection is more convenient for display,
search, or language-model input.

## Parse HTML

```csharp
using AngleSharp.Dom;
using HtmlTinkerX;

IDocument document = HtmlParser.ParseWithAngleSharp("""
    <article>
      <h1>Service status</h1>
      <a href="/incidents/42">Current incident</a>
    </article>
    """);

string title = document.QuerySelector("h1")?.TextContent ?? string.Empty;
```

## Audit generated or supplied HTML

`HtmlDocumentAudit` provides one reusable contract for static output checks. It reports duplicate IDs, missing document metadata, image alternatives, control names and labels, unsafe URL schemes, and heading-order problems.

```csharp
HtmlDocumentAuditResult audit = HtmlDocumentAudit.Analyze(html);

foreach (HtmlDocumentAuditIssue issue in audit.Issues) {
    Console.WriteLine($"{issue.Severity}: {issue.Code} - {issue.Message}");
}
```

The audit is diagnostic; it does not rewrite the document or claim full WCAG conformance.

For client-rendered pages, run the same contract after navigation:

```csharp
await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(url);
HtmlDocumentAuditResult renderedAudit = await HtmlBrowser.AuditDocumentAsync(session);
```

Rendered UI tests can inspect styles and attributes without creating another Playwright wrapper:

```csharp
IReadOnlyDictionary<string, string> values = await HtmlBrowser.GetComputedStylesAsync(
    session,
    ".report-panel",
    new[] { "position", "padding", "overflow" });

string? label = await HtmlBrowser.GetAttributeAsync(session, "#export", "aria-label");
```

URL parsers stream responses with a 16 MiB default limit and cooperative
cancellation. Override the bound for a specific operation when needed:

```csharp
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var document = await HtmlParser.ParseUrlWithAngleSharpAsync(
    "https://example.org/status",
    fetchOptions: new HtmlHttpFetchOptions { MaximumResponseBytes = 8 * 1024 * 1024 },
    cancellationToken: timeout.Token);
```

## Choose an extraction workflow

`HtmlExtractionPlanner` inspects page signals before you decide whether static parsing, a browserless relay, or rendered browser extraction is appropriate.

```csharp
using HtmlTinkerX;

HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(
    html,
    new Uri("https://example.org/status"));

Console.WriteLine($"{plan.RecommendedMode}: {plan.Confidence}");
foreach (string reason in plan.Reasons) {
    Console.WriteLine(reason);
}
```

## Crawl a bounded website scope

```csharp
using HtmlTinkerX;

HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(
    "https://example.org/docs/",
    new HtmlCrawlOptions {
        MaxDepth = 2,
        MaxPages = 100,
        PathPrefix = "/docs/",
        IncludeMarkdown = true
    });

Console.WriteLine($"Fetched {result.Pages.Count} pages");
```

The crawler restricts requests to the starting host, honors `robots.txt`, and uses sitemaps by default. Review `HtmlCrawlOptions` before crawling sites you do not control.

See the [repository](https://github.com/EvotecIT/HtmlTinkerX) for PowerShell examples, browser automation, API discovery, SSO handoff analysis, and package documentation.

HtmlTinkerX is available under the MIT License. See the LICENSE file included
in the package.
