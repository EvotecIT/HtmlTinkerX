BeforeAll {
    Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force
}

Describe 'Page discovery parsing cmdlets' {
    BeforeAll {
        $script:Html = @'
<!doctype html>
<html>
<head>
    <script type="application/json" id="settings">{"enabled":true}</script>
    <link rel="preload" as="image" href="/preload.png" />
</head>
<body>
    <picture>
        <source type="image/webp" srcset="/hero.webp 1x, /hero@2x.webp 2x" />
        <img src="/hero.jpg" srcset="/hero-small.jpg 480w, /hero-large.jpg 960w" sizes="100vw" alt="Hero" />
    </picture>
</body>
</html>
'@
        $script:Manifest = @'
{
  "name": "Example App",
  "short_name": "Example",
  "start_url": "/app",
  "icons": [
    { "src": "/icon-192.png", "sizes": "192x192", "type": "image/png" }
  ]
}
'@
        $script:SecurityTxt = @'
Contact: /security
Expires: 2026-12-31T23:59:59Z
'@
    }

    It 'extracts generic script JSON data' {
        $items = ConvertFrom-HtmlScriptData -Content $script:Html

        $items | Should -HaveCount 1
        $items[0].Id | Should -Be 'settings'
        $items[0].IsJson | Should -BeTrue
    }

    It 'extracts image candidates' {
        $images = ConvertFrom-HtmlImageCandidate -Content $script:Html -BaseUrl 'https://example.org/page'

        $images.Url | Should -Contain 'https://example.org/hero.jpg'
        $images.WidthDescriptor | Should -Contain '480w'
        $images.PixelDensityDescriptor | Should -Contain '2x'
        $images.Url | Should -Contain 'https://example.org/preload.png'
    }

    It 'parses web manifests' {
        $manifest = ConvertFrom-WebManifest -Content $script:Manifest -BaseUrl 'https://example.org/manifest.webmanifest'

        $manifest.Name | Should -Be 'Example App'
        $manifest.StartUrl | Should -Be 'https://example.org/app'
        $manifest.Icons | Should -HaveCount 1
    }

    It 'parses well-known text files' {
        $records = ConvertFrom-WellKnownText -Content $script:SecurityTxt -Kind SecurityTxt -BaseUrl 'https://example.org/.well-known/security.txt'

        $records | Should -HaveCount 2
        ($records | Where-Object Field -EQ 'Contact').Url | Should -Be 'https://example.org/security'
    }

    It 'exports the new commands and aliases' {
        Get-Command ConvertFrom-HtmlLinkedJavaScriptEndpoint | Should -Not -BeNullOrEmpty
        Get-Command ConvertFrom-HtmlLinkedJSEndpoint | Should -Not -BeNullOrEmpty
        Get-Command ConvertFrom-HtmlScriptData | Should -Not -BeNullOrEmpty
        Get-Command ConvertFrom-WebManifest | Should -Not -BeNullOrEmpty
        Get-Command ConvertFrom-WellKnownText | Should -Not -BeNullOrEmpty
    }
}
