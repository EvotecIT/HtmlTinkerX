Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Set-HTMLInput' {
    It 'Can type input through keyboard events' {
        $htmlPath = Join-Path $TestDrive 'typed-input.html'
        @'
<!doctype html>
<html>
<body>
<input id="search" value="old">
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8
        $uri = [System.Uri]::new($htmlPath).AbsoluteUri
        $session = Invoke-HtmlRendering -Url $uri -Session

        try {
            Set-HtmlInput -Session $session -Selector '#search' -Value 'new value' -Type -DelayMs 0
            $value = Invoke-HtmlScript -Session $session -Script 'document.getElementById("search").value'

            $value | Should -Be 'new value'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
