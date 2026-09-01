using System.Collections.Generic;
using System.Text.Json.Serialization;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;

namespace HtmlTinkerX;

/// <summary>
/// Configures an offline crawl run.
/// </summary>
public sealed class HtmlCrawlOptions {
    private readonly HashSet<string> _explicitScenarioOptions = new(System.StringComparer.Ordinal);
    private bool _applyingScenarioDefaults;
    private bool _useCanonicalUrls;
    private bool _deduplicatePages;
    private bool _downloadAssets;
    private bool _includeMarkdown;
    private bool _includeStructuredJson;
    private HtmlCrawlStructuredJsonPreset _structuredJsonPreset;
    private HtmlCrawlContentMode _contentMode = HtmlCrawlContentMode.Focused;
    private bool _compareContentModes;
    private int _readerMinimumWordCount = 20;
    private double _readerMinimumScore = 25;
    private bool _smartContentCleanup = true;

    /// <summary>Default maximum size in bytes for each downloaded crawl asset.</summary>
    public const int DefaultMaximumAssetResponseBytes = 64 * 1024 * 1024;

    /// <summary>Maximum link depth to traverse from the starting page.</summary>
    public int MaxDepth { get; set; } = 1;

    /// <summary>Maximum number of pages to fetch during the crawl.</summary>
    public int MaxPages { get; set; } = 25;

    /// <summary>Maximum static page or robots.txt response size in bytes.</summary>
    public int MaximumPageResponseBytes { get; set; } = HtmlHttpFetchOptions.DefaultMaximumResponseBytes;

    /// <summary>Maximum size in bytes for each downloaded crawl asset.</summary>
    public int MaximumAssetResponseBytes { get; set; } = DefaultMaximumAssetResponseBytes;

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
    public bool UseCanonicalUrls {
        get => _useCanonicalUrls;
        set => SetScenarioOption(ref _useCanonicalUrls, value, nameof(UseCanonicalUrls));
    }

    /// <summary>When true, skips fetched pages whose selected content duplicates an earlier page.</summary>
    public bool DeduplicatePages {
        get => _deduplicatePages;
        set => SetScenarioOption(ref _deduplicatePages, value, nameof(DeduplicatePages));
    }

    /// <summary>When true, removes common tracking query parameters during URL normalization.</summary>
    public bool IgnoreTrackingQueryParameters { get; set; } = true;

    /// <summary>When true, only configured page-like content types are crawled.</summary>
    public bool RestrictToAllowedContentTypes { get; set; } = true;

    /// <summary>When true, obvious asset/document URLs are skipped before fetching.</summary>
    public bool SkipKnownAssetUrls { get; set; } = true;

    /// <summary>When true, downloads asset URLs discovered from fetched pages into the crawl dataset.</summary>
    public bool DownloadAssets {
        get => _downloadAssets;
        set => SetScenarioOption(ref _downloadAssets, value, nameof(DownloadAssets));
    }

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

    /// <summary>Stores Markdown converted from the selected page content in the result.</summary>
    public bool IncludeMarkdown {
        get => _includeMarkdown;
        set => SetScenarioOption(ref _includeMarkdown, value, nameof(IncludeMarkdown));
    }

    /// <summary>
    /// Controls which markdown dialect profile is used when selected HTML is converted to markdown.
    /// </summary>
    public HtmlMarkdownProfile MarkdownProfile { get; set; } = HtmlMarkdownProfile.Portable;

    /// <summary>
    /// Controls how images are emitted when selected HTML is converted to markdown.
    /// </summary>
    public MarkdownImageRenderingMode MarkdownImageMode { get; set; } = MarkdownImageRenderingMode.PortableMarkdown;

    /// <summary>
    /// Controls whether low-value metadata inside repeated listing cards should be preserved or suppressed in markdown output.
    /// </summary>
    public HtmlListingCardMetadataMode ListingCardMetadataMode { get; set; } = HtmlListingCardMetadataMode.SuppressInRepeatedCards;

    /// <summary>Stores structured JSON-friendly page data extracted from the crawl.</summary>
    public bool IncludeStructuredJson {
        get => _includeStructuredJson;
        set => SetScenarioOption(ref _includeStructuredJson, value, nameof(IncludeStructuredJson));
    }

    /// <summary>Optional built-in structured JSON preset that adds flattened extracted fields for common page types.</summary>
    public HtmlCrawlStructuredJsonPreset StructuredJsonPreset {
        get => _structuredJsonPreset;
        set => SetScenarioOption(ref _structuredJsonPreset, value, nameof(StructuredJsonPreset));
    }

    /// <summary>Optional inline JSON schema describing caller-defined extracted fields for structured crawl output.</summary>
    public string? StructuredJsonSchema { get; set; }

    /// <summary>Optional JSON file path containing a structured crawl extraction schema.</summary>
    public string? StructuredJsonSchemaPath { get; set; }

    /// <summary>Optional CSS selector used to extract a focused section from the page.</summary>
    public string? Selector { get; set; }

