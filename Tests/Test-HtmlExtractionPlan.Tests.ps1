Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Test-HtmlExtractionPlan' {
    It 'recommends static extraction for useful static content' {
        $html = @'
<html>
<head><title>Docs</title><meta property="og:title" content="Docs" /></head>
<body>
<main>
<h1>Docs</h1>
<p>This page contains useful readable documentation content with enough words to parse statically.</p>
<form action="/search"><input name="q" value="" /></form>
</main>
</body>
</html>
'@

        $plan = Test-HtmlExtractionPlan -Content $html

        $plan.RecommendedMode | Should -Be 'Static'
        $plan.FormCount | Should -Be 1
        $plan.HasStructuredData | Should -BeTrue
        $plan.SuggestedCommand | Should -Be 'Select-HtmlData -Content $html'
    }

    It 'recommends rendered snapshots for thin JavaScript shells' {
        $html = @'
<html>
<head><title>App</title><script src="/runtime.js"></script><script src="/app.js"></script></head>
<body><div id="root">Loading...</div></body>
</html>
'@

        $plan = Test-HtmlExtractionPlan -Content $html

        $plan.RecommendedMode | Should -Be 'RenderedSnapshot'
        $plan.LooksLikeJavaScriptShell | Should -BeTrue
        $plan.SuggestedCommand | Should -Match 'Invoke-HtmlRendering'
    }

    It 'detects hidden-form relay candidates' {
        $html = @'
<html>
<body>
<form method="POST" name="hiddenform" action="https://site.example/signinws">
<input type="hidden" name="wa" value="signin1.0" />
<input type="hidden" name="wresult" value="redacted" />
<input type="hidden" name="wctx" value="redacted" />
</form>
<script>window.setTimeout('document.forms[0].submit()', 0);</script>
</body>
</html>
'@

        $plan = Test-HtmlExtractionPlan -Content $html

        $plan.RecommendedMode | Should -Be 'BrowserlessRelayCandidate'
        $plan.HasAutoSubmitForm | Should -BeTrue
        $plan.HiddenFieldCount | Should -Be 3
        $plan.Warnings.Count | Should -BeGreaterThan 0
        $plan.SuggestedCommand | Should -Match 'Invoke-HtmlFormRelay'
    }
}
