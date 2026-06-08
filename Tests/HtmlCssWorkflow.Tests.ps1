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

    It 'extracts assets with resolved urls' {
        $html = @'
<link rel="stylesheet" href="/css/site.css">
<link rel="preload" href="/fonts/site.woff2" as="font">
<link rel="manifest" href="/site.webmanifest">
<link rel="icon" href="/favicon.ico">
<script src="/app/site.js"></script>
<img src="/img/logo.png" srcset="/img/logo-2x.png 2x">
'@

        $assets = @(Select-HtmlAsset -Content $html -BaseUrl 'https://example.org/app/page.html')

        $assets.Kind | Should -Contain 'Stylesheet'
        $assets.Kind | Should -Contain 'Preload'
        $assets.Kind | Should -Contain 'Manifest'
        $assets.Kind | Should -Contain 'Icon'
        $assets.Kind | Should -Contain 'Script'
        $assets.Kind | Should -Contain 'Image'
        $assets.Kind | Should -Contain 'ImageCandidate'
        ($assets | Where-Object Kind -eq 'Stylesheet').ResolvedUrl | Should -Be 'https://example.org/css/site.css'
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
}
