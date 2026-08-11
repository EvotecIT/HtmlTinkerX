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

## Render HTML to PDF with a warm Chromium pool

`HtmlBrowserPdfRenderer` keeps Chromium processes warm while creating a fresh browser context for every render. The renderer bounds active and queued work, recycles browsers by age, render count, or failure, and reports queue and render timings. Failed captures are not replayed by default because navigation or caller scripts may have external side effects. Set `retryOnBrowserFailure: true` only for an idempotent request to permit one retry after an actual browser-process failure.

```csharp
using HtmlTinkerX;

await using var renderer = new HtmlBrowserPdfRenderer(
    new HtmlBrowserPdfRendererOptions(
        minimumBrowserInstances: 1,
        maximumBrowserInstances: 4,
        maximumQueuedCaptures: 32));

await renderer.PreWarmAsync();

var request = new HtmlBrowserPdfRequest(
    HtmlBrowserPdfSource.FromUrl("https://reports.example.org/quarterly"),
    pdfOptions: new HtmlBrowserPdfOptions(
        format: PdfPageFormat.A4,
        printBackground: true,
        marginTop: "12mm",
        marginBottom: "12mm",
        tagged: true,
        outline: true),
    readiness: new HtmlBrowserPdfReadiness(
        selector: "[data-report-ready]",
        timeout: 30_000),
    navigationTimeout: 60_000,
    maximumPdfBytes: 64L * 1024 * 1024,
    headers: new Dictionary<string, string> {
        ["X-Correlation-Id"] = correlationId
    });

HtmlBrowserPdfResult result = await renderer.CaptureAsync(request, cancellationToken);
await File.WriteAllBytesAsync("quarterly.pdf", result.PdfBytes, cancellationToken);

Console.WriteLine(
    $"Browser {result.Diagnostics.BrowserInstanceId}: " +
    $"queued {result.Diagnostics.QueueDuration.TotalMilliseconds:N0} ms, " +
    $"rendered {result.Diagnostics.TotalDuration.TotalMilliseconds:N0} ms");
```

Use `HtmlBrowserPdfSource.FromHtml(markup, baseUri)` for an HTML string or `HtmlBrowserPdfSource.FromFile(path)` for a local document. An HTTP/HTTPS base gives an HTML string that origin; a direct local `file:` base loads relative styles, scripts, and images under the same local-directory boundary as file capture. Per-render headers and local/session storage are restricted to the URL source origin, so HTML-string capture requires an absolute HTTP/HTTPS `baseUri` when using them. Cookies retain their own URL/domain scope. CSS, JavaScript, media type, readiness conditions, sensitive-element masking, and Chromium print options are captured in the same immutable request snapshot. For a page already loaded in an authenticated `HtmlBrowserSession`, pass its `IPage` to `GetPagePdfAsync` or `SavePagePdfAsync` with `HtmlBrowserPdfOptions`.

File capture accepts local paths only. UNC/device paths are rejected on every platform. On Windows, mapped or substituted drives and symbolic-link, junction, or reparse-point indirection are also rejected before file content is probed; user-controlled Unix symbolic-link components and remote or userspace filesystem mounts are rejected as well.

Per-render headers cover same-origin HTTP(S) pages and popups, subresources, and dedicated worker requests. Shared worker requests and JavaScript WebSocket handshakes cannot carry securely origin-scoped arbitrary headers through Playwright's public APIs; use a scoped cookie or authenticated page state for those endpoints.

`HtmlBrowserPdfRendererOptions.SetupTimeout` limits browser provisioning, isolated context, page, and interception setup before navigation and defaults to 30 seconds. `navigationTimeout` limits initial source loading. `beforeCaptureScriptTimeout` limits an optional pre-capture script, and `pdfTimeout` limits Chromium PDF generation; both default to 30 seconds. `maximumPdfBytes` streams Chromium output through a bounded 128 MiB default; set a smaller service-specific limit, or zero only for a trusted document that intentionally needs unbounded Playwright output. `HtmlBrowserPdfReadiness.Timeout` independently limits each readiness condition after navigation. Set an individual timeout to zero only when that stage may intentionally run without a deadline. Explicit mask selectors are validated by Chromium before masking begins; an invalid selector fails the capture rather than returning an unredacted PDF.

Browser PDF output is a Chromium capability. Selecting Firefox or WebKit for a PDF request throws before a browser is launched.

When HtmlTinkerX owns the public-network or host-policy proxy, Chromium also disables non-proxied WebRTC UDP and QUIC so page traffic cannot bypass that policy boundary. An explicit private-network policy leaves those browser transports available.

The pooled renderer validates HTTPS certificates, rejects browser downloads, and permits only public HTTP(S) and WS(S) targets by default. Its browser-slot proxy connects to the same DNS address that passed policy evaluation, preventing a second browser-side lookup from changing the destination. `HtmlBrowserNetworkPolicy` can allow specific private hosts and canonical file roots for trusted internal workloads; symlink escapes are rejected. Deployments that route an RFC 6052 network-specific NAT64 prefix should pass that CIDR through `nat64Prefixes` so the embedded IPv4 destination receives the same public/private classification as native traffic. The well-known `64:ff9b::/96` prefix is handled automatically. A caller-supplied proxy requires explicit private-network mode because that trusted proxy owns DNS and outbound enforcement. These controls are defense in depth, not a process sandbox: services that accept untrusted URLs or markup should also enforce outbound policy at the container, host, firewall, or proxy boundary and should not expose arbitrary local-file paths.

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
