using HtmlTinkerX;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Crawls a site offline and returns extracted pages with optional browser rendering.
/// </summary>
/// <example>
/// <code>Invoke-HtmlCrawl -Url https://example.com/docs -MaxDepth 1</code>
/// </example>
/// <example>
/// <code>Invoke-HtmlCrawl -Url https://example.com/app -Render -WaitForSelector main -StorageStatePath .\state.json</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlCrawl")]
[OutputType(typeof(HtmlCrawlResult))]
public sealed class CmdletInvokeHtmlCrawl : AsyncPSCmdlet {
    /// <summary>Starting URL for the crawl.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Maximum depth to follow links from the starting page.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int MaxDepth { get; set; } = 1;

    /// <summary>Maximum number of pages to fetch.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxPages { get; set; } = 25;

    /// <summary>Render pages through Playwright before extraction.</summary>
    [Parameter]
    public SwitchParameter Render { get; set; }

    /// <summary>Try static fetch first and fall back to Playwright when the page looks like a thin JavaScript shell.</summary>
    [Parameter]
    public SwitchParameter AutoRender { get; set; }

    /// <summary>Allow links outside the starting host.</summary>
    [Parameter]
    public SwitchParameter IncludeExternal { get; set; }

    /// <summary>Allow subdomains of the starting host while still blocking unrelated domains.</summary>
    [Parameter]
    public SwitchParameter IncludeSubdomains { get; set; }

    /// <summary>Optional path prefix that queued URLs must stay under.</summary>
    [Parameter]
    public string? PathPrefix { get; set; }

    /// <summary>Prefer canonical URLs discovered in page markup.</summary>
    [Parameter]
    public SwitchParameter UseCanonicalUrls { get; set; }

    /// <summary>Skip fetched pages whose selected content duplicates an earlier page.</summary>
    [Parameter]
    public SwitchParameter DeduplicatePages { get; set; }

    /// <summary>Keep tracking query parameters instead of normalizing them away.</summary>
    [Parameter]
    public SwitchParameter KeepTrackingQueryParameters { get; set; }

    /// <summary>Allow crawling responses regardless of content type.</summary>
    [Parameter]
    public SwitchParameter AllowAnyContentType { get; set; }

    /// <summary>Allow known asset/document URLs to be queued and fetched.</summary>
    [Parameter]
    public SwitchParameter AllowAssetUrls { get; set; }

    /// <summary>Download assets referenced from fetched pages into the crawl dataset.</summary>
    [Parameter]
    public SwitchParameter DownloadAssets { get; set; }

    /// <summary>Keep stored HTML pointing at original asset URLs instead of rewriting to local downloaded files.</summary>
    [Parameter]
    public SwitchParameter KeepRemoteAssetUrls { get; set; }

    /// <summary>Keep stored HTML page links pointing at original URLs instead of local saved pages.</summary>
    [Parameter]
    public SwitchParameter KeepRemotePageUrls { get; set; }

    /// <summary>Do not discover candidates from sitemap files.</summary>
    [Parameter]
    public SwitchParameter NoSitemaps { get; set; }

    /// <summary>Ignore robots.txt rules during crawling.</summary>
    [Parameter]
    public SwitchParameter IgnoreRobotsTxt { get; set; }

    /// <summary>User-agent token used when evaluating robots.txt groups.</summary>
    [Parameter]
    public string RobotsUserAgent { get; set; } = "*";

    /// <summary>Optional directory or manifest file used to persist crawl progress.</summary>
    [Parameter]
    public string? OutPath { get; set; }

    /// <summary>Optional directory or manifest file used to resume a previous crawl.</summary>
    [Parameter]
    public string? ResumePath { get; set; }

    /// <summary>Optional built-in crawl profile name used to preconfigure crawl behavior.</summary>
    [Parameter]
    public string? Profile { get; set; }

    /// <summary>Optional JSON file containing custom crawl profiles.</summary>
    [Parameter]
    public string? ProfilePath { get; set; }

