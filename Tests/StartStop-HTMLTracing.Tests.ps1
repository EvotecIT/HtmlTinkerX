describe 'HTML Tracing and HAR' {
    it 'Creates a trace file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        Start-HtmlBrowserTracing -Session $session
        Invoke-HTMLNavigation -Session $session -Url $uri
        $trace = Join-Path $TestDrive 'trace.zip'
        Stop-HtmlBrowserTracing -Session $session -OutFile $trace
        (Test-Path $trace) | Should -BeTrue
        Close-HtmlBrowserSession -Session $session
    }

    it 'Exports a HAR file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        Invoke-HTMLNavigation -Session $session -Url $uri
        $har = Join-Path $TestDrive 'out.har'
        Save-HtmlBrowserHar -Session $session -OutFile $har
        (Test-Path $har) | Should -BeTrue
        Close-HtmlBrowserSession -Session $session
    }

    it 'Writes a HAR object to a file' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        Invoke-HTMLNavigation -Session $session -Url $uri
        $harTemp = Join-Path $TestDrive 'temp.har'
        Save-HtmlBrowserHar -Session $session -OutFile $harTemp
        $harObj = Show-HtmlBrowserHar -Path $harTemp
        $copy = Join-Path $TestDrive 'copy.har'
        Save-HtmlBrowserHar -Har $harObj -OutFile $copy
        (Test-Path $copy) | Should -BeTrue
        Close-HtmlBrowserSession -Session $session
    }
}
