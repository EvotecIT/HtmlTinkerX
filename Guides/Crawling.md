# Crawl and export an offline dataset

[Back to the project overview](../README.MD)

## Retry temporary HTTP failures

Use `HttpRetryCount` to retry static GET responses with status 408, 429, 500, 502, 503 or 504. It accepts zero to ten additional attempts and defaults to zero. The policy applies to pages, robots files, sitemaps and assets. Browser navigation has its own policy.

```powershell
Invoke-HtmlCrawl -Url 'https://example.com/docs/' -HttpRetryCount 2 -Timeout 15000 -OutPath './crawl'
```

Retries honor `Retry-After` as seconds or an HTTP date. Without a valid server delay, backoff starts at 250 ms, doubles and stops growing at 30 seconds. Redirects also honor `Retry-After` when retry policy is enabled. Attempts, redirects, waits and body reads share the request timeout. A wait beyond that budget leaves the response as a failure. Transport errors and canceled requests are not retried.

```powershell
# Offline crawl
$crawl = Invoke-HTMLCrawl -Url 'https://example.com/docs' -MaxDepth 1 -MaxPages 10 -DeduplicatePages
$crawl.Pages | Select-Object Url, Title, Depth
$crawl.SkippedPages | Select-Object Url, SkipReason
$crawl.Summary.ToReportText($crawl.SitemapUrls)

# AI-ready offline dataset with Markdown alongside HTML/text
$aiReady = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Selector 'main' -IncludeMarkdown -OutPath '.\crawl-output'
$aiReady.Pages | Select-Object Url, MarkdownPath

# Self-contained JSON document export for downstream automation
$structured = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Selector 'main' -IncludeStructuredJson -OutPath '.\crawl-output'
$structured.StructuredJsonPagesJsonlPath
$structured.OpenApiLikePath
$structured.OpenApiPath
$structured.OpenApiDocument["openapi"]
$structured.OpenApiLike.StrictOpenApiEligibleOperationCount
$structured.OpenApiLike.StrictOpenApiSkippedOperationCount
$structured.OpenApiDocument.ContainsKey("x-htmltinkerx-promotion")
$structured.Pages | Select-Object Url, StructuredJsonPath, @{ N = 'Summary'; E = { $_.StructuredJson.Document.Summary } }
$structured.Pages | Select-Object Url, @{ N = 'Description'; E = { $_.StructuredJson.Metadata.Description } }, @{ N = 'NavigationCount'; E = { $_.StructuredJson.Layout.NavigationCount } }
$structured.Pages | Select-Object Url, @{ N = 'CodeBlocks'; E = { $_.StructuredJson.CodeBlocks.Count } }, @{ N = 'Breadcrumbs'; E = { $_.StructuredJson.Breadcrumbs.Count } }, @{ N = 'FaqItems'; E = { $_.StructuredJson.FaqItems.Count } }
$structured.Pages | Select-Object Url, @{ N = 'CodeSamples'; E = { $_.StructuredJson.CodeSamples.Count } }, @{ N = 'ApiEndpoints'; E = { $_.StructuredJson.ApiEndpoints.Count } }
$structured.Pages | Select-Object Url, @{ N = 'AuthenticatedEndpoints'; E = { $_.StructuredJson.ApiEndpoints.Where({ $_.Authentication.Required -ne $null -or $_.Authentication.Schemes.Count -gt 0 -or $_.Authentication.Headers.Count -gt 0 }).Count } }, @{ N = 'RateLimitedEndpoints'; E = { $_.StructuredJson.ApiEndpoints.Where({ $_.RateLimit.Mentioned -or $_.RateLimit.StatusCode -ne $null }).Count } }
$structured.Pages | Select-Object Url, @{ N = 'ApiErrorResponses'; E = { $_.StructuredJson.ApiEndpoints.ForEach({ $_.ErrorResponses.Count }) | Measure-Object -Sum | Select-Object -ExpandProperty Sum } }
$structured.Pages | Select-Object Url, @{ N = 'SpecTables'; E = { $_.StructuredJson.SpecTables.Count } }, @{ N = 'Callouts'; E = { $_.StructuredJson.Callouts.Count } }, @{ N = 'PrimaryActions'; E = { $_.StructuredJson.PrimaryActions.Count } }

# Built-in preset fields for common page types
$docs = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Selector 'main' -IncludeStructuredJson -StructuredJsonPreset Docs -OutPath '.\crawl-output'
$docs.Pages | Select-Object Url, @{ N = 'Preset'; E = { $_.StructuredJson.ResolvedPreset } }, @{ N = 'MainHeading'; E = { $_.StructuredJson.Extracted["mainHeading"] } }, @{ N = 'ApiEndpoints'; E = { $_.StructuredJson.Extracted["apiEndpoints"] } }, @{ N = 'Breadcrumbs'; E = { $_.StructuredJson.Extracted["breadcrumbs"] } }, @{ N = 'PrimaryActions'; E = { $_.StructuredJson.Extracted["primaryActions"] } }
$docs.Pages | Select-Object Url, @{ N = 'ApiCatalog'; E = { $_.StructuredJson.Extracted["apiCatalog"] } }, @{ N = 'ApiTags'; E = { $_.StructuredJson.Extracted["apiTags"] } }, @{ N = 'ApiResources'; E = { $_.StructuredJson.Extracted["apiResources"] } }, @{ N = 'OperationIds'; E = { $_.StructuredJson.Extracted["operationIds"] } }
$docs.Pages | Select-Object Url, @{ N = 'OpenApiLike'; E = { $_.StructuredJson.Extracted["openApiLike"] } }, @{ N = 'OpenApiPaths'; E = { $_.StructuredJson.Extracted["openApiPaths"] } }, @{ N = 'OpenApiServers'; E = { $_.StructuredJson.Extracted["openApiServers"] } }
$docs.Pages | Select-Object Url, @{ N = 'ApiParameterCount'; E = { $_.StructuredJson.ApiEndpoints[0].Parameters.Count } }, @{ N = 'OperationId'; E = { $_.StructuredJson.ApiEndpoints[0].OperationId } }, @{ N = 'Resource'; E = { $_.StructuredJson.ApiEndpoints[0].Resource } }, @{ N = 'Tags'; E = { $_.StructuredJson.ApiEndpoints[0].Tags -join ', ' } }, @{ N = 'ResponseExampleCount'; E = { $_.StructuredJson.ApiEndpoints[0].ResponseExamples.Count } }
$docs.OpenApiLike.Paths['/v1/widgets'].Operations['post']
$docs.OpenApiLike.Paths['/v1/widgets'].Operations['post'].StrictOpenApiScore
$docs.OpenApiLike.Paths['/v1/widgets'].Operations['post'].StrictOpenApiEligible
$docs.OpenApiLike.Paths['/v1/widgets'].Operations['post'].Provenance.PageUrls
$docs.OpenApiLike.Paths['/v1/widgets'].Operations['post'].Provenance.SourceKinds
$docs.OpenApiLike.Components.AuthProfiles
$docs.OpenApiLike.Components.FieldSets
$docs.OpenApiLike.Components.ParameterSets
$docs.OpenApiLike.Components.RequestHeaderSets
$docs.OpenApiLike.Components.ResponseExampleSets
$docs.Pages | Select-Object Url, @{ N = 'BodyParameterCount'; E = { $_.StructuredJson.ApiEndpoints[0].BodyParameters.Count } }, @{ N = 'HeaderParameterCount'; E = { $_.StructuredJson.ApiEndpoints[0].HeaderParameters.Count } }, @{ N = 'BodySchemaNameType'; E = { $_.StructuredJson.ApiEndpoints[0].RequestBodySchema["name"] } }
$docs.Pages | Select-Object Url, @{ N = 'BodyParameterFormat'; E = { $_.StructuredJson.ApiEndpoints[0].BodyParameters[0].Format } }, @{ N = 'BodyParameterEnum'; E = { $_.StructuredJson.ApiEndpoints[0].BodyParameters[0].EnumValues -join ', ' } }, @{ N = 'BodyParameterExample'; E = { $_.StructuredJson.ApiEndpoints[0].BodyParameters[0].ExampleValue } }
$docs.Pages | Select-Object Url, @{ N = 'RequestFieldSource'; E = { $_.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].Provenance[0].Kind } }, @{ N = 'SuccessFieldSource'; E = { $_.StructuredJson.ApiEndpoints[0].SuccessResponseFields[0].Provenance[0].Kind } }
$docs.Pages | Select-Object Url, @{ N = 'RequestFieldConfidence'; E = { $_.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].ConfidenceScore } }, @{ N = 'SuccessFieldConfidence'; E = { $_.StructuredJson.ApiEndpoints[0].SuccessResponseFields[0].ConfidenceScore } }
$docs.Pages | Select-Object Url, @{ N = 'RequestBodyFields'; E = { $_.StructuredJson.Extracted["requestBodyFields"] } }, @{ N = 'SuccessResponseFields'; E = { $_.StructuredJson.Extracted["successResponseFields"] } }, @{ N = 'ErrorResponseFields'; E = { $_.StructuredJson.Extracted["errorResponseFields"] } }
$docs.Pages | Select-Object Url, @{ N = 'SuccessFieldKind'; E = { $_.StructuredJson.ApiEndpoints[0].SuccessResponseFields[0].Kind } }, @{ N = 'SuccessFieldChildren'; E = { $_.StructuredJson.ApiEndpoints[0].SuccessResponseFields[0].ChildPaths -join ', ' } }
$docs.Pages | Select-Object Url, @{ N = 'AuthRequired'; E = { $_.StructuredJson.ApiEndpoints[0].Authentication.Required } }, @{ N = 'AuthSchemes'; E = { $_.StructuredJson.Extracted["authenticationSchemes"] } }, @{ N = 'RateLimit'; E = { $_.StructuredJson.ApiEndpoints[0].RateLimit.Limit } }, @{ N = 'RateLimitHeaders'; E = { $_.StructuredJson.Extracted["rateLimitHeaders"] } }
$docs.Pages | Select-Object Url, @{ N = 'RequestExamples'; E = { $_.StructuredJson.Extracted["requestExamples"] } }, @{ N = 'RequestHeaders'; E = { $_.StructuredJson.Extracted["requestHeaders"] } }, @{ N = 'RequestExampleCount'; E = { $_.StructuredJson.Extracted["requestExampleCount"] } }
$docs.Pages | Select-Object Url, @{ N = 'ResponseHeaders'; E = { $_.StructuredJson.Extracted["responseHeaders"] } }, @{ N = 'ErrorResponses'; E = { $_.StructuredJson.Extracted["errorResponses"] } }, @{ N = 'ErrorResponseCount'; E = { $_.StructuredJson.Extracted["errorResponseCount"] } }, @{ N = 'ErrorCatalog'; E = { $_.StructuredJson.Extracted["errorCatalog"] } }
$docs.Pages | Select-Object Url, @{ N = 'SuccessResponseSchema'; E = { $_.StructuredJson.Extracted["successResponseSchema"] } }, @{ N = 'ErrorResponseSchema'; E = { $_.StructuredJson.Extracted["errorResponseSchema"] } }

# Caller-defined JSON fields layered on top of the built-in structured model
$schema = @'
{
  "title": "Metadata.Title",
  "description": "Metadata.Description",
  "navLinks": { "selector": "nav a", "source": "page", "mode": "text", "all": true },
  "mainHeading": { "selector": "h1", "source": "selected", "mode": "text" }
}
'@
$extracted = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Selector 'main' -StructuredJsonPreset Docs -StructuredJsonSchema $schema
$extracted.Pages | Select-Object Url, @{ N = 'Title'; E = { $_.StructuredJson.Extracted["title"] } }, @{ N = 'MainHeading'; E = { $_.StructuredJson.Extracted["mainHeading"] } }

# One-call dataset mode turns on Markdown + structured JSON defaults
$dataset = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Scenario Dataset -OutPath '.\crawl-output'
$dataset.Pages | Select-Object Url, MarkdownPath, StructuredJsonPath, @{ N = 'Preset'; E = { $_.StructuredJson.ResolvedPreset } }

# Preserve full tracked URLs when query strings are meaningful
$exactUrls = Invoke-HTMLCrawl -Url 'https://example.com/docs' -KeepTrackingQueryParameters

# Allow non-HTML responses such as PDFs when needed
$permissive = Invoke-HTMLCrawl -Url 'https://example.com/docs' -AllowAssetUrls -AllowAnyContentType

# Download discovered images/documents into the offline dataset
$richSnapshot = Invoke-HTMLCrawl -Url 'https://example.com/docs' -MaxDepth 1 -DownloadAssets -OutPath '.\crawl-output'
# Stored HTML now points at local ../assets/... paths by default
# Internal page links are also rewritten to local saved .html files by default

# Hybrid crawl for JavaScript-heavy pages with lazy loading and noisy chrome removal
$hybrid = Invoke-HTMLCrawl -Url 'https://example.com/app' -AutoRender -WaitForSelector '#main' -DismissSelector '.cookie-banner button', '#consent-accept' -DismissText 'Accept', 'I agree' -ClickSelector '.load-more', '.expand-details' -ClickText 'Load more', 'Show more' -InteractionRepeatCount 2 -AutoScroll -ExcludeSelector '.language-switcher', '.share-links', '.related-posts'
$hybrid.Pages | Select-Object Url, RenderMode, RenderReason, AppliedInteractions
$hybrid.Summary.ToReportText($hybrid.SitemapUrls)
# summary now includes interaction totals and per-interaction counts

# Remove noisy blocks by class or id, and keep smart cleanup enabled by default
$clean = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Selector 'main' -ExcludeClass 'promo-box', 'doc-tools' -ExcludeId 'reader-tools'
# Use -DisableSmartContentCleanup if you want the raw selected content without heuristic pruning

# Start from an intent-focused scenario instead of hand-tuning many knobs
$docsScenario = Invoke-HTMLCrawl -Url 'https://docs.example.com/' -Scenario Docs
$docsScenario.AppliedScenario
$docsScenario.Pages[0] | Select-Object ContentModeUsed, ContentSelectionReasonCode

# Choose how content is selected before cleanup
$raw = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Selector '#main' -ContentMode Raw
$focused = Invoke-HTMLCrawl -Url 'https://example.com/docs' -Selector 'main' -ContentMode Focused
$reader = Invoke-HTMLCrawl -Url 'https://example.com/docs' -ContentMode Reader -CompareContentModes -ReaderMinimumWordCount 30 -ReaderMinimumScore 40
$reader.Pages | Select-Object Url, ContentModeUsed, ContentSelectionReasonCode, ContentElementSelectorHint, ContentSelectionScore, ReaderCandidateCount, ReaderRootElementSelectorHint
$reader.Pages[0].ContentComparisons | Select-Object Mode, ReasonCode, ElementSelectorHint, WordCount, Summary
$reader.Pages[0] | Select-Object BestContentComparisonMode, BestContentComparisonReasonCode, BestContentComparisonWordCount, RunnerUpContentComparisonMode, BestContentComparisonWordDelta, ContentComparisonDeltaSummary, ContentComparisonPreviewSummary
$reader.Summary.ContentComparisonWinnerPreviewSamples
# persisted index.html now shows the same compact deltas, for example:
# Reader 0 | Focused -12 | Raw -37
# and a side-by-side preview line, for example:
# Reader 142w @ article: Hello main article... | Focused 130w @ main: Hello main... | Raw 118w: Menu item Hello...
# summary/report output also includes one representative preview sample per winning mode

# Reuse a built-in profile for a common site family
$profiled = Invoke-HTMLCrawl -Url 'https://docs.example.com/' -Profile 'docs-content'
$profiled.AppliedProfileName
$profiled.AppliedProfileReasonCode
$profiled.Pages[0] | Select-Object ContentModeUsed, BestContentComparisonMode, ContentComparisonDeltaSummary
# Available profile names: api-docs-content, docs-content, wordpress-content
# Unknown names fail fast and report the built-in values
# docs-content also defaults to Reader mode and enables comparison mode so tuning output is available immediately

# AutoProfile can also infer the generic WordPress profile from page markers
$wordpress = Invoke-HTMLCrawl -Url 'https://example-blog.com/' -AutoProfile
$wordpress.AppliedProfileName
$wordpress.AppliedProfileReasonCode

# AutoProfile can also infer a documentation-style profile from docs markers
$docs = Invoke-HTMLCrawl -Url 'https://docs.example.com/' -AutoProfile
$docs.AppliedProfileName
$docs.AppliedProfileReasonCode

# AutoProfile can also infer API documentation profiles from Swagger/ReDoc-style markers
$apiDocs = Invoke-HTMLCrawl -Url 'https://api.example.com/docs/' -AutoProfile
$apiDocs.AppliedProfileName
$apiDocs.AppliedProfileReasonCode

# Load custom profiles from JSON
$custom = Invoke-HTMLCrawl -Url 'https://docs.example.com/' -Profile 'custom-docs' -ProfilePath '.\crawl-profiles.json'
$custom.AppliedProfileName
$custom.AppliedProfileReasonCode

# Example custom profile file snippet
# [
#   {
#     "name": "custom-docs",
#     "hosts": [ "docs.example.com" ],
#     "selector": "article",
#     "contentMode": "Reader",
#     "readerMinimumWordCount": 30,
#     "readerMinimumScore": 40,
#     "excludeClasses": [ "sidebar", "feedback-box" ]
#   }
# ]

# Inspect built-in or custom profiles
Get-HtmlCrawlProfile
Get-HtmlCrawlProfile -Path '.\crawl-profiles.json' -Name 'custom-docs'

# Resume a previous crawl snapshot
$resumed = Invoke-HTMLCrawl -Url 'https://example.com/docs' -ResumePath '.\crawl-output' -OutPath '.\crawl-output'
```

