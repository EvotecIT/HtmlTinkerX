using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Configures an offline crawl run.
/// </summary>
public sealed class HtmlCrawlOptions {
    /// <summary>Maximum link depth to traverse from the starting page.</summary>
    public int MaxDepth { get; set; } = 1;

    /// <summary>Maximum number of pages to fetch during the crawl.</summary>
    public int MaxPages { get; set; } = 25;

    /// <summary>When true, pages are rendered through Playwright before extraction.</summary>
    public bool Render { get; set; }

    /// <summary>When true, pages are first fetched statically and retried through Playwright when they look like thin JavaScript shells.</summary>
    public bool AutoRender { get; set; }

    /// <summary>Restricts crawling to the starting host.</summary>
    public bool RestrictToHost { get; set; } = true;

    /// <summary>Allows subdomains of the starting host while still blocking unrelated hosts.</summary>
    public bool IncludeSubdomains { get; set; }

    /// <summary>Optional absolute path prefix that queued URLs must stay under.</summary>
    public string? PathPrefix { get; set; }

    /// <summary>When true, stores and prefers canonical URLs discovered from pages.</summary>
    public bool UseCanonicalUrls { get; set; }

    /// <summary>When true, skips fetched pages whose selected content duplicates an earlier page.</summary>
    public bool DeduplicatePages { get; set; }

    /// <summary>When true, removes common tracking query parameters during URL normalization.</summary>
    public bool IgnoreTrackingQueryParameters { get; set; } = true;

    /// <summary>When true, only configured page-like content types are crawled.</summary>
    public bool RestrictToAllowedContentTypes { get; set; } = true;

    /// <summary>When true, obvious asset/document URLs are skipped before fetching.</summary>
    public bool SkipKnownAssetUrls { get; set; } = true;

    /// <summary>When true, downloads asset URLs discovered from fetched pages into the crawl dataset.</summary>
    public bool DownloadAssets { get; set; }

    /// <summary>When true, rewrites downloaded asset references in stored HTML to local relative paths.</summary>
    public bool RewriteAssetReferencesToLocal { get; set; } = true;

    /// <summary>When true, rewrites internal page links in stored HTML to local relative page paths.</summary>
    public bool RewritePageLinksToLocal { get; set; } = true;

    /// <summary>Discovers additional crawl candidates from sitemap files.</summary>
    public bool UseSitemaps { get; set; } = true;

    /// <summary>Honors robots.txt rules before fetching pages.</summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>User-agent token used when evaluating robots.txt groups.</summary>
    public string RobotsUserAgent { get; set; } = "*";

    /// <summary>Optional directory or manifest path used to persist crawl progress.</summary>
    public string? OutputPath { get; set; }

    /// <summary>Optional directory or manifest path used to resume a previous crawl.</summary>
    public string? ResumePath { get; set; }

    /// <summary>Optional built-in profile name used to preconfigure crawl behavior.</summary>
    public string? ProfileName { get; set; }

    /// <summary>Optional JSON file containing custom crawl profiles.</summary>
    public string? ProfilePath { get; set; }

    /// <summary>When true, applies a built-in profile automatically when the start host matches one.</summary>
    public bool AutoProfile { get; set; }

    /// <summary>Optional intent-focused scenario that applies product-style defaults before profiles and explicit options refine them.</summary>
    public HtmlCrawlScenario Scenario { get; set; } = HtmlCrawlScenario.Custom;

    /// <summary>Stores the fetched HTML in the result.</summary>
    public bool IncludeHtml { get; set; } = true;

    /// <summary>Stores plain text extracted from the page in the result.</summary>
    public bool IncludeText { get; set; } = true;

    /// <summary>Optional CSS selector used to extract a focused section from the page.</summary>
    public string? Selector { get; set; }

    /// <summary>Controls how the crawler chooses the HTML region used for stored content and text extraction.</summary>
    public HtmlCrawlContentMode ContentMode { get; set; } = HtmlCrawlContentMode.Focused;

    /// <summary>When true, the crawler also evaluates raw, focused, and reader extraction alternatives for diagnostics and persisted manifests.</summary>
    public bool CompareContentModes { get; set; }

    /// <summary>Minimum word count a reader-mode candidate must have before it is considered article-like content.</summary>
    public int ReaderMinimumWordCount { get; set; } = 20;

    /// <summary>Minimum score a reader-mode candidate must reach before it is preferred over the reader root element.</summary>
    public double ReaderMinimumScore { get; set; } = 25;

    /// <summary>Optional CSS selectors removed from extracted HTML and text before storage.</summary>
    public IList<string> ExcludeSelectors { get; set; } = new List<string>();

    /// <summary>Optional class names removed from extracted HTML and text before storage.</summary>
    public IList<string> ExcludeClasses { get; set; } = new List<string>();

    /// <summary>Optional element IDs removed from extracted HTML and text before storage.</summary>
    public IList<string> ExcludeIds { get; set; } = new List<string>();

    /// <summary>When true, applies conservative cleanup heuristics to remove low-value boilerplate inside the selected content area.</summary>
    public bool SmartContentCleanup { get; set; } = true;

