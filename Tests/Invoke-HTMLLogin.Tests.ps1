Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Invoke-HTMLLogin' {
    It 'Logs in using detected form' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        try {
            Invoke-HTMLLogin -Session $session -Username 'user' -Password 'pass' -PassThru | Should -Be $session
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
