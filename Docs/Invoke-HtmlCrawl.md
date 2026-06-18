---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Invoke-HtmlCrawl
## SYNOPSIS
Crawls a site offline and returns extracted pages with optional browser rendering.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-HtmlCrawl [-Url] <string> [-MaxDepth <int>] [-MaxPages <int>] [-Render] [-AutoRender] [-IncludeExternal] [-IncludeSubdomains] [-PathPrefix <string>] [-UseCanonicalUrls] [-DeduplicatePages] [-KeepTrackingQueryParameters] [-AllowAnyContentType] [-AllowAssetUrls] [-DownloadAssets] [-KeepRemoteAssetUrls] [-KeepRemotePageUrls] [-NoSitemaps] [-IgnoreRobotsTxt] [-RobotsUserAgent <string>] [-OutPath <string>] [-ResumePath <string>] [-Profile <string>] [-ProfilePath <string>] [-AutoProfile] [-Scenario <HtmlCrawlScenario>] [-Selector <string>] [-ContentMode <HtmlCrawlContentMode>] [-CompareContentModes] [-ReaderMinimumWordCount <int>] [-ReaderMinimumScore <double>] [-ExcludeSelector <string[]>] [-ExcludeClass <string[]>] [-ExcludeId <string[]>] [-DisableSmartContentCleanup] [-HiddenContentMode <HtmlCrawlHiddenContentMode>] [-ClickSelector <string[]>] [-ClickText <string[]>] [-DismissSelector <string[]>] [-DismissText <string[]>] [-WaitForSelector <string>] [-WaitAfterLoadMs <int>] [-AutoScroll] [-AutoScrollSteps <int>] [-AutoScrollDelayMs <int>] [-InteractionDelayMs <int>] [-InteractionRepeatCount <int>] [-AutoRenderTextWordThreshold <int>] [-DelayMs <int>] [-Timeout <int>] [-UserAgent <string>] [-Header <hashtable>] [-IncludePattern <string[]>] [-ExcludePattern <string[]>] [-BlockResourcePattern <string[]>] [-SitemapUrl <string[]>] [-IgnoredQueryParameterPattern <string[]>] [-AllowedContentTypePattern <string[]>] [-IgnoredAssetPathPattern <string[]>] [-AssetIncludePattern <string[]>] [-AssetExcludePattern <string[]>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-StorageStatePath <string>] [-Browser <HtmlBrowserEngine>] [-Visible] [-Clean] [-IncludeHtml] [-IncludeText] [-IncludeMarkdown] [-MarkdownProfile <HtmlMarkdownProfile>] [-MarkdownImageMode <MarkdownImageRenderingMode>] [-ListingCardMetadataMode <HtmlListingCardMetadataMode>] [-IncludeStructuredJson] [-StructuredJsonPreset <HtmlCrawlStructuredJsonPreset>] [-StructuredJsonSchema <string>] [-StructuredJsonSchemaPath <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Crawls a site offline and returns extracted pages with optional browser rendering.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlCrawl -Url https://example.com/docs -MaxDepth 1
```


### EXAMPLE 2
```powershell
Invoke-HtmlCrawl -Url https://example.com/app -Render -WaitForSelector main -StorageStatePath .\state.json
```


## PARAMETERS

### -AllowAnyContentType
Allow crawling responses regardless of content type.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AllowAssetUrls
Allow known asset/document URLs to be queued and fetched.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AllowedContentTypePattern
Additional response content-type patterns that should be treated as crawlable pages.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AssetExcludePattern
Optional asset URL exclude patterns using * wildcards.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AssetIncludePattern
Optional asset URL include patterns using * wildcards.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AutoProfile
Apply a built-in crawl profile automatically when the start host matches one.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AutoRender
Try static fetch first and fall back to Playwright when the page looks like a thin JavaScript shell.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AutoRenderTextWordThreshold
Minimum extracted word count before static pages are considered rich enough to skip AutoRender fallback.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AutoScroll
Scroll rendered pages before extraction to trigger lazy-loaded content.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AutoScrollDelayMs
Delay after each auto-scroll step in milliseconds.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AutoScrollSteps
Number of incremental scroll steps performed when AutoScroll is enabled.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -BlockResourcePattern
Optional Playwright route patterns to block during rendering.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Browser
Browser engine used when rendering.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Clean
Force browser runtime cleanup before rendering.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ClickSelector
Optional selectors to click once on rendered pages before extraction.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ClickText
Optional visible texts to click once on rendered pages before extraction.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -CompareContentModes
Also compare raw, focused, and reader extraction alternatives for diagnostics and persisted manifests.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ContentMode
Controls how the crawler chooses the content region for stored HTML and text extraction.

```yaml
Type: HtmlCrawlContentMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Raw, Focused, Reader

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Credential
Credentials for basic authentication.

```yaml
Type: PSCredential
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DeduplicatePages
Skip fetched pages whose selected content duplicates an earlier page.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DelayMs
Delay between pages in milliseconds.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DisableSmartContentCleanup
Disable the built-in cleanup heuristics that remove low-value boilerplate inside extracted content.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DismissSelector
Optional selectors to dismiss on rendered pages before extraction.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DismissText
Optional visible texts to dismiss on rendered pages before extraction.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DownloadAssets
Download assets referenced from fetched pages into the crawl dataset.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ExcludeClass
Optional class names removed from extracted HTML and text before storage.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ExcludeId
Optional element IDs removed from extracted HTML and text before storage.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ExcludePattern
Optional URL exclude patterns using * wildcards.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ExcludeSelector
Optional CSS selectors removed from extracted HTML and text before storage.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Header
Optional HTTP headers used for requests.

```yaml
Type: Hashtable
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -HiddenContentMode
Controls whether extraction should respect or include hidden DOM content. Static crawls only detect explicit DOM-hidden markers; use Render or AutoRender when you need stylesheet-hidden content filtered by computed visibility.

```yaml
Type: HtmlCrawlHiddenContentMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: RespectHidden, IncludeHidden

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IgnoredAssetPathPattern
Additional asset/document path patterns to skip before fetching.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IgnoredQueryParameterPattern
Additional query-parameter patterns to remove during URL normalization.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IgnoreRobotsTxt
Ignore robots.txt rules during crawling.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeExternal
Allow links outside the starting host.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeHtml
Include fetched HTML in the result.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeMarkdown
Include Markdown converted from the selected content in the result.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludePattern
Optional URL include patterns using * wildcards.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeStructuredJson
Include structured JSON-friendly data extracted from each crawled page.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeSubdomains
Allow subdomains of the starting host while still blocking unrelated domains.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeText
Include plain text in the result.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -InteractionDelayMs
Delay after each rendered interaction in milliseconds.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -InteractionRepeatCount
Number of times click interactions should be retried on rendered pages.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -KeepRemoteAssetUrls
Keep stored HTML pointing at original asset URLs instead of rewriting to local downloaded files.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -KeepRemotePageUrls
Keep stored HTML page links pointing at original URLs instead of local saved pages.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -KeepTrackingQueryParameters
Keep tracking query parameters instead of normalizing them away.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ListingCardMetadataMode
Controls whether low-value metadata inside repeated listing cards should be preserved or suppressed in markdown output.

```yaml
Type: HtmlListingCardMetadataMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Preserve, SuppressInRepeatedCards

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -LoginUrl
URL for form authentication before the rendered crawl starts.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MarkdownImageMode
Controls how images are emitted when selected HTML is converted to markdown. Use PortableMarkdown for broad renderer compatibility, RichMarkdown for OfficeIMO-style size suffixes, or Html for exact width and height fidelity.

```yaml
Type: MarkdownImageRenderingMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: RichMarkdown, PortableMarkdown, Html

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MarkdownProfile
Controls which markdown dialect profile is used when selected HTML is converted to markdown. Use Portable for broad compatibility or OfficeIMO when downstream consumers can benefit from richer OfficeIMO syntax.

```yaml
Type: HtmlMarkdownProfile
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Portable, OfficeIMO

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MaxDepth
Maximum depth to follow links from the starting page.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MaxPages
Maximum number of pages to fetch.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -NoSitemaps
Do not discover candidates from sitemap files.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -OutPath
Optional directory or manifest file used to persist crawl progress.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Password
Password for basic authentication or form login.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -PasswordSelector
CSS selector for the password field of the login form.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -PathPrefix
Optional path prefix that queued URLs must stay under.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Profile
Optional built-in crawl profile name used to preconfigure crawl behavior.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProfilePath
Optional JSON file containing custom crawl profiles.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server used for requests.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProxyCredential
Credentials for the proxy server.

```yaml
Type: PSCredential
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ReaderMinimumScore
Minimum score a reader-mode candidate must reach before it is preferred over the reader root element.

```yaml
Type: Double
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ReaderMinimumWordCount
Minimum word count a reader-mode candidate must have before it is treated as article-like content.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Render
Render pages through Playwright before extraction.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ResumePath
Optional directory or manifest file used to resume a previous crawl.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RobotsUserAgent
User-agent token used when evaluating robots.txt groups.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Scenario
Intent-focused scenario that applies product-style defaults before profiles and explicit options refine them.

```yaml
Type: HtmlCrawlScenario
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Custom, Content, Archive, Docs, Dataset

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Selector
Optional CSS selector used to focus extracted content.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -SitemapUrl
Explicit sitemap URLs to load before crawling.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StorageStatePath
Optional Playwright storage state file used to reuse an authenticated session.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StructuredJsonPreset
Optional built-in structured JSON preset used to flatten common page types.

```yaml
Type: HtmlCrawlStructuredJsonPreset
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: None, Auto, Docs, Article, Product

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StructuredJsonSchema
Optional inline JSON schema describing caller-defined extracted structured fields.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StructuredJsonSchemaPath
Optional JSON file path containing a structured extraction schema.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -SubmitSelector
CSS selector for the submit control of the login form.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Timeout
Per-page timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
Starting URL for the crawl.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -UseCanonicalUrls
Prefer canonical URLs discovered in page markup.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -UserAgent
User agent used for requests.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Username
Username for basic authentication or form login.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -UsernameSelector
CSS selector for the username field of the login form.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Visible
Show the browser instead of running headless.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -WaitAfterLoadMs
Optional delay after rendered page load in milliseconds.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -WaitForSelector
Optional selector to wait for on rendered pages.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `HtmlTinkerX.HtmlCrawlResult`

## RELATED LINKS

- None
