Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Get-HtmlExtractionProfile' {
    It 'exports and lists product-level extraction profiles' {
        $profiles = Get-HtmlExtractionProfile

        Get-Command Get-HtmlExtractionProfile | Should -Not -BeNullOrEmpty
        $profiles.Name | Should -Contain 'docs-content'
        $profiles.Name | Should -Contain 'api-docs-content'
        $profiles.Name | Should -Contain 'app-shell'
        $profiles.Name | Should -Contain 'auth-relay-page'
        $profiles.Name | Should -Contain 'dataset-page'
    }

    It 'filters extraction profiles by name and recommended mode' {
        $profiles = Get-HtmlExtractionProfile -RecommendedMode Crawl
        $profiles.Name | Should -Contain 'docs-content'
        $profiles.Name | Should -Contain 'api-docs-content'
        $profiles.Name | Should -Not -Contain 'app-shell'

        $profile = Get-HtmlExtractionProfile -Name 'app-shell'
        @($profile).Count | Should -Be 1
        $profile.RenderProfile | Should -Be 'HeavyDynamicPage'
        $profile.SuggestedCommand | Should -Match 'Invoke-HtmlRendering'
    }

    It 'returns the profile suggested by an extraction plan from the pipeline' {
        $html = @'
<html>
<head><title>App</title><script src="/runtime.js"></script><script src="/app.js"></script></head>
<body><div id="root">Loading...</div></body>
</html>
'@

        $plan = Test-HtmlExtractionPlan -Content $html
        $profile = $plan | Get-HtmlExtractionProfile

        $plan.SuggestedProfileName | Should -Be 'app-shell'
        $plan.SuggestedProfileCommand | Should -Match 'HeavyDynamicPage'
        $profile.Name | Should -Be 'app-shell'
        $profile.RecommendedMode | Should -Be 'RenderedSnapshot'
    }
}