    /// <summary>Controls how the crawler chooses the HTML region used for stored content and text extraction.</summary>
    public HtmlCrawlContentMode ContentMode {
        get => _contentMode;
        set => SetScenarioOption(ref _contentMode, value, nameof(ContentMode));
    }

    /// <summary>When true, the crawler also evaluates raw, focused, and reader extraction alternatives for diagnostics and persisted manifests.</summary>
    public bool CompareContentModes {
        get => _compareContentModes;
        set => SetScenarioOption(ref _compareContentModes, value, nameof(CompareContentModes));
    }

    /// <summary>Minimum word count a reader-mode candidate must have before it is considered article-like content.</summary>
    public int ReaderMinimumWordCount {
        get => _readerMinimumWordCount;
        set => SetScenarioOption(ref _readerMinimumWordCount, value, nameof(ReaderMinimumWordCount));
    }

    /// <summary>Minimum score a reader-mode candidate must reach before it is preferred over the reader root element.</summary>
    public double ReaderMinimumScore {
        get => _readerMinimumScore;
        set => SetScenarioOption(ref _readerMinimumScore, value, nameof(ReaderMinimumScore));
    }

    /// <summary>Optional CSS selectors removed from extracted HTML and text before storage.</summary>
    public IList<string> ExcludeSelectors { get; set; } = new List<string>();

    /// <summary>Optional class names removed from extracted HTML and text before storage.</summary>
    public IList<string> ExcludeClasses { get; set; } = new List<string>();

    /// <summary>Optional element IDs removed from extracted HTML and text before storage.</summary>
    public IList<string> ExcludeIds { get; set; } = new List<string>();

    /// <summary>When true, applies conservative cleanup heuristics to remove low-value boilerplate inside the selected content area.</summary>
    public bool SmartContentCleanup {
        get => _smartContentCleanup;
        set => SetScenarioOption(ref _smartContentCleanup, value, nameof(SmartContentCleanup));
    }

    /// <summary>
    /// Controls whether extraction should respect or include obviously hidden DOM content.
    /// </summary>
    public HtmlCrawlHiddenContentMode HiddenContentMode { get; set; } = HtmlCrawlHiddenContentMode.RespectHidden;

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

    /// <summary>
    /// Optional observer invoked for each successfully prepared rendered page while the browser session is still positioned on it.
    /// </summary>
    /// <remarks>
    /// This runtime hook is intentionally excluded from JSON serialization. It lets adapters export browser-backed evidence
    /// without repeating navigation or moving product-specific persistence concerns into HtmlTinkerX.
    /// </remarks>
    [JsonIgnore]
    public IHtmlCrawlRenderedPageObserver? RenderedPageObserver { get; set; }

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
        HtmlCrawlOptions clone = new() {
            MaxDepth = MaxDepth,
            MaxPages = MaxPages,
            MaximumPageResponseBytes = MaximumPageResponseBytes,
            MaximumAssetResponseBytes = MaximumAssetResponseBytes,
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
            IncludeMarkdown = IncludeMarkdown,
            MarkdownProfile = MarkdownProfile,
            MarkdownImageMode = MarkdownImageMode,
            ListingCardMetadataMode = ListingCardMetadataMode,
            IncludeStructuredJson = IncludeStructuredJson,
            StructuredJsonPreset = StructuredJsonPreset,
            StructuredJsonSchema = StructuredJsonSchema,
            StructuredJsonSchemaPath = StructuredJsonSchemaPath,
            Selector = Selector,
            ContentMode = ContentMode,
            CompareContentModes = CompareContentModes,
            ReaderMinimumWordCount = ReaderMinimumWordCount,
            ReaderMinimumScore = ReaderMinimumScore,
            ExcludeSelectors = new List<string>(ExcludeSelectors),
            ExcludeClasses = new List<string>(ExcludeClasses),
            ExcludeIds = new List<string>(ExcludeIds),
            SmartContentCleanup = SmartContentCleanup,
            HiddenContentMode = HiddenContentMode,
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
            RenderedPageObserver = RenderedPageObserver,
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
        clone._explicitScenarioOptions.Clear();
        clone._explicitScenarioOptions.UnionWith(_explicitScenarioOptions);
        return clone;
    }

    internal bool IsScenarioOptionExplicit(string optionName) => _explicitScenarioOptions.Contains(optionName);

    internal void ApplyScenarioDefaults(System.Action apply) {
        bool wasApplyingDefaults = _applyingScenarioDefaults;
        _applyingScenarioDefaults = true;
        try {
            apply();
        } finally {
            _applyingScenarioDefaults = wasApplyingDefaults;
        }
    }

    private void SetScenarioOption<T>(ref T field, T value, string optionName) {
        field = value;
        if (!_applyingScenarioDefaults) {
            _explicitScenarioOptions.Add(optionName);
        }
    }

    /// <summary>
    /// Clears in-memory passwords after a crawl has finished using them.
    /// </summary>
    public void ClearSensitiveData() {
        ProxyPassword = null;
        Password = null;
    }
}
