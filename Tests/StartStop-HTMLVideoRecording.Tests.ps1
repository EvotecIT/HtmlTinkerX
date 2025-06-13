describe 'HTML Video Recording' {
    it 'Records a short video' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'video.webm'
        $session = Start-HTMLVideoRecording -Url $uri -OutFile $out -Width 320 -Height 240
        Invoke-HTMLNavigation -Session $session -Url $uri
        Stop-HTMLVideoRecording -Session $session
        (Test-Path $out) | Should -BeTrue
    }

    it 'Uses default session variable' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $out = Join-Path $TestDrive 'default.webm'
        $null = Start-HTMLVideoRecording -Url $uri -OutFile $out -Width 320 -Height 240
        Invoke-HTMLNavigation -Url $uri
        Stop-HTMLVideoRecording
        (Test-Path $out) | Should -BeTrue
    }
}
