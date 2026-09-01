using HtmlTinkerX;
using Microsoft.Playwright;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public partial class HtmlCrawlerTests {
    [Fact]
    public void HtmlCrawlOptions_CloneAndClearSensitiveData_WorkIndependently() {
        HtmlCrawlOptions options = new() {
            Username = "user",
            Password = "secret",
            ProxyUsername = "proxy-user",
            ProxyPassword = "proxy-secret",
            MaximumPageResponseBytes = 2048,
            MaximumAssetResponseBytes = 4096,
            StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Docs,
            FormLogin = new HtmlFormLogin {
                LoginUrl = "https://example.com/login",
                UsernameSelector = "#user",
                PasswordSelector = "#password",
                SubmitSelector = "button[type=submit]"
            },
            RenderedPageObserver = new RecordingRenderedPageObserver()
        };
        options.Headers["X-Test"] = "one";
        options.IncludePatterns.Add("*docs*");
        options.ClickSelectors.Add(".load-more");
        options.ClickTexts.Add("Load more");
        options.DismissSelectors.Add(".cookie-banner");
        options.DismissTexts.Add("Accept");

        HtmlCrawlOptions clone = options.Clone();
        clone.Headers["X-Test"] = "two";
        clone.IncludePatterns.Add("*blog*");
        clone.ClickSelectors.Add(".expand");
        clone.ClickTexts.Add("Show more");
        clone.DismissSelectors.Add(".newsletter");
        clone.DismissTexts.Add("Dismiss");
        clone.MarkdownImageMode = OfficeIMO.Markdown.MarkdownImageRenderingMode.Html;
        clone.HiddenContentMode = HtmlCrawlHiddenContentMode.IncludeHidden;
        clone.ListingCardMetadataMode = OfficeIMO.Markdown.Html.HtmlListingCardMetadataMode.Preserve;
        clone.ClearSensitiveData();

        Assert.Equal("secret", options.Password);
        Assert.Equal("proxy-secret", options.ProxyPassword);
        Assert.Equal("one", options.Headers["X-Test"]);
        Assert.Single(options.IncludePatterns);
        Assert.Empty(options.ExcludeSelectors);
        Assert.Single(options.ClickSelectors);
        Assert.Single(options.ClickTexts);
        Assert.Single(options.DismissSelectors);
        Assert.Single(options.DismissTexts);
        Assert.Null(clone.Password);
        Assert.Null(clone.ProxyPassword);
        Assert.Equal("two", clone.Headers["X-Test"]);
        Assert.Equal(2, clone.IncludePatterns.Count);
        Assert.Equal(2, clone.ClickSelectors.Count);
        Assert.Equal(2, clone.ClickTexts.Count);
        Assert.Equal(2, clone.DismissSelectors.Count);
        Assert.Equal(2, clone.DismissTexts.Count);
        Assert.Equal(HtmlCrawlStructuredJsonPreset.Docs, clone.StructuredJsonPreset);
        Assert.Equal(2048, clone.MaximumPageResponseBytes);
        Assert.Equal(4096, clone.MaximumAssetResponseBytes);
        Assert.Equal(OfficeIMO.Markdown.MarkdownImageRenderingMode.PortableMarkdown, options.MarkdownImageMode);
        Assert.Equal(HtmlCrawlHiddenContentMode.RespectHidden, options.HiddenContentMode);
        Assert.Equal(OfficeIMO.Markdown.Html.HtmlListingCardMetadataMode.SuppressInRepeatedCards, options.ListingCardMetadataMode);
        Assert.Equal(OfficeIMO.Markdown.MarkdownImageRenderingMode.Html, clone.MarkdownImageMode);
        Assert.Equal(HtmlCrawlHiddenContentMode.IncludeHidden, clone.HiddenContentMode);
        Assert.Equal(OfficeIMO.Markdown.Html.HtmlListingCardMetadataMode.Preserve, clone.ListingCardMetadataMode);
        Assert.NotSame(options.FormLogin, clone.FormLogin);
        Assert.Same(options.RenderedPageObserver, clone.RenderedPageObserver);

        string json = JsonSerializer.Serialize(options);
        HtmlCrawlOptions? deserialized = JsonSerializer.Deserialize<HtmlCrawlOptions>(json);
        Assert.DoesNotContain("RenderedPageObserver", json, System.StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.RenderedPageObserver);
    }

    [Fact]
    public async Task CrawlAsync_InvokesRenderedPageObserverWithoutSecondNavigation() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = """
<html>
  <head><title>Rendered observer</title></head>
  <body><main>Prepared rendered content</main></body>
</html>
"""
        };

        int documentRequestCount = 0;
        using var server = StartServer(responses, out string rootUrl, onRequest: path => {
            if (path == "/") {
                Interlocked.Increment(ref documentRequestCount);
            }
        });
        RecordingRenderedPageObserver observer = new();
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 0,
            MaxPages = 1,
            Render = true,
            RenderedPageObserver = observer
        });

        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Equal(1, observer.Count);
        Assert.Same(page, observer.Page);
        Assert.Equal(page.Url, observer.SessionUrl);
        Assert.Contains("Prepared rendered content", observer.BrowserHtml, System.StringComparison.Ordinal);
        Assert.Equal(1, documentRequestCount);
        Assert.Equal(HtmlCrawlRenderReasonCode.ExplicitRender, observer.RenderReasonCodeAtObservation);
        Assert.NotEqual(default, observer.FinishedAtObservation);
        Assert.Equal(page.Finished, observer.FinishedAtObservation);
        Assert.NotEmpty(observer.NetworkLog);
        Assert.Contains(observer.NetworkLog, entry => entry.Url == rootUrl);
        Assert.True(observer.Session!.Page.IsClosed);
    }

    [Fact]
    public async Task CrawlAsync_PropagatesRenderedPageObserverFailureAndCancellation() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = "<html><body><main>Observer failures remain distinct.</main></body></html>"
        };

        using var server = StartServer(responses, out string rootUrl);
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Render = true,
                RenderedPageObserver = new ThrowingRenderedPageObserver(cancel: false)
            }));
        Assert.Equal("observer failure", failure.Message);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Render = true,
                RenderedPageObserver = new ThrowingRenderedPageObserver(cancel: true)
            }));
    }

    [Fact]
    public async Task CrawlAsync_AutoRenderObserverReceivesOnlyCurrentPageNetworkEntries() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = "<html><body><div id='app'></div><script>document.getElementById('app').innerHTML=\"<a href='/second'>Rendered home</a>\";</script></body></html>",
            ["/second"] = "<html><body><main>Second rendered page</main></body></html>"
        };

        using var server = StartServer(responses, out string rootUrl);
        RecordingRenderedPageObserver observer = new();
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 1,
            MaxPages = 2,
            AutoRender = true,
            AutoRenderTextWordThreshold = 20,
            RenderedPageObserver = observer
        });

        Assert.Equal(2, result.PageCount);
        Assert.Equal(2, observer.Observations.Count);
        Assert.All(observer.Observations, observation => Assert.Equal(HtmlCrawlRenderMode.AutoRendered, observation.RenderMode));
        Assert.Contains(observer.Observations[0].NetworkUrls, url => url == rootUrl);
        string secondUrl = new Uri(new Uri(rootUrl), "/second").AbsoluteUri;
        Assert.Contains(observer.Observations[1].NetworkUrls, url => url == secondUrl);
        Assert.DoesNotContain(observer.Observations[1].NetworkUrls, url => url == rootUrl);
    }

    [Fact]
    public async Task CrawlAsync_RespectsHiddenContentByDefault() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = """
<html>
  <body>
    <main>
      <p>Visible text</p>
      <div hidden>Hidden attribute text</div>
      <p style="display:none">Display none text</p>
      <span aria-hidden="true">Aria hidden text</span>
      <input type="hidden" value="secret" />
    </main>
  </body>
</html>
"""
        };

        using var server = StartServer(responses, out string rootUrl);
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 0,
            MaxPages = 1,
            Selector = "main",
            IncludeHtml = true,
            IncludeText = true,
            IncludeMarkdown = true
        });

        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Contains("Visible text", page.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden attribute text", page.Html, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Display none text", page.Html, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Aria hidden text", page.Html, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden attribute text", page.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Display none text", page.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Aria hidden text", page.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden attribute text", page.Markdown, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Display none text", page.Markdown, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Aria hidden text", page.Markdown, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task CrawlAsync_CanIncludeHiddenContentWhenRequested() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = """
<html>
  <body>
    <main>
      <p>Visible text</p>
      <div hidden>Hidden attribute text</div>
      <p style="display:none">Display none text</p>
      <span aria-hidden="true">Aria hidden text</span>
    </main>
  </body>
</html>
"""
        };

        using var server = StartServer(responses, out string rootUrl);
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 0,
            MaxPages = 1,
            Selector = "main",
            IncludeHtml = true,
            IncludeText = true,
            IncludeMarkdown = true,
            HiddenContentMode = HtmlCrawlHiddenContentMode.IncludeHidden
        });

        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Contains("Hidden attribute text", page.Html, System.StringComparison.Ordinal);
        Assert.Contains("Display none text", page.Html, System.StringComparison.Ordinal);
        Assert.Contains("Aria hidden text", page.Html, System.StringComparison.Ordinal);
        Assert.Contains("Hidden attribute text", page.Text, System.StringComparison.Ordinal);
        Assert.Contains("Display none text", page.Text, System.StringComparison.Ordinal);
        Assert.Contains("Aria hidden text", page.Text, System.StringComparison.Ordinal);
        Assert.Contains("Hidden attribute text", page.Markdown, System.StringComparison.Ordinal);
        Assert.Contains("Display none text", page.Markdown, System.StringComparison.Ordinal);
        Assert.Contains("Aria hidden text", page.Markdown, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkRenderedHiddenElementsAsync_MarksComputedHiddenContentFromStylesheets() {
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        using IPlaywright playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using IBrowser browser = await LaunchChromiumWithRetryAsync(playwright);
        IPage page = await browser.NewPageAsync();
        await page.SetContentAsync("""
<html>
  <head>
    <style>
      .theme-hidden { display: none; }
    </style>
  </head>
  <body>
    <main>
      <p>Visible text</p>
      <p class="theme-hidden">Hidden by stylesheet</p>
    </main>
  </body>
</html>
""");

        await HtmlCrawler.MarkRenderedHiddenElementsAsync(page);

        Assert.Equal("true", await page.Locator(".theme-hidden").GetAttributeAsync("data-htmltinkerx-hidden"));
        Assert.Null(await page.Locator("main > p:first-of-type").GetAttributeAsync("data-htmltinkerx-hidden"));
    }

    private static async Task<IBrowser> LaunchChromiumWithRetryAsync(IPlaywright playwright) {
        for (int attempt = 1; attempt <= 3; attempt++) {
            try {
                return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions {
                    Headless = true
                });
            } catch (PlaywrightException ex) when (attempt < 3 && IsTransientSpawnLock(ex)) {
                await Task.Delay(TimeSpan.FromMilliseconds(750 * attempt));
            }
        }

        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions {
            Headless = true
        });
    }

    private static bool IsTransientSpawnLock(PlaywrightException exception) {
        return exception.Message.Contains("spawn EBUSY", System.StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("spawn ETXTBSY", System.StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingRenderedPageObserver : IHtmlCrawlRenderedPageObserver {
        public int Count { get; private set; }

        public HtmlCrawlPage? Page { get; private set; }

        public string? SessionUrl { get; private set; }

        public string? BrowserHtml { get; private set; }

        public HtmlBrowserSession? Session { get; private set; }

        public HtmlCrawlRenderReasonCode RenderReasonCodeAtObservation { get; private set; }

        public System.DateTimeOffset FinishedAtObservation { get; private set; }

        public System.Collections.Generic.IReadOnlyList<HtmlNetworkEntry> NetworkLog { get; private set; } = System.Array.Empty<HtmlNetworkEntry>();

        public System.Collections.Generic.List<RenderedPageObservation> Observations { get; } = new();

        public async Task ObserveAsync(HtmlCrawlRenderedPageContext context, CancellationToken cancellationToken = default) {
            Count++;
            Page = context.Page;
            Session = context.Session;
            SessionUrl = context.Session.Page.Url;
            BrowserHtml = await context.Session.Page.ContentAsync();
            RenderReasonCodeAtObservation = context.Page.RenderReasonCode;
            FinishedAtObservation = context.Page.Finished;
            NetworkLog = context.NetworkLog;
            Observations.Add(new RenderedPageObservation(
                context.Page.Url,
                context.Page.RenderMode,
                context.NetworkLog.Select(entry => entry.Url).ToArray()));
        }
    }

    private sealed class ThrowingRenderedPageObserver : IHtmlCrawlRenderedPageObserver {
        private readonly bool _cancel;

        public ThrowingRenderedPageObserver(bool cancel) => _cancel = cancel;

        public Task ObserveAsync(HtmlCrawlRenderedPageContext context, CancellationToken cancellationToken = default) {
            if (_cancel) {
                throw new OperationCanceledException(cancellationToken);
            }

            throw new InvalidOperationException("observer failure");
        }
    }

    private sealed class RenderedPageObservation {
        public RenderedPageObservation(
            string url,
            HtmlCrawlRenderMode renderMode,
            System.Collections.Generic.IReadOnlyList<string> networkUrls) {
            Url = url;
            RenderMode = renderMode;
            NetworkUrls = networkUrls;
        }

        public string Url { get; }

        public HtmlCrawlRenderMode RenderMode { get; }

        public System.Collections.Generic.IReadOnlyList<string> NetworkUrls { get; }
    }

    [Fact]
    public async Task CrawlAsync_RespectsRenderedHiddenMarkerByDefault() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = """
<html>
  <body>
    <main>
      <p>Visible text</p>
      <p data-htmltinkerx-hidden="true">Hidden by stylesheet</p>
    </main>
  </body>
</html>
"""
        };

        using var server = StartServer(responses, out string rootUrl);
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 0,
            MaxPages = 1,
            Selector = "main",
            IncludeHtml = true,
            IncludeText = true,
            IncludeMarkdown = true
        });

        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.DoesNotContain("Hidden by stylesheet", page.Html, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden by stylesheet", page.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden by stylesheet", page.Markdown, System.StringComparison.Ordinal);
        Assert.Contains("Visible text", page.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlCrawlProfiles_ResolveByNameAndApplyBuiltInProfile() {
        HtmlCrawlProfile? profile = HtmlCrawlProfiles.ResolveByName("docs-content");

        Assert.NotNull(profile);
        Assert.Equal("docs-content", profile!.Name, ignoreCase: true);
        Assert.Contains("api-docs-content", HtmlCrawlProfiles.Names);
        Assert.Contains("docs-content", HtmlCrawlProfiles.Names);
        Assert.Contains("wordpress-content", HtmlCrawlProfiles.Names);

        HtmlCrawlOptions options = new();
        HtmlCrawlProfiles.Apply(options, profile);

        Assert.Equal("main", options.Selector);
        Assert.Equal(HtmlCrawlContentMode.Reader, options.ContentMode);
        Assert.True(options.CompareContentModes);
        Assert.Equal(35, options.ReaderMinimumWordCount);
        Assert.Equal(35, options.ReaderMinimumScore);
        Assert.Contains(".theme-doc-toc-desktop", options.ExcludeSelectors);
        Assert.Contains(".feedback-box", options.ExcludeSelectors);
    }

    [Fact]
    public async Task HtmlCrawlProfiles_LoadFromPathAsync_LoadsCustomProfiles() {
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, """
            {
              "profiles": [
                {
                  "name": "custom-docs",
                  "hosts": [ "docs.example.com" ],
                  "selector": "article",
                  "contentMode": "Reader",
                  "compareContentModes": true,
                  "readerMinimumWordCount": 30,
                  "readerMinimumScore": 40,
                  "excludeSelectors": [ ".sidebar", ".feedback" ],
                  "clickTexts": [ "Show more" ]
                }
              ]
            }
            """);

            IReadOnlyList<HtmlCrawlProfile> profiles = await HtmlCrawlProfiles.LoadFromPathAsync(path);
            HtmlCrawlProfile profile = Assert.Single(profiles);
            Assert.Equal("custom-docs", profile.Name);
            Assert.Contains("docs.example.com", profile.Hosts);
            Assert.Equal("article", profile.Selector);
            Assert.Equal(HtmlCrawlContentMode.Reader, profile.ContentMode);
            Assert.True(profile.CompareContentModes);
            Assert.Equal(30, profile.ReaderMinimumWordCount);
            Assert.Equal(40, profile.ReaderMinimumScore);
            Assert.Contains(".sidebar", profile.ExcludeSelectors);
            Assert.Contains("Show more", profile.ClickTexts);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CrawlAsync_UnknownProfile_ThrowsHelpfulError() {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            HtmlCrawler.CrawlAsync("https://example.com/", new HtmlCrawlOptions {
                ProfileName = "missing-profile"
            }));

        Assert.Contains("Unknown crawl profile", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docs-content", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wordpress-content", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetFreePort() {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpListener StartServer(
        Dictionary<string, string> responses,
        out string rootUrl,
        string host = "localhost",
        System.Action<string>? onRequest = null) {
        HttpListener listener = new();
        StartListenerWithFreePort(listener, out rootUrl, host);

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    onRequest?.Invoke(key);
                    if (responses.TryGetValue(key, out string? html)) {
                        byte[] data = Encoding.UTF8.GetBytes(html);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.Length;
                        await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    } else {
                        context.Response.StatusCode = 404;
                    }

                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        return listener;
    }

    private static void StartListenerWithFreePort(HttpListener listener, out string rootUrl, string host = "localhost") {
        for (int attempt = 0; attempt < 10; attempt++) {
            int port = GetFreePort();
            rootUrl = $"http://{host}:{port}/";
            listener.Prefixes.Clear();
            listener.Prefixes.Add(rootUrl);

            try {
                listener.Start();
                return;
            } catch (HttpListenerException) when (attempt < 9) {
                Thread.Sleep(25);
            }
        }

        rootUrl = string.Empty;
        throw new HttpListenerException();
    }

    private static void DisposeListenerSafely(HttpListener listener) {
        try {
            listener.Stop();
        } catch (HttpListenerException) {
        } catch (ObjectDisposedException) {
        }

        try {
            listener.Close();
        } catch (HttpListenerException) {
        } catch (ObjectDisposedException) {
        }
    }

    [Fact]
    public async Task CrawlAsync_FollowsSameHostLinksUpToDepth() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><a href='/about'>About</a><a href='https://example.com/offsite'>Offsite</a></body></html>",
            ["/about"] = "<html><head><title>About</title></head><body><a href='/team'>Team</a></body></html>",
            ["/team"] = "<html><head><title>Team</title></head><body>Team page</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl);
            Assert.Contains(result.Pages, page => page.Url == new Uri(new Uri(rootUrl), "/about").AbsoluteUri);
            Assert.DoesNotContain(result.Pages, page => page.Url.Contains("/team", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(result.Pages, page => page.Url.Contains("example.com", StringComparison.OrdinalIgnoreCase));
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_RespectsExcludePatterns() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><a href='/keep'>Keep</a><a href='/skip-me'>Skip</a></body></html>",
            ["/keep"] = "<html><body>Kept</body></html>",
            ["/skip-me"] = "<html><body>Skipped</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                ExcludePatterns = { "*skip-me*" }
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url.EndsWith("/keep", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(result.Pages, page => page.Url.EndsWith("/skip-me", StringComparison.OrdinalIgnoreCase));
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_UsesSelectorForStoredContent() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><nav>Ignore me</nav><main><h1>Hello</h1><p>World</p></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main"
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("<main>", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Ignore me", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Hello", page.Text, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_FallsBackToSemanticMainAndStripsBoilerplateText() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><header id='site-header'><nav id='primary-navigation'><a href='/about'>About</a></nav></header><div id='main' role='main'><div class='entry-content post-content'><h1>Hello</h1><p>World</p><div class='sharing-popup'><a href='https://facebook.com/sharer.php'>Share</a></div></div></div><footer id='footer-nav'>Footer text</footer><div class='wpml-ls-statics-footer'><a href='https://example.pl/'>Polish</a></div></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main"
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("About", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Footer text", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Share", page.Text, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_ExcludeSelectors_RemoveConfiguredNoiseFromStoredContentAndText() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><main><h1>Hello</h1><p>World</p><div class='blog-grid'><a href='/post'>Read More</a></div><div class='language-switcher'>Polish</div></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                ExcludeSelectors = { ".blog-grid", ".language-switcher" }
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Read More", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Read More", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Text, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_ExcludeClassesAndIds_RemoveConfiguredNoiseFromStoredContentAndText() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><main><h1>Hello</h1><p>World</p><div class='promo-box'>Sign up</div><div id='reader-tools'>Tools</div></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                SmartContentCleanup = false,
                ExcludeClasses = { "promo-box" },
                ExcludeIds = { "reader-tools" }
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sign up", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sign up", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Tools", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Tools", page.Text, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_SmartContentCleanup_RemovesLowValueInContentBoilerplate() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Docs</title></head><body><main><article><h1>Hello</h1><p>World content for the actual page body with enough text to keep.</p></article><div class='related-posts'><a href='/one'>One</a><a href='/two'>Two</a><a href='/three'>Three</a><a href='/four'>Four</a></div><div class='language-switcher'><a href='/pl'>Polish</a><a href='/de'>German</a><a href='/fr'>French</a></div></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main"
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World content", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("One", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("related-posts", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("language-switcher", page.Html, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_ContentMode_RawKeepsExactSelectionWithoutFallback() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><article><h1>Hello</h1><p>World</p></article></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                ContentMode = HtmlCrawlContentMode.Raw
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.True(string.IsNullOrWhiteSpace(page.Html));
            Assert.True(string.IsNullOrWhiteSpace(page.Text));
            Assert.Equal(HtmlCrawlContentMode.Raw, page.ContentModeUsed);
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.RawSelectorMiss, page.ContentSelectionReasonCode);
            Assert.Null(page.ContentElementSelectorHint);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_ContentMode_FocusedUsesSemanticFallback() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><article><h1>Hello</h1><p>World</p></article></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                ContentMode = HtmlCrawlContentMode.Focused
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HtmlCrawlContentMode.Focused, page.ContentModeUsed);
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.FocusedSemanticFallback, page.ContentSelectionReasonCode);
            Assert.Equal("article", page.ContentElementTag);
            Assert.Equal("article", page.ContentElementSelectorHint);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_ContentMode_ReaderSelectsArticleLikeBlock() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Docs</title></head><body><main><div class='sidebar'><a href='/a'>A</a><a href='/b'>B</a><a href='/c'>C</a><a href='/d'>D</a></div><article><h1>Hello</h1><p>This is the main body content with enough words to beat the sidebar links.</p><p>Second paragraph keeps the article score high.</p></article></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                ContentMode = HtmlCrawlContentMode.Reader
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("<article", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Second paragraph", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("A B C D", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HtmlCrawlContentMode.Reader, page.ContentModeUsed);
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.ReaderBestCandidate, page.ContentSelectionReasonCode);
            Assert.Equal("article", page.ContentElementTag);
            Assert.Contains("article", page.ContentElementSelectorHint, StringComparison.OrdinalIgnoreCase);
            Assert.True(page.ContentSelectionScore > 25);
            Assert.True(page.ReaderCandidateCount >= 3);
            Assert.Equal("body", page.ReaderRootElementSelectorHint);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_ContentMode_ReaderHonorsConfiguredThresholdsAndFallsBackToRoot() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Docs</title></head><body><main><article><h1>Hello</h1><p>This article has enough words to be considered by the reader heuristic, but we will raise the required score high enough to force a fallback to the root container.</p><p>Second paragraph keeps the article reasonably rich.</p></article></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                ContentMode = HtmlCrawlContentMode.Reader,
                ReaderMinimumScore = 500
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("<main", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.ReaderRootFallback, page.ContentSelectionReasonCode);
            Assert.Equal("main", page.ContentElementSelectorHint);
            Assert.Equal("main", page.ReaderRootElementSelectorHint);
            Assert.True(page.ReaderCandidateCount >= 2);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_CompareContentModes_PopulatesPageDiagnosticsAndManifestComparisons() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Docs</title></head><body><main><div class='sidebar'><a href='/a'>A</a><a href='/b'>B</a><a href='/c'>C</a><a href='/d'>D</a></div><article><h1>Hello</h1><p>This is the main body content with enough words to beat the sidebar links.</p><p>Second paragraph keeps the article score high.</p></article></main></body></html>"
        };

        string outputPath = Path.Combine(Path.GetTempPath(), "htmltinkerx-compare-" + Guid.NewGuid().ToString("N"));
        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                ContentMode = HtmlCrawlContentMode.Reader,
                CompareContentModes = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(3, page.ContentComparisons.Count);
            Assert.Equal(HtmlCrawlContentMode.Reader, page.BestContentComparisonMode);
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.ReaderBestCandidate, page.BestContentComparisonReasonCode);
            Assert.True(page.BestContentComparisonWordCount > 10);
            Assert.Equal(HtmlCrawlContentMode.Focused, page.RunnerUpContentComparisonMode);
            Assert.True(page.BestContentComparisonWordDelta >= 0);
            Assert.NotNull(page.ContentComparisonDeltaSummary);
            Assert.NotNull(page.ContentComparisonPreviewSummary);
            Assert.StartsWith("Reader 0", page.ContentComparisonDeltaSummary, StringComparison.Ordinal);
            Assert.Contains("Focused ", page.ContentComparisonDeltaSummary, StringComparison.Ordinal);
            Assert.Contains("Raw ", page.ContentComparisonDeltaSummary, StringComparison.Ordinal);
            Assert.StartsWith("Reader ", page.ContentComparisonPreviewSummary, StringComparison.Ordinal);
            Assert.Contains("@ article", page.ContentComparisonPreviewSummary, StringComparison.Ordinal);
            Assert.Contains("Hello", page.ContentComparisonPreviewSummary, StringComparison.Ordinal);
            Assert.Equal(1, result.Summary.ContentComparisonWinnerCounts["Reader"]);
            Assert.StartsWith("Reader ", result.Summary.ContentComparisonWinnerPreviewSamples["Reader"], StringComparison.Ordinal);
            Assert.Contains("@ article", result.Summary.ContentComparisonWinnerPreviewSamples["Reader"], StringComparison.Ordinal);
            Assert.True(result.Summary.AverageBestContentComparisonWordDelta >= 0);
            Assert.Contains(page.ContentComparisons, comparison => comparison.Mode == HtmlCrawlContentMode.Raw);
            Assert.Contains(page.ContentComparisons, comparison => comparison.Mode == HtmlCrawlContentMode.Focused);
            HtmlCrawlContentComparison reader = Assert.Single(page.ContentComparisons, comparison => comparison.Mode == HtmlCrawlContentMode.Reader);
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.ReaderBestCandidate, reader.ReasonCode);
            Assert.Equal("article", reader.ElementSelectorHint);
            Assert.True(reader.WordCount > 10);

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(page.ManifestPath!));
            JsonElement comparisons = manifest.RootElement.GetProperty("ContentComparisons");
            Assert.Equal(3, comparisons.GetArrayLength());
            Assert.Equal("Reader", manifest.RootElement.GetProperty("BestContentComparison").GetProperty("BestContentComparisonMode").GetString());
            Assert.Equal("Focused", manifest.RootElement.GetProperty("BestContentComparison").GetProperty("RunnerUpContentComparisonMode").GetString());
            Assert.True(manifest.RootElement.GetProperty("BestContentComparison").GetProperty("BestContentComparisonWordDelta").GetInt32() >= 0);
            string manifestDeltaSummary = manifest.RootElement.GetProperty("BestContentComparison").GetProperty("ContentComparisonDeltaSummary").GetString()!;
            Assert.StartsWith("Reader 0", manifestDeltaSummary, StringComparison.Ordinal);
            Assert.Contains("Focused ", manifestDeltaSummary, StringComparison.Ordinal);
            Assert.Contains("Raw ", manifestDeltaSummary, StringComparison.Ordinal);
            string manifestPreviewSummary = manifest.RootElement.GetProperty("ContentComparisonPreviewSummary").GetString()!;
            Assert.StartsWith("Reader ", manifestPreviewSummary, StringComparison.Ordinal);
            Assert.Contains("@ article", manifestPreviewSummary, StringComparison.Ordinal);
            Assert.Contains("Hello", manifestPreviewSummary, StringComparison.Ordinal);
            Assert.Contains(comparisons.EnumerateArray(), item =>
                string.Equals(item.GetProperty("Mode").GetString(), "Reader", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.GetProperty("ReasonCode").GetString(), HtmlCrawlContentSelectionReasonCode.ReaderBestCandidate.ToString(), StringComparison.OrdinalIgnoreCase));

            string indexHtml = File.ReadAllText(Path.Combine(outputPath, "index.html"));
            Assert.Contains("deltas: Reader 0", indexHtml, StringComparison.Ordinal);
            Assert.Contains("Focused ", indexHtml, StringComparison.Ordinal);
            Assert.Contains("Raw ", indexHtml, StringComparison.Ordinal);
            Assert.Contains("preview: Reader ", indexHtml, StringComparison.Ordinal);
            Assert.Contains("@ article", indexHtml, StringComparison.Ordinal);
            Assert.Contains("Best comparison sample <code>Reader</code>", indexHtml, StringComparison.Ordinal);
            string report = result.Summary.ToReportText(result.SitemapUrls);
            Assert.Contains("Best comparison sample Reader:", report, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Profile_AppliesSelectorExclusionsAndProfileMetadata() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><main><h1>Hello</h1><p>World</p><div class='sharing-popup'>Share</div><div class='wpml-ls-statics-footer'>Polish</div></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                ProfileName = "wordpress-content"
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal("wordpress-content", result.AppliedProfileName, ignoreCase: true);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.ExplicitProfileName, result.AppliedProfileReasonCode);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Share", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("wordpress-content", page.AppliedProfileName, ignoreCase: true);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.ExplicitProfileName, page.AppliedProfileReasonCode);
            Assert.Equal(HtmlCrawlContentMode.Focused, page.ContentModeUsed);
            Assert.Equal(HtmlCrawlRenderReasonCode.StaticRenderDisabled, page.RenderReasonCode);
            Assert.Empty(page.ContentComparisons);
            Assert.Null(page.BestContentComparisonMode);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_AutoProfile_DetectsWordPressMarkersAndAppliesGenericProfile() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Blog</title><meta name='generator' content='WordPress 6.8' /><link rel='stylesheet' href='/wp-content/themes/site/style.css' /></head><body><main><h1>Hello</h1><p>World</p><div class='sharing-popup'>Share</div><div class='wpml-ls-statics-footer'>Polish</div></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                AutoProfile = true
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal("wordpress-content", result.AppliedProfileName, ignoreCase: true);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileWordPressMarkers, result.AppliedProfileReasonCode);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Share", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileWordPressMarkers, page.AppliedProfileReasonCode);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_AutoProfile_DetectsDocumentationMarkersAndAppliesDocsProfile() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Docs</title></head><body><main><aside class='sidebar'><a href='/docs/start'>Start</a></aside><article><h1>Install</h1><nav aria-label='Table of contents'>On this page</nav><p>Documentation body with enough words to keep reader mode active and useful for extraction testing.</p></article></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                AutoProfile = true
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal("docs-content", result.AppliedProfileName, ignoreCase: true);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileDocumentationMarkers, result.AppliedProfileReasonCode);
            Assert.Equal(HtmlCrawlContentMode.Reader, page.ContentModeUsed);
            Assert.Equal(3, page.ContentComparisons.Count);
            Assert.DoesNotContain("On this page", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Documentation body", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileDocumentationMarkers, page.AppliedProfileReasonCode);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_Scenario_Docs_AppliesScenarioDefaultsAndMetadata() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Docs</title></head><body><main><aside class='sidebar'><a href='/docs/start'>Start</a></aside><article><h1>Install</h1><nav aria-label='Table of contents'>On this page</nav><p>Documentation body with enough words to keep reader mode active and useful for extraction testing.</p></article></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Scenario = HtmlCrawlScenario.Docs
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(HtmlCrawlScenario.Docs, result.AppliedScenario);
            Assert.Equal(HtmlCrawlScenario.Docs, page.AppliedScenario);
            Assert.Equal(HtmlCrawlContentMode.Reader, page.ContentModeUsed);
            Assert.Equal(3, page.ContentComparisons.Count);
            Assert.DoesNotContain("On this page", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Documentation body", page.Text, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_AutoProfile_DetectsApiDocumentationMarkersAndAppliesApiDocsProfile() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>API</title></head><body><main><div class='swagger-ui'><div class='topbar'>Swagger UI</div></div><article><h1>Users API</h1><p>API reference body with enough words to keep reader mode useful for extraction testing.</p><a href='/openapi.json'>OpenAPI</a><button>Try it out</button></article></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                AutoProfile = true
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal("api-docs-content", result.AppliedProfileName, ignoreCase: true);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileApiDocumentationMarkers, result.AppliedProfileReasonCode);
            Assert.Equal(HtmlCrawlContentMode.Reader, page.ContentModeUsed);
            Assert.Equal(3, page.ContentComparisons.Count);
            Assert.DoesNotContain("Swagger UI", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Users API", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileApiDocumentationMarkers, page.AppliedProfileReasonCode);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_CustomProfileFile_AppliesNamedAndAutoProfiles() {
        string profilePath = Path.GetTempFileName();
        File.WriteAllText(profilePath, """
        [
          {
            "name": "custom-docs",
            "hosts": [ "localhost" ],
            "selector": "article",
            "compareContentModes": true,
            "excludeSelectors": [ ".sidebar", ".feedback-box" ]
          }
        ]
        """);

        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Docs</title></head><body><article><h1>Hello</h1><p>World</p><div class='feedback-box'>Feedback</div></article><div class='sidebar'>Sidebar</div></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult named = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                ProfileName = "custom-docs",
                ProfilePath = profilePath
            });

            HtmlCrawlResult automatic = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                AutoProfile = true,
                ProfilePath = profilePath
            });

            Assert.Equal("custom-docs", named.AppliedProfileName, ignoreCase: true);
            Assert.Equal("custom-docs", automatic.AppliedProfileName, ignoreCase: true);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.ExplicitProfileName, named.AppliedProfileReasonCode);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileHostMatch, automatic.AppliedProfileReasonCode);
            HtmlCrawlPage namedPage = Assert.Single(named.Pages);
            HtmlCrawlPage automaticPage = Assert.Single(automatic.Pages);
            Assert.DoesNotContain("Sidebar", namedPage.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Feedback", automaticPage.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(3, namedPage.ContentComparisons.Count);
            Assert.Equal(3, automaticPage.ContentComparisons.Count);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.ExplicitProfileName, namedPage.AppliedProfileReasonCode);
            Assert.Equal(HtmlCrawlProfileSelectionReasonCode.AutoProfileHostMatch, automaticPage.AppliedProfileReasonCode);
        } finally {
            DisposeListenerSafely(server);
            File.Delete(profilePath);
        }
    }
}
