# HtmlTinkerX

HtmlTinkerX is a .NET library for parsing and inspecting HTML, CSS, and JavaScript. It includes static extraction, website crawling, audit-oriented page analysis, email CSS inlining, and Playwright browser automation. PowerShell users can access the same engine through the `PSParseHTML` module.

## Install

```shell
dotnet add package HtmlTinkerX --prerelease
```

The 2.1 line is prerelease while its required upstream AngleSharp CSS and
JavaScript integrations remain prerelease packages.

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

Copyright (c) 2011-2026 Przemyslaw Klys, Evotec. All rights reserved. See the
LICENSE file included in this package.