    /// <summary>Optional selectors to click once on rendered pages before extraction.</summary>
    public IList<string> ClickSelectors { get; set; } = new List<string>();

    /// <summary>Optional visible texts to click once on rendered pages before extraction.</summary>
    public IList<string> ClickTexts { get; set; } = new List<string>();

    /// <summary>Optional selectors to dismiss on rendered pages before extraction.</summary>
    public IList<string> DismissSelectors { get; set; } = new List<string>();

    /// <summary>Optional visible texts to dismiss on rendered pages before extraction.</summary>
    public IList<string> DismissTexts { get; set; } = new List<string>();

    /// <summary>Optional selector that must appear before rendered extraction continues.</summary>
    public string? WaitForSelector { get; set; }

    /// <summary>Additional delay after page load in milliseconds.</summary>
    public int WaitAfterLoadMs { get; set; }

    /// <summary>When true, rendered pages are scrolled before extraction to trigger lazy-loaded content.</summary>
    public bool AutoScroll { get; set; }

    /// <summary>Number of incremental scroll steps to perform when <see cref="AutoScroll"/> is enabled.</summary>
    public int AutoScrollSteps { get; set; } = 3;

    /// <summary>Delay after each auto-scroll step in milliseconds.</summary>
    public int AutoScrollDelayMs { get; set; } = 400;

    /// <summary>Delay after each rendered interaction in milliseconds.</summary>
    public int InteractionDelayMs { get; set; } = 300;

    /// <summary>Number of times click interactions should be retried on rendered pages.</summary>
    public int InteractionRepeatCount { get; set; } = 1;

    /// <summary>Minimum extracted word count before static pages are considered rich enough to skip auto-render fallback.</summary>
    public int AutoRenderTextWordThreshold { get; set; } = 40;

    /// <summary>Delay between crawled pages in milliseconds.</summary>
    public int DelayMs { get; set; }

    /// <summary>Per-page timeout in milliseconds.</summary>
    public int Timeout { get; set; } = 10000;

    /// <summary>Optional user agent used for HTTP and browser requests.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Optional HTTP headers applied to requests.</summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Proxy address used for HTTP and browser requests.</summary>
    public string? Proxy { get; set; }

    /// <summary>Optional proxy username.</summary>
    public string? ProxyUsername { get; set; }

    /// <summary>Optional proxy password.</summary>
    public string? ProxyPassword { get; set; }

    /// <summary>Optional username for basic authentication or form login.</summary>
    public string? Username { get; set; }

    /// <summary>Optional password for basic authentication or form login.</summary>
    public string? Password { get; set; }

    /// <summary>Optional form-login details for rendered crawls.</summary>
    public HtmlFormLogin? FormLogin { get; set; }

    /// <summary>Optional Playwright storage state path to reuse authenticated sessions.</summary>
    public string? StorageStatePath { get; set; }

    /// <summary>Browser engine used when <see cref="Render"/> is enabled.</summary>
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Run the browser headless when rendering pages.</summary>
    public bool Headless { get; set; } = true;

    /// <summary>Force Playwright runtime cleanup and reinstall before a rendered crawl.</summary>
    public bool CleanBrowserInstall { get; set; }

    /// <summary>Optional include patterns using * wildcards.</summary>
    public IList<string> IncludePatterns { get; set; } = new List<string>();

    /// <summary>Optional exclude patterns using * wildcards.</summary>
    public IList<string> ExcludePatterns { get; set; } = new List<string>();

    /// <summary>Optional explicit sitemap URLs to load before crawling.</summary>
    public IList<string> SitemapUrls { get; set; } = new List<string>();

    /// <summary>Allowed response content-type patterns when content-type restriction is enabled.</summary>
    public IList<string> AllowedContentTypePatterns { get; set; } = new List<string> {
        "text/html*",
        "application/xhtml+xml*"
    };

    /// <summary>Path patterns for asset/document URLs that should be skipped before fetching.</summary>
    public IList<string> IgnoredAssetPathPatterns { get; set; } = new List<string> {
        "*.pdf",
        "*.doc",
        "*.docx",
        "*.xls",
        "*.xlsx",
        "*.ppt",
        "*.pptx",
        "*.jpg",
        "*.jpeg",
        "*.png",
        "*.gif",
        "*.webp",
        "*.svg",
        "*.ico",
        "*.zip",
        "*.tar",
        "*.tar.gz",
        "*.tgz",
        "*.gz",
        "*.7z",
        "*.rar",
        "*.mp3",
        "*.wav",
        "*.ogg",
        "*.mp4",
        "*.avi",
        "*.mov",
        "*.webm",
        "*.woff",
        "*.woff2",
        "*.ttf",
        "*.eot"
    };

    /// <summary>Optional asset URL include patterns using * wildcards.</summary>
    public IList<string> AssetIncludePatterns { get; set; } = new List<string>();

    /// <summary>Optional asset URL exclude patterns using * wildcards.</summary>
    public IList<string> AssetExcludePatterns { get; set; } = new List<string>();