Crawler credentials and custom headers are sent only to the starting origin (scheme, host, and port), including across redirects, assets, and sitemaps. The `User-Agent` header identifies the crawler on every allowed destination. Allowing external URLs or subdomains does not share those credentials. Rendering with custom headers requires Chromium; Firefox and WebKit still support rendering without custom headers and origin-scoped HTTP authentication. Chromium crawls using scoped headers, including Basic authentication, block service workers. `AutoRender` launches a browser only when a page needs it.

`MaxPages` counts fetched page candidates, including duplicate and unsupported responses. `DelayMs` also applies between those requests. URL paths and query values retain their case, and relative links and assets resolve against the response URL and any HTML `<base>` element.

An active crawl writes an atomic checkpoint to `crawl-result.json` and stores completed records under `crawl-result.json.state/`. Keep both together when moving an interrupted crawl. `ResumePath` and `HtmlCrawler.LoadResultAsync` read these checkpoints as well as existing full manifests. Global datasets, the offline index, and final link rewriting are generated once when the crawl finishes; its final manifest is self-contained and the checkpoint records are removed.

Each page records the HTTP document's `ResponseUrl`, `EntityTag`, and `LastModified` when available. `ResponseUrl` preserves the final response address before HTML base resolution, canonical rewriting, or tracking-query normalization. Static and rendered crawls use the response that supplied the extracted document; rendered interactions that navigate update these values. The fields are retained in saved manifests and page JSONL/CSV exports.

