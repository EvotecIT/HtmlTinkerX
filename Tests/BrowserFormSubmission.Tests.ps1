BeforeAll {
    Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force
}

Describe 'Browser form submission' {
    BeforeAll {
        $script:Session = [HtmlTinkerX.HtmlBrowser]::OpenSessionAsync('about:blank').GetAwaiter().GetResult()
    }

    AfterAll {
        if ($script:Session) { $script:Session.DisposeAsync().AsTask().GetAwaiter().GetResult() }
    }

    It 'sets typed controls and preserves values not overridden' {
        $html = '<form id="settings" onsubmit="event.preventDefault(); window.submitted=Array.from(new FormData(this)).map(([k,v]) => k+''=''+v).join(''|'')">' +
            '<input name="token" type="hidden" value="keep"><select name="color"><option value="red">Red</option><option value="blue">Blue</option></select>' +
            '<input name="accept" type="checkbox" value="yes"><input name="size" type="radio" value="small" checked><input name="size" type="radio" value="large"></form>'
        $script:Session.Page.SetContentAsync($html).GetAwaiter().GetResult()
        $form = ConvertFrom-HtmlForm -Content $html -IncludeMetadata

        $returned = Submit-HtmlBrowserForm -Session $script:Session -Form $form -FieldValue @{ color='blue'; accept='yes'; size='large' } -PassThru

        $returned | Should -Be $script:Session
        Invoke-HtmlBrowserScript -Session $script:Session -Script '() => window.submitted' | Should -Be 'token=keep|color=blue|accept=yes|size=large'
    }

    It 'respects required controls instead of bypassing native validation' {
        $html = '<form id="required" onsubmit="event.preventDefault(); window.submitted=true"><input name="q" required></form><script>window.submitted=false</script>'
        $script:Session.Page.SetContentAsync($html).GetAwaiter().GetResult()
        $form = ConvertFrom-HtmlForm -Content $html -IncludeMetadata

        Submit-HtmlBrowserForm -Session $script:Session -Form $form -FieldValue @{}

        Invoke-HtmlBrowserScript -Session $script:Session -Script '() => window.submitted' | Should -BeFalse
    }

    It 'selects a nested form by document index when no id is present' {
        $html = '<section><form onsubmit="event.preventDefault()"><input name="q" value="first"></form></section>' +
            '<section><form onsubmit="event.preventDefault(); window.submitted=new FormData(this).get(''q'')"><input name="q" value="second"></form></section>'
        $script:Session.Page.SetContentAsync($html).GetAwaiter().GetResult()
        $forms = ConvertFrom-HtmlForm -Content $html -IncludeMetadata

        Submit-HtmlBrowserForm -Session $script:Session -Form $forms[1] -FieldValue @{ q='selected' }

        Invoke-HtmlBrowserScript -Session $script:Session -Script '() => window.submitted' | Should -Be 'selected'
        $script:Session.Page.Locator('input').First.InputValueAsync().GetAwaiter().GetResult() | Should -Be 'first'
    }
}
