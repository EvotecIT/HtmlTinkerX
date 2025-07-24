Describe 'Get-HtmlBrowserConsoleLog' {
    It 'Captures console output from a session' {
        $path = Join-Path $PSScriptRoot 'Documents/console_page.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        Start-Sleep -Milliseconds 500
        $log = Get-HtmlBrowserConsoleLog -Session $session
        Close-HtmlBrowserSession -Session $session

        $log.Count | Should -BeGreaterThan 0
    }
}

