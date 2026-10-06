using System.Globalization;
using System.Text;
using AngleSharp.Dom;

namespace HtmlTinkerX.Benchmarks;

/// <summary>Optional benchmark inputs and independent output expectations.</summary>
public sealed class Corpus : IDisposable {
    private static readonly Dictionary<string, Corpus> fixtures = new();
    private const string UnicodeText = "Zażółć gęślą jaźń · 東京 · café · 😀";
    private readonly string family;
    private readonly int size;
    private readonly string html;
    private readonly LoopbackSite? site;
    private readonly HttpClient client = new(new HttpClientHandler { UseProxy = false });
    private readonly HtmlBrowserPdfRenderer? renderer;
    private readonly HtmlBrowserPdfRequest? request;
    private object? actual;

    private Corpus(string family, int size) {
        try {
            this.family = family;
            this.size = size;
            string rows = string.Concat(Enumerable.Range(0, size).Select(index =>
                $"<tr><td>{index}</td><td>{index * 7}</td><td>{UnicodeText}</td></tr>"));
            string table = $"<table><thead><tr><th>Id</th><th>Value</th><th>Label</th></tr></thead><tbody>{rows}</tbody></table>";
            string paragraphs = string.Concat(Enumerable.Range(0, size).Select(index =>
                $"<p>Record {index:D4}: {UnicodeText}. A complete paragraph for readable content extraction.</p>"));
            html = family switch {
                "Nested" => Page(string.Concat(Enumerable.Repeat("<section>", 24)) + paragraphs + string.Concat(Enumerable.Repeat("</section>", 24))),
                "Tables" or "WarmBrowser" => Page(table),
                "Cards" => Page(string.Concat(Enumerable.Range(0, size).Select(index =>
                    $"<article class='card'><h2>Card {index:D4}</h2><a href='/item/{index}'>Details</a><p>{UnicodeText}</p></article>"))),
                _ => Page(paragraphs)
            };
            if (family is "StaticRead" or "Crawl") {
                var pages = new Dictionary<string, string> { ["/"] = html };
                if (family == "Crawl") {
                    pages.Clear();
                    for (int index = 0; index < size; index++) {
                        pages[index == 0 ? "/" : $"/page/{index}"] = Page(
                            $"<h1>Page {index:D4}</h1><p>{UnicodeText} unique-page-{index:D4}</p>" +
                            (index + 1 < size ? $"<a href='/page/{index + 1}'>Next page</a>" : ""));
                    }
                }
                site = new LoopbackSite(pages);
            }
            if (family == "WarmBrowser") {
                HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium).GetAwaiter().GetResult();
                renderer = new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(
                    maximumBrowserInstances: 1, maximumRendersPerBrowser: int.MaxValue,
                    maximumBrowserAge: TimeSpan.FromHours(12)));
                request = new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromHtml(html), beforeCaptureScript:
                    $"() => {{ if (document.querySelectorAll('tbody tr').length !== {size} || !document.body.textContent.includes('東京')) throw new Error('Benchmark DOM mismatch'); }}");
                actual = renderer.CaptureAsync(request).GetAwaiter().GetResult();
                Validate(); // Browser startup and first render are outside the measured operation.
            }
        } catch { Dispose(); throw; }
    }

    /// <summary>Gets one prepared fixture shared by warmups and measured iterations.</summary>
    public static Corpus Get(string family, int size) {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (!new[] { "Nested", "Tables", "Cards", "Unicode", "StaticRead", "WarmBrowser", "Crawl" }.Contains(family))
            throw new ArgumentException("Unknown corpus family.", nameof(family));
        string key = $"{family}/{size}";
        if (!fixtures.TryGetValue(key, out Corpus? fixture)) fixtures.Add(key, fixture = new Corpus(family, size));
        return fixture;
    }

    /// <summary>Runs the public operation; input generation and result validation are separate.</summary>
    public void Run() {
        actual = family switch {
            "Nested" => HtmlParserToText.ExtractReadableText(html),
            "Tables" => HtmlParserFromTable.ParseTablesWithAngleSharpDetailed(html),
            "Cards" => HtmlPageReader.Read(html, new HtmlPageReaderOptions { IncludeMarkdown = false }),
            "Unicode" => HtmlParser.ParseWithAngleSharp(html),
            "StaticRead" => HtmlParser.ParseUrlWithAngleSharpAsync(site!.Root.AbsoluteUri, client).GetAwaiter().GetResult(),
            "WarmBrowser" => renderer!.CaptureAsync(request!).GetAwaiter().GetResult(),
            "Crawl" => HtmlCrawler.CrawlAsync(site!.Root.AbsoluteUri, new HtmlCrawlOptions {
                MaxPages = size,
                MaxDepth = size,
                RespectRobotsTxt = false,
                UseSitemaps = false,
                IncludeMarkdown = false,
                DownloadAssets = false,
                OutputPath = null
            }).GetAwaiter().GetResult(),
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>Releases the previous document outside the next measured operation.</summary>
    public void Prepare() {
        (actual as IDisposable)?.Dispose();
        actual = null;
    }

    /// <summary>Checks work volume and content after each operation, without accepting empty output.</summary>
    public void Validate() {
        switch (family) {
            case "Nested":
                string text = ((HtmlReadableTextResult)actual!).Text;
                Require(Enumerable.Range(0, size).All(index => text.Contains($"Record {index:D4}:")), "Readable paragraphs missing");
                Require(text.Contains(UnicodeText), "Readable Unicode changed");
                break;
            case "Tables":
                var tables = (List<HtmlTableResult>)actual!;
                Require(tables.Count == 1 && tables[0].Data.Count == size, "Table row count changed");
                Require(tables[0].Data.Select(row => long.Parse(row["Value"]!, CultureInfo.InvariantCulture)).Sum() == 7L * size * (size - 1) / 2, "Table checksum changed");
                Require(tables[0].Data.Select(row => row["Id"]).SequenceEqual(Enumerable.Range(0, size).Select(index => index.ToString(CultureInfo.InvariantCulture))), "Table row identity changed");
                Require(tables[0].Data.All(row => row["Label"] == UnicodeText), "Table Unicode changed");
                break;
            case "Cards":
                var page = (HtmlPageDocument)actual!;
                Require(page.Headings.Count == size && page.Links.Count == size, "Card headings or links missing");
                Require(page.ReadableText.Text.Contains(UnicodeText), "Card Unicode changed");
                Require(page.Collections.Any(collection => collection.Count == size), "Repeated collection incomplete");
                break;
            case "Unicode":
            case "StaticRead":
                var document = (IDocument)actual!;
                Require(document.QuerySelectorAll("p").Length == size, "Paragraph count changed");
                Require(document.Title == "Benchmark corpus" && document.QuerySelectorAll("p").All(p => p.TextContent.Contains(UnicodeText)), "Document encoding or title changed");
                break;
            case "WarmBrowser":
                byte[] pdf = ((HtmlBrowserPdfResult)actual!).PdfBytes;
                Require(pdf.Length > 1000 && Encoding.ASCII.GetString(pdf, 0, 5) == "%PDF-", "PDF payload missing");
                Require(renderer!.GetMetricsSnapshot().BrowsersCreated == 1, "Warm renderer recreated its browser");
                break;
            case "Crawl":
                var crawl = (HtmlCrawlResult)actual!;
                Require(crawl.Pages.Count == size && crawl.FailedPageCount == 0, "Crawl did not finish all pages");
                Require(crawl.Pages.Select(p => p.Url).Distinct().Count() == size, "Crawl duplicated a page");
                Require(Enumerable.Range(0, size).All(index => crawl.Pages.Any(p => p.Html.Contains($"unique-page-{index:D4}") && p.Html.Contains(UnicodeText))), "Crawl content missing");
                break;
        }
    }

    /// <summary>Number of rows, cards, paragraphs or pages validated for this case.</summary>
    public int Units => size;

    /// <summary>Disposes all prepared fixtures after the native runner exits, including failures.</summary>
    public static void DisposeAll() {
        List<Exception> failures = new();
        foreach (Corpus fixture in fixtures.Values) {
            try { fixture.Dispose(); } catch (Exception error) { failures.Add(error); }
        }
        fixtures.Clear();
        if (failures.Count != 0) throw new AggregateException(failures);
    }

    public void Dispose() {
        try { Prepare(); renderer?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } finally { try { site?.Dispose(); } finally { client.Dispose(); } }
    }

    private static string Page(string body) => $"<!doctype html><html><head><meta charset='utf-8'><title>Benchmark corpus</title></head><body><main>{body}</main></body></html>";
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}