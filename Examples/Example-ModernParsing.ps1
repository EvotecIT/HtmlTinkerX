Import-Module .\PSParseHTML.psd1 -Force

$Html = @'
<!doctype html>
<html>
<head>
    <link rel="canonical" href="/products/widget" />
    <link rel="alternate" type="application/rss+xml" href="/feed.xml" />
    <link rel="preload" as="image" href="/images/widget-preload.png" />
    <meta name="csrf-token" content="meta-token-123" />
    <script type="application/json" id="settings">
    {"featureFlags":{"checkout":true}}
    </script>
    <script type="application/ld+json">
    {
        "@context": "https://schema.org",
        "@type": "Product",
        "@id": "https://example.org/products/widget",
        "name": "Widget"
    }
    </script>
    <script id="__NEXT_DATA__" type="application/json">
    {"props":{"pageProps":{"productId":42}}}
    </script>
    <script>
    self.__next_f = self.__next_f || [];
    self.__next_f.push([1, "1:{\"name\":\"Widget\"}\n"]);
    fetch("/api/products/42", { method: "POST" });
    client.post("/graphql", { query: "query ProductDetails { product { id } }" });
    </script>
    <script src="/assets/app.js"></script>
</head>
<body>
    <picture>
        <source type="image/webp" srcset="/images/widget.webp 1x, /images/widget@2x.webp 2x" />
        <img src="/images/widget.jpg" srcset="/images/widget-small.jpg 480w, /images/widget-large.jpg 960w" sizes="100vw" alt="Widget" />
    </picture>
    <form>
        <input type="hidden" name="__RequestVerificationToken" value="form-token-456" />
    </form>
</body>
</html>
'@

$Robots = @'
User-agent: *
Allow: /
Disallow: /admin
Crawl-delay: 5
Sitemap: /sitemap.xml
'@

$Manifest = @'
{
    "name": "Example App",
    "short_name": "Example",
    "start_url": "/app",
    "icons": [
        { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any maskable" }
    ]
}
'@

$SecurityTxt = @'
Contact: /security
Expires: 2026-12-31T23:59:59Z
'@

$JsonLd = ConvertFrom-HtmlJsonLd -Content $Html
$ScriptData = ConvertFrom-HtmlScriptData -Content $Html
$AppState = ConvertFrom-HtmlAppState -Content $Html
$HeadLinks = ConvertFrom-HtmlHeadLink -Content $Html -BaseUrl 'https://example.org/'
$Images = ConvertFrom-HtmlImageCandidate -Content $Html -BaseUrl 'https://example.org/products/widget'
$Tokens = Select-HtmlToken -Content $Html
$ReactFlightRows = ConvertFrom-HtmlRscPayload -Content $Html
$Endpoints = ConvertFrom-JavaScriptEndpoint -Content $Html -Html
$RobotsRules = ConvertFrom-RobotsTxt -Content $Robots -BaseUrl 'https://example.org/robots.txt'
$WebManifest = ConvertFrom-WebManifest -Content $Manifest -BaseUrl 'https://example.org/manifest.webmanifest'
$Security = ConvertFrom-WellKnownText -Content $SecurityTxt -Kind SecurityTxt -BaseUrl 'https://example.org/.well-known/security.txt'

# Use -Url when you want to download <script src> assets and inspect linked JavaScript bundles.
# $LinkedEndpoints = ConvertFrom-HtmlLinkedJavaScriptEndpoint -Url 'https://example.org/' -IncludeExternal

'JSON-LD'
$JsonLd | Select-Object Type, Id, SourceKind | Format-Table -AutoSize

'Script data'
$ScriptData | Select-Object Id, Type, IsJson, SourceKind | Format-Table -AutoSize

'Application state'
$AppState | Select-Object Name, Framework, SourceKind | Format-Table -AutoSize

'Head links'
$HeadLinks | Select-Object Element, Rel, Url, Type | Format-Table -AutoSize

'Image candidates'
$Images | Select-Object Element, SourceAttribute, Url, WidthDescriptor, PixelDensityDescriptor, Type | Format-Table -AutoSize

'Tokens'
$Tokens | Select-Object Name, Source, Selector | Format-Table -AutoSize

'React Flight rows'
$ReactFlightRows | Select-Object Id, Kind, IsJson, Data | Format-Table -AutoSize

'JavaScript endpoints'
$Endpoints | Select-Object Method, Client, Url, OperationName | Format-Table -AutoSize

'Robots.txt rules'
$RobotsRules | Select-Object UserAgent, Directive, Path, Url, CrawlDelay | Format-Table -AutoSize

'Web manifest'
$WebManifest | Select-Object Name, ShortName, StartUrl, Display | Format-Table -AutoSize
$WebManifest.Icons | Select-Object Src, Url, Sizes, Purpose | Format-Table -AutoSize

'security.txt'
$Security | Select-Object Field, Value, Url | Format-Table -AutoSize
