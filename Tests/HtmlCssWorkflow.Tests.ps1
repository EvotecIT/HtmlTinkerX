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

    It 'matches CSS selectors case-sensitively' {
        $css = @'
.btn {
    color: red;
}

.Btn {
    color: blue;
}
'@

        $rule = Select-CssRule -Content $css -Selector '.btn'
        $declaration = Select-CssDeclaration -Content $css -Selector '.btn' -Property color

        $rule.Count | Should -Be 1
        $rule.Selector | Should -Be '.btn'
        $declaration.Count | Should -Be 1
        $declaration.Selector | Should -Be '.btn'
    }

    It 'preserves CSS rule source indexes after filtering' {
        $css = @'
.first {
    color: red;
}

.second {
    color: blue;
}
'@

        $rule = Select-CssRule -Content $css -Selector '.second'

        $rule.Count | Should -Be 1
        $rule.Index | Should -Be 1
        $rule.Selector | Should -Be '.second'
    }

    It 'matches CSS custom property names case-sensitively' {
        $css = @'
:root {
    --brand: #111111;
    --Brand: #222222;
}
'@

        $variable = Get-CssVariable -Content $css -Name '--brand'

        $variable.Count | Should -Be 1
        $variable.Name | Should -Be '--brand'
        $variable.Value | Should -Be '#111111'
    }

    It 'selects custom property declarations case-sensitively' {
        $css = @'
:root {
    --brand: #111111;
    --Brand: #222222;
}
'@

        $declarations = @(Select-CssDeclaration -Content $css -Property '--brand')

        $declarations.Count | Should -Be 1
        $declarations[0].Property | Should -Be '--brand'
        $declarations[0].Value | Should -Be '#111111'
    }

    It 'preserves CSS declaration source indexes after filtering' {
        $css = @'
.card {
    color: red;
    background-color: blue;
}
'@

        $declaration = Select-CssDeclaration -Content $css -Property background-color

        $declaration.Count | Should -Be 1
        $declaration.Index | Should -Be 1
        $declaration.Property | Should -Be 'background-color'
    }

    It 'preserves CSS variable source indexes after filtering' {
        $css = @'
:root {
    --first: 1;
    --second: 2;
}
'@

        $variable = Get-CssVariable -Content $css -Name '--second'

        $variable.Count | Should -Be 1
        $variable.Index | Should -Be 1
        $variable.Name | Should -Be '--second'
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

    It 'ignores url-like text inside CSS strings' {
        $urls = @(ConvertFrom-CssUrl -Content @'
.badge {
    content: "url(/not-an-asset)";
    background-image: url("/img/badge.png");
}
'@)

        $urls.Count | Should -Be 1
        $urls[0].Url | Should -Be '/img/badge.png'
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

    It 'preserves script source indexes when filtering JavaScript scripts' {
        $html = @'
<script type="application/ld+json">{"name":"schema"}</script>
<script src="/app/site.js"></script>
'@

        $scripts = @(Select-HtmlScript -Content $html -BaseUrl 'https://example.org/' -JavaScript)

        $scripts.Count | Should -Be 1
        $scripts[0].Index | Should -Be 1
        $scripts[0].ResolvedUrl | Should -Be 'https://example.org/app/site.js'
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

    It 'extracts video poster image assets' {
        $assets = @(Select-HtmlAsset -Content '<video poster="/img/poster.jpg" src="/media/movie.mp4"></video>' -BaseUrl 'https://example.org/')

        ($assets | Where-Object Attribute -eq 'poster').Kind | Should -Be 'Image'
        ($assets | Where-Object Attribute -eq 'poster').ResolvedUrl | Should -Be 'https://example.org/img/poster.jpg'
        ($assets | Where-Object Kind -eq 'Media').ResolvedUrl | Should -Be 'https://example.org/media/movie.mp4'
    }

    It 'extracts image submit button sources as image assets' {
        $assets = @(Select-HtmlAsset -Content '<input type="image" src="/submit.png" alt="Submit">' -BaseUrl 'https://example.org/')

        $assets.Count | Should -Be 1
        $assets[0].Kind | Should -Be 'Image'
        $assets[0].Element | Should -Be 'input'
        $assets[0].ResolvedUrl | Should -Be 'https://example.org/submit.png'
    }

    It 'extracts prefetch links as preload-style assets' {
        $assets = @(Select-HtmlAsset -Content '<link rel="prefetch" href="/next.html">' -BaseUrl 'https://example.org/')

        $assets.Count | Should -Be 1
        $assets[0].Kind | Should -Be 'Preload'
        $assets[0].ResolvedUrl | Should -Be 'https://example.org/next.html'
    }

    It 'indexes assets in document source order' {
        $html = @'
<link rel="stylesheet" href="/first.css">
<script src="/second.js"></script>
<img src="/third.png">
<style>.fourth { color: red; }</style>
'@

        $assets = @(Select-HtmlAsset -Content $html -BaseUrl 'https://example.org/' -IncludeInline)

        $assets.Kind | Should -Be @('Stylesheet', 'Script', 'Image', 'InlineStyle')
        $assets.Index | Should -Be @(0, 1, 2, 3)
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

    It 'requires associated labels to be readable' {
        $html = @'
<label for="email"></label>
<input id="email">
'@

        $findings = @(Measure-HtmlCompatibility -Content $html)

        $findings.RuleId | Should -Contain 'empty-label'
        $findings.RuleId | Should -Contain 'form-field-missing-label'
    }

    It 'checks image submit buttons for accessible text' {
        $findings = @(Measure-HtmlCompatibility -Content '<input type="image" src="/submit.png">')

        $findings.RuleId | Should -Contain 'form-field-missing-label'
    }

    It 'checks input buttons for accessible names' {
        $findings = @(Measure-HtmlCompatibility -Content '<input type="button" id="save">')
        $named = @(Measure-HtmlCompatibility -Content '<input type="button" value="Save">')

        $findings.RuleId | Should -Contain 'form-field-missing-label'
        $named.RuleId | Should -Not -Contain 'form-field-missing-label'
    }

    It 'checks unlabeled buttons and ignores alt on non-image fields' {
        $html = @'
<button id="save"></button>
<input type="text" alt="Email">
<input type="image" src="/submit.png" alt="Submit">
'@

        $findings = @(Measure-HtmlCompatibility -Content $html)

        @($findings | Where-Object RuleId -eq 'form-field-missing-label').Count | Should -Be 2
    }

    It 'counts image alt text inside labels as readable text' {
        $html = @'
<label for="search"><img src="/search.png" alt="Search"></label>
<input id="search">
'@

        $findings = @(Measure-HtmlCompatibility -Content $html)

        $findings.RuleId | Should -Not -Contain 'empty-label'
        $findings.RuleId | Should -Not -Contain 'form-field-missing-label'
    }

    It 'counts labelledby image alt text as an accessible name' {
        $html = @'
<span id="search"><img src="/search.png" alt="Search"></span>
<input aria-labelledby="search">
'@

        $findings = @(Measure-HtmlCompatibility -Content $html)

        $findings.RuleId | Should -Not -Contain 'form-field-missing-label'
    }

    It 'checks all labelable controls for labels' {
        $html = @'
<progress id="load"></progress>
<output id="total"></output>
'@

        $findings = @(Measure-HtmlCompatibility -Content $html)

        @($findings | Where-Object RuleId -eq 'form-field-missing-label').Count | Should -Be 2
    }

    It 'rejects labels targeting non-labelable elements' {
        $html = @'
<label for="email">Email</label>
<div id="email"></div>
'@

        $findings = @(Measure-HtmlCompatibility -Content $html)

        $findings.RuleId | Should -Contain 'label-target-invalid'
    }

    It 'does not let for-bound ancestor labels name nested controls' {
        $html = @'
<label for="other">Email <input id="email"></label>
<input id="other">
'@

        $findings = @(Measure-HtmlCompatibility -Content $html)

        ($findings | Where-Object RuleId -eq 'form-field-missing-label').Value | Should -Contain $null
    }
}