To revisit a saved crawl, use `RefreshPath`. A refresh starts a new traversal from the starting URL and applies the current extraction settings. `ResumePath` continues unfinished work instead. Use a different output manifest when saving a refresh so cancellation or a failed request leaves the source crawl available.

```powershell
Invoke-HtmlCrawl -Url 'https://example.com/docs/' -CacheResponses -OutPath './crawl-original'
Invoke-HtmlCrawl -Url 'https://example.com/docs/' -RefreshPath './crawl-original' -OutPath './crawl-refreshed'
```

`CacheResponses` retains eligible original static HTTP HTML in the manifest, before content selection, so a validated body can be extracted again with a different selector or profile. A refresh retains eligible bodies for another refresh. This adds storage and includes content outside the selected region. Browser pages are rendered again. Responses with `no-store`, `Vary: *`, session cookies, authorization, or custom request headers are fetched in full; their original bodies are not added to the reusable cache. Changes to accepted request negotiation headers also cause a full fetch. Validators are sent only to their matching final response URL, including through redirects.

Static pages report `ResponseContentHash` for downloaded body bytes, `ResponseRevalidated` when a 304 response validated the stored body, and nullable `ResponseChanged` for comparison with the source record. Full responses compare body hashes; revalidated responses report unchanged and retain the stored body's hash. Missing older hashes or browser rendering leave the comparison unavailable. A failed refresh request is reported as failed; it does not return the old content as a successful page.