    /// <summary>Query-parameter patterns to remove during URL normalization when tracking cleanup is enabled.</summary>
    public IList<string> IgnoredQueryParameterPatterns { get; set; } = new List<string> {
        "utm_*",
        "fbclid",
        "gclid",
        "msclkid",
        "mc_*",
        "_hs*",
        "vero_*",
        "yclid"
    };

    /// <summary>Optional Playwright route patterns to block when rendering.</summary>
    public IList<string> BlockResourcePatterns { get; set; } = new List<string>();

    /// <summary>
    /// Creates a deep copy of the crawl options so callers can safely reuse their original configuration.
    /// </summary>
    /// <returns>A copy of the current crawl options.</returns>
    public HtmlCrawlOptions Clone() {
        return new HtmlCrawlOptions {
            MaxDepth = MaxDepth,
            MaxPages = MaxPages,
            Render = Render,
            AutoRender = AutoRender,
            RestrictToHost = RestrictToHost,
            IncludeSubdomains = IncludeSubdomains,
            PathPrefix = PathPrefix,
            UseCanonicalUrls = UseCanonicalUrls,
            DeduplicatePages = DeduplicatePages,
            IgnoreTrackingQueryParameters = IgnoreTrackingQueryParameters,
            RestrictToAllowedContentTypes = RestrictToAllowedContentTypes,
            SkipKnownAssetUrls = SkipKnownAssetUrls,
            DownloadAssets = DownloadAssets,
            RewriteAssetReferencesToLocal = RewriteAssetReferencesToLocal,
            RewritePageLinksToLocal = RewritePageLinksToLocal,
            UseSitemaps = UseSitemaps,
            RespectRobotsTxt = RespectRobotsTxt,
            RobotsUserAgent = RobotsUserAgent,
            OutputPath = OutputPath,
            ResumePath = ResumePath,
            ProfileName = ProfileName,
            ProfilePath = ProfilePath,
            AutoProfile = AutoProfile,
            Scenario = Scenario,
            IncludeHtml = IncludeHtml,
            IncludeText = IncludeText,
            Selector = Selector,
            ContentMode = ContentMode,
            CompareContentModes = CompareContentModes,
            ReaderMinimumWordCount = ReaderMinimumWordCount,
            ReaderMinimumScore = ReaderMinimumScore,
            ExcludeSelectors = new List<string>(ExcludeSelectors),
            ExcludeClasses = new List<string>(ExcludeClasses),
            ExcludeIds = new List<string>(ExcludeIds),
            SmartContentCleanup = SmartContentCleanup,
            ClickSelectors = new List<string>(ClickSelectors),
            ClickTexts = new List<string>(ClickTexts),
            DismissSelectors = new List<string>(DismissSelectors),
            DismissTexts = new List<string>(DismissTexts),
            WaitForSelector = WaitForSelector,
            WaitAfterLoadMs = WaitAfterLoadMs,
            AutoScroll = AutoScroll,
            AutoScrollSteps = AutoScrollSteps,
            AutoScrollDelayMs = AutoScrollDelayMs,
            InteractionDelayMs = InteractionDelayMs,
            InteractionRepeatCount = InteractionRepeatCount,
            AutoRenderTextWordThreshold = AutoRenderTextWordThreshold,
            DelayMs = DelayMs,
            Timeout = Timeout,
            UserAgent = UserAgent,
            Headers = new Dictionary<string, string>(Headers, System.StringComparer.OrdinalIgnoreCase),
            Proxy = Proxy,
            ProxyUsername = ProxyUsername,
            ProxyPassword = ProxyPassword,
            Username = Username,
            Password = Password,
            FormLogin = FormLogin == null ? null : new HtmlFormLogin {
                LoginUrl = FormLogin.LoginUrl,
                UsernameSelector = FormLogin.UsernameSelector,
                PasswordSelector = FormLogin.PasswordSelector,
                SubmitSelector = FormLogin.SubmitSelector
            },
            StorageStatePath = StorageStatePath,
            Browser = Browser,
            Headless = Headless,
            CleanBrowserInstall = CleanBrowserInstall,
            IncludePatterns = new List<string>(IncludePatterns),
            ExcludePatterns = new List<string>(ExcludePatterns),
            SitemapUrls = new List<string>(SitemapUrls),
            AllowedContentTypePatterns = new List<string>(AllowedContentTypePatterns),
            IgnoredAssetPathPatterns = new List<string>(IgnoredAssetPathPatterns),
            AssetIncludePatterns = new List<string>(AssetIncludePatterns),
            AssetExcludePatterns = new List<string>(AssetExcludePatterns),
            IgnoredQueryParameterPatterns = new List<string>(IgnoredQueryParameterPatterns),
            BlockResourcePatterns = new List<string>(BlockResourcePatterns)
        };
    }

    /// <summary>
    /// Clears in-memory passwords after a crawl has finished using them.
    /// </summary>
    public void ClearSensitiveData() {
        ProxyPassword = null;
        Password = null;
    }
}