    /// <summary>Apply a built-in crawl profile automatically when the start host matches one.</summary>
    [Parameter]
    public SwitchParameter AutoProfile { get; set; }

    /// <summary>Intent-focused scenario that applies product-style defaults before profiles and explicit options refine them.</summary>
    [Parameter]
    public HtmlCrawlScenario Scenario { get; set; } = HtmlCrawlScenario.Custom;

    /// <summary>Optional CSS selector used to focus extracted content.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>Controls how the crawler chooses the content region for stored HTML and text extraction.</summary>
    [Parameter]
    public HtmlCrawlContentMode ContentMode { get; set; } = HtmlCrawlContentMode.Focused;

    /// <summary>Also compare raw, focused, and reader extraction alternatives for diagnostics and persisted manifests.</summary>
    [Parameter]
    public SwitchParameter CompareContentModes { get; set; }

    /// <summary>Minimum word count a reader-mode candidate must have before it is treated as article-like content.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ReaderMinimumWordCount { get; set; } = 20;

    /// <summary>Minimum score a reader-mode candidate must reach before it is preferred over the reader root element.</summary>
    [Parameter]
    [ValidateRange(0.01, double.MaxValue)]
    public double ReaderMinimumScore { get; set; } = 25;

    /// <summary>Optional CSS selectors removed from extracted HTML and text before storage.</summary>
    [Parameter]
    public string[] ExcludeSelector { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional class names removed from extracted HTML and text before storage.</summary>
    [Parameter]
    public string[] ExcludeClass { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional element IDs removed from extracted HTML and text before storage.</summary>
    [Parameter]
    public string[] ExcludeId { get; set; } = System.Array.Empty<string>();

    /// <summary>Disable the built-in cleanup heuristics that remove low-value boilerplate inside extracted content.</summary>
    [Parameter]
    public SwitchParameter DisableSmartContentCleanup { get; set; }

    /// <summary>Controls whether extraction should respect or include hidden DOM content. Static crawls only detect explicit DOM-hidden markers; use Render or AutoRender when you need stylesheet-hidden content filtered by computed visibility.</summary>
    [Parameter]
    public HtmlCrawlHiddenContentMode HiddenContentMode { get; set; } = HtmlCrawlHiddenContentMode.RespectHidden;

    /// <summary>Optional selectors to click once on rendered pages before extraction.</summary>
    [Parameter]
    public string[] ClickSelector { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional visible texts to click once on rendered pages before extraction.</summary>
    [Parameter]
    public string[] ClickText { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional selectors to dismiss on rendered pages before extraction.</summary>
    [Parameter]
    public string[] DismissSelector { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional visible texts to dismiss on rendered pages before extraction.</summary>
    [Parameter]
    public string[] DismissText { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional selector to wait for on rendered pages.</summary>
    [Parameter]
    public string? WaitForSelector { get; set; }

    /// <summary>Optional delay after rendered page load in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int WaitAfterLoadMs { get; set; }

    /// <summary>Scroll rendered pages before extraction to trigger lazy-loaded content.</summary>
    [Parameter]
    public SwitchParameter AutoScroll { get; set; }

    /// <summary>Number of incremental scroll steps performed when AutoScroll is enabled.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int AutoScrollSteps { get; set; } = 3;

    /// <summary>Delay after each auto-scroll step in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int AutoScrollDelayMs { get; set; } = 400;

    /// <summary>Delay after each rendered interaction in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int InteractionDelayMs { get; set; } = 300;

    /// <summary>Number of times click interactions should be retried on rendered pages.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int InteractionRepeatCount { get; set; } = 1;

    /// <summary>Minimum extracted word count before static pages are considered rich enough to skip AutoRender fallback.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int AutoRenderTextWordThreshold { get; set; } = 40;

    /// <summary>Delay between pages in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int DelayMs { get; set; }

    /// <summary>Per-page timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>User agent used for requests.</summary>
    [Parameter]
    public string? UserAgent { get; set; }

    /// <summary>Optional HTTP headers used for requests.</summary>
    [Parameter]
    public Hashtable? Header { get; set; }

    /// <summary>Optional URL include patterns using * wildcards.</summary>
    [Parameter]
    public string[] IncludePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional URL exclude patterns using * wildcards.</summary>
    [Parameter]
    public string[] ExcludePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional Playwright route patterns to block during rendering.</summary>
    [Parameter]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Explicit sitemap URLs to load before crawling.</summary>
    [Parameter]
    public string[] SitemapUrl { get; set; } = System.Array.Empty<string>();

    /// <summary>Additional query-parameter patterns to remove during URL normalization.</summary>
    [Parameter]
    public string[] IgnoredQueryParameterPattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Additional response content-type patterns that should be treated as crawlable pages.</summary>
    [Parameter]
    public string[] AllowedContentTypePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Additional asset/document path patterns to skip before fetching.</summary>
    [Parameter]
    public string[] IgnoredAssetPathPattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional asset URL include patterns using * wildcards.</summary>
    [Parameter]
    public string[] AssetIncludePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional asset URL exclude patterns using * wildcards.</summary>
    [Parameter]
    public string[] AssetExcludePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Proxy server used for requests.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Credentials for basic authentication.</summary>
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Username for basic authentication or form login.</summary>
    [Parameter]
    public string? Username { get; set; }

    /// <summary>Password for basic authentication or form login.</summary>
    [Parameter]
    public string? Password { get; set; }

    /// <summary>URL for form authentication before the rendered crawl starts.</summary>
    [Parameter]
    public string? LoginUrl { get; set; }

    /// <summary>CSS selector for the username field of the login form.</summary>
    [Parameter]
    public string? UsernameSelector { get; set; }

    /// <summary>CSS selector for the password field of the login form.</summary>
    [Parameter]
    public string? PasswordSelector { get; set; }

    /// <summary>CSS selector for the submit control of the login form.</summary>
    [Parameter]
    public string? SubmitSelector { get; set; }

    /// <summary>Optional Playwright storage state file used to reuse an authenticated session.</summary>
    [Parameter]
    public string? StorageStatePath { get; set; }

    /// <summary>Browser engine used when rendering.</summary>
    [Parameter]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Force browser runtime cleanup before rendering.</summary>
    [Parameter]
    public SwitchParameter Clean { get; set; }

    /// <summary>Include fetched HTML in the result.</summary>
    [Parameter]
    public SwitchParameter IncludeHtml { get; set; } = true;

    /// <summary>Include plain text in the result.</summary>
    [Parameter]
    public SwitchParameter IncludeText { get; set; } = true;

    /// <summary>Include Markdown converted from the selected content in the result.</summary>
    [Parameter]
    public SwitchParameter IncludeMarkdown { get; set; }

    /// <summary>Controls which markdown dialect profile is used when selected HTML is converted to markdown. Use Portable for broad compatibility or OfficeIMO when downstream consumers can benefit from richer OfficeIMO syntax.</summary>
    [Parameter]
    public HtmlMarkdownProfile MarkdownProfile { get; set; } = HtmlMarkdownProfile.Portable;

    /// <summary>Controls how images are emitted when selected HTML is converted to markdown. Use PortableMarkdown for broad renderer compatibility, RichMarkdown for OfficeIMO-style size suffixes, or Html for exact width and height fidelity.</summary>
    [Parameter]
    public MarkdownImageRenderingMode MarkdownImageMode { get; set; } = MarkdownImageRenderingMode.PortableMarkdown;

    /// <summary>Controls whether low-value metadata inside repeated listing cards should be preserved or suppressed in markdown output.</summary>
    [Parameter]
    public HtmlListingCardMetadataMode ListingCardMetadataMode { get; set; } = HtmlListingCardMetadataMode.SuppressInRepeatedCards;

    /// <summary>Include structured JSON-friendly data extracted from each crawled page.</summary>
    [Parameter]
    public SwitchParameter IncludeStructuredJson { get; set; }

    /// <summary>Optional built-in structured JSON preset used to flatten common page types.</summary>
    [Parameter]
    public HtmlCrawlStructuredJsonPreset StructuredJsonPreset { get; set; }

    /// <summary>Optional inline JSON schema describing caller-defined extracted structured fields.</summary>
    [Parameter]
    public string? StructuredJsonSchema { get; set; }

    /// <summary>Optional JSON file path containing a structured extraction schema.</summary>
    [Parameter]
    public string? StructuredJsonSchemaPath { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        ValidateProfile(Profile, ProfilePath);

        string? user = Credential?.UserName ?? Username;
        string? pass = Credential?.GetNetworkCredential().Password ?? Password;
        string? proxyUser = ProxyCredential?.UserName;
        string? proxyPass = ProxyCredential?.GetNetworkCredential().Password;

        HtmlFormLogin? form = null;
        if (!string.IsNullOrEmpty(LoginUrl) && !string.IsNullOrEmpty(UsernameSelector) && !string.IsNullOrEmpty(PasswordSelector) && !string.IsNullOrEmpty(SubmitSelector)) {
            form = new HtmlFormLogin {
                LoginUrl = LoginUrl!,
                UsernameSelector = UsernameSelector!,
                PasswordSelector = PasswordSelector!,
                SubmitSelector = SubmitSelector!
            };
        }

        HtmlCrawlOptions options = new() {
            MaxDepth = MaxDepth,
            MaxPages = MaxPages,
            Render = Render.IsPresent,
            AutoRender = AutoRender.IsPresent,
            RestrictToHost = !IncludeExternal.IsPresent,
            IncludeSubdomains = IncludeSubdomains.IsPresent,
            PathPrefix = PathPrefix,
            UseCanonicalUrls = UseCanonicalUrls.IsPresent,
            DeduplicatePages = DeduplicatePages.IsPresent,
            IgnoreTrackingQueryParameters = !KeepTrackingQueryParameters.IsPresent,
            RestrictToAllowedContentTypes = !AllowAnyContentType.IsPresent,
            SkipKnownAssetUrls = !AllowAssetUrls.IsPresent,
            DownloadAssets = DownloadAssets.IsPresent,
            RewriteAssetReferencesToLocal = !KeepRemoteAssetUrls.IsPresent,
            RewritePageLinksToLocal = !KeepRemotePageUrls.IsPresent,
            UseSitemaps = !NoSitemaps.IsPresent,
            RespectRobotsTxt = !IgnoreRobotsTxt.IsPresent,
            RobotsUserAgent = RobotsUserAgent,
            OutputPath = OutPath?.ToFullPath(),
            ResumePath = ResumePath?.ToFullPath(),
            ProfileName = Profile,
            ProfilePath = ProfilePath?.ToFullPath(),
            AutoProfile = AutoProfile.IsPresent,
            Scenario = Scenario,
            IncludeHtml = IncludeHtml.IsPresent,
            IncludeText = IncludeText.IsPresent,
            IncludeMarkdown = IncludeMarkdown.IsPresent,
            MarkdownProfile = MarkdownProfile,
            MarkdownImageMode = MarkdownImageMode,
            ListingCardMetadataMode = ListingCardMetadataMode,
            IncludeStructuredJson = IncludeStructuredJson.IsPresent,
            StructuredJsonPreset = StructuredJsonPreset,
            StructuredJsonSchema = StructuredJsonSchema,
            StructuredJsonSchemaPath = StructuredJsonSchemaPath?.ToFullPath(),
            Selector = Selector,
            ContentMode = ContentMode,
            CompareContentModes = CompareContentModes.IsPresent,
            ReaderMinimumWordCount = ReaderMinimumWordCount,
            ReaderMinimumScore = ReaderMinimumScore,
            ExcludeSelectors = new List<string>(ExcludeSelector),
            ExcludeClasses = new List<string>(ExcludeClass),
            ExcludeIds = new List<string>(ExcludeId),
            SmartContentCleanup = !DisableSmartContentCleanup.IsPresent,
            HiddenContentMode = HiddenContentMode,
            ClickSelectors = new List<string>(ClickSelector),
            ClickTexts = new List<string>(ClickText),
            DismissSelectors = new List<string>(DismissSelector),
            DismissTexts = new List<string>(DismissText),
            WaitForSelector = WaitForSelector,
            WaitAfterLoadMs = WaitAfterLoadMs,
            AutoScroll = AutoScroll.IsPresent,
            AutoScrollSteps = AutoScrollSteps,
            AutoScrollDelayMs = AutoScrollDelayMs,
            InteractionDelayMs = InteractionDelayMs,
            InteractionRepeatCount = InteractionRepeatCount,
            AutoRenderTextWordThreshold = AutoRenderTextWordThreshold,
            DelayMs = DelayMs,
            Timeout = Timeout,
            UserAgent = UserAgent,
            Proxy = Proxy,
            ProxyUsername = proxyUser,
            ProxyPassword = proxyPass,
            Username = user,
            Password = pass,
            FormLogin = form,
            StorageStatePath = StorageStatePath?.ToFullPath(),
            Browser = Browser,
            Headless = !Visible.IsPresent,
            CleanBrowserInstall = Clean.IsPresent
        };

        if (Header != null) {
            foreach (DictionaryEntry entry in Header) {
                if (entry.Key is null || entry.Value is null) {
                    continue;
                }

                options.Headers[entry.Key.ToString()!] = entry.Value.ToString()!;
            }
        }

        foreach (string pattern in IncludePattern) {
            options.IncludePatterns.Add(pattern);
        }

        foreach (string pattern in ExcludePattern) {
            options.ExcludePatterns.Add(pattern);
        }

        foreach (string pattern in BlockResourcePattern) {
            options.BlockResourcePatterns.Add(pattern);
        }

        foreach (string sitemap in SitemapUrl) {
            options.SitemapUrls.Add(sitemap);
        }

        foreach (string pattern in IgnoredQueryParameterPattern) {
            options.IgnoredQueryParameterPatterns.Add(pattern);
        }

        foreach (string pattern in AllowedContentTypePattern) {
            options.AllowedContentTypePatterns.Add(pattern);
        }

        foreach (string pattern in IgnoredAssetPathPattern) {
            options.IgnoredAssetPathPatterns.Add(pattern);
        }

        foreach (string pattern in AssetIncludePattern) {
            options.AssetIncludePatterns.Add(pattern);
        }

        foreach (string pattern in AssetExcludePattern) {
            options.AssetExcludePatterns.Add(pattern);
        }

        try {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(Url, options, linkedCts.Token).ConfigureAwait(false);
            WriteObject(result);
        } finally {
            options.ClearSensitiveData();
            pass = null;
            proxyPass = null;
            Password = null;
            Credential = null;
            ProxyCredential = null;
        }
    }

    private static void ValidateProfile(string? profileName, string? profilePath) {
        if (string.IsNullOrWhiteSpace(profileName)) {
            return;
        }

        if (!string.IsNullOrWhiteSpace(profilePath)) {
            return;
        }

        string normalizedProfileName = profileName!.Trim();
        foreach (string knownProfile in HtmlCrawlProfiles.Names) {
            if (string.Equals(knownProfile, normalizedProfileName, System.StringComparison.OrdinalIgnoreCase)) {
                return;
            }
        }

        string availableProfiles = string.Join(", ", HtmlCrawlProfiles.Names);
        throw new PSArgumentException($"Unknown crawl profile '{profileName}'. Available profiles: {availableProfiles}.", nameof(Profile));
    }
}