Completed crawl artifacts include:

- `crawl-result.json` for the manifest and resume state
- `index.html` as a browsable offline entry point into the saved dataset
- `pages/` with per-page `.html`, `.txt`, optional `.md`, and `.json` sidecar manifests, including extraction metadata such as content mode, selection reason, and selected element hints
- `pages.jsonl` and `pages.csv` for page-level datasets
- `skipped-pages.jsonl` for skipped content-page candidates
- `skipped-assets.jsonl` for discovered asset/document URLs that were intentionally not crawled as pages
- `links.jsonl` for discovered page-to-page links
- `assets/` and `assets.jsonl` for downloaded images/documents when `DownloadAssets` is enabled
- `chunks.jsonl` for deduplicated text chunks ready for local search/RAG pipelines
- `graph.json` for page nodes and cross-page link edges with offline degree metadata
- `summary.json` and `summary.txt` for machine-readable and human-readable crawl reports

The crawl reports and per-page manifests now also expose extraction observability data, so you can tell whether a page used `Raw`, `Focused`, or `Reader` mode, whether it matched an exact selector or fell back to semantic/full-document selection, which element was ultimately used, and in reader mode what score/candidate count led to that decision.

If you want a simpler product-style starting point, use `Scenario` in C# or `-Scenario` in PowerShell. Scenarios apply high-level defaults first, then built-in profiles, auto-profile detection, and explicit options can refine them. For example:
- `Content` prefers clean readable extraction and canonical/deduplicated pages.
- `Archive` favors offline browsing by turning on asset download and disabling aggressive cleanup.
- `Docs` applies article-first documentation defaults and docs-chrome cleanup.
- `Dataset` enables reader-style extraction plus comparison diagnostics and deduplication for downstream pipelines.

