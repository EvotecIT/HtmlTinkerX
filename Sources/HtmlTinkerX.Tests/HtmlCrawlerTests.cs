using HtmlTinkerX;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlCrawlerTests {
    [Fact]
    public void HtmlCrawlOptions_CloneAndClearSensitiveData_WorkIndependently() {
        HtmlCrawlOptions options = new() {
            Username = "user",
            Password = "secret",
            ProxyUsername = "proxy-user",
            ProxyPassword = "proxy-secret",
            FormLogin = new HtmlFormLogin {
                LoginUrl = "https://example.com/login",
                UsernameSelector = "#user",
                PasswordSelector = "#password",
                SubmitSelector = "button[type=submit]"
            }
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
        Assert.NotSame(options.FormLogin, clone.FormLogin);
    }

    [Fact]
    public void HtmlCrawlProfiles_ResolveAndApplyBuiltInProfile() {
        Uri startUri = new("https://evotec.xyz/");
        HtmlCrawlProfile? profile = HtmlCrawlProfiles.Resolve(null, startUri, autoProfile: true);

        Assert.NotNull(profile);
        Assert.Equal("evotec-xyz", profile!.Name, ignoreCase: true);
        Assert.Contains("evotec-xyz", HtmlCrawlProfiles.Names);
        Assert.Contains("wordpress-content", HtmlCrawlProfiles.Names);

        HtmlCrawlOptions options = new();
        HtmlCrawlProfiles.Apply(options, profile);

        Assert.Equal("main", options.Selector);
        Assert.Equal("#main", options.WaitForSelector);
        Assert.True(options.CompareContentModes);
        Assert.Equal(20, options.ReaderMinimumWordCount);
        Assert.Equal(25, options.ReaderMinimumScore);
        Assert.Contains(".sharing-popup", options.ExcludeSelectors);
        Assert.Contains("Load more", options.ClickTexts);
        Assert.Equal(2, options.InteractionRepeatCount);
    }

    [Fact]
    public async Task HtmlCrawlProfiles_LoadFromPathAsync_LoadsCustomProfiles() {
        string path = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(path, """
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
        Assert.Contains("evotec-xyz", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wordpress-content", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetFreePort() {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpListener StartServer(Dictionary<string, string> responses, out string rootUrl) {
        int port = GetFreePort();
        rootUrl = $"http://localhost:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(rootUrl);
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Profile_AppliesSelectorExclusionsAndProfileMetadata() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><div id='main'><h1>Hello</h1><p>World</p><div class='sharing-popup'>Share</div><div class='wpml-ls-statics-footer'>Polish</div></div></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                ProfileName = "evotec-xyz"
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal("evotec-xyz", result.AppliedProfileName, ignoreCase: true);
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Share", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HtmlCrawlRenderReasonCode.StaticRenderDisabled, page.RenderReasonCode);
            Assert.Equal(3, page.ContentComparisons.Count);
            Assert.NotNull(page.BestContentComparisonMode);
        } finally {
            server.Stop();
            server.Close();
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
            Assert.Contains("Hello", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("World", page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Share", page.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Polish", page.Text, StringComparison.OrdinalIgnoreCase);
        } finally {
            server.Stop();
            server.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_CustomProfileFile_AppliesNamedAndAutoProfiles() {
        string profilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(profilePath, """
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
            HtmlCrawlPage namedPage = Assert.Single(named.Pages);
            HtmlCrawlPage automaticPage = Assert.Single(automatic.Pages);
            Assert.DoesNotContain("Sidebar", namedPage.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Feedback", automaticPage.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(3, namedPage.ContentComparisons.Count);
            Assert.Equal(3, automaticPage.ContentComparisons.Count);
        } finally {
            server.Stop();
            server.Close();
            File.Delete(profilePath);
        }
    }

    [Fact]
    public void ShouldRetryWithRendering_DetectsThinJavascriptShells() {
        HtmlCrawlPage page = new() {
            Status = HtmlCrawlPageStatus.Success,
            Html = "<html><body><div id='app'></div><script src='/runtime.js'></script><script src='/main.js'></script><script src='/vendor.js'></script><script src='/chunk-a.js'></script><script src='/chunk-b.js'></script><script src='/chunk-c.js'></script></body></html>",
            Text = string.Empty
        };

        HtmlCrawlOptions options = new() {
            AutoRenderTextWordThreshold = 20
        };

        HtmlCrawler.AutoRenderDecision decision = HtmlCrawler.EvaluateAutoRender(page, options);
        Assert.True(decision.ShouldRender);
        Assert.Contains("JavaScript shell", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HtmlCrawlRenderReasonCode.AutoRenderJavaScriptShell, decision.ReasonCode);
        Assert.True(HtmlCrawler.ShouldRetryWithRendering(page, options));
    }

    [Fact]
    public void ShouldRetryWithRendering_KeepsRichStaticPagesStatic() {
        HtmlCrawlPage page = new() {
            Status = HtmlCrawlPageStatus.Success,
            Html = "<html><body><main><h1>Hello</h1><p>This page already contains enough static content to avoid a browser fallback.</p></main></body></html>",
            Text = "Hello This page already contains enough static content to avoid a browser fallback."
        };

        HtmlCrawlOptions options = new() {
            AutoRenderTextWordThreshold = 8
        };

        HtmlCrawler.AutoRenderDecision decision = HtmlCrawler.EvaluateAutoRender(page, options);
        Assert.False(decision.ShouldRender);
        Assert.Contains("meeting", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HtmlCrawlRenderReasonCode.StaticThresholdMet, decision.ReasonCode);
        Assert.False(HtmlCrawler.ShouldRetryWithRendering(page, options));
    }

    [Fact]
    public void HtmlCrawlSummary_AggregatesRenderModesAndReasonCodes() {
        HtmlCrawlResult result = new() {
            StartUrl = "https://example.com/",
            Started = DateTimeOffset.UtcNow.AddSeconds(-2),
            Finished = DateTimeOffset.UtcNow,
            Pages = {
                new HtmlCrawlPage {
                    Url = "https://example.com/",
                    Status = HtmlCrawlPageStatus.Success,
                    Rendered = false,
                    RenderMode = HtmlCrawlRenderMode.Static,
                    RenderReasonCode = HtmlCrawlRenderReasonCode.StaticRenderDisabled,
                    RenderReason = "Kept static because browser rendering was not enabled.",
                    AppliedInteractions = { "Dismissed text: Accept" },
                    Text = "Hello world",
                    Started = DateTimeOffset.UtcNow.AddSeconds(-2),
                    Finished = DateTimeOffset.UtcNow.AddSeconds(-1)
                },
                new HtmlCrawlPage {
                    Url = "https://example.com/app",
                    Status = HtmlCrawlPageStatus.Success,
                    Rendered = true,
                    RenderMode = HtmlCrawlRenderMode.AutoRendered,
                    RenderReasonCode = HtmlCrawlRenderReasonCode.AutoRenderJavaScriptShell,
                    RenderReason = "Auto-render triggered because the static HTML looked like a JavaScript shell container.",
                    AppliedInteractions = { "Clicked text [1]: Load more", "Clicked text [2]: Load more" },
                    Text = "Rendered content",
                    Started = DateTimeOffset.UtcNow.AddSeconds(-1),
                    Finished = DateTimeOffset.UtcNow
                }
            }
        };

        HtmlCrawlSummary summary = result.Summary;

        Assert.Equal(1, summary.RenderModeCounts["Static"]);
        Assert.Equal(1, summary.RenderModeCounts["AutoRendered"]);
        Assert.Equal(1, summary.RenderReasonCounts["StaticRenderDisabled"]);
        Assert.Equal(1, summary.RenderReasonCounts["AutoRenderJavaScriptShell"]);
        Assert.Equal(1, summary.AutoRenderedPageCount);
        Assert.Equal(2, summary.InteractedPageCount);
        Assert.Equal(3, summary.InteractionCount);
        Assert.Equal(1, summary.InteractionCounts["Dismissed text: Accept"]);
        Assert.Equal(1, summary.InteractionCounts["Clicked text [1]: Load more"]);
        string report = summary.ToReportText(result.SitemapUrls);
        Assert.Contains("Render summary:", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Render mode AutoRendered: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Render reason AutoRenderJavaScriptShell: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Interaction summary:", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Applied interactions: 3", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrawlAsync_UsesSitemapsAndRespectsRobotsTxt() {
        Dictionary<string, string> responses = new();
        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            responses["/"] = "<html><head><title>Home</title></head><body>Home</body></html>";
            responses["/robots.txt"] = $"User-agent: *\nDisallow: /blocked\nSitemap: {rootUrl}sitemap.xml\n";
            responses["/sitemap.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" +
                $"<url><loc>{rootUrl}from-sitemap</loc></url>" +
                $"<url><loc>{rootUrl}blocked</loc></url>" +
                "</urlset>";
            responses["/from-sitemap"] = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>";
            responses["/blocked"] = "<html><head><title>Blocked</title></head><body>Blocked page</body></html>";

            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 10,
                UseSitemaps = true,
                RespectRobotsTxt = true
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "from-sitemap");
            Assert.Contains(result.SitemapUrls, url => url == rootUrl + "sitemap.xml");
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "blocked" && page.SkipReason == HtmlCrawlSkipReason.DisallowedByRobots);
        } finally {
            server.Stop();
            server.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_IgnoreRobotsTxt_AllowsDisallowedSitemapPage() {
        Dictionary<string, string> responses = new();
        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            responses["/"] = "<html><head><title>Home</title></head><body>Home</body></html>";
            responses["/robots.txt"] = $"User-agent: *\nDisallow: /blocked\nSitemap: {rootUrl}sitemap.xml\n";
            responses["/sitemap.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" +
                $"<url><loc>{rootUrl}blocked</loc></url>" +
                "</urlset>";
            responses["/blocked"] = "<html><head><title>Blocked</title></head><body>Blocked page</body></html>";

            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 10,
                UseSitemaps = true,
                RespectRobotsTxt = false
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "blocked");
            Assert.DoesNotContain(result.SkippedPages, page => page.Url == rootUrl + "blocked");
        } finally {
            server.Stop();
            server.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_CanPersistAndResumePendingPages() {
        Dictionary<string, string> responses = new();
        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerTests", Guid.NewGuid().ToString("N"));
        try {
            responses["/"] = "<html><head><title>Home</title></head><body>Home</body></html>";
            responses["/robots.txt"] = $"User-agent: *\nSitemap: {rootUrl}sitemap.xml\n";
            responses["/sitemap.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" +
                $"<url><loc>{rootUrl}from-sitemap</loc></url>" +
                "</urlset>";
            responses["/from-sitemap"] = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>";

            HtmlCrawlResult partial = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                UseSitemaps = true,
                OutputPath = outputPath
            });

            Assert.Single(partial.Pages);
            Assert.NotEmpty(partial.PendingPages);
            Assert.True(File.Exists(Path.Combine(outputPath, "crawl-result.json")));

            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 10,
                ResumePath = outputPath,
                OutputPath = outputPath
            });

            Assert.Equal(2, resumed.PageCount);
            Assert.Contains(resumed.Pages, page => page.Url == rootUrl + "from-sitemap");
            Assert.Empty(resumed.PendingPages);
            Assert.Contains(resumed.Pages, page => !string.IsNullOrEmpty(page.HtmlPath) && File.Exists(page.HtmlPath));

            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(outputPath);
            Assert.Equal(resumed.PageCount, loaded.PageCount);
            Assert.True(File.Exists(Path.Combine(outputPath, "pages.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "pages.csv")));
            Assert.True(File.Exists(Path.Combine(outputPath, "skipped-pages.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "links.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "summary.json")));
            Assert.True(File.Exists(Path.Combine(outputPath, "summary.txt")));

            string summaryText = File.ReadAllText(Path.Combine(outputPath, "summary.txt"));
            Assert.Contains("Sitemap sources:", summaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(rootUrl + "sitemap.xml", summaryText, StringComparison.OrdinalIgnoreCase);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_CanRestrictToPathPrefix() {
        Dictionary<string, string> responses = new() {
            ["/docs/index"] = "<html><body><a href='/docs/page-1'>Page 1</a><a href='/blog/post-1'>Blog</a></body></html>",
            ["/docs/page-1"] = "<html><body>Docs page</body></html>",
            ["/blog/post-1"] = "<html><body>Blog page</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string startUrl = rootUrl + "docs/index";
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(startUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                PathPrefix = "/docs"
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == startUrl);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "docs/page-1");
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "blog/post-1" && page.SkipReason == HtmlCrawlSkipReason.OutsidePathScope);
        } finally {
            server.Stop();
            server.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_CanPreferCanonicalUrls() {
        Dictionary<string, string> responses = new() {
            ["/landing"] = $"<html><head><title>Landing</title><link rel='canonical' href='{"/docs/home"}' /></head><body>Landing</body></html>",
            ["/docs/home"] = "<html><head><title>Home</title></head><body>Home</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "landing", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                UseCanonicalUrls = true
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(rootUrl + "docs/home", page.Url);
            Assert.Equal(rootUrl + "landing", page.RequestedUrl);
            Assert.Equal(rootUrl + "docs/home", page.CanonicalUrl);
        } finally {
            server.Stop();
            server.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_CanSkipDuplicateContentPages() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><a href='/copy-a'>Copy A</a><a href='/copy-b'>Copy B</a><a href='/unique'>Unique</a></body></html>",
            ["/copy-a"] = "<html><head><title>Copy A</title></head><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>",
            ["/copy-b"] = "<html><head><title>Copy B</title></head><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>",
            ["/unique"] = "<html><head><title>Unique</title></head><body><main><h1>Different</h1></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                Selector = "main",
                DeduplicatePages = true
            });

            Assert.Equal(3, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "copy-a");
            Assert.DoesNotContain(result.Pages, page => page.Url == rootUrl + "copy-b");
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "unique");
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "copy-b" && page.SkipReason == HtmlCrawlSkipReason.DuplicateContent);
            Assert.Equal(1, result.Summary.DuplicatePageCount);
        } finally {
            server.Stop();
            server.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_IgnoresTrackingQueryParametersDuringNormalization() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><a href='/page?utm_source=newsletter'>Tracked A</a><a href='/page?fbclid=12345'>Tracked B</a></body></html>",
            ["/page?utm_source=newsletter"] = "<html><head><title>Tracked</title></head><body>Tracked page</body></html>",
            ["/page?fbclid=12345"] = "<html><head><title>Tracked</title></head><body>Tracked page</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "page");
        } finally {
            server.Stop();
            server.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_SkipsKnownAssetUrlsByDefault() {
        int port = GetFreePort();
        string rootUrl = $"http://localhost:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(rootUrl);
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    if (key == "/") {
                        string body = "<html><body><a href='/file.pdf'>PDF</a></body></html>";
                        byte[] data = Encoding.UTF8.GetBytes(body);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.Length;
                        await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                        context.Response.OutputStream.Close();
                    } else {
                        context.Response.StatusCode = 404;
                        context.Response.OutputStream.Close();
                        continue;
                    }
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10
            });

            Assert.Single(result.Pages);
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "file.pdf" && page.SkipReason == HtmlCrawlSkipReason.AssetPath);
        } finally {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public async Task CrawlAsync_CanDownloadDiscoveredAssets() {
        int port = GetFreePort();
        string rootUrl = $"http://localhost:{port}/";
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        listener.Prefixes.Add(rootUrl);
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    string body;
                    string contentType;
                    switch (key) {
                        case "/":
                            body = "<html><head><link rel='stylesheet' href='/css/site.css' /><style>.hero{background-image:url('/images/bg.png');}</style></head><body><img src='/images/logo.png' alt='Logo' /><div style=\"background-image:url('/images/card.png')\"></div><a href='/files/manual.pdf'>Manual</a></body></html>";
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/css/site.css":
                            body = "@import '/css/theme.css'; .page{background-image:url('/images/logo.png'); color:#333;}";
                            contentType = "text/css";
                            break;
                        case "/css/theme.css":
                            body = ".theme{background-image:url('/images/bg.png');}";
                            contentType = "text/css";
                            break;
                        case "/images/logo.png":
                            body = "fake-png";
                            contentType = "image/png";
                            break;
                        case "/images/bg.png":
                            body = "fake-bg";
                            contentType = "image/png";
                            break;
                        case "/images/card.png":
                            body = "fake-card";
                            contentType = "image/png";
                            break;
                        case "/files/manual.pdf":
                            body = "%PDF-1.7 fake";
                            contentType = "application/pdf";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(5, page.AssetUrls.Count);
            Assert.Equal(6, result.AssetCount);
            Assert.All(result.Assets, asset => Assert.True(File.Exists(asset.FilePath)));
            Assert.All(result.Pages, crawledPage => {
                if (!string.IsNullOrWhiteSpace(crawledPage.HtmlPath)) {
                    Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(crawledPage.HtmlPath!), StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrWhiteSpace(crawledPage.TextPath)) {
                    Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(crawledPage.TextPath!), StringComparison.OrdinalIgnoreCase);
                }

                Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(crawledPage.ManifestPath!), StringComparison.OrdinalIgnoreCase);
            });
            Assert.All(result.Assets, asset => Assert.StartsWith(Path.GetFullPath(Path.Combine(outputPath, "assets")), Path.GetFullPath(asset.FilePath!), StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(outputPath, "assets.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "skipped-assets.jsonl")));
            Assert.True(File.Exists(result.IndexHtmlPath));
            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("site-", persistedHtml, StringComparison.OrdinalIgnoreCase);
            HtmlCrawlAsset stylesheet = Assert.Single(result.Assets, asset => asset.Url == rootUrl + "css/site.css");
            Assert.Contains(result.Assets, asset => asset.Url == rootUrl + "css/theme.css");
            string persistedCss = File.ReadAllText(stylesheet.FilePath!);
            Assert.DoesNotContain("/images/logo.png", persistedCss, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/css/theme.css", persistedCss, StringComparison.OrdinalIgnoreCase);
            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            Assert.Contains("Pages CSV", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("assets/site-", indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_RewritesInternalPageLinksToLocalFiles() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><a href='/about'>About</a><a href='https://example.com/remote'>Remote</a></body></html>",
            ["/about"] = "<html><body>About page</body></html>"
        };

        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerPageLinkTests", Guid.NewGuid().ToString("N"));
        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                OutputPath = outputPath
            });

            HtmlCrawlPage home = result.Pages.Single(page => page.Url == rootUrl);
            HtmlCrawlPage about = result.Pages.Single(page => page.Url == rootUrl + "about");
            string persistedHtml = File.ReadAllText(home.HtmlPath!);
            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            string expectedRelative = BuildRelativeForTest(home.HtmlPath!, about.HtmlPath!);

            Assert.Contains(expectedRelative, persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("https://example.com/remote", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.GetFileName(home.HtmlPath!), indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.GetFileName(about.HtmlPath!), indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_HonorsBaseHrefForLinksAssetsAndOfflineRewrite() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerBaseHrefTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        string rootUrl;
        {
            int port = GetFreePort();
            rootUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(rootUrl);
        }
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    string body;
                    string contentType;
                    switch (key) {
                        case "/":
                            body = "<html><head><title>Offline Home</title><base href='/docs/' /><link rel='stylesheet' href='css/site.css' /></head><body><h1>Offline Home</h1><p>Useful docs for offline testing and local search metadata.</p><a href='guide'>Guide</a><a href='manual.pdf'>Manual</a><a href='https://example.com/offsite'>Offsite</a><img src='images/logo.png' alt='Logo' /></body></html>";
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/docs/guide":
                            body = "<html><body><h1>Guide page</h1><p>Guide content for offline browsing.</p></body></html>";
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/docs/css/site.css":
                            body = ".hero{background-image:url('../images/logo.png');}";
                            contentType = "text/css";
                            break;
                        case "/docs/images/logo.png":
                            body = "fake-png";
                            contentType = "image/png";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage home = result.Pages.Single(page => page.Url == rootUrl);
            HtmlCrawlPage guide = result.Pages.Single(page => page.Url == rootUrl + "docs/guide");
            string chunksJson = File.ReadAllText(result.ChunksJsonlPath!);
            string graphJson = File.ReadAllText(result.GraphJsonPath!);
            Assert.Contains(home.Links, link => string.Equals(link, rootUrl + "docs/guide", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.Links, link => string.Equals(link, rootUrl + "docs/manual.pdf", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.Links, link => string.Equals(link, "https://example.com/offsite", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.AssetUrls, asset => string.Equals(asset, rootUrl + "docs/css/site.css", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.AssetUrls, asset => string.Equals(asset, rootUrl + "docs/images/logo.png", StringComparison.OrdinalIgnoreCase));

            string persistedHtml = File.ReadAllText(home.HtmlPath!);
            string manifestJson = File.ReadAllText(home.ManifestPath!);
            using JsonDocument manifest = JsonDocument.Parse(manifestJson);
            using JsonDocument graph = JsonDocument.Parse(graphJson);
            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            Assert.DoesNotContain("<base", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(BuildRelativeForTest(home.HtmlPath!, guide.HtmlPath!), persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.ChunksJsonlPath));
            Assert.True(File.Exists(result.GraphJsonPath));
            Assert.True(result.Summary.ChunkCount >= 1);
            Assert.Equal(1, result.SkippedContentPageCount);
            Assert.Equal(1, result.SkippedAssetCount);
            Assert.Equal(1, result.Summary.SkippedContentPageCount);
            Assert.Equal(1, result.Summary.SkippedAssetCount);
            Assert.True(File.Exists(result.SkippedAssetsJsonlPath));
            Assert.Equal(4, result.GraphNodeCount);
            Assert.Equal(3, result.GraphEdgeCount);
            Assert.Equal(2, result.GraphFetchedNodeCount);
            Assert.Equal(1, result.GraphSkippedNodeCount);
            Assert.Equal(1, result.GraphExternalNodeCount);
            Assert.Equal(4, result.Summary.GraphNodeCount);
            Assert.Equal(3, result.Summary.GraphEdgeCount);
            Assert.Equal(2, result.Summary.GraphFetchedNodeCount);
            Assert.Equal(1, result.Summary.GraphSkippedNodeCount);
            Assert.Equal(1, result.Summary.GraphExternalNodeCount);
            Assert.Equal(2, result.GraphNodeCategories["Fetched"]);
            Assert.Equal(1, result.GraphNodeCategories["Skipped"]);
            Assert.Equal(1, result.GraphNodeCategories["External"]);
            Assert.Equal(1, result.GraphEdgeRelations["fetched"]);
            Assert.Equal(1, result.GraphEdgeRelations["skipped"]);
            Assert.Equal(1, result.GraphEdgeRelations["external"]);
            Assert.Equal(1, result.GraphSkippedNodeReasons[HtmlCrawlSkipReason.AssetPath.ToString()]);
            Assert.Equal(2, result.Summary.GraphNodeCategories["Fetched"]);
            Assert.Equal(1, result.Summary.GraphEdgeRelations["external"]);
            Assert.Equal(1, result.Summary.GraphSkippedNodeReasons[HtmlCrawlSkipReason.AssetPath.ToString()]);
            Assert.Equal(Path.GetFileName(home.HtmlPath), manifest.RootElement.GetProperty("PageFiles").GetProperty("HtmlPath").GetString());
            Assert.Equal(HtmlCrawlContentMode.Focused.ToString(), manifest.RootElement.GetProperty("Extraction").GetProperty("ContentModeUsed").GetString());
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.FocusedFullDocumentFallback.ToString(), manifest.RootElement.GetProperty("Extraction").GetProperty("ContentSelectionReasonCode").GetString());
            Assert.True(manifest.RootElement.GetProperty("Extraction").GetProperty("ContentElementSelectorHint").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
            Assert.Equal("Offline Home", manifest.RootElement.GetProperty("Search").GetProperty("Headings")[0].GetString());
            Assert.True(manifest.RootElement.GetProperty("Search").GetProperty("WordCount").GetInt32() >= 8);
            Assert.True(manifest.RootElement.GetProperty("Search").GetProperty("ChunkCount").GetInt32() >= 1);
            Assert.Contains("Useful docs for offline testing", manifest.RootElement.GetProperty("Search").GetProperty("Summary").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(manifest.RootElement.GetProperty("Search").GetProperty("Keywords").EnumerateArray(), item =>
                string.Equals(item.GetString(), "offline", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(manifest.RootElement.GetProperty("Search").GetProperty("Keywords").EnumerateArray(), item =>
                string.Equals(item.GetString(), "testing", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(manifest.RootElement.GetProperty("Links").EnumerateArray(), item =>
                string.Equals(item.GetProperty("Url").GetString(), rootUrl + "docs/guide", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.GetProperty("LocalPagePath").GetString(), Path.GetFileName(guide.HtmlPath), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(manifest.RootElement.GetProperty("ReferencedAssets").EnumerateArray(), item =>
                string.Equals(item.GetProperty("Url").GetString(), rootUrl + "docs/images/logo.png", StringComparison.OrdinalIgnoreCase)
                && item.GetProperty("LocalFilePath").GetString()!.StartsWith("../assets/", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("\"ChunkId\"", chunksJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"ManifestPath\":\"pages/", chunksJson.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Keywords\":[\"offline\"", chunksJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(4, graph.RootElement.GetProperty("Nodes").GetArrayLength());
            Assert.Equal(3, graph.RootElement.GetProperty("Edges").GetArrayLength());
            Assert.Contains(graph.RootElement.GetProperty("Nodes").EnumerateArray(), node =>
                string.Equals(node.GetProperty("Url").GetString(), rootUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("Category").GetString(), "Fetched", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Nodes").EnumerateArray(), node =>
                string.Equals(node.GetProperty("Url").GetString(), rootUrl + "docs/manual.pdf", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("Category").GetString(), "Skipped", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("SkipReason").GetString(), HtmlCrawlSkipReason.AssetPath.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Nodes").EnumerateArray(), node =>
                string.Equals(node.GetProperty("Url").GetString(), "https://example.com/offsite", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("Category").GetString(), "External", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Edges").EnumerateArray(), edge =>
                string.Equals(edge.GetProperty("SourceUrl").GetString(), rootUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("TargetUrl").GetString(), rootUrl + "docs/guide", StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("Relation").GetString(), "fetched", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Edges").EnumerateArray(), edge =>
                string.Equals(edge.GetProperty("TargetUrl").GetString(), rootUrl + "docs/manual.pdf", StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("Relation").GetString(), "skipped", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Edges").EnumerateArray(), edge =>
                string.Equals(edge.GetProperty("TargetUrl").GetString(), "https://example.com/offsite", StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("Relation").GetString(), "external", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Offline Home", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Useful docs for offline testing", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("keywords: offline", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Render Summary", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Content Summary", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FocusedFullDocumentFallback", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Render mode", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("StaticRenderDisabled", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Chunks JSONL", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Graph JSON", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Graph Summary", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Node category", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Edge relation", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Skipped-node reason", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Skipped Pages", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Skipped Assets", indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    private static string BuildRelativeForTest(string fromFilePath, string toFilePath) {
        string fromDirectory = Path.GetDirectoryName(fromFilePath)!;
        Uri fromUri = new((fromDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? fromDirectory : fromDirectory + Path.DirectorySeparatorChar));
        Uri toUri = new(toFilePath);
        return Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString()).Replace('\\', '/');
    }
}
