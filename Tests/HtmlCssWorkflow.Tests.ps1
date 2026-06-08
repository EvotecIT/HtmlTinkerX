Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'CSS query cmdlets' {
    It 'selects CSS rules, declarations, variables, urls, and specificity' {
        $css = @'
:root {
    --brand-color: #0369a1;
}

.btn {
    color: var(--brand-color);
    background-image: url("/img/button.png");
}

@media (min-width: 40rem) {
    .btn.primary {
        color: white !important;
    }
}
'@

        $rule = Select-CssRule -Content $css -Selector '.btn'
        $declarations = @(Select-CssDeclaration -Content $css -Property color)
        $variable = Get-CssVariable -Content $css -Name '--brand-color'
        $url = ConvertFrom-CssUrl -Content $css -BaseUrl 'https://example.org/app/'
        $specificity = Measure-CssSpecificity -Selector '#app .btn:hover'

        $rule.Selector | Should -Be '.btn'
        $declarations.Count | Should -Be 2
        $declarations[1].Context | Should -Match '@media'
        $declarations[1].Important | Should -BeTrue
        $variable.Name | Should -Be '--brand-color'
        $variable.Value | Should -Be '#0369a1'
        $url.Url | Should -Be '/img/button.png'
        $url.ResolvedUrl | Should -Be 'https://example.org/img/button.png'
        $specificity.Id | Should -Be 1
        $specificity.Class | Should -Be 2
    }

    It 'extracts urls from non-style CSS declaration rules' {
        $urls = @(ConvertFrom-CssUrl -Content @'
@font-face {
    font-family: "Site";
    src: url("/fonts/site.woff2") format("woff2");
}
'@ -BaseUrl 'https://example.org/app/')

        $urls.Count | Should -Be 1
        $urls[0].Selector | Should -Be '@font-face'
        $urls[0].Property | Should -Be 'src'
        $urls[0].ResolvedUrl | Should -Be 'https://example.org/fonts/site.woff2'
    }

    It 'selects declarations from non-style CSS declaration rules' {
        $declarations = @(Select-CssDeclaration -Content @'
@font-face {
    font-family: "Site";
    src: url("/fonts/site.woff2") format("woff2");
}
'@ -Property src)

        $declarations.Count | Should -Be 1
        $declarations[0].Selector | Should -Be '@font-face'
        $declarations[0].Property | Should -Be 'src'
    }
}

Describe 'HTML workflow cmdlets' {
    It 'selects JavaScript scripts with real script type rules' {
        $html = @'
<script type="application/ld+json">{"name":"schema"}</script>
<script type="module" src="/app/module.js"></script>
<script src="/app/site.js"></script>
'@

        $scripts = @(Select-HtmlScript -Content $html -BaseUrl 'https://example.org/' -JavaScript)

        $scripts.Count | Should -Be 2
        $scripts[0].IsModule | Should -BeTrue
        $scripts[0].ResolvedUrl | Should -Be 'https://example.org/app/module.js'
        $scripts[1].IsJavaScript | Should -BeTrue
    }

    It 'resolves script and asset urls through document base elements' {
        $html = @'
<base href="/assets/">
<script src="app.js"></script>
<link rel="stylesheet" href="site.css">
'@

        $script = Select-HtmlScript -Content $html -BaseUrl 'https://example.org/page/index.html' -JavaScript
        $asset = Select-HtmlAsset -Content $html -BaseUrl 'https://example.org/page/index.html' |
            Where-Object Kind -eq 'Stylesheet'

        $script.ResolvedUrl | Should -Be 'https://example.org/assets/app.js'
        $asset.ResolvedUrl | Should -Be 'https://example.org/assets/site.css'
    }

    It 'extracts assets with resolved urls' {
        $html = @'
<link rel="stylesheet" href="/css/site.css">
<link rel="preload" href="/fonts/site.woff2" as="font">
<link rel="preload" as="image" imagesrcset="/img/small.png 400w, /img/large.png 800w">
<link rel="manifest" href="/site.webmanifest">
<link rel="icon" href="/favicon.ico">
<script src=" /app/site.js "></script>
<img src="/img/logo.png" srcset="data:image/svg+xml,%3Csvg%3E%3C/svg%3E 1x, /img/logo-2x.png 2x">
<picture><source srcset="/img/wide.png 1200w"></picture>
<video><source src="/media/movie.mp4" type="video/mp4"></video>
'@

        $assets = @(Select-HtmlAsset -Content $html -BaseUrl 'https://example.org/app/page.html')
        $imageCandidates = @($assets | Where-Object Kind -eq 'ImageCandidate')

        $assets.Kind | Should -Contain 'Stylesheet'
        $assets.Kind | Should -Contain 'Preload'
        $assets.Kind | Should -Contain 'Manifest'
        $assets.Kind | Should -Contain 'Icon'
        $assets.Kind | Should -Contain 'Script'
        $assets.Kind | Should -Contain 'Image'
        $assets.Kind | Should -Contain 'ImageCandidate'
        ($assets | Where-Object Kind -eq 'Stylesheet').ResolvedUrl | Should -Be 'https://example.org/css/site.css'
        ($assets | Where-Object Kind -eq 'Script').ResolvedUrl | Should -Be 'https://example.org/app/site.js'
        $imageCandidates.Url | Should -Contain 'data:image/svg+xml,%3Csvg%3E%3C/svg%3E'
        $imageCandidates.ResolvedUrl | Should -Contain 'https://example.org/img/small.png'
        $imageCandidates.ResolvedUrl | Should -Contain 'https://example.org/img/large.png'
        $imageCandidates.ResolvedUrl | Should -Contain 'https://example.org/img/wide.png'
        $imageCandidates.ResolvedUrl | Should -Not -Contain 'https://example.org/media/movie.mp4'
        ($assets | Where-Object Kind -eq 'Media').ResolvedUrl | Should -Be 'https://example.org/media/movie.mp4'
    }

    It 'reports common HTML compatibility findings' {
        $html = @'
<form>
    <img id="dupe" src="bad image.png">
    <div id="dupe"></div>
    <label for="missing"></label>
    <input id="email" name="email">
    <span style=""></span>
</form>
'@

        $findings = @(Measure-HtmlCompatibility -Content $html -BaseUrl 'https://example.org/')

        $findings.RuleId | Should -Contain 'duplicate-id'
        $findings.RuleId | Should -Contain 'missing-img-alt'
        $findings.RuleId | Should -Contain 'empty-label'
        $findings.RuleId | Should -Contain 'label-target-missing'
        $findings.RuleId | Should -Contain 'form-field-missing-label'
        $findings.RuleId | Should -Contain 'empty-inline-style'
        $findings.RuleId | Should -Contain 'invalid-resource-url'
    }

    It 'allows decorative images with empty alt text' {
        $findings = @(Measure-HtmlCompatibility -Content '<img src="/img/divider.png" alt="">' -BaseUrl 'https://example.org/')

        $findings.RuleId | Should -Not -Contain 'missing-img-alt'
    }

    It 'reports blank resource urls and unresolved aria-labelledby labels' {
        $html = @'
<link rel="stylesheet" href="">
<input id="search" aria-labelledby="missing empty">
<span id="empty"></span>
'@

        $findings = @(Measure-HtmlCompatibility -Content $html -BaseUrl 'https://example.org/')

        $findings.RuleId | Should -Contain 'invalid-resource-url'
        $findings.RuleId | Should -Contain 'form-field-missing-label'
    }
}