If you enable `CompareContentModes` in C# or `-CompareContentModes` in PowerShell, each page also gets a compact side-by-side comparison for `Raw`, `Focused`, and `Reader` extraction, plus a computed best mode winner based on extracted text size with a slight preference for cleaner modes when the results are close. The persisted manifests and offline index now also show the runner-up mode and the word-count delta between them, which makes it much easier to see whether the winning mode was a clear improvement or only a marginal cleanup win.

Profiles can also carry tuning defaults. The built-in `docs-content` and `api-docs-content` profiles default to `Reader` mode and enable comparison mode automatically, with tuned reader thresholds for their content shapes. Site-specific tuning should live in custom profile JSON loaded through `ProfilePath` / `-ProfilePath`, which can opt in with `"contentMode": "Reader"`, `"readerMinimumWordCount": 30`, `"readerMinimumScore": 40`, and `"compareContentModes": true`.

The crawl result, summary, page manifests, `pages.jsonl/csv`, and offline `index.html` now also expose why a profile was chosen through `AppliedProfileReasonCode` and `AppliedProfileReason`, so it is easy to tell explicit selection from host matching, WordPress markers, docs markers, and API-doc markers.

By default the crawler also normalizes away common tracking query parameters such as `utm_*`, `fbclid`, and `gclid` so the dataset does not fill up with duplicate tracked URLs. Use `IgnoreTrackingQueryParameters = false` in C# or `-KeepTrackingQueryParameters` in PowerShell to opt out.

