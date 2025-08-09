Import-Module "$PSScriptRoot/../PSParseHTML.psd1"
Describe 'Export-HTMLOutline' {
    It 'Exports outline to JSON file' {
        $htmlPath = Join-Path $PSScriptRoot 'Documents/outline.html'
        $content = Get-Content -LiteralPath $htmlPath -Raw
        $outFile = Join-Path $PSScriptRoot 'outline_test.json'
        try {
            Export-HTMLOutline -Content $content -Path $outFile
            $outline = Get-Content -LiteralPath $outFile -Raw | ConvertFrom-Json
            $outline.Count | Should -Be 2
            $outline[0].title | Should -Be 'Section 1'
            $outline[0].children[0].title | Should -Be 'Subsection 1.1'
            $outline[0].children[0].children[0].title | Should -Be 'Detail 1.1.1'
        } finally {
            Remove-Item -LiteralPath $outFile -ErrorAction SilentlyContinue
        }
    }

    It 'Skips malformed heading tags' {
        $content = @'
<h1>Good</h1>
<hX>Bad</hX>
<h1>Also Good</h1>
'@
        $outFile = Join-Path $PSScriptRoot 'outline_test_malformed.json'
        try {
            Export-HTMLOutline -Content $content -Path $outFile
            $outline = Get-Content -LiteralPath $outFile -Raw | ConvertFrom-Json
            $outline.Count | Should -Be 2
            $outline[0].title | Should -Be 'Good'
            $outline[1].title | Should -Be 'Also Good'
        } finally {
            Remove-Item -LiteralPath $outFile -ErrorAction SilentlyContinue
        }
    }
}
