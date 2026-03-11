Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Invoke-HTMLCrawl' {
    It 'Crawls same-host links offline' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
                '/'      = "<html><head><title>Home</title></head><body><a href='/about'>About</a></body></html>"
                '/about' = "<html><head><title>About</title></head><body>About page</body></html>"
            }

            try {
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if ($responses.ContainsKey($rawUrl)) {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($responses[$rawUrl])
                        $context.Response.ContentType = 'text/html; charset=utf-8'
                        $context.Response.ContentLength64 = $bytes.Length
                        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    } else {
                        $context.Response.StatusCode = 404
                    }
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10
            $result.PageCount | Should -Be 2
            $result.Pages.Url | Should -Contain $prefix
            $result.Pages.Url | Should -Contain ($prefix + 'about')
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Uses sitemap and skips robots-blocked pages' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
                '/'           = "<html><head><title>Home</title></head><body>Home</body></html>"
                '/robots.txt' = "User-agent: *`nDisallow: /blocked`nSitemap: ${JobPrefix}sitemap.xml`n"
                '/sitemap.xml' = "<?xml version='1.0' encoding='UTF-8'?><urlset xmlns='http://www.sitemaps.org/schemas/sitemap/0.9'><url><loc>${JobPrefix}from-sitemap</loc></url><url><loc>${JobPrefix}blocked</loc></url></urlset>"
                '/from-sitemap' = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>"
                '/blocked'    = "<html><head><title>Blocked</title></head><body>Blocked page</body></html>"
            }

            try {
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if ($responses.ContainsKey($rawUrl)) {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($responses[$rawUrl])
                        $context.Response.ContentType = 'text/plain; charset=utf-8'
                        $context.Response.ContentLength64 = $bytes.Length
                        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    } else {
                        $context.Response.StatusCode = 404
                    }
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 10
            $result.PageCount | Should -Be 2
            $result.Pages.Url | Should -Contain $prefix
            $result.Pages.Url | Should -Contain ($prefix + 'from-sitemap')
            $result.SkippedPages.Url | Should -Contain ($prefix + 'blocked')
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Persists and resumes a crawl from disk' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $outputPath = Join-Path $TestDrive 'crawl-artifacts'
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
                '/'            = "<html><head><title>Home</title></head><body>Home</body></html>"
                '/robots.txt'  = "User-agent: *`nSitemap: ${JobPrefix}sitemap.xml`n"
                '/sitemap.xml' = "<?xml version='1.0' encoding='UTF-8'?><urlset xmlns='http://www.sitemaps.org/schemas/sitemap/0.9'><url><loc>${JobPrefix}from-sitemap</loc></url></urlset>"
                '/from-sitemap' = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>"
            }

            try {
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if ($responses.ContainsKey($rawUrl)) {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($responses[$rawUrl])
                        $context.Response.ContentType = 'text/plain; charset=utf-8'
                        $context.Response.ContentLength64 = $bytes.Length
                        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    } else {
                        $context.Response.StatusCode = 404
                    }
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
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
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Supports path prefix scoping and canonical URLs' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
                '/docs/index' = "<html><body><a href='/docs/page-1'>Docs</a><a href='/blog/post-1'>Blog</a></body></html>"
                '/docs/page-1' = "<html><head><link rel='canonical' href='/docs/canonical-page' /></head><body>Docs page</body></html>"
                '/blog/post-1' = "<html><body>Blog page</body></html>"
            }

            try {
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if ($responses.ContainsKey($rawUrl)) {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($responses[$rawUrl])
                        $context.Response.ContentType = 'text/html; charset=utf-8'
                        $context.Response.ContentLength64 = $bytes.Length
                        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    } else {
                        $context.Response.StatusCode = 404
                    }
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url ($prefix + 'docs/index') -MaxDepth 1 -MaxPages 10 -PathPrefix '/docs' -UseCanonicalUrls
            $result.PageCount | Should -Be 2
            $result.SkippedPages | Where-Object { $_.SkipReason -eq 'OutsidePathScope' } | Should -Not -BeNullOrEmpty
            ($result.Pages | Where-Object { $_.RequestedUrl -eq ($prefix + 'docs/page-1') }).Url | Should -Be ($prefix + 'docs/canonical-page')
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Can skip duplicate-content pages' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
                '/' = "<html><body><a href='/copy-a'>Copy A</a><a href='/copy-b'>Copy B</a><a href='/unique'>Unique</a></body></html>"
                '/copy-a' = "<html><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>"
                '/copy-b' = "<html><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>"
                '/unique' = "<html><body><main><h1>Different</h1></main></body></html>"
            }

            try {
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if ($responses.ContainsKey($rawUrl)) {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($responses[$rawUrl])
                        $context.Response.ContentType = 'text/html; charset=utf-8'
                        $context.Response.ContentLength64 = $bytes.Length
                        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    } else {
                        $context.Response.StatusCode = 404
                    }
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -Selector 'main' -DeduplicatePages
            $result.PageCount | Should -Be 3
            $result.Pages.Url | Should -Contain ($prefix + 'copy-a')
            $result.Pages.Url | Should -Contain ($prefix + 'unique')
            $result.Pages.Url | Should -Not -Contain ($prefix + 'copy-b')
            ($result.SkippedPages | Where-Object { $_.SkipReason -eq 'DuplicateContent' }).Url | Should -Contain ($prefix + 'copy-b')
            $result.Summary.DuplicatePageCount | Should -Be 1
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Can keep tracking query parameters when requested' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
                '/' = "<html><body><a href='/page?utm_source=newsletter'>Tracked A</a><a href='/page?fbclid=12345'>Tracked B</a></body></html>"
                '/page?utm_source=newsletter' = "<html><body>Tracked A</body></html>"
                '/page?fbclid=12345' = "<html><body>Tracked B</body></html>"
            }

            try {
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if ($responses.ContainsKey($rawUrl)) {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($responses[$rawUrl])
                        $context.Response.ContentType = 'text/html; charset=utf-8'
                        $context.Response.ContentLength64 = $bytes.Length
                        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    } else {
                        $context.Response.StatusCode = 404
                    }
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -KeepTrackingQueryParameters
            $result.PageCount | Should -Be 3
            $result.Pages.Url | Should -Contain ($prefix + 'page?utm_source=newsletter')
            $result.Pages.Url | Should -Contain ($prefix + 'page?fbclid=12345')
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Can allow non-html content types when requested' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
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
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if (-not $responses.ContainsKey($rawUrl)) {
                        $context.Response.StatusCode = 404
                        $context.Response.OutputStream.Close()
                        continue
                    }

                    $response = $responses[$rawUrl]
                    $bytes = [System.Text.Encoding]::UTF8.GetBytes($response.Body)
                    $context.Response.ContentType = $response.ContentType
                    $context.Response.ContentLength64 = $bytes.Length
                    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -AllowAnyContentType -AllowAssetUrls
            $result.PageCount | Should -Be 2
            ($result.Pages | Where-Object { $_.Url -eq ($prefix + 'file.pdf') }).ContentType | Should -Be 'application/pdf'
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Can download assets into the crawl dataset' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $outputPath = Join-Path $TestDrive 'crawl-assets'
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
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
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if (-not $responses.ContainsKey($rawUrl)) {
                        $context.Response.StatusCode = 404
                        $context.Response.OutputStream.Close()
                        continue
                    }

                    $response = $responses[$rawUrl]
                    $bytes = [System.Text.Encoding]::UTF8.GetBytes($response.Body)
                    $context.Response.ContentType = $response.ContentType
                    $context.Response.ContentLength64 = $bytes.Length
                    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 0 -MaxPages 5 -DownloadAssets -OutPath $outputPath
            $result.AssetCount | Should -Be 6
            $result.Assets.FilePath | ForEach-Object { Test-Path $_ | Should -BeTrue }
            (Test-Path (Join-Path $outputPath 'assets.jsonl')) | Should -BeTrue
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
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Rewrites internal page links to local files in saved HTML' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $outputPath = Join-Path $TestDrive 'crawl-local-links'
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
                '/' = "<html><body><a href='/about'>About</a><a href='https://example.com/remote'>Remote</a></body></html>"
                '/about' = "<html><body>About page</body></html>"
            }

            try {
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if (-not $responses.ContainsKey($rawUrl)) {
                        $context.Response.StatusCode = 404
                        $context.Response.OutputStream.Close()
                        continue
                    }

                    $bytes = [System.Text.Encoding]::UTF8.GetBytes($responses[$rawUrl])
                    $context.Response.ContentType = 'text/html; charset=utf-8'
                    $context.Response.ContentLength64 = $bytes.Length
                    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
            $result = Invoke-HTMLCrawl -Url $prefix -MaxDepth 1 -MaxPages 10 -OutPath $outputPath
            $homePage = $result.Pages | Where-Object { $_.Url -eq $prefix } | Select-Object -First 1
            $about = $result.Pages | Where-Object { $_.Url -eq ($prefix + 'about') } | Select-Object -First 1
            $html = Get-Content $homePage.HtmlPath -Raw
            $indexHtml = Get-Content $result.IndexHtmlPath -Raw
            $expected = [System.Uri]::UnescapeDataString(([System.Uri]((Split-Path $homePage.HtmlPath -Parent) + [System.IO.Path]::DirectorySeparatorChar)).MakeRelativeUri([System.Uri]$about.HtmlPath).ToString()).Replace('\', '/')

            $html | Should -Match ([regex]::Escape($expected))
            $html | Should -Match 'https://example\.com/remote'
            $indexHtml | Should -Match ([regex]::Escape([System.IO.Path]::GetFileName($homePage.HtmlPath)))
            $indexHtml | Should -Match ([regex]::Escape([System.IO.Path]::GetFileName($about.HtmlPath)))
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }

    It 'Honors base href for discovered links, assets, and offline rewrites' {
        $tcpListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $tcpListener.Start()
        $port = ([System.Net.IPEndPoint]$tcpListener.LocalEndpoint).Port
        $tcpListener.Stop()

        $prefix = "http://localhost:$port/"
        $outputPath = Join-Path $TestDrive 'crawl-base-href'
        $job = Start-Job -ScriptBlock {
            param($JobPrefix)

            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add($JobPrefix)
            $listener.Start()

            $responses = @{
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
                while ($listener.IsListening) {
                    $pending = $listener.GetContextAsync()
                    if (-not $pending.Wait(3000)) {
                        break
                    }
                    $context = $pending.Result
                    $rawUrl = if ($context.Request.RawUrl) { $context.Request.RawUrl } else { '/' }
                    if (-not $responses.ContainsKey($rawUrl)) {
                        $context.Response.StatusCode = 404
                        $context.Response.OutputStream.Close()
                        continue
                    }

                    $response = $responses[$rawUrl]
                    $bytes = [System.Text.Encoding]::UTF8.GetBytes($response.Body)
                    $context.Response.ContentType = $response.ContentType
                    $context.Response.ContentLength64 = $bytes.Length
                    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                    $context.Response.OutputStream.Close()
                }
            } finally {
                $listener.Stop()
                $listener.Close()
            }
        } -ArgumentList $prefix

        try {
            Start-Sleep -Milliseconds 200
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
            $expected = [System.Uri]::UnescapeDataString(([System.Uri]((Split-Path $homePage.HtmlPath -Parent) + [System.IO.Path]::DirectorySeparatorChar)).MakeRelativeUri([System.Uri]$guidePage.HtmlPath).ToString()).Replace('\', '/')

            $html | Should -Not -Match '<base'
            $html | Should -Match ([regex]::Escape($expected))
            $html | Should -Match '\.\./assets/'
            (Test-Path $result.ChunksJsonlPath) | Should -BeTrue
            (Test-Path $result.GraphJsonPath) | Should -BeTrue
            $result.Summary.ChunkCount | Should -BeGreaterThan 0
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
            $indexHtml | Should -Match 'Chunks JSONL'
            $indexHtml | Should -Match 'Graph JSON'
            $indexHtml | Should -Match 'Graph Summary'
            $indexHtml | Should -Match 'Node category'
            $indexHtml | Should -Match 'Edge relation'
            $indexHtml | Should -Match 'Skipped-node reason'
        } finally {
            $null = Receive-Job -Job $job -Wait -AutoRemoveJob
        }
    }
}