By default the crawler is page-oriented and only keeps `text/html` and `application/xhtml+xml` responses. Use `RestrictToAllowedContentTypes = false` in C# or `-AllowAnyContentType` in PowerShell when you intentionally want non-HTML responses included.

It also skips obvious asset/document URLs such as `*.pdf`, `*.jpg`, `*.zip`, and fonts before fetching them. Use `SkipKnownAssetUrls = false` in C# or `-AllowAssetUrls` in PowerShell when those URLs are part of the dataset you want.

If you want those assets as part of the offline dataset without treating them as pages, enable `DownloadAssets` in C# or `-DownloadAssets` in PowerShell. That saves discovered image/document URLs, stylesheet links, and CSS `url(...)` assets into `assets/` and records them in `assets.jsonl`. Without `DownloadAssets`, `assets.jsonl` is still created as part of the dataset shape, but it stays empty because the crawl is page-only.

When assets are downloaded, stored HTML snapshots are also rewritten to local relative paths by default, so an `<img>` can point at `../assets/...` instead of the original remote URL. Use `RewriteAssetReferencesToLocal = false` in C# or `-KeepRemoteAssetUrls` in PowerShell if you want to keep the original references.

Stored HTML also rewrites internal page links to local saved `.html` files by default, which makes the persisted crawl much more browsable offline. Use `RewritePageLinksToLocal = false` in C# or `-KeepRemotePageUrls` in PowerShell if you want to preserve original page URLs.

