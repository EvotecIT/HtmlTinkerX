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

    /// <summary>Stores the fetched HTML in the result.</summary>
    public bool IncludeHtml { get; set; } = true;

    /// <summary>Stores plain text extracted from the page in the result.</summary>
    public bool IncludeText { get; set; } = true;

    /// <summary>Optional CSS selector used to extract a focused section from the page.</summary>
    public string? Selector { get; set; }

    /// <summary>Optional selector that must appear before rendered extraction continues.</summary>
    public string? WaitForSelector { get; set; }

    /// <summary>Additional delay after page load in milliseconds.</summary>
    public int WaitAfterLoadMs { get; set; }

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
}
