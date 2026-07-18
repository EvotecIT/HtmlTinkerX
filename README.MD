# HtmlTinkerX and PSParseHTML

HtmlTinkerX is a .NET library for parsing, extracting, auditing, formatting, and
rendering web content. PSParseHTML is its PowerShell surface. Both use the same
typed engines so scripts can start in PowerShell and move to C# without changing
the underlying behavior.

Use the project for work such as:

- extracting tables, lists, forms, metadata, JSON-LD, microdata, Open Graph,
  app state, tokens, image candidates, and JavaScript endpoints;
- auditing a page's interaction surface, resources, browser requests, console
  output, cookies, storage, and SSO handoffs;
- crawling websites into offline HTML, Markdown, JSON, JSONL, graph, and asset
  datasets;
- rendering JavaScript applications with Playwright and collecting screenshots,
  PDFs, HAR data, traces, and repeatable browser recipes;
- formatting or optimizing HTML, CSS, JavaScript, and email markup.

## Install

### PowerShell

```powershell
Install-Module PSParseHTML -AllowPrerelease -AllowClobber
Import-Module PSParseHTML
```

### .NET

```shell
dotnet add package HtmlTinkerX --prerelease
```

The library targets .NET Framework 4.7.2, .NET 8, and .NET 10. The PowerShell
module supports Windows PowerShell 5.1 and modern PowerShell.

## PowerShell quick start

Parse a remote page or raw markup:

```powershell
$document = ConvertFrom-Html -Url 'https://example.com' -Engine AngleSharp -Raw
$document.QuerySelector('h1').TextContent

$local = ConvertFrom-Html -Content '<main><h1>Audit me</h1></main>' -Raw
```

Turn real-world tables into objects and keep link targets:

```powershell
$rows = ConvertFrom-HtmlTable `
    -Url 'https://example.com/status' `
    -Engine AngleSharp `
    -IncludeLinkUrls `
    -CleanHeaders

$rows | Format-Table
```

Select normalized data families without scripting against parser internals:

```powershell
$data = Select-HtmlData `
    -Url 'https://example.com/product' `
    -Kind JsonLd, Microdata, OpenGraph, Meta, HeadLink, Form, Link, Asset

$data | Group-Object Kind
```

Map interactive and script-backed entry points for an audit:

```powershell
$html = (Invoke-WebRequest 'https://example.com/app').Content

Find-HtmlInteractionSurface `
    -Content $html `
    -BaseUrl 'https://example.com/app' `
    -IncludeLinkedScripts
```

Crawl a documentation site into an offline dataset:

```powershell
$crawl = Invoke-HtmlCrawl `
    -Url 'https://example.com/docs/' `
    -Scenario Dataset `
    -MaxDepth 2 `
    -MaxPages 100 `
    -AutoRender `
    -DownloadAssets `
    -OutPath '.\audit-dataset'

$crawl.Summary
```

For authenticated or highly dynamic sites, create a browser session and collect
evidence through the typed browser cmdlets:

```powershell
$session = Start-HtmlBrowserSession -Url 'https://example.com/app'
try {
    Wait-HtmlBrowserReady -Session $session
    Get-HtmlBrowserNetworkLog -Session $session
    Get-HtmlBrowserConsoleLog -Session $session
    Save-HtmlBrowserScreenshot -Session $session -Path '.\evidence\app.png'
} finally {
    Close-HtmlBrowserSession -Session $session
}
```

## C# quick start

Parse a bounded remote response with cooperative cancellation:

```csharp
using HtmlTinkerX;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var fetch = new HtmlHttpFetchOptions {
    MaximumResponseBytes = 8 * 1024 * 1024
};

var document = await HtmlParser.ParseUrlWithAngleSharpAsync(
    "https://example.com",
    fetchOptions: fetch,
    cancellationToken: timeout.Token);

Console.WriteLine(document.QuerySelector("h1")?.TextContent);
```

Every URL-based parser uses streamed response handling and a 16 MiB default
limit. `Content-Length` is checked before reading and the limit is enforced again
while streaming, so missing or false response metadata cannot bypass it. Supply
`HtmlHttpFetchOptions` when an operation needs a different bound.

Extract normalized data and interaction surfaces:

```csharp
string html = await File.ReadAllTextAsync("page.html");
var baseUri = new Uri("https://example.com/app");

var data = HtmlParsingToolbox.SelectData(
    html,
    new[] { "JsonLd", "Microdata", "OpenGraph", "Form", "Asset" },
    baseUri);

var interactions = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
    html,
    baseUri,
    includeLinkedScripts: true,
    includeExternalLinkedScripts: false,
    client: null);
```

Crawl and persist a dataset:

```csharp
var result = await HtmlCrawler.CrawlAsync(
    "https://example.com/docs/",
    new HtmlCrawlOptions {
        Scenario = HtmlCrawlScenario.Dataset,
        MaxDepth = 2,
        MaxPages = 100,
        AutoRender = true,
        DownloadAssets = true,
        OutputPath = "audit-dataset"
    },
    timeout.Token);
```

## Real API map

These are the supported high-level routes. The PowerShell commands are thin
surfaces over the listed HtmlTinkerX owners.

| Task | PowerShell | C# owner |
| --- | --- | --- |
| Parse a document | `ConvertFrom-Html` | `HtmlParser.ParseWithAngleSharp`, `HtmlParser.ParseWithHtmlAgilityPack` |
| Extract tables | `ConvertFrom-HtmlTable` | `HtmlParser.ParseTablesWithAngleSharpDetailed`, `HtmlParser.ParseTablesWithHtmlAgilityPackDetailed` |
| Extract lists | `ConvertFrom-HtmlList` | `HtmlParser.ParseListsWithAngleSharpDetailed`, `HtmlParser.ParseListsWithHtmlAgilityPackDetailed` |
| Extract forms | `ConvertFrom-HtmlForm` | `HtmlParser.ParseFormsWithAngleSharp` |
| Extract metadata | `ConvertFrom-HtmlMeta` | `HtmlParser.ParseMetaTags` |
| Extract Open Graph | `ConvertFrom-HtmlOpenGraph` | `HtmlParser.ParseOpenGraph` |
| Extract microdata | `ConvertFrom-HtmlMicrodata` | `HtmlParser.ParseMicrodataItems` |
| Normalize mixed page data | `Select-HtmlData` | `HtmlParsingToolbox.SelectData` |
| Find interaction surfaces | `Find-HtmlInteractionSurface` | `HtmlParsingToolbox.FindInteractionSurfaceAsync` |
| Discover API endpoints | `Find-HtmlApiEndpoint` | `HtmlApiEndpointInventory.BuildAsync` |
| Compare static and rendered HTML | `Compare-HtmlStaticRendered` | `HtmlParsingToolbox.CompareStaticRendered` |
| Crawl and export a dataset | `Invoke-HtmlCrawl` | `HtmlCrawler.CrawlAsync` |
| Browser automation | `Start-HtmlBrowserSession` and related cmdlets | `HtmlBrowser` |
| Inline email CSS | `Optimize-Email` | `PreMailerClient` |

The complete PowerShell command reference is generated in [Docs](Docs). Package
and dependency policy is documented in [Docs/Dependencies.md](Docs/Dependencies.md).

## Crawler behavior worth knowing

The crawler is page-oriented by default:

- only HTML/XHTML responses are treated as pages;
- obvious document, image, archive, and font URLs are skipped before fetching;
- `robots.txt` and sitemaps are honored unless explicitly disabled;
- tracking query parameters are normalized away;
- internal page links and downloaded asset references are rewritten to local
  relative paths in persisted snapshots;
- each page can produce HTML, text, Markdown, metadata, structured JSON, chunks,
  graph edges, and offline-dependency diagnostics.

Use `AllowAnyContentType`, `SkipKnownAssetUrls = false`, or their PowerShell
switch equivalents only when those broader responses are part of the intended
dataset. Use the browser path when computed visibility, client-side navigation,
lazy loading, or post-login state matters.

## Development and validation

```powershell
dotnet test .\Sources\HtmlTinkerX.Tests\HtmlTinkerX.Tests.csproj -c Release
Invoke-Pester .\Tests
& .\Build\Build-Module.ps1 -RunMode Build -SignModule:$false
```

Hand-authored C#, PowerShell, JavaScript, TypeScript, CSS, and build-source files
are kept at or below 1,000 lines. A contract test enforces the limit; generated
output and large HTML fixtures are excluded.