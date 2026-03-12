Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Invoke-HTMLCrawl' {
    BeforeAll {
        if (-not ('PesterTestHttpServer' -as [type])) {
            Add-Type -TypeDefinition @"
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class PesterTestHttpServer : IDisposable {
    private sealed class ServerResponse {
        public string Body { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/html; charset=utf-8";
    }

    private readonly HttpListener _listener = new HttpListener();
    private readonly ConcurrentDictionary<string, ServerResponse> _responses = new ConcurrentDictionary<string, ServerResponse>(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    private readonly Task _serverTask;

    public PesterTestHttpServer(string prefix) {
        Prefix = prefix;
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _serverTask = Task.Run(ListenAsync);
    }

    public string Prefix { get; }

    public void AddResponse(string path, string body, string contentType) {
        _responses[path] = new ServerResponse {
            Body = body ?? string.Empty,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "text/html; charset=utf-8" : contentType
        };
    }

    private async Task ListenAsync() {
        while (!_cancellation.IsCancellationRequested) {
            HttpListenerContext context;

            try {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            } catch (HttpListenerException) when (_cancellation.IsCancellationRequested || !_listener.IsListening) {
                break;
            } catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) {
                break;
            }

            string rawUrl = string.IsNullOrWhiteSpace(context.Request.RawUrl) ? "/" : context.Request.RawUrl;
            if (!_responses.TryGetValue(rawUrl, out ServerResponse response)) {
                context.Response.StatusCode = 404;
                context.Response.Close();
                continue;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(response.Body);
            context.Response.ContentType = response.ContentType;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }
    }

    public void Dispose() {
        _cancellation.Cancel();
        try {
            if (_listener.IsListening) {
                _listener.Stop();
            }
        } catch {
        }

        _listener.Close();

        try {
            _serverTask.Wait(TimeSpan.FromSeconds(5));
        } catch {
        }

        _cancellation.Dispose();
    }
}
"@
        }

        function New-TestServerPrefix {
            $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $tcpListener.Start()
            $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
            $tcpListener.Stop()

            "http://127.0.0.1:$port/"
        }

        function Start-TestHttpServer {
            param(
                [Parameter(Mandatory)]
                [hashtable] $Responses,
                [string] $Prefix = (New-TestServerPrefix)
            )

            $server = [PesterTestHttpServer]::new($Prefix)
            foreach ($entry in $Responses.GetEnumerator()) {
                $contentType = 'text/html; charset=utf-8'
                $body = ''

                if ($entry.Value -is [hashtable]) {
                    $body = [string] $entry.Value.Body
                    if ($entry.Value.ContainsKey('ContentType')) {
                        $contentType = [string] $entry.Value.ContentType
                    }
                } else {
                    $body = [string] $entry.Value
                }

                $server.AddResponse([string] $entry.Key, $body, $contentType)
            }

            $server
        }

        function Stop-TestHttpServer {
            param(
                [Parameter(Mandatory)]
                $Server
            )

            try {
                $Server.Dispose()
            } catch {
            }
        }

        function Get-ExpectedRelativePath {
            param(
                [Parameter(Mandatory)]
                [string] $FromFilePath,
                [Parameter(Mandatory)]
                [string] $ToFilePath
            )

            $fromDirectory = Split-Path $FromFilePath -Parent
            [System.IO.Path]::GetRelativePath($fromDirectory, $ToFilePath).Replace('\', '/')
        }
    }

    It 'Crawls same-host links offline' {
        $server = Start-TestHttpServer -Responses @{
            '/'      = "<html><head><title>Home</title></head><body><a href='/about'>About</a></body></html>"
            '/about' = "<html><head><title>About</title></head><body>About page</body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10
            $result.PageCount | Should -Be 2
            $result.Pages.Url | Should -Contain $prefix
            $result.Pages.Url | Should -Contain ($prefix + 'about')
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Uses sitemap and skips robots-blocked pages' {
        $prefix = New-TestServerPrefix
        $server = Start-TestHttpServer -Prefix $prefix -Responses @{
            '/'            = "<html><head><title>Home</title></head><body>Home</body></html>"
            '/robots.txt'  = @{
                Body = "User-agent: *`nDisallow: /blocked`nSitemap: ${prefix}sitemap.xml`n"
                ContentType = 'text/plain; charset=utf-8'
            }
            '/sitemap.xml' = @{
                Body = "<?xml version='1.0' encoding='UTF-8'?><urlset xmlns='http://www.sitemaps.org/schemas/sitemap/0.9'><url><loc>${prefix}from-sitemap</loc></url><url><loc>${prefix}blocked</loc></url></urlset>"
                ContentType = 'text/plain; charset=utf-8'
            }
            '/from-sitemap' = @{
                Body = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>"
                ContentType = 'text/plain; charset=utf-8'
            }
            '/blocked' = @{
                Body = "<html><head><title>Blocked</title></head><body>Blocked page</body></html>"
                ContentType = 'text/plain; charset=utf-8'
            }
        }

        try {
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 10
            $result.PageCount | Should -Be 2
            $result.Pages.Url | Should -Contain $prefix
            $result.Pages.Url | Should -Contain ($prefix + 'from-sitemap')
            $result.SkippedPages.Url | Should -Contain ($prefix + 'blocked')
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can exclude configured selectors from stored content and text' {
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><main><h1>Hello</h1><p>World</p><div class='blog-grid'>Read More</div><div class='language-switcher'>Polish</div></main></body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -Selector 'main' -ExcludeSelector '.blog-grid', '.language-switcher'
            $page = $result.Pages | Select-Object -First 1

            $page.Html | Should -Match 'Hello'
            $page.Text | Should -Match 'World'
            $page.Html | Should -Not -Match 'Read More'
            $page.Text | Should -Not -Match 'Read More'
            $page.Html | Should -Not -Match 'Polish'
            $page.Text | Should -Not -Match 'Polish'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can exclude configured classes and ids from stored content and text' {
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><main><h1>Hello</h1><p>World</p><div class='promo-box'>Sign up</div><div id='reader-tools'>Tools</div></main></body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -Selector 'main' -ExcludeClass 'promo-box' -ExcludeId 'reader-tools' -DisableSmartContentCleanup
            $page = $result.Pages | Select-Object -First 1

            $page.Html | Should -Match 'Hello'
            $page.Text | Should -Match 'World'
            $page.Html | Should -Not -Match 'Sign up'
            $page.Text | Should -Not -Match 'Sign up'
            $page.Html | Should -Not -Match 'Tools'
            $page.Text | Should -Not -Match 'Tools'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can remove low-value in-content boilerplate automatically' {
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><main><article><h1>Hello</h1><p>World content for the actual page body with enough text to keep.</p></article><div class='related-posts'><a href='/one'>One</a><a href='/two'>Two</a><a href='/three'>Three</a><a href='/four'>Four</a></div><div class='language-switcher'><a href='/pl'>Polish</a><a href='/de'>German</a><a href='/fr'>French</a></div></main></body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -Selector 'main'
            $page = $result.Pages | Select-Object -First 1

            $page.Html | Should -Match 'Hello'
            $page.Text | Should -Match 'World content'
            $page.Text | Should -Not -Match 'Polish'
            $page.Text | Should -Not -Match 'One'
            $page.Html | Should -Not -Match 'related-posts'
            $page.Html | Should -Not -Match 'language-switcher'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can apply a built-in crawl profile' {
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><div id='main'><h1>Hello</h1><p>World</p><div class='sharing-popup'>Share</div><div class='wpml-ls-statics-footer'>Polish</div></div></body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -Profile 'evotec-xyz'
            $page = $result.Pages | Select-Object -First 1

            $result.AppliedProfileName | Should -Be 'evotec-xyz'
            $page.Html | Should -Match 'Hello'
            $page.Text | Should -Match 'World'
            $page.Html | Should -Not -Match 'Share'
            $page.Text | Should -Not -Match 'Polish'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can auto-apply the generic WordPress profile from page markers' {
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><head><meta name='generator' content='WordPress 6.8' /><link rel='stylesheet' href='/wp-content/themes/site/style.css' /></head><body><main><h1>Hello</h1><p>World</p><div class='sharing-popup'>Share</div><div class='wpml-ls-statics-footer'>Polish</div></main></body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -AutoProfile
            $page = $result.Pages | Select-Object -First 1

            $result.AppliedProfileName | Should -Be 'wordpress-content'
            $page.Html | Should -Match 'Hello'
            $page.Text | Should -Match 'World'
            $page.Html | Should -Not -Match 'Share'
            $page.Text | Should -Not -Match 'Polish'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can load custom crawl profiles from JSON' {
        $profilePath = Join-Path $TestDrive 'crawl-profiles.json'
        @'
[
  {
    "name": "custom-docs",
    "hosts": [ "127.0.0.1" ],
    "selector": "article",
    "excludeSelectors": [ ".sidebar", ".feedback-box" ]
  }
]
'@ | Set-Content -Path $profilePath

        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><article><h1>Hello</h1><p>World</p><div class='feedback-box'>Feedback</div></article><div class='sidebar'>Sidebar</div></body></html>"
        }

        try {
            $prefix = $server.Prefix
            $named = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -Profile 'custom-docs' -ProfilePath $profilePath
            $automatic = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -AutoProfile -ProfilePath $profilePath

            $named.AppliedProfileName | Should -Be 'custom-docs'
            $automatic.AppliedProfileName | Should -Be 'custom-docs'
            $named.Pages[0].Text | Should -Not -Match 'Sidebar'
            $automatic.Pages[0].Text | Should -Not -Match 'Feedback'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Rejects unknown crawl profile names with available values' {
        $thrown = {
            Invoke-HTMLCrawl -Url 'https://example.com/' -Profile 'missing-profile'
        } | Should -Throw -PassThru

        $thrown.Exception.Message | Should -Match 'evotec-xyz'
        $thrown.Exception.Message | Should -Match 'wordpress-content'
    }

    It 'Reports custom profile names when a profile file is provided' {
        $profilePath = Join-Path $TestDrive 'custom-only-profiles.json'
        @'
{
  "profiles": [
    {
      "name": "custom-docs",
      "selector": "article"
    }
  ]
}
'@ | Set-Content -Path $profilePath

        $thrown = {
            Invoke-HTMLCrawl -Url 'https://example.com/' -Profile 'missing-profile' -ProfilePath $profilePath
        } | Should -Throw -PassThru

        $thrown.Exception.Message | Should -Match 'custom-docs'
    }

    It 'Persists and resumes a crawl from disk' {
        $outputPath = Join-Path $TestDrive 'crawl-artifacts'
        $prefix = New-TestServerPrefix
        $server = Start-TestHttpServer -Prefix $prefix -Responses @{
            '/'            = "<html><head><title>Home</title></head><body>Home</body></html>"
            '/robots.txt'  = @{
                Body = "User-agent: *`nSitemap: ${prefix}sitemap.xml`n"
                ContentType = 'text/plain; charset=utf-8'
            }
            '/sitemap.xml' = @{
                Body = "<?xml version='1.0' encoding='UTF-8'?><urlset xmlns='http://www.sitemaps.org/schemas/sitemap/0.9'><url><loc>${prefix}from-sitemap</loc></url></urlset>"
                ContentType = 'text/plain; charset=utf-8'
            }
            '/from-sitemap' = @{
                Body = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>"
                ContentType = 'text/plain; charset=utf-8'
            }
        }

        try {
            $partial = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 1 -OutPath $outputPath
            $partial.PageCount | Should -Be 1
            $partial.PendingPages.Count | Should -BeGreaterThan 0

            $resumed = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 10 -ResumePath $outputPath -OutPath $outputPath
            $resumed.PageCount | Should -Be 2
            $resumed.Pages.Url | Should -Contain ($prefix + 'from-sitemap')
            (Test-Path (Join-Path $outputPath 'crawl-result.json')) | Should -BeTrue
            (Test-Path (Join-Path $outputPath 'pages.jsonl')) | Should -BeTrue
            (Test-Path (Join-Path $outputPath 'pages.csv')) | Should -BeTrue
            (Test-Path (Join-Path $outputPath 'skipped-pages.jsonl')) | Should -BeTrue
            (Test-Path (Join-Path $outputPath 'links.jsonl')) | Should -BeTrue
            (Test-Path (Join-Path $outputPath 'summary.json')) | Should -BeTrue
            (Test-Path (Join-Path $outputPath 'summary.txt')) | Should -BeTrue
            (Get-Content (Join-Path $outputPath 'summary.txt') -Raw) | Should -Match 'Sitemap sources:'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Supports path prefix scoping and canonical URLs' {
        $server = Start-TestHttpServer -Responses @{
            '/docs/index' = "<html><body><a href='/docs/page-1'>Docs</a><a href='/blog/post-1'>Blog</a></body></html>"
            '/docs/page-1' = "<html><head><link rel='canonical' href='/docs/canonical-page' /></head><body>Docs page</body></html>"
            '/blog/post-1' = "<html><body>Blog page</body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url ($prefix + 'docs/index') -MaxDepth 1 -MaxPages 10 -PathPrefix '/docs' -UseCanonicalUrls
            $result.PageCount | Should -Be 2
            $result.SkippedPages | Where-Object { $_.SkipReason -eq 'OutsidePathScope' } | Should -Not -BeNullOrEmpty
            ($result.Pages | Where-Object { $_.RequestedUrl -eq ($prefix + 'docs/page-1') }).Url | Should -Be ($prefix + 'docs/canonical-page')
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can skip duplicate-content pages' {
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><a href='/copy-a'>Copy A</a><a href='/copy-b'>Copy B</a><a href='/unique'>Unique</a></body></html>"
            '/copy-a' = "<html><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>"
            '/copy-b' = "<html><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>"
            '/unique' = "<html><body><main><h1>Different</h1></main></body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -Selector 'main' -DeduplicatePages
            $result.PageCount | Should -Be 3
            $result.Pages.Url | Should -Contain ($prefix + 'copy-a')
            $result.Pages.Url | Should -Contain ($prefix + 'unique')
            $result.Pages.Url | Should -Not -Contain ($prefix + 'copy-b')
            ($result.SkippedPages | Where-Object { $_.SkipReason -eq 'DuplicateContent' }).Url | Should -Contain ($prefix + 'copy-b')
            $result.Summary.DuplicatePageCount | Should -Be 1
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can keep tracking query parameters when requested' {
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><a href='/page?utm_source=newsletter'>Tracked A</a><a href='/page?fbclid=12345'>Tracked B</a></body></html>"
            '/page?utm_source=newsletter' = "<html><body>Tracked A</body></html>"
            '/page?fbclid=12345' = "<html><body>Tracked B</body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -KeepTrackingQueryParameters
            $result.PageCount | Should -Be 3
            $result.Pages.Url | Should -Contain ($prefix + 'page?utm_source=newsletter')
            $result.Pages.Url | Should -Contain ($prefix + 'page?fbclid=12345')
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can allow non-html content types when requested' {
        $server = Start-TestHttpServer -Responses @{
            '/' = @{
                Body = "<html><body><a href='/file.pdf'>PDF</a></body></html>"
                ContentType = 'text/html; charset=utf-8'
            }
            '/file.pdf' = @{
                Body = '%PDF-1.7 fake'
                ContentType = 'application/pdf'
            }
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -AllowAnyContentType -AllowAssetUrls
            $result.PageCount | Should -Be 2
            ($result.Pages | Where-Object { $_.Url -eq ($prefix + 'file.pdf') }).ContentType | Should -Be 'application/pdf'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Can download assets into the crawl dataset' {
        $outputPath = Join-Path $TestDrive 'crawl-assets'
        $server = Start-TestHttpServer -Responses @{
            '/' = @{
                Body = "<html><head><link rel='stylesheet' href='/css/site.css' /><style>.hero{background-image:url('/images/bg.png');}</style></head><body><img src='/images/logo.png' alt='Logo' /><div style=""background-image:url('/images/card.png')""></div><a href='/files/manual.pdf'>Manual</a></body></html>"
                ContentType = 'text/html; charset=utf-8'
            }
            '/css/site.css' = @{
                Body = "@import '/css/theme.css'; .page{background-image:url('/images/logo.png'); color:#333;}"
                ContentType = 'text/css'
            }
            '/css/theme.css' = @{
                Body = ".theme{background-image:url('/images/bg.png');}"
                ContentType = 'text/css'
            }
            '/images/logo.png' = @{
                Body = 'fake-png'
                ContentType = 'image/png'
            }
            '/images/bg.png' = @{
                Body = 'fake-bg'
                ContentType = 'image/png'
            }
            '/images/card.png' = @{
                Body = 'fake-card'
                ContentType = 'image/png'
            }
            '/files/manual.pdf' = @{
                Body = '%PDF-1.7 fake'
                ContentType = 'application/pdf'
            }
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 5 -DownloadAssets -OutPath $outputPath
            $result.AssetCount | Should -Be 6
            $result.Assets.FilePath | ForEach-Object { Test-Path $_ | Should -BeTrue }
            (Test-Path (Join-Path $outputPath 'assets.jsonl')) | Should -BeTrue
            (Test-Path (Join-Path $outputPath 'skipped-assets.jsonl')) | Should -BeTrue
            (Test-Path $result.IndexHtmlPath) | Should -BeTrue
            (Get-Content $result.Pages[0].HtmlPath -Raw) | Should -Match '\.\./assets/'
            $stylesheet = $result.Assets | Where-Object { $_.Url -eq ($prefix + 'css/site.css') } | Select-Object -First 1
            $css = Get-Content $stylesheet.FilePath -Raw
            $css | Should -Not -Match '/images/logo\.png'
            $css | Should -Not -Match '/css/theme\.css'
            $indexHtml = Get-Content $result.IndexHtmlPath -Raw
            $indexHtml | Should -Match 'Pages CSV'
            $indexHtml | Should -Match 'assets/site-'
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Rewrites internal page links to local files in saved HTML' {
        $outputPath = Join-Path $TestDrive 'crawl-local-links'
        $server = Start-TestHttpServer -Responses @{
            '/' = "<html><body><a href='/about'>About</a><a href='https://example.com/remote'>Remote</a></body></html>"
            '/about' = "<html><body>About page</body></html>"
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -OutPath $outputPath
            $homePage = $result.Pages | Where-Object { $_.Url -eq $prefix } | Select-Object -First 1
            $about = $result.Pages | Where-Object { $_.Url -eq ($prefix + 'about') } | Select-Object -First 1
            $html = Get-Content $homePage.HtmlPath -Raw
            $indexHtml = Get-Content $result.IndexHtmlPath -Raw
            $expected = Get-ExpectedRelativePath -FromFilePath $homePage.HtmlPath -ToFilePath $about.HtmlPath

            $html | Should -Match ([regex]::Escape($expected))
            $html | Should -Match 'https://example\.com/remote'
            $indexHtml | Should -Match ([regex]::Escape([System.IO.Path]::GetFileName($homePage.HtmlPath)))
            $indexHtml | Should -Match ([regex]::Escape([System.IO.Path]::GetFileName($about.HtmlPath)))
        } finally {
            Stop-TestHttpServer $server
        }
    }

    It 'Honors base href for discovered links, assets, and offline rewrites' {
        $outputPath = Join-Path $TestDrive 'crawl-base-href'
        $server = Start-TestHttpServer -Responses @{
            '/' = @{
                Body = "<html><head><title>Offline Home</title><base href='/docs/' /><link rel='stylesheet' href='css/site.css' /></head><body><h1>Offline Home</h1><p>Useful docs for offline testing and local search metadata.</p><a href='guide'>Guide</a><a href='manual.pdf'>Manual</a><a href='https://example.com/offsite'>Offsite</a><img src='images/logo.png' alt='Logo' /></body></html>"
                ContentType = 'text/html; charset=utf-8'
            }
            '/docs/guide' = @{
                Body = '<html><body><h1>Guide page</h1><p>Guide content for offline browsing.</p></body></html>'
                ContentType = 'text/html; charset=utf-8'
            }
            '/docs/css/site.css' = @{
                Body = ".hero{background-image:url('../images/logo.png');}"
                ContentType = 'text/css'
            }
            '/docs/images/logo.png' = @{
                Body = 'fake-png'
                ContentType = 'image/png'
            }
        }

        try {
            $prefix = $server.Prefix
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -DownloadAssets -OutPath $outputPath
            $homePage = $result.Pages | Where-Object { $_.Url -eq $prefix } | Select-Object -First 1
            $guidePage = $result.Pages | Where-Object { $_.Url -eq ($prefix + 'docs/guide') } | Select-Object -First 1
            $manifest = Get-Content $homePage.ManifestPath -Raw | ConvertFrom-Json
            $homePage.Links | Should -Contain ($prefix + 'docs/guide')
            $homePage.Links | Should -Contain ($prefix + 'docs/manual.pdf')
            $homePage.Links | Should -Contain 'https://example.com/offsite'
            $homePage.AssetUrls | Should -Contain ($prefix + 'docs/css/site.css')
            $homePage.AssetUrls | Should -Contain ($prefix + 'docs/images/logo.png')
            $html = Get-Content $homePage.HtmlPath -Raw
            $indexHtml = Get-Content $result.IndexHtmlPath -Raw
            $chunksJson = Get-Content $result.ChunksJsonlPath -Raw
            $graph = Get-Content $result.GraphJsonPath -Raw | ConvertFrom-Json
            $expected = Get-ExpectedRelativePath -FromFilePath $homePage.HtmlPath -ToFilePath $guidePage.HtmlPath

            $html | Should -Not -Match '<base'
            $html | Should -Match ([regex]::Escape($expected))
            $html | Should -Match '\.\./assets/'
            (Test-Path $result.ChunksJsonlPath) | Should -BeTrue
            (Test-Path $result.GraphJsonPath) | Should -BeTrue
            $result.Summary.ChunkCount | Should -BeGreaterThan 0
            $result.SkippedContentPageCount | Should -Be 1
            $result.SkippedAssetCount | Should -Be 1
            $result.Summary.SkippedContentPageCount | Should -Be 1
            $result.Summary.SkippedAssetCount | Should -Be 1
            (Test-Path $result.SkippedAssetsJsonlPath) | Should -BeTrue
            $result.GraphNodeCount | Should -Be 4
            $result.GraphEdgeCount | Should -Be 3
            $result.GraphFetchedNodeCount | Should -Be 2
            $result.GraphSkippedNodeCount | Should -Be 1
            $result.GraphExternalNodeCount | Should -Be 1
            $result.Summary.GraphNodeCount | Should -Be 4
            $result.Summary.GraphEdgeCount | Should -Be 3
            $result.Summary.GraphFetchedNodeCount | Should -Be 2
            $result.Summary.GraphSkippedNodeCount | Should -Be 1
            $result.Summary.GraphExternalNodeCount | Should -Be 1
            $result.GraphNodeCategories.Fetched | Should -Be 2
            $result.GraphNodeCategories.Skipped | Should -Be 1
            $result.GraphNodeCategories.External | Should -Be 1
            $result.GraphEdgeRelations.fetched | Should -Be 1
            $result.GraphEdgeRelations.skipped | Should -Be 1
            $result.GraphEdgeRelations.external | Should -Be 1
            $result.GraphSkippedNodeReasons.AssetPath | Should -Be 1
            $result.Summary.GraphNodeCategories.Fetched | Should -Be 2
            $result.Summary.GraphEdgeRelations.external | Should -Be 1
            $result.Summary.GraphSkippedNodeReasons.AssetPath | Should -Be 1
            $manifest.PageFiles.HtmlPath | Should -Be ([System.IO.Path]::GetFileName($homePage.HtmlPath))
            $manifest.Search.Headings[0] | Should -Be 'Offline Home'
            $manifest.Search.WordCount | Should -BeGreaterThan 7
            $manifest.Search.ChunkCount | Should -BeGreaterThan 0
            $manifest.Search.Summary | Should -Match 'Useful docs for offline testing'
            $manifest.Search.Keywords | Should -Contain 'offline'
            $manifest.Search.Keywords | Should -Contain 'testing'
            ($manifest.Links | Where-Object { $_.Url -eq ($prefix + 'docs/guide') }).LocalPagePath | Should -Be ([System.IO.Path]::GetFileName($guidePage.HtmlPath))
            ($manifest.ReferencedAssets | Where-Object { $_.Url -eq ($prefix + 'docs/images/logo.png') }).LocalFilePath | Should -Match '^\.\./assets/'
            $chunksJson | Should -Match '"ChunkId"'
            $chunksJson.Replace('\', '/') | Should -Match '"ManifestPath":"pages/'
            $chunksJson | Should -Match '"Keywords":\["offline"'
            $graph.Nodes.Count | Should -Be 4
            $graph.Edges.Count | Should -Be 3
            ($graph.Nodes | Where-Object { $_.Url -eq $prefix }).Category | Should -Be 'Fetched'
            ($graph.Nodes | Where-Object { $_.Url -eq ($prefix + 'docs/manual.pdf') }).Category | Should -Be 'Skipped'
            ($graph.Nodes | Where-Object { $_.Url -eq ($prefix + 'docs/manual.pdf') }).SkipReason | Should -Be 'AssetPath'
            ($graph.Nodes | Where-Object { $_.Url -eq 'https://example.com/offsite' }).Category | Should -Be 'External'
            ($graph.Edges | Where-Object { $_.TargetUrl -eq ($prefix + 'docs/guide') }).Relation | Should -Be 'fetched'
            ($graph.Edges | Where-Object { $_.TargetUrl -eq ($prefix + 'docs/manual.pdf') }).Relation | Should -Be 'skipped'
            ($graph.Edges | Where-Object { $_.TargetUrl -eq 'https://example.com/offsite' }).Relation | Should -Be 'external'
            $indexHtml | Should -Match 'Offline Home'
            $indexHtml | Should -Match 'Useful docs for offline testing'
            $indexHtml | Should -Match 'keywords: offline'
            $indexHtml | Should -Match 'Render Summary'
            $indexHtml | Should -Match 'Render mode'
            $indexHtml | Should -Match 'StaticRenderDisabled'
            $indexHtml | Should -Match 'Chunks JSONL'
            $indexHtml | Should -Match 'Graph JSON'
            $indexHtml | Should -Match 'Graph Summary'
            $indexHtml | Should -Match 'Node category'
            $indexHtml | Should -Match 'Edge relation'
            $indexHtml | Should -Match 'Skipped-node reason'
            $indexHtml | Should -Match 'Skipped Pages'
            $indexHtml | Should -Match 'Skipped Assets'
        } finally {
            Stop-TestHttpServer $server
        }
    }
}
