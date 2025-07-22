describe 'HTML Recording Helpers' {
    it 'Returns path when stopping' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'record.webm'
        $session = Start-HTMLRecording -Url $uri -OutFile $out
        Invoke-HTMLNavigation -Session $session -Url $uri
        $result = Stop-HTMLRecording -Session $session
        $result | Should -Be (Resolve-Path $out)
        (Test-Path $out) | Should -BeTrue
    }
}
