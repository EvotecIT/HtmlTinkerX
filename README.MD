# HtmlTinkerX & PSParseHTML - Modern HTML Processing for .NET and PowerShell

HtmlTinkerX is available as NuGet from the Nuget Gallery and PSParseHTML PowerShell module from PSGallery

## 📦 NuGet Package
[![nuget downloads](https://img.shields.io/nuget/dt/HtmlTinkerX?label=nuget%20downloads)](https://www.nuget.org/packages/HtmlTinkerX)
[![nuget version](https://img.shields.io/nuget/v/HtmlTinkerX)](https://www.nuget.org/packages/HtmlTinkerX)

## 💻 PowerShell Module
[![powershell gallery version](https://img.shields.io/powershellgallery/v/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery preview](https://img.shields.io/powershellgallery/vpre/PSParseHTML.svg?label=powershell%20gallery%20preview&colorB=yellow)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery platforms](https://img.shields.io/powershellgallery/p/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery downloads](https://img.shields.io/powershellgallery/dt/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)

## 🛠️ Project Information
[![.NET Tests](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/dotnet-tests.yml/badge.svg)](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/dotnet-tests.yml)
[![PowerShell Tests](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/powershell-tests-7.yml/badge.svg)](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/powershell-tests-7.yml)
[![top language](https://img.shields.io/github/languages/top/evotecit/HtmlTinkerX.svg)](https://github.com/EvotecIT/HtmlTinkerX)
[![codecov](https://codecov.io/gh/EvotecIT/HtmlTinkerX/branch/v2-speedygonzales/graph/badge.svg)](https://codecov.io/gh/EvotecIT/HtmlTinkerX)

## 👨‍💻 Author & Social
[![Twitter Follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Discord](https://img.shields.io/discord/508328927853281280?style=flat-square&label=discord%20chat)](https://evo.yt/discord)

## What it's all about

**HtmlTinkerX** is a powerful async C# library for HTML, CSS, and JavaScript processing, parsing, formatting, and optimization. It provides comprehensive web content processing capabilities including browser automation with Playwright, document parsing with multiple engines, resource optimization, and much more. **PSParseHTML** is the PowerShell module that exposes HtmlTinkerX functionality through easy-to-use cmdlets.

Whether you're working in C# or PowerShell, you get access to:
- 🔍 **HTML Parsing** - Multiple parsing engines (AngleSharp, HtmlAgilityPack)
- 🎨 **Resource Optimization** - Minify and format HTML, CSS, JavaScript
- 🌐 **Browser Automation** - Full Playwright integration for screenshots, PDFs, interaction
- 📊 **Data Extraction** - Tables, forms, metadata, microdata, Open Graph
- 📧 **Email Processing** - CSS inlining for email compatibility
- 🔧 **Network Tools** - HAR export, request interception, console logging
- 🍪 **State Management** - Cookie handling, session persistence
- 📱 **Multi-Platform** - .NET Framework 4.7.2, .NET Standard 2.0, .NET 8.0

## 🔄 PowerShell to C# Method Mapping

This comprehensive table shows how PowerShell cmdlets map to C# methods, making it easy to transition between platforms:

### HTML Parsing & Processing
| PowerShell Cmdlet            | C# Method                                | Description                                 |
| ---------------------------- | ---------------------------------------- | ------------------------------------------- |
| `ConvertFrom-HTML`           | `HtmlParser.ParseWithAngleSharp()`       | Parse HTML documents                        |
| `ConvertFrom-HtmlTable`      | `HtmlParser.ParseTablesWithAngleSharp()` | Extract tables with rowspan/colspan support |
| `ConvertFrom-HTMLAttributes` | `HtmlParserExtensions.GetElements()`     | Query elements by tag, class, id, or name   |
| `ConvertFrom-HtmlForm`       | `HtmlFormExtractor.ExtractForms()`       | Extract form data and structure             |
| `ConvertFrom-HtmlList`       | `HtmlListParser.ParseLists()`            | Parse list elements into structured data    |
| `ConvertFrom-HtmlMeta`       | `HtmlMetaParser.ExtractMeta()`           | Extract meta tag name/content pairs         |
| `ConvertFrom-HtmlMicrodata`  | `HtmlMicrodataParser.ExtractMicrodata()` | Extract schema.org structured data          |
| `ConvertFrom-HtmlOpenGraph`  | `HtmlOpenGraphParser.ExtractOpenGraph()` | Extract Open Graph metadata                 |
| `Convert-HTMLToText`         | `HtmlUtilities.ConvertToText()`          | Convert HTML to plain text                  |
| `Compare-HTML`               | `HtmlDiffer.Compare()`                   | Compare HTML documents                      |
| `Measure-HTMLDocument`       | `HtmlParser.AnalyzeDocument()`           | Analyze document metrics                    |

### Resource Formatting & Optimization
| PowerShell Cmdlet     | C# Method                            | Description                        |
| --------------------- | ------------------------------------ | ---------------------------------- |
| `Format-HTML`         | `HtmlFormatter.FormatHtml()`         | Pretty-print HTML markup           |
| `Format-CSS`          | `HtmlFormatter.FormatCss()`          | Format CSS stylesheets             |
| `Format-JavaScript`   | `HtmlFormatter.FormatJavaScript()`   | Beautify JavaScript with options   |
| `Optimize-HTML`       | `HtmlOptimizer.OptimizeHtml()`       | Minify HTML content                |
| `Optimize-CSS`        | `HtmlOptimizer.OptimizeCss()`        | Minify CSS stylesheets             |
| `Optimize-JavaScript` | `HtmlOptimizer.OptimizeJavaScript()` | Minify JavaScript code             |
| `Optimize-Email`      | `PreMailerClient.MoveCssInline()`    | Inline CSS for email compatibility |

### Browser Automation & Sessions
| PowerShell Cmdlet       | C# Method                          | Description                      |
| ----------------------- | ---------------------------------- | -------------------------------- |
| `Start-HTMLSession`     | `HtmlBrowser.OpenSessionAsync()`   | Create browser session           |
| `Close-HTMLSession`     | `session.DisposeAsync()`           | Close browser session            |
| `Invoke-HTMLRendering`  | `HtmlBrowser.OpenSessionAsync()`   | Render pages with authentication |
| `Invoke-HTMLNavigation` | `HtmlBrowser.NavigateAsync()`      | Navigate to different URLs       |
| `Invoke-HTMLScript`     | `HtmlBrowser.ExecuteScriptAsync()` | Execute JavaScript in browser    |
| `Invoke-HTMLDomScript`  | `HtmlScriptRunner.ExecuteScript()` | Run JavaScript with AngleSharp   |
| `Export-BrowserState`   | `HtmlBrowser.ExportStateAsync()`   | Save browser state               |
| `Import-BrowserState`   | `HtmlBrowser.ImportStateAsync()`   | Restore browser state            |
| `Export-HTMLSession`    | `HtmlBrowser.ExportSessionAsync()` | Export session data              |
| `Import-HTMLSession`    | `HtmlBrowser.ImportSessionAsync()` | Import session data              |

### Screenshots & Media
| PowerShell Cmdlet          | C# Method                                | Description                     |
| -------------------------- | ---------------------------------------- | ------------------------------- |
| `Save-HTMLScreenshot`      | `HtmlBrowser.SaveScreenshotAsync()`      | Capture page screenshots        |
| `Save-HTMLPdf`             | `HtmlBrowser.SavePdfAsync()`             | Generate PDFs from pages        |
| `Start-HTMLVideoRecording` | `HtmlBrowser.StartVideoRecordingAsync()` | Start recording browser session |
| `Stop-HTMLVideoRecording`  | `HtmlBrowser.StopVideoRecordingAsync()`  | Stop video recording            |

### Element Interaction
| PowerShell Cmdlet      | C# Method                                    | Description                    |
| ---------------------- | -------------------------------------------- | ------------------------------ |
| `Invoke-HTMLClick`     | `HtmlBrowser.ClickAsync()`                   | Click elements                 |
| `Set-HTMLInput`        | `HtmlBrowser.SetInputAsync()`                | Set input field values         |
| `Set-HTMLSelectOption` | `HtmlBrowser.SetSelectAsync()`               | Select dropdown options        |
| `Set-HTMLChecked`      | `HtmlBrowser.SetCheckedAsync()`              | Check/uncheck checkboxes       |
| `Submit-HTMLForm`      | `HtmlBrowser.SubmitFormAsync()`              | Submit forms                   |
| `Get-HTMLInteractable` | `HtmlBrowser.GetInteractableElementsAsync()` | List clickable elements        |
| `Get-HTMLFormField`    | `HtmlFormExtractor.GetFormFields()`          | Extract form field information |
| `Get-HTMLLoginForm`    | `HtmlFormExtractor.DetectLoginForms()`       | Detect login forms             |

### Network & Debugging
| PowerShell Cmdlet      | C# Method                            | Description                     |
| ---------------------- | ------------------------------------ | ------------------------------- |
| `Get-HTMLNetworkLog`   | `HtmlBrowser.GetNetworkLog()`        | View network requests/responses |
| `Get-HTMLConsoleLog`   | `HtmlBrowser.GetConsoleLog()`        | Retrieve console messages       |
| `Save-HTMLHar`         | `HtmlBrowser.SaveHarAsync()`         | Export network traffic to HAR   |
| `Start-HTMLTracing`    | `HtmlBrowser.StartTracingAsync()`    | Start Playwright tracing        |
| `Stop-HTMLTracing`     | `HtmlBrowser.StopTracingAsync()`     | Stop tracing and save           |
| `Register-HTMLRoute`   | `HtmlBrowser.RegisterRouteAsync()`   | Intercept requests              |
| `Unregister-HTMLRoute` | `HtmlBrowser.UnregisterRouteAsync()` | Remove route handler            |
| `Show-HTMLHar`         | `HtmlHarViewer.ShowHar()`            | Visualize HAR files             |

### Browser Testing
| PowerShell Cmdlet        | C# Method                                    | Description                                     |
| ------------------------ | -------------------------------------------- | ----------------------------------------------- |
| `Test-HtmlBrowser`       | `HtmlBrowserTester.TestUrlAsync()`           | Comprehensive browser testing                   |
| `Test-HtmlBrowser`       | `HtmlBrowserTester.TestFileAsync()`          | Test local HTML file (with -Path)               |
| `Test-HtmlBrowser`       | `HtmlBrowserTester.TestCssResourceAsync()`   | Test specific CSS resource (with -CssResource)  |
| `Test-HtmlBrowser`       | `HtmlBrowserTester.TestConsoleErrorsAsync()` | Get console errors (with -ErrorsOnly)           |
| `Test-HtmlBrowser`       | `HtmlBrowserTester.TestPerformanceAsync()`   | Get performance metrics (with -PerformanceOnly) |
| `Clear-HtmlBrowserCache` | `HtmlBrowserCacheCleaner.CleanAllCache()`    | Clean Playwright browser downloads              |

### Cookies & State
| PowerShell Cmdlet | C# Method                       | Description              |
| ----------------- | ------------------------------- | ------------------------ |
| `Get-HTMLCookie`  | `HtmlBrowser.GetCookiesAsync()` | Retrieve session cookies |
| `Set-HTMLCookie`  | `HtmlBrowser.SetCookieAsync()`  | Add cookies to session   |
| `New-HTMLCookie`  | `new HtmlCookie()`              | Create cookie objects    |

### Content & Resources
| PowerShell Cmdlet          | C# Method                               | Description                   |
| -------------------------- | --------------------------------------- | ----------------------------- |
| `Get-HTMLResource`         | `HtmlResourceParser.ExtractResources()` | Extract scripts and CSS       |
| `Invoke-HTMLCrawl`         | `HtmlCrawler.CrawlAsync()`              | Crawl a site offline          |
| `Save-HTMLAttachment`      | `HtmlBrowser.SaveAttachmentsAsync()`    | Download files from pages     |
| `Get-HTMLContent`          | `HtmlBrowser.GetContentAsync()`         | Retrieve page content         |
| `Export-HTMLOutline`       | `HtmlOutlineBuilder.BuildOutline()`     | Generate document outlines    |
| `Set-HTMLHttpClientOption` | `HttpClientFactory.ConfigureClient()`   | Configure HTTP client options |

## 📦 Installation & Packages

### 📦 NuGet Package (C#/.NET)
```bash
dotnet add package HtmlTinkerX
```

### 🔧 PowerShell Module
```powershell
Install-Module -Name PSParseHTML -AllowClobber -Force
```

### 📋 Package Information
- **📦 NuGet Package**: `HtmlTinkerX` - Core .NET library
- **🔧 PowerShell Module**: `PSParseHTML` - PowerShell cmdlets wrapper
- **🎯 Target Frameworks**: .NET Framework 4.7.2, .NET Standard 2.0, .NET 8.0
- **💻 PowerShell Compatibility**: Windows PowerShell 5.1+ and PowerShell Core 6.0+

## 🚀 Quick Start

### C# Example
```csharp
using HtmlTinkerX;

// Parse HTML and extract tables
string html = await File.ReadAllTextAsync("page.html");
var tables = HtmlParser.ParseTablesWithAngleSharp(html);

// Format and optimize resources
string formatted = HtmlFormatter.FormatHtml(html);
string minified = HtmlOptimizer.OptimizeHtml(html);

// Browser automation
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");
await HtmlBrowser.SaveScreenshotAsync(session, "screenshot.png");

// Offline crawl
var crawl = await HtmlCrawler.CrawlAsync("https://example.com/docs", new HtmlCrawlOptions {
    MaxDepth = 1,
    MaxPages = 10,
    UseSitemaps = true,
    RespectRobotsTxt = true,
    DeduplicatePages = true,
    OutputPath = "crawl-output"
});

Console.WriteLine(crawl.Summary.ToReportText(crawl.SitemapUrls));
Console.WriteLine(crawl.PagesCsvPath);

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

// Reuse a built-in profile for a known site
var profiled = await HtmlCrawler.CrawlAsync("https://evotec.xyz/", new HtmlCrawlOptions {
    ProfileName = "evotec-xyz"
});
Console.WriteLine(profiled.AppliedProfileName);
```

### PowerShell Example
```powershell
# Parse HTML tables from a webpage
$tables = ConvertFrom-HtmlTable -Url 'https://example.com'

# Format and optimize resources
$formatted = Format-HTML -Path 'page.html'
$minified = Optimize-HTML -Path 'page.html'

# Browser automation
$session = Start-HTMLSession -Url 'https://example.com'
Save-HTMLScreenshot -Session $session -OutFile 'screenshot.png'
Close-HTMLSession -Session $session

# Offline crawl
$crawl = Invoke-HTMLCrawl -Url 'https://example.com/docs' -MaxDepth 1 -MaxPages 10 -DeduplicatePages
$crawl.Pages | Select-Object Url, Title, Depth
$crawl.SkippedPages | Select-Object Url, SkipReason
$crawl.Summary.ToReportText($crawl.SitemapUrls)

# Preserve full tracked URLs when query strings are meaningful
$exactUrls = Invoke-HTMLCrawl -Url 'https://example.com/docs' -KeepTrackingQueryParameters

# Allow non-HTML responses such as PDFs when needed
$permissive = Invoke-HTMLCrawl -Url 'https://example.com/docs' -AllowAssetUrls -AllowAnyContentType

# Download discovered images/documents into the offline dataset
$richSnapshot = Invoke-HTMLCrawl -Url 'https://example.com/docs' -MaxDepth 1 -DownloadAssets -OutPath '.\crawl-output'
# Stored HTML now points at local ../assets/... paths by default
# Internal page links are also rewritten to local saved .html files by default

# Hybrid crawl for JavaScript-heavy pages with lazy loading and noisy chrome removal
$hybrid = Invoke-HTMLCrawl -Url 'https://example.com/app' -AutoRender -WaitForSelector '#main' -DismissSelector '.cookie-banner button', '#consent-accept' -DismissText 'Accept', 'I agree' -ClickSelector '.load-more', '.expand-details' -ClickText 'Load more', 'Show more' -InteractionRepeatCount 2 -AutoScroll -ExcludeSelector '.language-switcher', '.share-links', '.related-posts'
$hybrid.Pages | Select-Object Url, RenderMode, RenderReason, AppliedInteractions
$hybrid.Summary.ToReportText($hybrid.SitemapUrls)
# summary now includes interaction totals and per-interaction counts

# Reuse a built-in profile for a known site
$profiled = Invoke-HTMLCrawl -Url 'https://evotec.xyz/' -Profile 'evotec-xyz'
$profiled.AppliedProfileName

# Resume a previous crawl snapshot
$resumed = Invoke-HTMLCrawl -Url 'https://example.com/docs' -ResumePath '.\crawl-output' -OutPath '.\crawl-output'
```

Persisted crawl artifacts now include:
- `crawl-result.json` for the manifest and resume state
- `index.html` as a browsable offline entry point into the saved dataset
- `pages/` with per-page `.html`, `.txt`, and `.json` sidecar manifests
- `pages.jsonl` and `pages.csv` for page-level datasets
- `skipped-pages.jsonl` for skipped content-page candidates
- `skipped-assets.jsonl` for discovered asset/document URLs that were intentionally not crawled as pages
- `links.jsonl` for discovered page-to-page links
- `assets/` and `assets.jsonl` for downloaded images/documents when `DownloadAssets` is enabled
- `chunks.jsonl` for deduplicated text chunks ready for local search/RAG pipelines
- `graph.json` for page nodes and cross-page link edges with offline degree metadata
- `summary.json` and `summary.txt` for machine-readable and human-readable crawl reports

By default the crawler also normalizes away common tracking query parameters such as `utm_*`, `fbclid`, and `gclid` so the dataset does not fill up with duplicate tracked URLs. Use `IgnoreTrackingQueryParameters = false` in C# or `-KeepTrackingQueryParameters` in PowerShell to opt out.

By default the crawler is page-oriented and only keeps `text/html` and `application/xhtml+xml` responses. Use `RestrictToAllowedContentTypes = false` in C# or `-AllowAnyContentType` in PowerShell when you intentionally want non-HTML responses included.

It also skips obvious asset/document URLs such as `*.pdf`, `*.jpg`, `*.zip`, and fonts before fetching them. Use `SkipKnownAssetUrls = false` in C# or `-AllowAssetUrls` in PowerShell when those URLs are part of the dataset you want.

If you want those assets as part of the offline dataset without treating them as pages, enable `DownloadAssets` in C# or `-DownloadAssets` in PowerShell. That saves discovered image/document URLs, stylesheet links, and CSS `url(...)` assets into `assets/` and records them in `assets.jsonl`. Without `DownloadAssets`, `assets.jsonl` is still created as part of the dataset shape, but it stays empty because the crawl is page-only.

When assets are downloaded, stored HTML snapshots are also rewritten to local relative paths by default, so an `<img>` can point at `../assets/...` instead of the original remote URL. Use `RewriteAssetReferencesToLocal = false` in C# or `-KeepRemoteAssetUrls` in PowerShell if you want to keep the original references.

Stored HTML also rewrites internal page links to local saved `.html` files by default, which makes the persisted crawl much more browsable offline. Use `RewritePageLinksToLocal = false` in C# or `-KeepRemotePageUrls` in PowerShell if you want to preserve original page URLs.

Pages that use `<base href>` are also handled correctly during crawl discovery and offline rewrite, and the saved HTML strips the original `<base>` tag so local links and assets do not get redirected back to the live site.

Each saved page also gets a sidecar `.json` manifest with its metadata, outgoing links, referenced asset URLs, and any downloaded asset file paths resolved relative to that page. The generated `index.html` links to those manifests directly.

Those per-page manifests now also include lightweight search metadata such as heading extraction, word counts, character counts, and a short summary snippet, and `index.html` surfaces the same summary information for quick offline scanning.

The persisted dataset also exports a global `chunks.jsonl` file with deduplicated text chunks, per-chunk summaries, heading context, normalized keywords, and relative links back to each saved page, text file, and manifest so it is easy to feed into local search or RAG tooling.

It also exports `graph.json`, which captures fetched pages as nodes and discovered page-to-page links as edges, including fetched/skipped/external node categories, edge relation types, in-degree/out-degree counts, and relative paths back to saved HTML and manifest files for offline analysis or navigation tooling. The generated summaries now also break those graph counts down by node category, edge relation, and skipped-node reason.

## 🔧 PowerShell Cmdlets

### HTML/CSS/JavaScript Processing
- **Convert-HTMLToText** - Convert markup to plain text
- **ConvertFrom-HtmlTable** - Extract table elements into objects (supports rowspan/colspan)
- **ConvertFrom-HTMLAttributes** - Extract elements by tag, class, id or name
- **ConvertFrom-HTML** - Parse full documents or fragments
- **ConvertFrom-HtmlForm** - Extract form data and structure
- **ConvertFrom-HtmlList** - Parse list elements into structured data
- **ConvertFrom-HtmlMeta** - Extract name/content pairs from meta tags
- **ConvertFrom-HtmlMicrodata** - Extract structured data items (schema.org types)
- **ConvertFrom-HtmlOpenGraph** - Extract Open Graph metadata
- **Format-CSS** - Pretty-print style sheets
- **Format-HTML** - Tidy up HTML markup
- **Format-JavaScript** - Beautify JavaScript with customizable options
- **Optimize-CSS** - Minify style sheets
- **Optimize-Email** - Inline CSS for email bodies
- **Optimize-HTML** - Minify HTML
- **Optimize-JavaScript** - Minify JavaScript

### Browser Automation & Interaction
- **Start-HTMLSession** / **Invoke-HTMLRendering** - Create browser sessions with authentication support
- **Close-HTMLSession** - Dispose browser sessions
- **Invoke-HTMLNavigation** - Navigate to different URLs
- **Invoke-HTMLScript** - Execute JavaScript in browser context
- **Invoke-HTMLDomScript** - Run JavaScript with AngleSharp (no browser required)
- **Invoke-HTMLClick** - Click elements in the browser
- **Get-HTMLInteractable** - List clickable elements
- **Set-HTMLInput** - Set input field values
- **Set-HTMLSelectOption** - Select dropdown options
- **Set-HTMLChecked** - Check/uncheck checkboxes and radio buttons
- **Submit-HTMLForm** - Submit forms

### Screenshots & Media
- **Save-HTMLScreenshot** - Capture page screenshots with advanced options
- **Save-HTMLPdf** - Generate PDFs from rendered pages
- **Start-HTMLVideoRecording** / **Stop-HTMLVideoRecording** - Record browser sessions

### Network & Debugging
- **Get-HTMLNetworkLog** - View captured network requests and responses
- **Get-HTMLConsoleLog** - Retrieve browser console messages
- **Save-HTMLHar** - Export network traffic to HAR files
- **Start-HTMLTracing** / **Stop-HTMLTracing** - Record Playwright traces
- **Register-HTMLRoute** / **Unregister-HTMLRoute** - Intercept and mock requests
- **Test-HtmlBrowser** - Comprehensive browser testing for errors, performance, and resources
- **Clear-HtmlBrowserCache** - Clean Playwright browser downloads

### Cookies & State Management
- **Get-HTMLCookie** - Retrieve cookies from sessions
- **Set-HTMLCookie** - Add cookies to sessions
- **New-HTMLCookie** - Create cookie objects
- **Export-BrowserState** / **Import-BrowserState** - Save/restore browser state
- **Export-HTMLSession** / **Import-HTMLSession** - Session state management

### Content & Resources
- **Get-HTMLResource** - Extract script and CSS resources
- **Invoke-HTMLCrawl** - Crawl sites offline with optional browser rendering
: supports sitemap discovery, `robots.txt`, filtering, auth, and selector-based extraction
- **Save-HTMLAttachment** - Download files from pages
- **Get-HTMLContent** - Retrieve page content
- **Get-HTMLFormField** - Extract form field information
- **Get-HTMLLoginForm** - Detect login forms
- **Export-HTMLOutline** - Generate document outlines
- **Show-HTMLHar** - Visualize HAR files
- **Compare-HTML** - Compare HTML documents
- **Measure-HTMLDocument** - Analyze document metrics##
 🎯 C# API Reference

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
var urlDoc = await HtmlParser.ParseFromUrlAsync("https://example.com");
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
string minifiedHtml = HtmlOptimizer.OptimizeHtml(html);
string minifiedCss = HtmlOptimizer.OptimizeCss(css);
string minifiedJs = HtmlOptimizer.OptimizeJavaScript(javascript);

// File operations
await HtmlOptimizer.OptimizeHtmlFileAsync("input.html", "output.html");
```

#### HtmlBrowser (Browser Automation)
```csharp
// Create browser sessions
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");

// Authentication
var credentials = new NetworkCredential("user", "pass");
await using var authSession = await HtmlBrowser.OpenSessionAsync(
    "https://example.com/protected",
    credential: credentials,
    loginUrl: "https://example.com/login"
);

// Screenshots
await HtmlBrowser.SaveScreenshotAsync(session, "screenshot.png");
await HtmlBrowser.SaveScreenshotAsync(session, "full.png", fullPage: true);

// PDF generation
await HtmlBrowser.SavePdfAsync(session, "document.pdf");

// Navigation
await HtmlBrowser.NavigateAsync(session, "https://example.com/page2");

// JavaScript execution
var result = await HtmlBrowser.ExecuteScriptAsync(session, "return document.title;");

// Element interaction
await HtmlBrowser.ClickAsync(session, "#button");
await HtmlBrowser.SetInputAsync(session, "#username", "user");
await HtmlBrowser.SubmitFormAsync(session, "#loginForm");
```

#### HtmlUtilities
```csharp
// Convert HTML to plain text
string plainText = HtmlUtilities.ConvertToText(html);

// HTTP client operations
using var httpClient = HttpClientFactory.CreateHttpClient();
string content = await httpClient.GetStringAsync("https://example.com");
```

#### PreMailerClient
```csharp
// Email optimization
string inlinedCss = PreMailerClient.MoveCssInline(emailHtml);
string optimized = await PreMailerClient.MoveCssInlineAsync(emailHtml, downloadRemoteCss: true);
```

### Extension Methods

#### HtmlParserExtensions
```csharp
// Quick element queries
var elements = doc.GetElements("div.class-name");
var byId = doc.GetElements("#element-id");
var byTag = doc.GetElements("p");
```

## 📚 Examples

### PowerShell Examples

#### Table Extraction
```powershell
# Extract tables from Wikipedia
$tables = ConvertFrom-HtmlTable -Url 'https://en.wikipedia.org/wiki/PowerShell'
$tables[0] | Format-Table -AutoSize

# Parse local HTML file
$tables = ConvertFrom-HtmlTable -Path './data.html'
foreach ($table in $tables) {
    $table | Export-Csv "table_$($tables.IndexOf($table)).csv" -NoTypeInformation
}
```

#### Resource Optimization
```powershell
# Format and minify HTML
$formatted = Format-HTML -Path './messy.html'
$minified = Optimize-HTML -Content $formatted -OutputFile './clean.min.html'

# Optimize JavaScript with custom options
$js = Format-JavaScript -Path './script.js' -IndentSize 2 -BraceStyle Expand
Optimize-JavaScript -Content $js -OutputFile './script.min.js'

# Email optimization
$emailHtml = Get-Content './newsletter.html' -Raw
$optimized = Optimize-Email -Body $emailHtml -UseEmailFormatter -DownloadRemoteCss
```

#### Browser Automation
```powershell
# Authenticated session with form login
$cred = Get-Credential
$session = Start-HTMLSession -Url 'https://example.com/protected' `
    -Credential $cred `
    -LoginUrl 'https://example.com/login' `
    -UsernameSelector 'input[name=username]' `
    -PasswordSelector 'input[name=password]' `
    -SubmitSelector 'button[type=submit]'

# Take screenshots with different options
Save-HTMLScreenshot -Session $session -OutFile 'full-page.png' -Full
Save-HTMLScreenshot -Session $session -OutFile 'element.png' -ElementSelector '#content'
Save-HTMLScreenshot -Session $session -OutFile 'highlighted.png' -HighlightSelector '.important'

# Download files
Save-HTMLAttachment -Session $session -Path './downloads' -Filter '.pdf'

# Network monitoring
Start-HTMLTracing -Session $session
Invoke-HTMLNavigation -Session $session -Url 'https://example.com/api/data'
Stop-HTMLTracing -Session $session -OutFile 'trace.zip'
Save-HTMLHar -Session $session -OutFile 'network.har'

Close-HTMLSession -Session $session
```

### C# Examples

#### Document Processing
```csharp
using HtmlTinkerX;

// Parse and process HTML
string html = await File.ReadAllTextAsync("document.html");
var document = HtmlParser.ParseWithAngleSharp(html);

// Extract specific elements
var links = document.GetElements("a[href]");
var images = document.GetElements("img[src]");

// Extract tables with detailed information
var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html);
foreach (var table in tables)
{
    Console.WriteLine($"Table has {table.Rows.Count} rows and {table.Headers.Count} columns");
    foreach (var row in table.Rows)
    {
        Console.WriteLine(string.Join(" | ", row.Values));
    }
}
```

#### Resource Optimization
```csharp
// Format resources
string formattedHtml = HtmlFormatter.FormatHtml(html);
string formattedCss = HtmlFormatter.FormatCss(css);

// Custom JavaScript formatting
var jsOptions = new BeautifierOptions
{
    IndentSize = 4,
    BraceStyle = BraceStyle.Collapse,
    PreserveNewlines = true
};
string formattedJs = HtmlFormatter.FormatJavaScript(javascript, jsOptions);

// Minification
string minifiedHtml = HtmlOptimizer.OptimizeHtml(html);
string minifiedCss = HtmlOptimizer.OptimizeCss(css);
string minifiedJs = HtmlOptimizer.OptimizeJavaScript(javascript);

// Email optimization
string emailBody = await File.ReadAllTextAsync("newsletter.html");
string inlined = await PreMailerClient.MoveCssInlineAsync(emailBody, downloadRemoteCss: true);
```

#### Browser Automation
```csharp
// Basic browser session
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");

// Authenticated session
var credentials = new NetworkCredential("username", "password");
await using var authSession = await HtmlBrowser.OpenSessionAsync(
    "https://example.com/protected",
    credential: credentials,
    loginUrl: "https://example.com/login",
    usernameSelector: "#username",
    passwordSelector: "#password",
    submitSelector: "#login-button"
);

// Interact with the page
await HtmlBrowser.SetInputAsync(session, "#search", "query");
await HtmlBrowser.ClickAsync(session, "#search-button");
await Task.Delay(2000); // Wait for results

// Capture results
await HtmlBrowser.SaveScreenshotAsync(session, "results.png");
var consoleMessages = HtmlBrowser.GetConsoleLog(session);
foreach (var message in consoleMessages)
{
    Console.WriteLine($"{message.Type}: {message.Text}");
}

// Download files
var downloads = await HtmlBrowser.SaveAttachmentsAsync(session, "./downloads", ".pdf");
Console.WriteLine($"Downloaded {downloads.Count} files");
```

## 🧪 Browser Testing & Network Monitoring

PSParseHTML now includes comprehensive browser testing capabilities for checking network requests, CSS resources, console errors, and performance metrics. This feature uses strongly-typed classes instead of dictionaries for better IntelliSense and type safety.

### PowerShell Browser Testing

#### Basic Testing
```powershell
# Run a comprehensive test on a URL
$result = Test-HtmlBrowser -Url 'https://example.com'

# Test a local HTML file
$result = Test-HtmlBrowser -Path 'C:\MyProject\index.html'

# Check if test passed (no errors or failed requests)
if ($result.Passed) {
    Write-Host "✅ All tests passed!"
} else {
    Write-Host "❌ Issues found: $($result.Summary)"
}

# View detailed results
Write-Host "Total Requests: $($result.TotalRequests)"
Write-Host "Failed Requests: $($result.FailedRequestCount)"
Write-Host "Console Errors: $($result.ErrorCount)"
Write-Host "Console Warnings: $($result.WarningCount)"
```

#### Testing Local HTML Files
```powershell
# Test local HTML files created by HTMLForgeX or other tools
$htmlFile = "C:\Projects\MyReport\report.html"
$result = Test-HtmlBrowser -Path $htmlFile

# Check for JavaScript errors in local file
$errors = Test-HtmlBrowser -Path $htmlFile -ErrorsOnly
if ($errors.Count -gt 0) {
    Write-Host "Found $($errors.Count) JavaScript errors:"
    $errors | ForEach-Object {
        Write-Host "  - $($_.Text) at $($_.FullLocation)"
    }
}

# Test CSS loading in local file
$cssCheck = Test-HtmlBrowser -Path $htmlFile -CssResource 'styles.css'
if ($cssCheck) {
    Write-Host "CSS loaded successfully in $($cssCheck.Duration.TotalMilliseconds)ms"
}

# Test with visible browser (not headless) for debugging
$result = Test-HtmlBrowser -Path $htmlFile -Headless:$false
```

#### Testing for Console Errors
```powershell
# Get only console errors
$errors = Test-HtmlBrowser -Url 'https://example.com' -ErrorsOnly

foreach ($error in $errors) {
    Write-Host "Error: $($error.Text)"
    Write-Host "  Location: $($error.FullLocation)"
    Write-Host "  Severity: $($error.SeverityLevel)"

    if ($error.StackTrace) {
        Write-Host "  Stack: $($error.StackTrace)"
    }
}
```

#### CSS Resource Testing
```powershell
# Check if a specific CSS file is loaded
$cssResource = Test-HtmlBrowser -Url 'https://example.com' -CssResource 'styles.css'

if ($cssResource) {
    Write-Host "CSS found: $($cssResource.Url)"
    Write-Host "Load time: $($cssResource.Duration.TotalMilliseconds)ms"
    Write-Host "Size: $($cssResource.TransferSize) bytes"
    Write-Host "From cache: $($cssResource.ServedFromCache)"
}
```

#### Performance Testing
```powershell
# Get performance metrics only
$metrics = Test-HtmlBrowser -Url 'https://example.com' -PerformanceOnly

# Display performance report
Write-Host $metrics.GetReport()

# Access specific metrics
Write-Host "Page Load Time: $($metrics.TotalLoadTime.TotalSeconds)s"
Write-Host "Average Request Duration: $($metrics.AverageRequestDuration.TotalMilliseconds)ms"
Write-Host "Total Bytes: $($metrics.TotalBytesTransferred / 1KB)KB"

# Resource breakdown by type
$metrics.ResourceBreakdown | ForEach-Object {
    Write-Host "$($_.Key): $($_.Value) requests"
}

# Or get the full formatted report
Write-Host $metrics.GetReport()
```

#### Advanced Testing with Proxy
```powershell
# Test through a proxy with authentication
$cred = Get-Credential
$result = Test-HtmlBrowser -Url 'https://example.com' `
    -Proxy 'http://proxy:8080' `
    -ProxyCredential $cred `
    -Timeout 60000
```

#### Batch Testing Multiple URLs
```powershell
# Test multiple URLs and generate report
$urls = @(
    'https://example.com/home',
    'https://example.com/about',
    'https://example.com/contact'
)

$results = $urls | ForEach-Object {
    $result = Test-HtmlBrowser -Url $_
    [PSCustomObject]@{
        Url = $_
        Status = if ($result.Passed) { 'PASS' } else { 'FAIL' }
        LoadTime = $result.PageLoadTime.TotalSeconds
        Requests = $result.TotalRequests
        Failed = $result.FailedRequestCount
        Errors = $result.ErrorCount
        Warnings = $result.WarningCount
    }
}

# Display results in a table
$results | Format-Table -AutoSize

# Export to CSV for further analysis
$results | Export-Csv -Path 'browser-test-results.csv' -NoTypeInformation

# Find pages with issues
$results | Where-Object { $_.Status -eq 'FAIL' } | ForEach-Object {
    Write-Warning "Failed: $($_.Url) - $($_.Failed) failed requests, $($_.Errors) errors"
}
```

#### Integration with Pester Tests
```powershell
# Save as MyWebsite.Tests.ps1
Describe "Website Browser Tests" {

    BeforeAll {
        $baseUrl = 'https://mywebsite.com'
    }

    It "Homepage should load without errors" {
        $result = Test-HtmlBrowser -Url $baseUrl
        $result.Passed | Should -BeTrue
        $result.ConsoleErrors.Count | Should -Be 0
        $result.FailedRequestCount | Should -Be 0
    }

    It "All CSS files should load successfully" {
        $result = Test-HtmlBrowser -Url $baseUrl
        $cssFiles = $result.CssResources

        $cssFiles.Count | Should -BeGreaterThan 0
        $cssFiles | ForEach-Object {
            $_.Status | Should -Be 200
            $_.ErrorType | Should -BeNullOrEmpty
        }
    }

    It "Page should load within 3 seconds" {
        $result = Test-HtmlBrowser -Url $baseUrl
        $result.PageLoadTime.TotalSeconds | Should -BeLessOrEqual 3
    }

    It "Console should not contain JavaScript errors" {
        $errors = Test-HtmlBrowser -Url $baseUrl -ErrorsOnly
        $errors | Should -BeNullOrEmpty
    }

    It "Total page size should be under 5MB" {
        $metrics = Test-HtmlBrowser -Url $baseUrl -PerformanceOnly
        $totalMB = $metrics.TotalBytesTransferred / 1MB
        $totalMB | Should -BeLessOrEqual 5
    }
}

# Run tests
Invoke-Pester -Path .\MyWebsite.Tests.ps1 -Output Detailed
```

#### Testing Local HTML Reports
```powershell
# Test HTMLForgeX generated reports
$reportPath = "C:\Reports\MonthlyReport.html"

# Basic test
$result = Test-HtmlBrowser -Path $reportPath
if (-not $result.Passed) {
    Write-Warning "Report has issues:"
    $result.ConsoleErrors | ForEach-Object {
        Write-Warning "  JS Error: $($_.Text)"
    }
    $result.FailedRequests | ForEach-Object {
        Write-Warning "  Failed Resource: $($_.Url)"
    }
}

# Test multiple reports
Get-ChildItem -Path "C:\Reports" -Filter "*.html" | ForEach-Object {
    $result = Test-HtmlBrowser -Path $_.FullName
    [PSCustomObject]@{
        Report = $_.Name
        Status = if ($result.Passed) { '✅' } else { '❌' }
        LoadTime = "$($result.PageLoadTime.TotalSeconds)s"
        Errors = $result.ErrorCount
        MissingResources = $result.FailedRequestCount
    }
} | Format-Table -AutoSize

# Test with visible browser for debugging
$debugResult = Test-HtmlBrowser -Path $reportPath -Headless:$false -Timeout 60000
```

#### Monitoring and Alerting
```powershell
# Monitor website health
function Test-WebsiteHealth {
    param(
        [string]$Url,
        [int]$MaxLoadTime = 5,
        [int]$MaxErrors = 0
    )

    $result = Test-HtmlBrowser -Url $Url

    $issues = @()

    if ($result.PageLoadTime.TotalSeconds -gt $MaxLoadTime) {
        $issues += "Slow load time: $($result.PageLoadTime.TotalSeconds)s"
    }

    if ($result.ErrorCount -gt $MaxErrors) {
        $issues += "Console errors: $($result.ErrorCount)"
    }

    if ($result.FailedRequestCount -gt 0) {
        $issues += "Failed requests: $($result.FailedRequestCount)"
    }

    if ($issues.Count -eq 0) {
        Write-Host "✅ $Url is healthy" -ForegroundColor Green
    } else {
        Write-Host "❌ $Url has issues:" -ForegroundColor Red
        $issues | ForEach-Object { Write-Host "   - $_" -ForegroundColor Yellow }

        # Send alert (example)
        # Send-MailMessage -To "admin@company.com" -Subject "Website Issue" -Body ($issues -join "`n")
    }

    return @{
        Url = $Url
        Healthy = $issues.Count -eq 0
        Issues = $issues
        Timestamp = Get-Date
    }
}

# Test multiple sites
$sites = @('https://site1.com', 'https://site2.com')
$healthChecks = $sites | ForEach-Object { Test-WebsiteHealth -Url $_ }

# Save results
$healthChecks | ConvertTo-Json | Out-File "health-check-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
```

### C# Browser Testing

#### Basic Testing
```csharp
using HtmlTinkerX;

// Run comprehensive test on URL
var result = await HtmlBrowserTester.TestUrlAsync("https://example.com");

// Test a local HTML file
var fileResult = await HtmlBrowserTester.TestFileAsync(@"C:\MyProject\index.html");

if (result.Passed)
{
    Console.WriteLine("✅ All tests passed!");
}
else
{
    Console.WriteLine($"❌ {result.Summary}");
}

// Analyze results
Console.WriteLine($"Total Requests: {result.TotalRequests}");
Console.WriteLine($"Failed: {result.FailedRequestCount}");
Console.WriteLine($"Errors: {result.ErrorCount}");
Console.WriteLine($"Warnings: {result.WarningCount}");
```

#### Testing Local HTML Files
```csharp
// Test local HTML file with full analysis
var testResult = await HtmlBrowserTester.TestFileAsync(
    @"C:\Projects\MyReport\report.html",
    HtmlBrowserEngine.Chromium,
    headless: true,
    timeout: 30000);

// Check specific issues
if (testResult.ConsoleErrors.Any())
{
    Console.WriteLine($"Found {testResult.ErrorCount} JavaScript errors:");
    foreach (var error in testResult.ConsoleErrors)
    {
        Console.WriteLine($"  - {error.Text}");
        Console.WriteLine($"    Location: {error.FullLocation}");
        if (!string.IsNullOrEmpty(error.StackTrace))
        {
            Console.WriteLine($"    Stack: {error.StackTrace}");
        }
    }
}

// Analyze resource loading
var slowResources = testResult.NetworkEntries
    .Where(r => r.Duration > TimeSpan.FromSeconds(1))
    .OrderByDescending(r => r.Duration);

foreach (var resource in slowResources)
{
    Console.WriteLine($"Slow resource: {resource.Url} took {resource.Duration?.TotalSeconds}s");
}
```

#### Network Request Analysis
```csharp
// Test and analyze network requests
var result = await HtmlBrowserTester.TestUrlAsync("https://example.com");

// Check CSS resources
foreach (var css in result.CssResources)
{
    Console.WriteLine($"CSS: {css.Url}");
    Console.WriteLine($"  Duration: {css.Duration?.TotalMilliseconds}ms");
    Console.WriteLine($"  Size: {css.TransferSize} bytes");
    Console.WriteLine($"  Cached: {css.ServedFromCache}");
}

// Check failed requests
foreach (var failed in result.FailedRequests)
{
    Console.WriteLine($"Failed: {failed.Url}");
    Console.WriteLine($"  Error: {failed.ErrorType} - {failed.ErrorMessage}");
}

// Check JavaScript resources
var jsFiles = result.JavaScriptResources;
var totalJsSize = jsFiles.Sum(js => js.TransferSize ?? 0);
Console.WriteLine($"Total JS size: {totalJsSize / 1024}KB");
```

#### Console Error Detection
```csharp
// Get only console errors
var errors = await HtmlBrowserTester.TestConsoleErrorsAsync("https://example.com");

foreach (var error in errors)
{
    Console.WriteLine($"Error: {error.Text}");
    Console.WriteLine($"  Type: {error.Type}");
    Console.WriteLine($"  Location: {error.FullLocation}");
    Console.WriteLine($"  Timestamp: {error.Timestamp}");

    if (!string.IsNullOrEmpty(error.StackTrace))
    {
        Console.WriteLine($"  Stack: {error.StackTrace}");
    }
}
```

#### Performance Analysis
```csharp
// Get performance metrics
var metrics = await HtmlBrowserTester.TestPerformanceAsync("https://example.com");

// Display performance report
Console.WriteLine(metrics.GetReport());

// Check specific thresholds
if (metrics.TotalLoadTime > TimeSpan.FromSeconds(5))
{
    Console.WriteLine("⚠️ Page load time exceeds 5 seconds!");
}

if (metrics.LongestRequest?.Duration > TimeSpan.FromSeconds(2))
{
    Console.WriteLine($"⚠️ Slow resource: {metrics.LongestRequest.Url}");
}
```

#### Testing Local HTML Files
```csharp
// Test a local HTML file created by HTMLForgeX or other tools
var localResult = await HtmlBrowserTester.TestFileAsync(@"C:\Projects\MyReport\report.html");

// Check if all resources loaded correctly
if (localResult.Passed)
{
    Console.WriteLine("✅ Local HTML file passed all tests!");
}
else
{
    // Analyze what went wrong
    foreach (var failed in localResult.FailedRequests)
    {
        Console.WriteLine($"❌ Failed to load: {failed.Url}");
        Console.WriteLine($"   Error: {failed.ErrorType}");
    }
}

// Test with custom timeout for slow local resources
var slowResult = await HtmlBrowserTester.TestFileAsync(
    @"C:\MyProject\index.html",
    timeout: 30000  // 30 seconds
);
```

#### Integration Testing Examples
```csharp
// Example: Testing in xUnit
[Fact]
public async Task Website_Should_Load_Without_Errors()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");

    Assert.True(result.Passed, $"Test failed: {result.Summary}");
    Assert.Empty(result.ConsoleErrors);
    Assert.Empty(result.FailedRequests);
    Assert.True(result.PageLoadTime < TimeSpan.FromSeconds(3),
        "Page load time exceeded 3 seconds");
}

// Example: Testing specific CSS resources
[Theory]
[InlineData("styles.css")]
[InlineData("theme.css")]
public async Task CSS_Files_Should_Load_Successfully(string cssFile)
{
    var css = await HtmlBrowserTester.TestCssResourceAsync(
        "https://mysite.com", cssFile);

    Assert.NotNull(css);
    Assert.Equal(200, css.Status);
    Assert.True(css.Duration < TimeSpan.FromSeconds(1));
}

// Example: Performance regression test
[Fact]
public async Task Page_Performance_Should_Meet_Thresholds()
{
    var metrics = await HtmlBrowserTester.TestPerformanceAsync("https://mysite.com");

    Assert.True(metrics.TotalLoadTime < TimeSpan.FromSeconds(5));
    Assert.True(metrics.TotalBytesTransferred < 5 * 1024 * 1024); // 5MB
    Assert.True(metrics.TotalRequests < 50);
    Assert.All(metrics.RequestsByResourceType, kvp =>
    {
        if (kvp.Key == HtmlNetworkResourceType.Image)
        {
            Assert.True(kvp.Value.TotalSizeMB < 2,
                $"Images exceed 2MB limit: {kvp.Value.TotalSizeMB:F2}MB");
        }
    });
}
```

#### Batch Testing Multiple Pages
```csharp
// Test multiple pages efficiently
var urls = new[] {
    "https://example.com/home",
    "https://example.com/about",
    "https://example.com/contact"
};

var results = await Task.WhenAll(
    urls.Select(url => HtmlBrowserTester.TestUrlAsync(url))
);

// Generate summary report
foreach (var (url, result) in urls.Zip(results))
{
    Console.WriteLine($"\n{url}:");
    Console.WriteLine($"  Status: {(result.Passed ? "PASS" : "FAIL")}");
    Console.WriteLine($"  Load Time: {result.PageLoadTime.TotalSeconds:F2}s");
    Console.WriteLine($"  Requests: {result.TotalRequests} ({result.FailedRequestCount} failed)");
    Console.WriteLine($"  Console: {result.ErrorCount} errors, {result.WarningCount} warnings");
}

// Find slowest page
var slowest = results.OrderByDescending(r => r.PageLoadTime).First();
Console.WriteLine($"\nSlowest page: {slowest.Url} ({slowest.PageLoadTime.TotalSeconds:F2}s)");
```

### Test Result Properties

#### HtmlBrowserTestResult
- **Url** - The tested URL
- **PageLoadTime** - Total page load duration
- **NetworkEntries** - All network requests with detailed info
- **ConsoleEntries** - All console messages
- **ConsoleErrors** - Only error messages
- **ConsoleWarnings** - Only warning messages
- **FailedRequests** - Failed network requests
- **CssResources** - CSS file requests
- **JavaScriptResources** - JS file requests
- **ImageResources** - Image requests
- **Passed** - Whether all tests passed
- **Summary** - Human-readable summary

#### HtmlNetworkEntryDetailed
- **Url** - Request URL
- **Method** - HTTP method
- **Status** - Response status code
- **ProtocolVersion** - HTTP protocol version
- **Duration** - Request duration
- **ResourceType** - Type of resource (Document, Stylesheet, Script, etc.)
- **TransferSize** - Total bytes transferred
- **ServedFromCache** - Whether served from cache
- **ErrorType** - Error type if failed
- **ContentType** - Response content type

#### HtmlConsoleEntryDetailed
- **Text** - Console message text
- **Type** - Message type (Error, Warning, Info, etc.)
- **Timestamp** - When logged
- **SourceUrl** - Source file URL
- **LineNumber** - Line in source
- **StackTrace** - Stack trace for errors
- **SeverityLevel** - 1=Info, 2=Warning, 3=Error
- **IsError/IsWarning/IsInfo** - Quick type checks

#### HtmlPerformanceMetrics
- **TotalLoadTime** - Total time to load the page
- **TotalRequests** - Number of network requests made
- **TotalBytesTransferred** - Total bytes downloaded
- **AverageRequestDuration** - Average time per request
- **LongestRequest** - The slowest network request
- **ResourceBreakdown** - Dictionary of requests grouped by type (Document, Stylesheet, Script, Image, Font, etc.)
- **GetReport()** - Returns a formatted text report with all metrics

### Playwright Auto-Setup

Playwright browsers are automatically downloaded on first use. No manual setup required. The download is cached per-user (default locations below).

#### How Auto-Download Works
When you first use browser testing, Playwright automatically downloads required components:

1. **Playwright Driver & Node.js**:
   - Windows: `%LOCALAPPDATA%\ms-playwright-driver`
   - macOS: `~/Library/Caches/ms-playwright-driver`
   - Linux: `~/.cache/ms-playwright-driver`
   - Contains the Playwright driver and embedded Node.js runtime

2. **Browser Installations**:
   - Windows: `%LOCALAPPDATA%\ms-playwright`
   - macOS: `~/Library/Caches/ms-playwright`
   - Linux: `~/.cache/ms-playwright`
   - Contains Chromium, Firefox, and/or WebKit browsers

3. **Download Process**:
   - Shows progress: "Downloading Playwright driver... X% (Y MB/s)"
   - Thread-safe - prevents concurrent downloads
   - Subsequent runs use cached components - no re-download needed
   - You can manually ensure browsers are installed using `HtmlBrowser.EnsureInstalledAsync()`

#### Linux: Avoiding sudo prompts
On Linux, Playwright can also install OS-level dependencies when invoked with `--with-deps` (this typically requires root/sudo).

By default, HtmlTinkerX only uses `--with-deps` when running as root to avoid unexpected sudo prompts during normal test execution. You can override this behavior by setting:
- `HTMLTINKERX_PLAYWRIGHT_WITH_DEPS=1` to force `--with-deps`
- `HTMLTINKERX_PLAYWRIGHT_WITH_DEPS=0` to never use `--with-deps`

#### Cleaning Playwright Cache
```powershell
# View cache size and clean if needed
Clear-HtmlBrowserCache -WhatIf

# Force clean without confirmation
Clear-HtmlBrowserCache -Force

# Skip cleaning temporary files (only clean browser downloads)
Clear-HtmlBrowserCache -SkipTemp -Force

# Skip cleaning browser downloads (only clean temp files)
Clear-HtmlBrowserCache -SkipBrowsers -Force

# View detailed information about what will be cleaned
Clear-HtmlBrowserCache -Verbose
```

The enhanced cache cleaner now:
- Cleans multiple Playwright cache locations (LocalAppData and .cache)
- Removes temporary Playwright files from the temp directory
- Cleans up trace files left behind by debugging sessions
- Shows detailed size information for each location
- Provides granular control over what to clean

#### C# Cache Cleaning
```csharp
// Manually ensure browser is installed (usually not needed - happens automatically)
await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

// Get all cache locations
var locations = HtmlBrowserCacheCleaner.GetCacheLocations();
Console.WriteLine($"Found {locations.Count} locations totaling {locations.Sum(l => l.SizeMB):F2} MB");

// Clean all cache
var result = HtmlBrowserCacheCleaner.CleanAllCache();
if (result.Success)
{
    Console.WriteLine($"Cleaned {result.TotalSizeClearedMB:F2} MB");
}
else
{
    Console.WriteLine($"Failed to clean {result.Failed.Count} locations");
}

// Clean only browser downloads
var browserResult = HtmlBrowserCacheCleaner.CleanAllCache(
    includeBrowsers: true,
    includeTemp: false);

// Get locations without cleaning (for inspection)
var tempOnly = HtmlBrowserCacheCleaner.GetCacheLocations(
    includeBrowsers: false,
    includeTemp: true);
foreach (var location in tempOnly)
{
    Console.WriteLine($"{location.Description}: {location.SizeMB:F2} MB at {location.Path}");
}
```

### Integration with Test Frameworks

#### xUnit Example
```csharp
[Fact]
public async Task WebsiteShouldHaveNoErrors()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");

    Assert.True(result.Passed, result.Summary);
    Assert.Empty(result.ConsoleErrors);
    Assert.Empty(result.FailedRequests);
}

[Fact]
public async Task CssShouldLoadQuickly()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");

    foreach (var css in result.CssResources)
    {
        Assert.True(css.Duration < TimeSpan.FromSeconds(2),
            $"CSS {css.Url} took {css.Duration?.TotalSeconds}s");
    }
}
```

#### Pester Example
```powershell
Describe "Website Health Check" {
    It "Should have no console errors" {
        $result = Test-HtmlBrowser -Url "https://mysite.com"
        $result.ErrorCount | Should -Be 0
    }

    It "Should load all resources successfully" {
        $result = Test-HtmlBrowser -Url "https://mysite.com"
        $result.FailedRequestCount | Should -Be 0
    }

    It "Should load within 5 seconds" {
        $metrics = Test-HtmlBrowser -Url "https://mysite.com" -PerformanceOnly
        $metrics.TotalLoadTime.TotalSeconds | Should -BeLessThan 5
    }
}
```

## 🔧 Advanced Features

### Browser Configuration
```powershell
# Custom browser settings
$session = Start-HTMLSession -Url 'https://example.com' `
    -UserAgent 'Custom Bot 1.0' `
    -ViewportWidth 1920 `
    -ViewportHeight 1080 `
    -DeviceScaleFactor 2 `
    -Visible `
    -SlowMo 1000
```

### Request Interception
```powershell
# Mock API responses
$handler = Register-HTMLRoute -Session $session -Pattern '**/api/data' -ScriptBlock {
    param($route)
    $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
        Status = 200
        ContentType = 'application/json'
        Body = '{"status": "success", "data": []}'
    }) | Out-Null
}

# Navigate and test
Invoke-HTMLNavigation -Session $session -Url 'https://example.com/app'
Unregister-HTMLRoute -Session $session -Pattern '**/api/data' -Handler $handler
```

### State Management
```powershell
# Save browser state
Export-BrowserState -Session $session -Path 'session-state.json'

# Restore in new session
$newSession = Import-BrowserState -Path 'session-state.json' -Url 'https://example.com/dashboard'
```

## 🏗️ Third-Party Dependencies

HtmlTinkerX utilizes several high-quality open-source libraries:

### 📦 HTML & DOM Processing
- **[AngleSharp](https://github.com/AngleSharp/AngleSharp)** - MIT License - Modern HTML5 parser
- **[AngleSharp.Css](https://github.com/AngleSharp/AngleSharp.Css)** - MIT License - CSS parsing and styling
- **[AngleSharp.Js](https://github.com/AngleSharp/AngleSharp.Js)** - MIT License - JavaScript engine integration
- **[AngleSharp.Diffing](https://github.com/AngleSharp/Diffing)** - MIT License - HTML document comparison
- **[Html Agility Pack](https://github.com/zzzprojects/html-agility-pack)** - MIT License - Alternative HTML parser

### 🎨 Resource Optimization
- **[NUglify](https://github.com/trullock/NUglify)** - BSD 2-Clause License - HTML/CSS/JS minification
- **[Jsbeautifier](https://github.com/denis-ivanov/Jsbeautifier)** - MIT License - JavaScript formatting
- **[PreMailer.Net](https://github.com/milkshakesoftware/PreMailer.Net)** - Apache 2.0 License - Email CSS inlining

### 🌐 Browser Automation
- **[Microsoft.Playwright](https://github.com/microsoft/playwright-dotnet)** - Apache 2.0 License - Browser automation
- **[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)** - Apache 2.0 License - Image processing
- **[SixLabors.ImageSharp.Drawing](https://github.com/SixLabors/ImageSharp.Drawing)** - Apache 2.0 License - Image drawing
- **[SixLabors.Fonts](https://github.com/SixLabors/Fonts)** - Apache 2.0 License - Font handling

### 🔧 System Libraries
- **[System.Net.Http](https://learn.microsoft.com/dotnet/api/system.net.http)** - MIT License - HTTP client
- **[System.IO.Compression](https://learn.microsoft.com/dotnet/api/system.io.compression)** - MIT License - Compression support
- **[System.Threading.Channels](https://www.nuget.org/packages/System.Threading.Channels)** - MIT License - Async communication

All dependencies are distributed under permissive licenses. Refer to each project's repository for complete license information.

## 📖 Documentation & Support

- **📚 Examples**: Check the `Examples` folder for comprehensive usage samples
- **🐛 Issues**: Report bugs and request features on [GitHub Issues](https://github.com/EvotecIT/PSParseHTML/issues)
- **💬 Discord**: Join our [Discord community](https://evo.yt/discord) for support and discussions
- **📝 Blog**: Read detailed tutorials on [evotec.xyz](https://evotec.xyz/hub)

## 🔄 Updates & Versioning

### PowerShell Module Updates
```powershell
Update-Module -Name PSParseHTML
```

### NuGet Package Updates
```bash
dotnet add package HtmlTinkerX --version latest
```

**⚠️ Important**: Always test updates in a development environment before deploying to production. Breaking changes may occur between versions.

## 🔧 Troubleshooting

### Jint Version Conflict Warning

You may see warnings about conflicting Jint versions when building for .NET Framework 4.7.2:

```
warning MSB3277: Found conflicts between different versions of "Jint" that could not be resolved.
There was a conflict between "Jint, Version=3.1.6.0" and "Jint, Version=4.1.0.0"
```

**Why this happens:**
- HtmlTinkerX references Jint 3.1.6 directly
- AngleSharp.Js 1.0.0-beta.43 (a dependency) was compiled against a different version
- This is a known issue with prerelease packages

**Impact:**
- The warning only affects .NET Framework 4.7.2 builds
- .NET 8.0 and .NET Standard 2.0 builds are not affected
- The library will still work correctly as the older Jint version (3.1.6) is used

**Solutions:**
1. **Ignore the warning** - It doesn't affect functionality
2. **Target only modern frameworks** - Use .NET 8.0 or .NET Standard 2.0
3. **Add binding redirect** in your app.config (for .NET Framework apps):
   ```xml
   <configuration>
     <runtime>
       <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
         <dependentAssembly>
           <assemblyIdentity name="Jint" publicKeyToken="2e92ba9c8d81157f" />
           <bindingRedirect oldVersion="0.0.0.0-4.0.0.0" newVersion="3.1.6.0" />
         </dependentAssembly>
       </assemblyBinding>
     </runtime>
   </configuration>
   ```

### Browser Testing Issues

If browser tests fail:

1. **First run downloads browsers automatically** - This can take a few minutes (~400MB)
   - You'll see: "Downloading Playwright driver... X% (Y MB/s)"
   - This only happens once per system
   
2. **Network timeout issues** - Some sites may be slow or blocked
   - Try increasing timeout: `Test-HtmlBrowser -Url $url -Timeout 60000`
   - Test with a simple URL first: `Test-HtmlBrowser -Url "http://httpbin.org/html"`
   
3. **Behind a proxy** - Set proxy environment variables:
   ```powershell
   $env:HTTPS_PROXY = "http://proxy:8080"
   $env:HTTP_PROXY = "http://proxy:8080"
   ```
   Or use proxy parameters:
   ```powershell
   Test-HtmlBrowser -Url $url -Proxy "http://proxy:8080" -ProxyCredential (Get-Credential)
   ```
   
4. **Clean and retry** if you suspect corrupted downloads:
   ```powershell
   Clear-HtmlBrowserCache -Force
   # Then run your test again - it will re-download browsers
   ```
   
5. **Manual browser installation** (C#):
   ```csharp
   // Ensure browser is installed before testing
   await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
   ```

## 📄 License

Copyright (c) 2011 - 2025 Przemyslaw Klys @ Evotec. All rights reserved.

This project and its dependencies are distributed under various permissive licenses. See individual dependency repositories for specific license terms.

---

*Built with ❤️ by [Evotec](https://evotec.xyz) - Making web content processing simple and powerful.*
