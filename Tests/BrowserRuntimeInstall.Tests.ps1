Describe 'Browser runtime caching' {
    It 'Installs the runtime only once' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri

        $temp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
        $env:PLAYWRIGHT_BROWSERS_PATH = $temp

        try {
            Invoke-HTMLRendering -Url $uri | Out-Null
            $count1 = (Get-ChildItem -Path $temp -Directory).Count

            Invoke-HTMLRendering -Url $uri | Out-Null
            $count2 = (Get-ChildItem -Path $temp -Directory).Count
        }
        finally {
            Remove-Item -Path $temp -Recurse -Force -ErrorAction SilentlyContinue
            $env:PLAYWRIGHT_BROWSERS_PATH = $null
        }

        $count1 | Should -BeGreaterThan 0
        $count2 | Should -Be $count1
    }
}
