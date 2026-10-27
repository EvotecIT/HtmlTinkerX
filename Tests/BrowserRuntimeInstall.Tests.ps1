Describe 'Browser runtime caching' {
    It 'Installs the runtime only once' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri

        $temp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
        $originalBrowserPath = $env:PLAYWRIGHT_BROWSERS_PATH
        $env:PLAYWRIGHT_BROWSERS_PATH = $temp

        try {
            Invoke-HTMLRendering -Url $uri | Out-Null
            $runtime1 = [HtmlTinkerX.HtmlBrowser]::GetBrowserRuntimeDirectory([HtmlTinkerX.HtmlBrowserEngine]::Chromium)
            $manifest1 = Get-FileHash -LiteralPath (Join-Path $runtime1 'htmltinkerx-integrity.json')
            $versions = Join-Path $temp '.htmltinkerx/versions'
            $count1 = @(Get-ChildItem -LiteralPath $versions -Directory -Force).Count

            Invoke-HTMLRendering -Url $uri | Out-Null
            $runtime2 = [HtmlTinkerX.HtmlBrowser]::GetBrowserRuntimeDirectory([HtmlTinkerX.HtmlBrowserEngine]::Chromium)
            $manifest2 = Get-FileHash -LiteralPath (Join-Path $runtime2 'htmltinkerx-integrity.json')
            $count2 = @(Get-ChildItem -LiteralPath $versions -Directory -Force).Count
        }
        finally {
            $env:PLAYWRIGHT_BROWSERS_PATH = $originalBrowserPath
            # The generated cache deliberately contains a hidden .htmltinkerx directory on Unix.
            if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction Stop }
        }

        $count1 | Should -BeGreaterThan 0
        $count2 | Should -Be $count1
        $runtime2 | Should -Be $runtime1
        $manifest2.Hash | Should -Be $manifest1.Hash
    }
}