Pages that use `<base href>` are also handled correctly during crawl discovery and offline rewrite, and the saved HTML strips the original `<base>` tag so local links and assets do not get redirected back to the live site.

Each saved page also gets a sidecar `.json` manifest with its metadata, outgoing links, referenced asset URLs, and any downloaded asset file paths resolved relative to that page. The generated `index.html` links to those manifests directly.

Those per-page manifests now also include lightweight search metadata such as heading extraction, word counts, character counts, and a short summary snippet, and `index.html` surfaces the same summary information for quick offline scanning.

The persisted dataset also exports a global `chunks.jsonl` file with deduplicated text chunks, per-chunk summaries, heading context, normalized keywords, and relative links back to each saved page, text file, and manifest so it is easy to feed into local search or RAG tooling.

It also exports `graph.json`, which captures fetched pages as nodes and discovered page-to-page links as edges, including fetched/skipped/external node categories, edge relation types, in-degree/out-degree counts, and relative paths back to saved HTML and manifest files for offline analysis or navigation tooling. The generated summaries now also break those graph counts down by node category, edge relation, and skipped-node reason.
## Process completed pages

Use `HtmlCrawlOptions.PageObserver` with an implementation of
`IHtmlCrawlPageObserver.ObserveAsync` to process each completed page. The crawler
awaits the callback before committing its checkpoint. Successful and failed pages
arrive in crawl order; skipped candidates and pages loaded from a resume checkpoint
are excluded. If a callback fails, resuming retries that uncommitted page, so
external writes should tolerate retries. Pages remain in the final result; this
hook does not limit memory use.

PowerShell can emit pages while the crawl runs:

```powershell
Invoke-HtmlCrawl -Url https://example.com/docs -MaxPages 100 -StreamPages |
    Where-Object Status -eq Success |
    Select-Object Url, Title, Text
```

`-StreamPages` returns `HtmlCrawlPage` objects instead of the final
`HtmlCrawlResult`. Use `-OutPath` alongside it to keep the normal exports and
checkpoints.

Pipeline output does not acknowledge that downstream processing completed.
Export files may be committed after a page is emitted. For a sink that must
finish its work before the checkpoint advances, use the awaited C# observer.
## Release saved page content

For larger crawls, use `-ReleasePageContent` in PowerShell or
`RetainPageContent = false` in `HtmlCrawlOptions`, together with an output or resume
path. The crawler saves each completed page before clearing its HTML, text,
Markdown and HTTP cache body from the returned page object. Links, structured
data and other metadata remain available. Page files, search chunks, the offline
index and the final manifest still contain the selected content.

```powershell
$crawl = Invoke-HtmlCrawl -Url 'https://example.com/docs/' -OutPath './crawl-output' -IncludeMarkdown -ReleasePageContent
```

Completed output contains `pages/*.content.json` files with the selected bodies
and eligible HTTP cache bodies. Keep these files while using
`HtmlCrawler.SaveResultAsync` on the returned result; saving reads one page at a
time and leaves its strings empty afterward. Use `HtmlCrawler.LoadResultAsync`
to obtain a fully retained result for reading or editing content. The final
`crawl-result.json` is self-contained and can be moved without those sidecars.

This option reduces retained page bodies. Page metadata, structured data and
chunk fingerprints still grow with the crawl. Loading an existing final manifest
or a source for conditional refresh still loads its content before crawling;
this option does not limit total process memory.

With `-StreamPages -ReleasePageContent`, emitted pages keep their selected HTML,
text and Markdown while the crawler's retained page records release those bodies.
The awaited C# observer also receives the content before it is released.
