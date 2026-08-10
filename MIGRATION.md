# Migrating to HtmlTinkerX 3.0

HtmlTinkerX 3.0 replaces the browser PDF parameter-list APIs with request and options objects. The PowerShell `Save-HtmlBrowserPdf` command keeps its existing parameters.

## URL, HTML string, and file capture

Use `HtmlBrowserPdfRenderer` for capture that owns the browser lifecycle. It reuses bounded Chromium processes and creates an isolated browser context for each request.

```csharp
await using var renderer = new HtmlBrowserPdfRenderer(
    new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 2));

var request = new HtmlBrowserPdfRequest(
    HtmlBrowserPdfSource.FromUrl("https://example.com"),
    pdfOptions: new HtmlBrowserPdfOptions(
        format: PdfPageFormat.A4,
        printBackground: true),
    navigationTimeout: 30_000);

HtmlBrowserPdfResult result = await renderer.CaptureAsync(request);
await File.WriteAllBytesAsync("page.pdf", result.PdfBytes);
```

`HtmlBrowserPdfSource.FromHtml(markup, baseUri)` and `HtmlBrowserPdfSource.FromFile(path)` use the same request contract.

Initial navigation uses `HtmlBrowserPdfRequest.NavigationTimeout`. `HtmlBrowserPdfReadiness.Timeout` now applies only to each configured load-state, selector, function, or stability check, so a short readiness deadline no longer shortens source loading.

Per-render headers and local/session storage are limited to the source origin. HTML-string capture must provide an absolute HTTP or HTTPS `baseUri` when using those values; HtmlTinkerX navigates the supplied markup at that origin while still resolving relative resources from the base URI. This prevents credentials from being broadcast to cross-origin frames and resources.

Per-render headers apply to same-origin HTTP(S) document and subresource requests. Browser WebSocket handshakes do not support arbitrary request headers; use scoped cookies or page authentication state when a WS/WSS endpoint requires credentials.

## Already-loaded pages

The long `GetPagePdfAsync` and `SavePagePdfAsync` overloads were removed. Pass immutable print and readiness objects instead:

```csharp
await HtmlBrowser.SavePagePdfAsync(
    session.Page,
    "page.pdf",
    new HtmlBrowserPdfOptions(
        landscape: true,
        format: PdfPageFormat.A4,
        marginTop: "12mm",
        marginBottom: "12mm"),
    new HtmlBrowserPdfReadiness(
        skipLoadState: true,
        selector: "[data-report-ready]"),
    cancellationToken);
```

Omit readiness when the caller has already prepared the page. Cancelling an active direct-page PDF operation closes that caller-owned page because Playwright does not expose cancellation for Chromium printing.

`HtmlBrowserPdfOptions` defaults to A4 with background graphics enabled. To reproduce the old direct-page defaults, set `format: null` and `printBackground: false` explicitly.

## HTTPS certificates

Browser sessions and browser tests now validate HTTPS certificates by default. Set `HtmlBrowserLaunchOptions.IgnoreHTTPSErrors = true`, `HtmlBrowserPdfRendererOptions(ignoreHttpsErrors: true)`, or the PowerShell `-IgnoreHttpsErrors` switch only for a source whose certificate you intentionally trust. The pooled renderer applies this opt-in to its dedicated Chromium processes and isolated contexts.

Browser PDF output remains Chromium-only. Firefox and WebKit cannot service a PDF request.
